using Diploma.Application.DTOs;
using Diploma.Application.Interfaces;
using Diploma.Domain.Entities;
using Diploma.Domain.Enums;
using Diploma.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Text.Json;
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
                Content = content,
                FileSizeBytes = System.Text.Encoding.UTF8.GetByteCount(content)
            };
            _dbContext.Documents.Add(document);

            // PERFORMANCE OPTIMIZATION: Use Batch Embedding Generation
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

            _dbContext.DocumentChunks.AddRange(chunksToAdd);
            document.Status = IngestionStatus.Success;

            await _dbContext.SaveChangesAsync(ct);

            await _vectorDb.EnsureCollectionExistsAsync(_config.Qdrant.CollectionName, _config.Qdrant.VectorSize, ct);
            await _vectorDb.UpsertChunksAsync(_config.Qdrant.CollectionName, vectorDataList, userId, ct);
            
            sw.Stop();
            document.ProcessingTimeMs = sw.ElapsedMilliseconds;
            await _dbContext.SaveChangesAsync(ct);
            
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

    public async Task<QueryResponse> QueryAsync(string question, Guid? sessionId = null, int? topK = null, QueryIntent? intent = null, string? modelName = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Executing RAG query: {Question} (Explicit Intent: {Intent}, Model: {Model})", 
            question, intent?.ToString() ?? "None", modelName ?? "Default");
        var sw = Stopwatch.StartNew();

        try
        {
            ChatSession? session = null;
            if (sessionId.HasValue)
            {
                session = await _dbContext.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId.Value, ct);
            }

            if (session == null)
            {
                var title = question.Length > 30 ? question.Substring(0, 27) + "..." : question;
                session = new ChatSession 
                { 
                    UserId = userId, 
                    Title = title,
                    RelatedDocumentIds = await _dbContext.Documents.Select(d => d.Id).ToListAsync(ct),
                    SelectedModel = modelName
                };
                _dbContext.ChatSessions.Add(session);
                await _dbContext.SaveChangesAsync(ct);
            }
            else
            {
                session.LastUpdatedAt = DateTime.UtcNow;
                // Update model if changed/specified
                if (!string.IsNullOrEmpty(modelName) && session.SelectedModel != modelName)
                {
                    session.SelectedModel = modelName;
                    await _dbContext.SaveChangesAsync(ct);
                }
            }

            _dbContext.ChatMessages.Add(new ChatMessage
            {
                UserId = userId,
                ChatSessionId = session.Id,
                Role = "user",
                Content = question
            });

            // --- LEVEL 0 OPTIMIZATION: EMPTY REPO CHECK ---
            var docCount = await _dbContext.Documents.CountAsync(ct);
            if (docCount == 0 && intent != QueryIntent.Research)
            {
                _logger.LogInformation("Zero-Document state detected. Routing directly to GENERAL path for: {Question}", question);
                var emptyRepoPrompt = $"The user asked: '{question}'. Note: There are currently NO documents in their research repository. Answer their question to the best of your ability as a helpful research assistant, but keep the response concise.";
                return await HandleGeneralQueryAsync(session, question, emptyRepoPrompt, sw, modelName, ct);
            }

            // --- LEVEL 1 OPTIMIZATION: SEMANTIC ROUTING / FAST-PATH ---
            // Use Fast-Path to detect greetings even if intent is Research to prevent irrelevant context binding
            var detectedIntent = await ResolveIntentWithFastPathAsync(question, ct);
            
            // If Research Mode is ON, we are very biased toward Research path unless it's a confirmed simple greeting
            var resolvedIntent = (intent == QueryIntent.Research && !IsSimpleGreeting(question))
                ? QueryIntent.Research
                : detectedIntent;

            if (resolvedIntent == QueryIntent.General)
            {
                _logger.LogInformation("Router directed to GENERAL path for: {Question}", question);
                var generalPrompt = $"The user asked: '{question}'. You are a professional AI research assistant. Respond naturally and concisely. If it's a greeting, acknowledge it politely and ask how you can assist with their research repository. Do not mention searching a database.";
                return await HandleGeneralQueryAsync(session, question, generalPrompt, sw, modelName, ct);
            }

            _logger.LogInformation("Router directed to RESEARCH path for: {Question}", question);
            var questionEmbedding = await _aiService.GetTextEmbeddingAsync(question, ct);
            
            // Apply dynamic retrieval depth and similarity thresholding
            var k = topK ?? _config.Qdrant.DefaultTopK;
            var searchResults = (await _vectorDb.SearchAsync(_config.Qdrant.CollectionName, questionEmbedding, userId, k, ct)).ToList();
            
            // RELEVANCE GATING: Filter results by threshold to prevent "hallucinated binding"
            // We use the raw score for filtering, but will normalize it for the UI
            var results = searchResults
                .Where(r => r.Score >= _config.Qdrant.SimilarityThreshold)
                .ToList();

            _logger.LogDebug("Vector search returned {TotalCount} chunks. {FilteredCount} passed threshold {Threshold}.", 
                searchResults.Count, results.Count, _config.Qdrant.SimilarityThreshold);

            // Fetch document names for source transparency
            var docIds = results.Select(r => r.DocumentId).Distinct().ToList();
            var docNames = await _dbContext.Documents
                .Where(d => docIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.FileName, ct);

            var contextText = results.Any() 
                ? string.Join("\n---\n", results.Select(r => $"[Source: {docNames.GetValueOrDefault(r.DocumentId, "Unknown")}] {r.Content}")) 
                : "No relevant documents found.";
            
            var prompt = results.Any()
                ? $"""
                  You are a professional research assistant. Use the following pieces of retrieved context to answer the user's question.
                  If you don't know the answer based on the context, state that the repository doesn't have specific data, then provide a general answer if possible but CLEARLY distinguish it.
                  Always prefer information from the context.
                  
                  CONTEXT:
                  ---------------------
                  {contextText}
                  ---------------------
                  
                  QUERY: {question}
                  
                  INSTRUCTIONS:
                  1. Answer accurately based on the context.
                  2. Use a professional, academic tone.
                  3. If multiple sources are provided, synthesize them.
                  
                  ANSWER:
                  """
                : $"""
                  The user asked: '{question}'. 
                  Note: No relevant documents were found in the research repository with high enough similarity for this specific query.
                  Provide a professional response informing the user that the knowledge base doesn't contain specific data on this. 
                  However, as a research assistant, provide a helpful general answer to their question if possible, while being clear that this information is NOT from their uploaded documents.
                  """;

            var answer = await _aiService.GenerateAnswerAsync(prompt, modelName, ct);
            
            var assistantMessageId = Guid.NewGuid();
            var sources = results.Select(r => new SourceCitation 
            { 
                Content = r.Content, 
                SourceDocument = docNames.TryGetValue(r.DocumentId, out var name) ? name : "Unknown Source",
                Score = NormalizeScore(r.Score, _config.Qdrant.SimilarityThreshold) 
            }).ToList();

            _dbContext.ChatMessages.Add(new ChatMessage
            {
                Id = assistantMessageId,
                UserId = userId,
                ChatSessionId = session.Id,
                Role = "assistant",
                Content = answer,
                Metadata = sources.Any() ? JsonSerializer.Serialize(sources) : null,
                ModelName = modelName ?? _config.Ollama.ChatModel,
                ProcessingTimeMs = sw.ElapsedMilliseconds,
                TokenCount = answer.Length / 4 // Heuristic: 1 token ~= 4 chars
            });
            
            await _dbContext.SaveChangesAsync(ct);

            sw.Stop();
            _logger.LogInformation("Query completed in {ElapsedMs}ms.", sw.ElapsedMilliseconds);

            return new QueryResponse 
            { 
                MessageId = assistantMessageId,
                SessionId = session.Id,
                SessionTitle = session.Title,
                Answer = answer,
                Sources = sources,
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

    private double NormalizeScore(float rawScore, double threshold)
    {
        // Vector similarity scores (Cosine) often cluster. 
        // We want to transform them into a "confidence" percentage that feels natural to users.
        // A score at the threshold should feel like "Fairly Relevant" (~50-60%)
        // A score of 0.8+ should feel like "Highly Relevant" (90%+)
        
        if (rawScore >= 0.9) return rawScore; // Already very high
        
        // Linear mapping from [threshold, 0.9] to [0.6, 0.95]
        double normalized = 0.6 + (rawScore - threshold) * (0.35 / (0.9 - threshold));
        return Math.Clamp(normalized, 0.0, 1.0);
    }

    private async Task<QueryResponse> HandleGeneralQueryAsync(ChatSession session, string question, string prompt, Stopwatch sw, string? modelName, CancellationToken ct)
    {
        var answer = await _aiService.GenerateAnswerAsync(prompt, modelName, ct);
        
        var messageId = Guid.NewGuid();
        _dbContext.ChatMessages.Add(new ChatMessage
        {
            Id = messageId,
            UserId = userId,
            ChatSessionId = session.Id,
            Role = "assistant",
            Content = answer
        });
        await _dbContext.SaveChangesAsync(ct);
        
        sw.Stop();
        _logger.LogInformation("General query handled in {ElapsedMs}ms.", sw.ElapsedMilliseconds);
        
        return new QueryResponse 
        { 
            MessageId = messageId,
            SessionId = session.Id,
            SessionTitle = session.Title,
            Answer = answer,
            Sources = new List<SourceCitation>(),
            ProcessingTimeMs = sw.ElapsedMilliseconds
        };
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
                Effectiveness = (int)m.Effectiveness,
                ModelName = m.ModelName,
                ProcessingTimeMs = m.ProcessingTimeMs,
                TokenCount = m.TokenCount
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
                ChunkCount = d.Chunks.Count,
                FileSizeBytes = d.FileSizeBytes,
                ProcessingTimeMs = d.ProcessingTimeMs
            })
            .ToListAsync(ct);
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting document {DocumentId} for user: {UserId}", documentId, userId);
        try
        {
            var document = await _dbContext.Documents
                .FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId, ct);

            if (document == null) return false;

            // 1. Delete from Vector DB
            await _vectorDb.DeleteDocumentVectorsAsync(_config.Qdrant.CollectionName, documentId, userId, ct);

            // 2. Delete from PostgreSQL
            _dbContext.Documents.Remove(document);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Successfully deleted document {DocumentId}", documentId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document {DocumentId}", documentId);
            return false;
        }
    }

    public async Task<bool> ClearCollectionAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Clearing all data for user: {UserId}", userId);
        try
        {
            // 1. Delete from Vector DB
            await _vectorDb.DeleteUserVectorsAsync(_config.Qdrant.CollectionName, userId, ct);

            // 2. Delete from PostgreSQL
            // Global query filters will ensure we only delete the current user's documents
            var documents = await _dbContext.Documents.ToListAsync(ct);
            _dbContext.Documents.RemoveRange(documents);
            
            await _dbContext.SaveChangesAsync(ct);
            
            _logger.LogInformation("Successfully cleared all data for user: {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear collection for user: {UserId}", userId);
            return false;
        }
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

    private async Task<QueryIntent> ResolveIntentWithFastPathAsync(string question, CancellationToken ct)
    {
        // FAST-PATH: Static analysis for greetings/small talk to save LLM tokens and time
        var greetings = new[] { "hi", "hello", "hey", "thanks", "thank you", "good morning", "good evening", "who are you" };
        var cleanQuestion = question.Trim().ToLower();
        
        if (greetings.Any(g => cleanQuestion.StartsWith(g) || cleanQuestion == g))
        {
            _logger.LogDebug("Fast-path: Detected GENERAL intent via static analysis.");
            return QueryIntent.General;
        }

        return await GetQueryIntentAsync(question, ct);
    }

    private async Task<QueryIntent> GetQueryIntentAsync(string question, CancellationToken ct)
    {
        // THESIS NOTE: Using the LLM as a Semantic Router to minimize 
        // infrastructure pressure on the Vector Database.
        var intentPrompt = $"""
            Analyze the user's query: '{question}'
            Categorize it as either:
            1. 'RESEARCH' - If it asks about facts, data, documents, or specific technical knowledge that would likely be in a repository.
            2. 'GENERAL' - If it is a greeting, small talk, or a question about general world knowledge (like weather, basic facts).
            
            Respond ONLY with the word 'RESEARCH' or 'GENERAL'.
            """;

        try
        {
            var result = await _aiService.GenerateAnswerAsync(intentPrompt, ct: ct);
            return result.Contains("RESEARCH", StringComparison.OrdinalIgnoreCase) 
                ? QueryIntent.Research 
                : QueryIntent.General;
        }
        catch
        {
            // Fallback to Research to ensure we don't miss data if AI is flaky
            return QueryIntent.Research;
        }
    }

    public async Task<ResearchAnalyticsDto> GetAnalyticsAsync(CancellationToken ct = default)
    {
        var analytics = new ResearchAnalyticsDto();

        // 1. Model Latency
        analytics.ModelLatency = await _dbContext.ChatMessages
            .Where(m => m.UserId == userId && m.Role == "assistant" && m.ModelName != null)
            .GroupBy(m => m.ModelName)
            .Select(g => new ModelLatencyData
            {
                ModelName = g.Key!,
                AvgLatencyMs = g.Average(m => m.ProcessingTimeMs)
            })
            .ToListAsync(ct);

        // 2. Ingestion Efficiency
        analytics.IngestionEfficiency = await _dbContext.Documents
            .Where(d => d.UserId == userId)
            .Select(d => new IngestionData
            {
                FileSizeBytes = d.FileSizeBytes,
                ProcessingTimeMs = d.ProcessingTimeMs,
                FileName = d.FileName
            })
            .ToListAsync(ct);

        // 3. Semantic Precision (Last 20)
        var lastMessages = await _dbContext.ChatMessages
            .Where(m => m.UserId == userId && m.Role == "assistant" && m.Metadata != null)
            .OrderByDescending(m => m.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        foreach (var msg in lastMessages)
        {
            try
            {
                var sources = JsonSerializer.Deserialize<List<SourceCitation>>(msg.Metadata!);
                if (sources != null && sources.Any())
                {
                    analytics.SemanticPrecision.Add(new PrecisionData
                    {
                        Timestamp = msg.CreatedAt,
                        Score = sources.Max(s => s.Score)
                    });
                }
            }
            catch { /* Ignore corrupt metadata */ }
        }

        // 4. Generation Throughput
        analytics.GenerationThroughput = await _dbContext.ChatMessages
            .Where(m => m.UserId == userId && m.Role == "assistant" && m.ProcessingTimeMs > 0 && m.TokenCount > 0)
            .GroupBy(m => m.ModelName)
            .Select(g => new ThroughputData
            {
                ModelName = g.Key ?? "Unknown",
                TokensPerSec = g.Average(m => m.TokenCount / (m.ProcessingTimeMs / 1000.0))
            })
            .ToListAsync(ct);

        // 5. Math-to-Human Correlation
        foreach (var msg in lastMessages)
        {
            try
            {
                var sources = JsonSerializer.Deserialize<List<SourceCitation>>(msg.Metadata!);
                if (sources != null && sources.Any())
                {
                    analytics.MathHumanCorrelation.Add(new CorrelationData
                    {
                        Score = sources.Max(s => s.Score),
                        Feedback = (int)msg.Effectiveness
                    });
                }
            }
            catch { }
        }

        // 6. Knowledge Density
        analytics.KnowledgeDensity = await _dbContext.Documents
            .Where(d => d.UserId == userId)
            .Select(d => new DensityData
            {
                DocName = d.FileName,
                Chunks = d.Chunks.Count
            })
            .ToListAsync(ct);

        return analytics;
    }

    private bool IsSimpleGreeting(string question)
    {
        var greetings = new[] { "hi", "hello", "hey", "greetings", "good morning", "good afternoon", "good evening", "howdy", "thanks", "thank you", "who are you" };
        var clean = question.Trim().ToLower().TrimEnd('?', '!', '.', ',');
        
        // Match if it's exactly a greeting or a short phrase containing one
        return greetings.Contains(clean) || (clean.Split(' ').Length <= 3 && greetings.Any(g => clean.StartsWith(g)));
    }
}
