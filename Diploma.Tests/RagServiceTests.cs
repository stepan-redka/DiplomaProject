using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Diploma.Application.DTOs;
using Diploma.Application.Interfaces.AI;
using Diploma.Application.Interfaces.Analytics;
using Diploma.Application.Interfaces.Documents;
using Diploma.Application.Interfaces.Identity;
using Diploma.Application.Interfaces.Storage;
using Diploma.Domain.Entities;
using Diploma.Domain.Enums;
using Diploma.Infrastructure.Persistence;
using Diploma.Infrastructure.Services.AI;
using Diploma.Infrastructure.Services.Analytics;
using Diploma.Infrastructure.Services.Chat;
using Diploma.Infrastructure.Services.Documents;
using Diploma.Infrastructure.Services.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Diploma.Tests;

public class RagServiceTests
{
    private readonly Mock<ITextChunkingService> _mockChunker;
    private readonly Mock<IAiService> _mockAi;
    private readonly Mock<IVectorDatabase> _mockVectorDb;
    private readonly Mock<ICurrentUserService> _mockUserService;
    private readonly Mock<IIntentResolver> _mockIntentResolver;
    private readonly Mock<IRetrievalService> _mockRetrievalService;
    private readonly Mock<IDocumentService> _mockDocumentService;
    private readonly Mock<IAnalyticsService> _mockAnalyticsService;
    private readonly Mock<IPromptRegistry> _mockPromptRegistry;
    private readonly Mock<IEvaluationService> _mockEvaluationService;
    private readonly Mock<ITokenizerService> _mockTokenizerService;
    private readonly Mock<ISemanticCacheService> _mockSemanticCache;
    private readonly Mock<ILogger<RagService>> _mockLogger;
    private readonly RagConfiguration _config;
    private readonly ApplicationDbContext _dbContext;

    public RagServiceTests()
    {
        _mockChunker = new Mock<ITextChunkingService>();
        _mockAi = new Mock<IAiService>();
        _mockVectorDb = new Mock<IVectorDatabase>();
        _mockUserService = new Mock<ICurrentUserService>();
        _mockIntentResolver = new Mock<IIntentResolver>();
        _mockRetrievalService = new Mock<IRetrievalService>();
        _mockDocumentService = new Mock<IDocumentService>();
        _mockAnalyticsService = new Mock<IAnalyticsService>();
        _mockPromptRegistry = new Mock<IPromptRegistry>();
        _mockEvaluationService = new Mock<IEvaluationService>();
        _mockTokenizerService = new Mock<ITokenizerService>();
        _mockSemanticCache = new Mock<ISemanticCacheService>();
        _mockLogger = new Mock<ILogger<RagService>>();
        _mockSemanticCache
            .Setup(c => c.GetCachedResponseAsync(It.IsAny<string>(), It.IsAny<List<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _config = new RagConfiguration
        {
            Chunking = new ChunkingSettings { MaxChunkSize = 500, ChunkOverlap = 100 },
            Qdrant = new QdrantSettings { CollectionName = "test", VectorSize = 768, SimilarityThreshold = 0.5, DefaultTopK = 3 }
        };

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ApplicationDbContext(options, _mockUserService.Object);
    }

    private RagService CreateService()
    {
        return new RagService(
            _dbContext,
            _mockUserService.Object,
            _mockIntentResolver.Object,
            _mockRetrievalService.Object,
            _mockDocumentService.Object,
            _mockAnalyticsService.Object,
            _mockPromptRegistry.Object,
            _mockEvaluationService.Object,
            _mockTokenizerService.Object,
            _mockAi.Object,
            _mockChunker.Object,
            _mockVectorDb.Object,
            _config,
            _mockSemanticCache.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task IngestDocumentAsync_ShouldEnforceMultiTenancy()
    {
        // Arrange
        var userId = "user-123";
        _mockUserService.Setup(s => s.UserId).Returns(userId);
        _mockChunker.Setup(c => c.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new List<string> { "chunk1" });

        _mockAi.Setup(s => s.GetTextEmbeddingsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<float[]> { new float[768] });

        var service = CreateService();

        // Act
        await service.IngestDocumentAsync("Test content", "test.txt");

        // Assert
        var doc = await _dbContext.Documents.FirstAsync();
        Assert.Equal(userId, doc.UserId);

        _mockVectorDb.Verify(v => v.UpsertChunksAsync(It.IsAny<string>(), It.IsAny<IEnumerable<VectorData>>(), userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryAsync_ShouldCoordinateFlow()
    {
        // Arrange
        var userId = "user-999";
        _mockUserService.Setup(s => s.UserId).Returns(userId);

        _mockDocumentService.Setup(d => d.GetDocumentCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockIntentResolver.Setup(i => i.ResolveAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(QueryIntent.Research);

        _mockRetrievalService.Setup(r => r.GetRelevantContextAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SourceCitation>());

        _mockAi.Setup(s => s.GenerateAnswerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mocked AI Answer");

        _mockPromptRegistry.Setup(p => p.GetGeneralPrompt(It.IsAny<string>())).Returns("General Prompt");

        var service = CreateService();

        // Act
        await service.QueryAsync("What is RAG?", null, 3, null, null, false);

        // Assert
        _mockIntentResolver.Verify(i => i.ResolveAsync("What is RAG?", false, true, It.IsAny<CancellationToken>()), Times.Once);
        _mockRetrievalService.Verify(r => r.GetRelevantContextAsync("What is RAG?", 3, It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryAsync_WithSpecificDocumentBounds_AppliesPayloadFilter()
    {
        // Arrange
        var userId = "user-bounded";
        var sessionId = Guid.NewGuid();
        var docIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        _mockUserService.Setup(s => s.UserId).Returns(userId);

        var session = new ChatSession
        {
            Id = sessionId,
            UserId = userId,
            Title = "Bounded Session",
            RelatedDocumentIds = docIds
        };
        _dbContext.ChatSessions.Add(session);
        await _dbContext.SaveChangesAsync();

        _mockDocumentService.Setup(d => d.GetDocumentCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(10);
        _mockIntentResolver.Setup(i => i.ResolveAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(QueryIntent.Research);

        _mockRetrievalService.Setup(r => r.GetRelevantContextAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SourceCitation>());

        _mockAi.Setup(s => s.GenerateAnswerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Bounded Answer");

        _mockPromptRegistry.Setup(p => p.GetGeneralPrompt(It.IsAny<string>())).Returns("Prompt");

        var service = CreateService();

        // Act
        await service.QueryAsync("Bounded question", sessionId, 3, null, null, false);

        // Assert
        // Verify that GetRelevantContextAsync was called with the specific docIds from the session
        _mockRetrievalService.Verify(r => r.GetRelevantContextAsync(
            "Bounded question",
            3,
            docIds,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockSemanticCache.Verify(c => c.GetCachedResponseAsync(
            "Bounded question",
            docIds,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
