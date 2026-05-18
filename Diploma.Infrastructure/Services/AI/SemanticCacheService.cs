using Diploma.Application.DTOs;
using Diploma.Application.Interfaces.AI;
using Diploma.Application.Interfaces.Identity;
using Diploma.Application.Interfaces.Storage;
using Microsoft.Extensions.Logging;

namespace Diploma.Infrastructure.Services.AI;

public class SemanticCacheService : ISemanticCacheService
{
    private readonly IVectorDatabase _vectorDb;
    private readonly IAiService _aiService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SemanticCacheService> _logger;
    private readonly RagConfiguration _config;

    private string UserId => _currentUserService.UserId ?? "system";

    public SemanticCacheService(
        IVectorDatabase vectorDb,
        IAiService aiService,
        ICurrentUserService currentUserService,
        RagConfiguration config,
        ILogger<SemanticCacheService> logger)
    {
        _vectorDb = vectorDb;
        _aiService = aiService;
        _currentUserService = currentUserService;
        _config = config;
        _logger = logger;
    }

    public async Task<string?> GetCachedResponseAsync(string question, CancellationToken ct = default)
    {
        _logger.LogDebug("Checking semantic cache for: {Question}", question);

        try
        {
            await _vectorDb.EnsureCollectionExistsAsync(_config.Qdrant.CacheCollectionName, _config.Qdrant.VectorSize, ct);

            var embedding = await _aiService.GetTextEmbeddingAsync(question, ct);
            var results = await _vectorDb.SearchAsync(_config.Qdrant.CacheCollectionName, embedding, UserId, limit: 1, ct: ct);

            var bestMatch = results.FirstOrDefault();
            if (bestMatch != null && bestMatch.Score >= _config.Qdrant.CacheSimilarityThreshold)
            {
                _logger.LogInformation("Semantic Cache Hit! Score: {Score}", bestMatch.Score);
                return bestMatch.Content;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve from semantic cache.");
        }

        return null;
    }

    public async Task SaveToCacheAsync(string question, string response, CancellationToken ct = default)
    {
        _logger.LogInformation("Saving query to semantic cache: {Question}", question);

        try
        {
            var embedding = await _aiService.GetTextEmbeddingAsync(question, ct);
            var vectorData = new VectorData(
                Guid.NewGuid(),
                Guid.Empty, // Cache entries aren't tied to specific documents
                embedding,
                response,
                new Dictionary<string, object>
                {
                    { "OriginalQuestion", question },
                    { "CachedAt", DateTime.UtcNow.ToString("o") }
                });

            await _vectorDb.UpsertChunksAsync(_config.Qdrant.CacheCollectionName, new[] { vectorData }, UserId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save to semantic cache.");
        }
    }
}
