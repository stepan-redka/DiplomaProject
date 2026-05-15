using Diploma.Application.DTOs;
using Diploma.Application.Interfaces;
using Diploma.Domain.Entities;
using Diploma.Domain.Enums;
using Diploma.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using Document = Diploma.Domain.Entities.Document;

namespace Diploma.Infrastructure.Services;

/// <summary>
/// Core RAG Orchestrator (Orchestration Pattern). 
/// Strictly coordinates high-level service execution without implementing business logic.
/// </summary>
public class RagService : IRagService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<RagService> _logger;

    // Orchestrated Sub-services (SOLID)
    private readonly IIntentResolver _intentResolver;
    private readonly IRetrievalService _retrievalService;
    private readonly IDocumentService _documentService;
    private readonly IAnalyticsService _analyticsService;
    private readonly IPromptRegistry _promptRegistry;
    private readonly IEvaluationService _evaluationService;
    private readonly ITokenizerService _tokenizerService;
    private readonly IAiService _aiService;
    private readonly ITextChunkingService _chunkingService;
    private readonly IVectorDatabase _vectorDb;
    private readonly RagConfiguration _config;

    private string userId => _currentUserService.UserId ?? throw new UnauthorizedAccessException("User must be authenticated.");

    public RagService(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        IIntentResolver intentResolver,
        IRetrievalService retrievalService,
        IDocumentService documentService,
        IAnalyticsService analyticsService,
        IPromptRegistry promptRegistry,
        IEvaluationService evaluationService,
        ITokenizerService tokenizerService,
        IAiService aiService,
        ITextChunkingService chunkingService,
        IVectorDatabase vectorDb,
        RagConfiguration config,
        ILogger<RagService> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _intentResolver = intentResolver;
        _retrievalService = retrievalService;
        _documentService = documentService;
        _analyticsService = analyticsService;
        _promptRegistry = promptRegistry;
        _evaluationService = evaluationService;
        _tokenizerService = tokenizerService;
        _aiService = aiService;
        _chunkingService = chunkingService;
        _vectorDb = vectorDb;
        _config = config;
        _logger = logger;
    }

    public async Task<QueryResponse> QueryAsync(string question, Guid? sessionId = null, int? topK = null, QueryIntent? intent = null, string? modelName = null, bool isHighFidelity = false, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var session = await GetOrCreateSessionAsync(sessionId, question, modelName, ct);
            _dbContext.ChatMessages.Add(new ChatMessage { UserId = userId, ChatSessionId = session.Id, Role = "user", Content = question });

            // 1. Semantic Intent Routing
            var docCount = await _documentService.GetDocumentCountAsync(ct);
            var resolvedIntent = intent ?? await _intentResolver.ResolveAsync(question, isHighFidelity, docCount > 0, ct);

            // 2. Flow Coordination
            if (resolvedIntent == QueryIntent.General)
            {
                var prompt = docCount == 0 ? _promptRegistry.GetEmptyRepoPrompt(question) : _promptRegistry.GetGeneralPrompt(question);
                var answer = await _aiService.GenerateAnswerAsync(prompt, modelName, ct);
                var messageId = await SaveAssistantMessageAsync(session.Id, answer, null, modelName, sw.ElapsedMilliseconds);
                
                return new QueryResponse { MessageId = messageId, SessionId = session.Id, SessionTitle = session.Title, Answer = answer, ProcessingTimeMs = sw.ElapsedMilliseconds };
            }

            // 3. RAG Pipeline Execution
            var sources = await _retrievalService.GetRelevantContextAsync(question, userId, topK, ct);
            var contextText = sources.Any() 
                ? string.Join("\n---\n", sources.Select(s => $"[Source: {s.SourceDocument}] {s.Content}")) 
                : "No relevant documents found.";

            var ragPrompt = sources.Any() ? _promptRegistry.GetRagPrompt(question, contextText) : _promptRegistry.GetGeneralPrompt(question);
            var ragAnswer = await _aiService.GenerateAnswerAsync(ragPrompt, modelName, ct);
            
            var assistantMessageId = await SaveAssistantMessageAsync(session.Id, ragAnswer, sources, modelName, sw.ElapsedMilliseconds);

            return new QueryResponse 
            { 
                MessageId = assistantMessageId, SessionId = session.Id, SessionTitle = session.Title, 
                Answer = ragAnswer, Sources = sources, ProcessingTimeMs = sw.ElapsedMilliseconds 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orchestration error during QueryAsync for question: {Question}", question);
            throw;
        }
    }

    public async Task<IngestResponse> IngestDocumentAsync(string content, string documentName, Guid? existingDocumentId = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Orchestrating ingestion for: {DocumentName}", documentName);
        var sw = Stopwatch.StartNew();

        try
        {
            var textChunks = _chunkingService.ChunkText(content, _config.Chunking.MaxChunkSize, _config.Chunking.ChunkOverlap);
            
            Document? document = null;
            var documentId = existingDocumentId ?? Guid.NewGuid();

            // 1. Try finding by ID (Ignore filters for background tasks)
            if (existingDocumentId.HasValue)
            {
                document = await _dbContext.Documents.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == existingDocumentId.Value, ct);
                
                // CRITICAL: Check local cache if DB query failed (prevents duplication in same scope)
                if (document == null)
                {
                    document = _dbContext.Documents.Local.FirstOrDefault(d => d.Id == existingDocumentId.Value);
                }
            }

            // 2. Fallback: Try finding by Name and User (Last ditch de-duplication)
            if (document == null)
            {
                document = await _dbContext.Documents.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.FileName == documentName && d.UserId == userId, ct);
            }

            if (document == null)
            {
                document = new Document 
                { 
                    Id = documentId, 
                    UserId = userId, 
                    FileName = documentName, 
                    CreatedAt = DateTime.UtcNow, 
                    Content = content, 
                    FileSizeBytes = System.Text.Encoding.UTF8.GetByteCount(content),
                    Status = IngestionStatus.Processing
                };
                _dbContext.Documents.Add(document);
            }
            else
            {
                // Update existing
                document.Content = content;
                document.FileSizeBytes = System.Text.Encoding.UTF8.GetByteCount(content);
                document.Status = IngestionStatus.Processing;
                documentId = document.Id; // Use the found ID
            }

            // Clean up existing chunks if we are updating
            var existingChunks = await _dbContext.DocumentChunks.IgnoreQueryFilters().Where(c => c.DocumentId == documentId).ToListAsync(ct);
            if (existingChunks.Any())
            {
                _dbContext.DocumentChunks.RemoveRange(existingChunks);
            }

            var embeddings = await _aiService.GetTextEmbeddingsAsync(textChunks, ct);
            var vectorDataList = new List<VectorData>();
            var chunksToAdd = new List<DocumentChunk>();

            for (int i = 0; i < textChunks.Count; i++)
            {
                var chunkId = Guid.NewGuid();
                vectorDataList.Add(new VectorData(chunkId, documentId, embeddings[i], textChunks[i], new Dictionary<string, object> { { "index", i } }));
                chunksToAdd.Add(new DocumentChunk { Id = chunkId, UserId = document.UserId, DocumentId = documentId, Content = textChunks[i], ChunkIndex = i });
            }

            _dbContext.DocumentChunks.AddRange(chunksToAdd);
            document.Status = IngestionStatus.Success;

            await _dbContext.SaveChangesAsync(ct);
            await _vectorDb.EnsureCollectionExistsAsync(_config.Qdrant.CollectionName, _config.Qdrant.VectorSize, ct);
            await _vectorDb.UpsertChunksAsync(_config.Qdrant.CollectionName, vectorDataList, document.UserId, ct);
            
            sw.Stop();
            document.ProcessingTimeMs = sw.ElapsedMilliseconds;
            await _dbContext.SaveChangesAsync(ct);
            
            return new IngestResponse { Success = true, ChunksCreated = textChunks.Count, Message = "Success!" }; 
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to orchestrate ingestion for {DocumentName}", documentName);
            return new IngestResponse { Success = false, Message = ex.Message };
        }
    }

    private async Task<ChatSession> GetOrCreateSessionAsync(Guid? sessionId, string question, string? modelName, CancellationToken ct)
    {
        ChatSession? session = null;
        if (sessionId.HasValue) session = await _dbContext.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId.Value, ct);

        if (session == null)
        {
            session = new ChatSession { UserId = userId, Title = question.Length > 30 ? question.Substring(0, 27) + "..." : question, RelatedDocumentIds = await _dbContext.Documents.Select(d => d.Id).ToListAsync(ct), SelectedModel = modelName };
            _dbContext.ChatSessions.Add(session);
        }
        else
        {
            session.LastUpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(modelName)) session.SelectedModel = modelName;
        }

        await _dbContext.SaveChangesAsync(ct);
        return session;
    }

    private async Task<Guid> SaveAssistantMessageAsync(Guid sessionId, string content, List<SourceCitation>? sources, string? modelName, long elapsedMs)
    {
        var messageId = Guid.NewGuid();
        _dbContext.ChatMessages.Add(new ChatMessage { Id = messageId, UserId = userId, ChatSessionId = sessionId, Role = "assistant", Content = content, Metadata = sources != null && sources.Any() ? JsonSerializer.Serialize(sources) : null, ModelName = modelName ?? _config.Ollama.ChatModel, ProcessingTimeMs = elapsedMs, TokenCount = _tokenizerService.GetTokenCount(content) });
        await _dbContext.SaveChangesAsync();
        return messageId;
    }

    // High-Level coordination wrappers
    public async Task<List<ChatMessageDto>> GetChatHistoryAsync(int limit = 50, CancellationToken ct = default) => 
        await _dbContext.ChatMessages
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ChatMessageDto 
            { 
                Id = m.Id, 
                Role = m.Role, 
                Content = m.Content, 
                CreatedAt = m.CreatedAt, 
                Effectiveness = (int)m.Effectiveness, 
                ModelName = m.ModelName, 
                ProcessingTimeMs = m.ProcessingTimeMs, 
                TokenCount = m.TokenCount,
                Sources = !string.IsNullOrEmpty(m.Metadata) 
                    ? JsonSerializer.Deserialize<List<SourceCitation>>(m.Metadata, (JsonSerializerOptions?)null) ?? new List<SourceCitation>()
                    : new List<SourceCitation>()
            }).ToListAsync(ct);
    public async Task<bool> SetFeedbackAsync(Guid messageId, int effectiveness, CancellationToken ct = default) => await _evaluationService.SetFeedbackAsync(messageId, userId, effectiveness, ct);
    public async Task<ResearchAnalyticsDto> GetAnalyticsAsync(CancellationToken ct = default) => await _analyticsService.GetAnalyticsAsync(userId, ct);
    public async Task<int> GetTotalQueriesAsync(CancellationToken ct = default) => await _analyticsService.GetTotalQueriesAsync(userId, ct);
    public async Task<long> GetStorageUsedAsync(CancellationToken ct = default) => await _analyticsService.GetStorageUsedAsync(userId, ct);
    public async Task<int> GetDocumentCountAsync(CancellationToken ct = default) => await _documentService.GetDocumentCountAsync(ct);
    public async Task<List<DocumentDto>> GetUserDocumentsAsync(CancellationToken ct = default) => await _documentService.GetUserDocumentsAsync(ct);
    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken ct = default) => await _documentService.DeleteDocumentAsync(documentId, userId, ct);
    public async Task<bool> ClearCollectionAsync(CancellationToken ct = default) => await _documentService.ClearCollectionAsync(userId, ct);
    public async Task<List<StoredChunkInfo>> GetStoredChunksAsync(int limit = 500, CancellationToken ct = default) => await _documentService.GetStoredChunksAsync(limit, ct);
    public async Task<int> DeleteChunksAsync(IEnumerable<string> chunkIds, CancellationToken ct = default) => await _documentService.DeleteChunksAsync(chunkIds, userId, ct);
    public async Task<bool> EnsureCollectionExistsAsync(CancellationToken ct = default) { await _vectorDb.EnsureCollectionExistsAsync(_config.Qdrant.CollectionName, _config.Qdrant.VectorSize, ct); return true; }
}
