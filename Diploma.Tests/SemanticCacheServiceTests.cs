using Diploma.Application.DTOs;
using Diploma.Application.Interfaces.AI;
using Diploma.Application.Interfaces.Identity;
using Diploma.Application.Interfaces.Storage;
using Diploma.Infrastructure.Persistence;
using Diploma.Infrastructure.Services.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Diploma.Tests;

public class SemanticCacheServiceTests
{
    [Fact]
    public async Task CacheScopeHash_IsStableForSameDocumentSet_AndDifferentFromGlobalScope()
    {
        var vectorDb = new Mock<IVectorDatabase>();
        var aiService = new Mock<IAiService>();
        var currentUser = new Mock<ICurrentUserService>();
        var logger = new Mock<ILogger<SemanticCacheService>>();
        var config = new RagConfiguration
        {
            Qdrant = new QdrantSettings
            {
                CacheCollectionName = "cached_queries",
                VectorSize = 3,
                CacheSimilarityThreshold = 0.9
            }
        };

        var docA = Guid.NewGuid();
        var docB = Guid.NewGuid();
        string? savedBoundScopeHash = null;
        string? searchedBoundScopeHash = null;
        string? searchedGlobalScopeHash = null;

        currentUser.Setup(s => s.UserId).Returns("user-1");
        aiService.Setup(s => s.GetTextEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 0.1f, 0.2f, 0.3f });

        vectorDb
            .Setup(v => v.UpsertChunksAsync(
                "cached_queries",
                It.IsAny<IEnumerable<VectorData>>(),
                "user-1",
                It.IsAny<CancellationToken>()))
            .Callback<string, IEnumerable<VectorData>, string, CancellationToken>((_, vectors, _, _) =>
            {
                savedBoundScopeHash = vectors.Single().Metadata?["cache_scope_hash"].ToString();
            })
            .Returns(Task.CompletedTask);

        vectorDb
            .Setup(v => v.SearchAsync(
                "cached_queries",
                It.IsAny<ReadOnlyMemory<float>>(),
                1,
                null,
                It.IsAny<CancellationToken>(),
                It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Callback<string, ReadOnlyMemory<float>, int, List<Guid>?, CancellationToken, IReadOnlyDictionary<string, string>?>(
                (_, _, _, _, _, payloadMatches) =>
                {
                    var scopeHash = payloadMatches?["cache_scope_hash"];
                    if (searchedBoundScopeHash == null)
                    {
                        searchedBoundScopeHash = scopeHash;
                    }
                    else
                    {
                        searchedGlobalScopeHash = scopeHash;
                    }
                })
            .ReturnsAsync(new List<DocumentChunkDto>());

        var service = new SemanticCacheService(vectorDb.Object, aiService.Object, currentUser.Object, config, logger.Object);

        await service.SaveToCacheAsync("question", "answer", new List<Guid> { docB, docA });
        await service.GetCachedResponseAsync("question", new List<Guid> { docA, docB });
        await service.GetCachedResponseAsync("question", null);

        Assert.False(string.IsNullOrWhiteSpace(savedBoundScopeHash));
        Assert.Equal(savedBoundScopeHash, searchedBoundScopeHash);
        Assert.NotEqual(savedBoundScopeHash, searchedGlobalScopeHash);
    }
}
