using Diploma.Application.DTOs;
using Diploma.Application.Interfaces.AI;
using Diploma.Application.Interfaces.Identity;
using Diploma.Application.Interfaces.Storage;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Diploma.Infrastructure.Services.AI;

public class SemanticCacheService : ISemanticCacheService
{
    private const string CacheScopeHashPayloadKey = "cache_scope_hash";
    private const string CacheScopeDocumentIdsPayloadKey = "cache_scope_document_ids";

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

    public async Task<string?> GetCachedResponseAsync(string question, List<Guid>? allowedDocumentIds = null, CancellationToken ct = default)
    {
        _logger.LogDebug("Checking semantic cache for: {Question}", question);

        try
        {
            await _vectorDb.EnsureCollectionExistsAsync(_config.Qdrant.CacheCollectionName, _config.Qdrant.VectorSize, ct);
            await _vectorDb.CreatePayloadIndexAsync(_config.Qdrant.CacheCollectionName, CacheScopeHashPayloadKey, ct);

            var embedding = await _aiService.GetTextEmbeddingAsync(question, ct);
            var cacheScopeHash = CreateScopeHash(allowedDocumentIds);
            var requiredPayloadMatches = new Dictionary<string, string>
            {
                [CacheScopeHashPayloadKey] = cacheScopeHash
            };

            var results = await _vectorDb.SearchAsync(
                _config.Qdrant.CacheCollectionName,
                new ReadOnlyMemory<float>(embedding),
                1,
                null,
                ct,
                requiredPayloadMatches);

            var bestMatch = results.FirstOrDefault();
            if (bestMatch != null && bestMatch.Score >= _config.Qdrant.CacheSimilarityThreshold)
            {
                _logger.LogInformation("Semantic Cache Hit! Score: {Score}, ScopeHash: {ScopeHash}", bestMatch.Score, cacheScopeHash);
                return bestMatch.Content;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve from semantic cache.");
        }

        return null;
    }

    public async Task SaveToCacheAsync(string question, string response, List<Guid>? allowedDocumentIds = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Saving query to semantic cache: {Question}", question);

        try
        {
            var embedding = await _aiService.GetTextEmbeddingAsync(question, ct);
            var normalizedScope = NormalizeScope(allowedDocumentIds);
            var cacheScopeHash = CreateScopeHash(normalizedScope);
            var vectorData = new VectorData(
                Guid.NewGuid(),
                Guid.Empty, // Cache entries aren't tied to specific documents
                embedding,
                response,
                new Dictionary<string, object>
                {
                    { "OriginalQuestion", question },
                    { "CachedAt", DateTime.UtcNow.ToString("o") },
                    { CacheScopeHashPayloadKey, cacheScopeHash },
                    { CacheScopeDocumentIdsPayloadKey, JsonSerializer.Serialize(normalizedScope) }
                });

            await _vectorDb.UpsertChunksAsync(_config.Qdrant.CacheCollectionName, new[] { vectorData }, UserId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save to semantic cache.");
        }
    }

    public async Task ClearCacheAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Clearing semantic cache for user {UserId}", UserId);
        await _vectorDb.DeleteUserVectorsAsync(_config.Qdrant.CacheCollectionName, UserId, ct);
    }

    private static List<Guid> NormalizeScope(IEnumerable<Guid>? allowedDocumentIds)
    {
        return allowedDocumentIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .OrderBy(id => id)
            .ToList() ?? new List<Guid>();
    }

    private static string CreateScopeHash(IEnumerable<Guid>? allowedDocumentIds)
    {
        var normalizedScope = NormalizeScope(allowedDocumentIds);
        var canonicalScope = normalizedScope.Count == 0
            ? "global"
            : string.Join("|", normalizedScope.Select(id => id.ToString("N")));

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalScope));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
