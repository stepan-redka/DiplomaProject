using Diploma.Application.DTOs;
using Diploma.Application.Interfaces;
using Diploma.Domain.Entities;
using Diploma.Domain.Enums;
using Diploma.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Collections.Concurrent;
using Document = Diploma.Domain.Entities.Document;

namespace Diploma.Infrastructure.Services;

public class RagService : IRagService
{
    private readonly ITextChunkingService _chunkingService;
    private readonly IAiService _aiService;
    private readonly IVectorDatabase _vectorDb;
    private readonly ApplicationDbContext _dbContext;
    private readonly RagConfiguration _config;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<RagService> _logger;
    private string userId => _currentUserService.UserId ?? throw new UnauthorizedAccessException("User must be authenticated to perform this action.");

    public RagService(
        ITextChunkingService chunkingService,
        IAiService aiService,
        IVectorDatabase vectorDb,
        ApplicationDbContext dbContext,
        RagConfiguration config,
        ICurrentUserService currentUserService,
        ILogger<RagService> logger)
    {
        _chunkingService = chunkingService;
        _aiService = aiService;
        _vectorDb = vectorDb;
        _dbContext = dbContext;
        _config = config;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<IngestResponse> IngestDocumentAsync(string content, string documentName, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting ingestion for document: {DocumentName}. Content length: {ContentLength}", documentName, content.Length);
        var sw = Stopwatch.StartNew();

        try
        {
            var textChunks = _chunkingService.ChunkText(content, _config.Chunking.MaxChunkSize, _config.Chunking.ChunkOverlap);
            _logger.LogDebug("Split document into {ChunkCount} chunks.", textChunks.Count);

            var documentId = Guid.NewGuid();
            var document = new Document
            {
                Id = documentId,
                UserId = userId,
                FileName = documentName,
                CreatedAt = DateTime.UtcNow,
                Content = content
            };
            _dbContext.Documents.Add(document);

            // PERFORMANCE OPTIMIZATION: Use Batch Embedding Generation
            // This is MUCH faster than individual calls in a loop
            var embeddings = await _aiService.GetTextEmbeddingsAsync(textChunks, ct);
            
            var vectorDataList = new List<VectorData>();
            var chunksToAdd = new List<DocumentChunk>();

            for (int i = 0; i < textChunks.Count; i++)
            {
                var chunkId = Guid.NewGuid();
                var text = textChunks[i];
                var embedding = embeddings[i];

                vectorDataList.Add(new VectorData(
                    chunkId,
                    documentId,
                    embedding,
                    text,
                    new Dictionary<string, object> { { "index", i } }
                ));

                chunksToAdd.Add(new DocumentChunk
                {
                    Id = chunkId,
                    UserId = userId,
                    DocumentId = documentId,
                    Content = text,
                    ChunkIndex = i,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // PERFORMANCE OPTIMIZATION: Use AddRange for batch addition
            _dbContext.DocumentChunks.AddRange(chunksToAdd);

            // Update document status
            document.Status = IngestionStatus.Success;

            await _dbContext.SaveChangesAsync(ct);
            _logger.LogDebug("Metadata and {ChunkCount} chunks saved to PostgreSQL.", chunksToAdd.Count);

            await _vectorDb.EnsureCollectionExistsAsync(_config.Qdrant.CollectionName, _config.Qdrant.VectorSize, ct);
            await _vectorDb.UpsertChunksAsync(_config.Qdrant.CollectionName, vectorDataList, userId, ct);
            
            sw.Stop();
            _logger.LogInformation("Successfully ingested {DocumentName} in {ElapsedMs}ms.", documentName, sw.ElapsedMilliseconds);

            return new IngestResponse { Success = true, ChunksCreated = textChunks.Count, Message = "Success!" }; 
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Ingestion of {DocumentName} was canceled.", documentName);
            return new IngestResponse { Success = false, Message = "Operation was canceled." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest document {DocumentName}", documentName);
            return new IngestResponse { Success = false, Message = ex.Message };
        }
    }

    public async Task<QueryResponse> QueryAsync(string question, int topK = 3, CancellationToken ct = default)
    {
        _logger.LogInformation("Executing RAG query: {Question}", question);
        var sw = Stopwatch.StartNew();

        try
        {
            var session = await _dbContext.ChatSessions.FirstOrDefaultAsync(ct);
            if (session == null)
            {
                session = new ChatSession { UserId = userId, Title = "Default Chat" };
                _dbContext.ChatSessions.Add(session);
                await _dbContext.SaveChangesAsync(ct);
            }

            _dbContext.ChatMessages.Add(new ChatMessage
            {
                UserId = userId,
                ChatSessionId = session.Id,
                Role = "user",
                Content = question
            });

            var questionEmbedding = await _aiService.GetTextEmbeddingAsync(question, ct);
            var results = (await _vectorDb.SearchAsync(_config.Qdrant.CollectionName, questionEmbedding, userId, topK, ct)).ToList();

            _logger.LogDebug("Vector search returned {ResultCount} chunks.", results.Count);

            // Fetch document names for source transparency
            var docIds = results.Select(r => r.DocumentId).Distinct().ToList();
            var docNames = await _dbContext.Documents
                .Where(d => docIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.FileName, ct);

            var contextText = string.Join("\n---\n", results.Select(r => r.Content));
            var prompt = $"Context information is below.\n---------------------\n{contextText}\n---------------------\nGiven the context information and not prior knowledge, answer the query.\nQuery: {question}\nAnswer: " ;

            var answer = await _aiService.GenerateAnswerAsync(prompt, ct);
            
            var assistantMessageId = Guid.NewGuid();
            _dbContext.ChatMessages.Add(new ChatMessage
            {
                Id = assistantMessageId,
                UserId = userId,
                ChatSessionId = session.Id,
                Role = "assistant",
                Content = answer
            });
            
            await _dbContext.SaveChangesAsync(ct);

            sw.Stop();
            _logger.LogInformation("Query completed in {ElapsedMs}ms.", sw.ElapsedMilliseconds);

            return new QueryResponse 
            { 
                MessageId = assistantMessageId,
                Answer = answer,
                Sources = results.Select(r => new SourceCitation 
                { 
                    Content = r.Content, 
                    SourceDocument = docNames.TryGetValue(r.DocumentId, out var name) ? name : "Unknown Source",
                    Score = r.Score 
                }).ToList(),
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Query for '{Question}' was canceled.", question);
            return new QueryResponse { Answer = "The operation was canceled.", ProcessingTimeMs = sw.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during QueryAsync for question: {Question}", question);
            throw;
        }
    }

    public async Task<List<ChatMessageDto>> GetChatHistoryAsync(int limit = 50, CancellationToken ct = default)
    {
        return await _dbContext.ChatMessages
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ChatMessageDto
            {
                Id = m.Id,
                Role = m.Role,
                Content = m.Content,
                CreatedAt = m.CreatedAt,
                Effectiveness = (int)m.Effectiveness
            })
            .ToListAsync(ct);
    }

    public async Task<bool> SetFeedbackAsync(Guid messageId, int effectiveness, CancellationToken ct = default)
    {
        var message = await _dbContext.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == messageId && m.UserId == userId, ct);

        if (message == null) return false;

        message.Effectiveness = (MessageEffectiveness)effectiveness;
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EnsureCollectionExistsAsync(CancellationToken ct = default)
    {
        await _vectorDb.EnsureCollectionExistsAsync(_config.Qdrant.CollectionName, _config.Qdrant.VectorSize, ct);
        return true;
    }

    public async Task<int> GetDocumentCountAsync(CancellationToken ct = default) => await _dbContext.Documents.CountAsync(ct);

    public async Task<List<DocumentDto>> GetUserDocumentsAsync(CancellationToken ct = default)
    {
        return await _dbContext.Documents
            .Select(d => new DocumentDto
            {
                Id = d.Id,
                FileName = d.FileName,
                CreatedAt = d.CreatedAt,
                ChunkCount = d.Chunks.Count
            })
            .ToListAsync(ct);
    }

    public async Task<List<StoredChunkInfo>> GetStoredChunksAsync(int limit = 500, CancellationToken ct = default)
    {
        return await _dbContext.DocumentChunks
            .Include(c => c.Document)
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .Select(c => new StoredChunkInfo
            {
                Id = c.Id.ToString(),
                SourceDocument = c.Document.FileName,
                ContentPreview = c.Content.Length > 100 ? c.Content.Substring(0, 100) + "..." : c.Content,
                ChunkIndex = c.ChunkIndex
            })
            .ToListAsync(ct);
    }

    public async Task<int> DeleteChunksAsync(IEnumerable<string> chunkIds, CancellationToken ct = default)
    {
        var guids = chunkIds.Select(Guid.Parse).ToList();
        var chunksToDelete = await _dbContext.DocumentChunks
            .Where(c => guids.Contains(c.Id))
            .ToListAsync(ct);

        if(!chunksToDelete.Any()) return 0;

        // PERFORMANCE OPTIMIZATION: Group by DocumentId to minimize redundant calls to Vector DB
        var documentGroups = chunksToDelete.GroupBy(c => c.DocumentId);

        await Parallel.ForEachAsync(documentGroups, new ParallelOptions 
        { 
            CancellationToken = ct, 
            MaxDegreeOfParallelism = Environment.ProcessorCount 
        }, async (group, token) => 
        {
            // The vector DB delete logic is document-scoped in this implementation
            await _vectorDb.DeleteDocumentVectorsAsync(_config.Qdrant.CollectionName, group.Key, userId, token);
        });

        _dbContext.DocumentChunks.RemoveRange(chunksToDelete);
        await _dbContext.SaveChangesAsync(ct);
        return chunksToDelete.Count;
    }

    public async Task<int> GetTotalQueriesAsync(CancellationToken ct = default)
    {
        return await _dbContext.ChatMessages
            .Where(m => m.UserId == userId && m.Role == "user")
            .CountAsync(ct);
    }

    public async Task<long> GetStorageUsedAsync(CancellationToken ct = default)
    {
        // Estimate storage based on content length in characters (rough approx for enterprise feel)
        return await _dbContext.Documents
            .Where(d => d.UserId == userId)
            .SumAsync(d => (long)d.Content.Length, ct);
    }
}
