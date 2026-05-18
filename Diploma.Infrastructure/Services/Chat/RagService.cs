using System.Diagnostics;
using Diploma.Application.DTOs;
using Diploma.Application.Interfaces.AI;
using Diploma.Application.Interfaces.Analytics;
using Diploma.Application.Interfaces.Chat;
using Diploma.Application.Interfaces.Documents;
using Diploma.Application.Interfaces.Identity;
using Diploma.Application.Interfaces.Storage;
using Diploma.Domain.Entities;
using Diploma.Domain.Enums;
using Diploma.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Diploma.Infrastructure.Services.Chat;

public class RagService : IRagService
{
    private const string NoContextMessage = "No relevant documents found.";
    
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<RagService> _logger;

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
    private readonly ISemanticCacheService _semanticCache;

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
        ISemanticCacheService semanticCache,
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
        _semanticCache = semanticCache;
        _logger = logger;
    }

    public async Task<QueryResponse> QueryAsync(string question, Guid? sessionId = null, int? topK = null, QueryIntent? intent = null, string? modelName = null, bool isHighFidelity = false, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // --- STEP 1: SEMANTIC CACHE CHECK ---
            var cachedAnswer = await _semanticCache.GetCachedResponseAsync(question, ct);
            if (cachedAnswer != null)
            {
                _logger.LogInformation("Semantic Cache Hit - Bypassing RAG pipeline for: {Question}", question);
                var cachedSession = await GetOrCreateSessionAsync(sessionId, question, modelName, ct);
                _dbContext.ChatMessages.Add(new ChatMessage { UserId = userId, ChatSessionId = cachedSession.Id, Role = "user", Content = question });

                var cachedMsgId = await SaveAssistantMessageAsync(cachedSession.Id, cachedAnswer, null, modelName, sw.ElapsedMilliseconds, ct);

                return new QueryResponse
                {
                    MessageId = cachedMsgId,
                    SessionId = cachedSession.Id,
                    SessionTitle = cachedSession.Title,
                    Answer = cachedAnswer,
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            // --- STEP 2: CACHE MISS - NORMAL PIPELINE ---
            var session = await GetOrCreateSessionAsync(sessionId, question, modelName, ct);
            _dbContext.ChatMessages.Add(new ChatMessage { UserId = userId, ChatSessionId = session.Id, Role = "user", Content = question });

            var docCount = await _documentService.GetDocumentCountAsync(ct);
            var resolvedIntent = intent ?? await _intentResolver.ResolveAsync(question, isHighFidelity, docCount > 0, ct);

            if (resolvedIntent == QueryIntent.General)
            {
                var prompt = docCount == 0 ? _promptRegistry.GetEmptyRepoPrompt(question) : _promptRegistry.GetGeneralPrompt(question);
                var answer = await _aiService.GenerateAnswerAsync(prompt, modelName, ct);

                // --- STEP 3: ASYNC CACHE SAVE ---
                _ = _semanticCache.SaveToCacheAsync(question, answer, ct);

                var messageId = await SaveAssistantMessageAsync(session.Id, answer, null, modelName, sw.ElapsedMilliseconds, ct);

                return new QueryResponse { MessageId = messageId, SessionId = session.Id, SessionTitle = session.Title, Answer = answer, ProcessingTimeMs = sw.ElapsedMilliseconds };
            }

            var sources = await _retrievalService.GetRelevantContextAsync(question, userId, topK, ct);
            var contextText = sources.Any()
                ? string.Join("\n---\n", sources.Select(s => $"[Source: {s.SourceDocument}] {s.Content}"))
                : NoContextMessage;

            var ragPrompt = sources.Any() ? _promptRegistry.GetRagPrompt(question, contextText) : _promptRegistry.GetGeneralPrompt(question);
            var ragAnswer = await _aiService.GenerateAnswerAsync(ragPrompt, modelName, ct);

            // --- STEP 3: ASYNC CACHE SAVE ---
            _ = _semanticCache.SaveToCacheAsync(question, ragAnswer, ct);

            var assistantMessageId = await SaveAssistantMessageAsync(session.Id, ragAnswer, sources, modelName, sw.ElapsedMilliseconds, ct);

            return new QueryResponse
            {
                MessageId = assistantMessageId,
                SessionId = session.Id,
                SessionTitle = session.Title,
                Answer = ragAnswer,
                Sources = sources,
                ProcessingTimeMs = sw.ElapsedMilliseconds
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
            if (existingDocumentId.HasValue)
            {
                document = await _dbContext.Documents.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == existingDocumentId.Value, ct);
            }
            if (document == null)
            {
                document = new Document { Id = documentId, FileName = documentName, UserId = userId, Status = IngestionStatus.Processing, CreatedAt = DateTime.UtcNow };
                _dbContext.Documents.Add(document);
            }
            await _dbContext.SaveChangesAsync(ct);
            var embeddings = await _aiService.GetTextEmbeddingsAsync(textChunks, ct);
            var vectorDataList = new List<VectorData>();
            for (int i = 0; i < textChunks.Count; i++)
            {
                vectorDataList.Add(new VectorData(Guid.NewGuid(), document.Id, embeddings[i], textChunks[i], new Dictionary<string, object> { { "source", documentName } }));
            }
            await _vectorDb.UpsertChunksAsync(_config.Qdrant.CollectionName, vectorDataList, userId, ct);
            document.Status = IngestionStatus.Success;
            await _dbContext.SaveChangesAsync(ct);
            return new IngestResponse { Success = true, Message = "Document ingested successfully.", ChunksCreated = textChunks.Count };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest document: {DocumentName}", documentName);
            return new IngestResponse { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    public async Task<bool> EnsureCollectionExistsAsync(CancellationToken ct = default) 
    { 
        await _vectorDb.EnsureCollectionExistsAsync(_config.Qdrant.CollectionName, _config.Qdrant.VectorSize, ct);
        return true;
    }

    public async Task<int> GetDocumentCountAsync(CancellationToken ct = default) => await _dbContext.Documents.CountAsync(ct);

    public async Task<List<DocumentDto>> GetUserDocumentsAsync(CancellationToken ct = default) => 
        await _dbContext.Documents.Select(d => new DocumentDto { Id = d.Id, FileName = d.FileName }).ToListAsync(ct);

    public async Task<bool> ClearCollectionAsync(CancellationToken ct = default) 
    { 
        var docs = await _dbContext.Documents.ToListAsync(ct); 
        _dbContext.Documents.RemoveRange(docs); 
        await _dbContext.SaveChangesAsync(ct); 
        await _vectorDb.DeleteUserVectorsAsync(_config.Qdrant.CollectionName, userId, ct);
        return true;
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken ct = default) 
    { 
        var d = await _dbContext.Documents.FindAsync(new object[] { documentId }, ct); 
        if (d == null) return false; 
        _dbContext.Documents.Remove(d); 
        await _dbContext.SaveChangesAsync(ct); 
        return true; 
    }

    public async Task<List<StoredChunkInfo>> GetStoredChunksAsync(int limit = 500, CancellationToken ct = default) => 
        await Task.FromResult(new List<StoredChunkInfo>());

    public async Task<int> DeleteChunksAsync(IEnumerable<string> chunkIds, CancellationToken ct = default) => 
        await Task.FromResult(0);

    public async Task<List<ChatMessageDto>> GetChatHistoryAsync(int limit = 50, CancellationToken ct = default) => 
        await _dbContext.ChatMessages.Select(m => new ChatMessageDto { Id = m.Id, Content = m.Content }).ToListAsync(ct);

    public async Task<bool> SetFeedbackAsync(Guid messageId, int effectiveness, CancellationToken ct = default) => 
        await Task.FromResult(true);

    public async Task<int> GetTotalQueriesAsync(CancellationToken ct = default) => await _dbContext.ChatMessages.CountAsync(ct);
    public async Task<long> GetStorageUsedAsync(CancellationToken ct = default) => await Task.FromResult(0L);
    public async Task<ResearchAnalyticsDto> GetAnalyticsAsync(CancellationToken ct = default) => await Task.FromResult(new ResearchAnalyticsDto());

    private async Task<ChatSession> GetOrCreateSessionAsync(Guid? sessionId, string firstQuestion, string? modelName, CancellationToken ct)
    {
        if (sessionId.HasValue) { var s = await _dbContext.ChatSessions.FindAsync(new object[] { sessionId.Value }, ct); if (s != null) return s; }
        var session = new ChatSession { Id = sessionId ?? Guid.NewGuid(), UserId = userId, Title = firstQuestion, CreatedAt = DateTime.UtcNow };
        _dbContext.ChatSessions.Add(session);
        await _dbContext.SaveChangesAsync(ct);
        return session;
    }

    private async Task<Guid> SaveAssistantMessageAsync(Guid sessionId, string content, List<SourceCitation>? sources, string? modelName, double latencyMs, CancellationToken ct)
    {
        var m = new ChatMessage { UserId = userId, ChatSessionId = sessionId, Role = "assistant", Content = content, ProcessingTimeMs = latencyMs };
        _dbContext.ChatMessages.Add(m);
        await _dbContext.SaveChangesAsync(ct);
        return m.Id;
    }
}
