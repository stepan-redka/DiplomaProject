using Diploma.Application.DTOs;
using Diploma.Application.Interfaces.AI;
using Diploma.Application.Interfaces.Analytics;
using Diploma.Application.Interfaces.Storage;
using Diploma.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Diploma.Infrastructure.Services.AI;

public class RetrievalService : IRetrievalService
{
    private readonly IVectorDatabase _vectorDb;
    private readonly IAiService _aiService;
    private readonly ApplicationDbContext _dbContext;
    private readonly RagConfiguration _config;
    private readonly IEvaluationService _evaluationService;
    private readonly ILogger<RetrievalService> _logger;

    public RetrievalService(
        IVectorDatabase vectorDb,
        IAiService aiService,
        ApplicationDbContext dbContext,
        RagConfiguration config,
        IEvaluationService evaluationService,
        ILogger<RetrievalService> logger)
    {
        _vectorDb = vectorDb;
        _aiService = aiService;
        _dbContext = dbContext;
        _config = config;
        _evaluationService = evaluationService;
        _logger = logger;
    }

    public async Task<List<SourceCitation>> GetRelevantContextAsync(string question, int? topK = null, List<Guid>? allowedDocumentIds = null, CancellationToken ct = default)
    {
        var questionEmbedding = await _aiService.GetTextEmbeddingAsync(question, ct);
        var k = topK ?? _config.Qdrant.DefaultTopK;

        // Passing the optional document filter to the vector store using the updated signature
        var searchResults = await _vectorDb.SearchAsync(_config.Qdrant.CollectionName, new ReadOnlyMemory<float>(questionEmbedding), k, allowedDocumentIds, ct);

        var validResults = searchResults.Where(r => r.Score >= _config.Qdrant.SimilarityThreshold).ToList();

        if (!validResults.Any()) return new List<SourceCitation>();

        var docIds = validResults.Select(r => r.DocumentId).Distinct().ToList();
        var docNames = await _dbContext.Documents
            .Where(d => docIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.FileName, ct);

        return validResults.Select(r => new SourceCitation
        {
            Content = r.Content,
            SourceDocument = docNames.GetValueOrDefault(r.DocumentId, "Unknown Source"),
            Score = _evaluationService.NormalizeScore(r.Score, _config.Qdrant.SimilarityThreshold),
            DocumentId = r.DocumentId,
            ChunkId = r.ChunkId,
            ChunkIndex = TryGetChunkIndex(r.Metadata)
        }).ToList();
    }

    private static int TryGetChunkIndex(Dictionary<string, object> metadata)
    {
        if (metadata.TryGetValue("chunk_index", out var value) &&
            int.TryParse(value?.ToString()?.Trim('"'), out var chunkIndex))
        {
            return chunkIndex;
        }

        if (metadata.TryGetValue("index", out var legacyValue) &&
            int.TryParse(legacyValue?.ToString()?.Trim('"'), out var legacyChunkIndex))
        {
            return legacyChunkIndex;
        }

        return -1;
    }
}
