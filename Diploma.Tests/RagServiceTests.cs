using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Diploma.Application.DTOs;
using Diploma.Application.Interfaces;
using Diploma.Domain.Entities;
using Diploma.Infrastructure.Persistence;
using Diploma.Infrastructure.Services;
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
    private readonly Mock<ILogger<RagService>> _mockLogger;
    private readonly RagConfiguration _config;
    private readonly ApplicationDbContext _dbContext;

    public RagServiceTests()
    {
        _mockChunker = new Mock<ITextChunkingService>();
        _mockAi = new Mock<IAiService>();
        _mockVectorDb = new Mock<IVectorDatabase>();
        _mockUserService = new Mock<ICurrentUserService>();
        _mockLogger = new Mock<ILogger<RagService>>();

        _config = new RagConfiguration
        {
            Chunking = new ChunkingSettings { MaxChunkSize = 500, ChunkOverlap = 100 },
            Qdrant = new QdrantSettings { CollectionName = "test", VectorSize = 768 }
        };

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ApplicationDbContext(options, _mockUserService.Object);
    }

    [Fact]
    public async Task IngestDocumentAsync_ShouldEnforceMultiTenancy()
    {
        // Arrange
        var userId = "user-123";
        _mockUserService.Setup(s => s.UserId).Returns(userId);
        _mockChunker.Setup(c => c.ChunkText(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(new List<string> { "chunk1" });
        
        // FIX: Specify CancellationToken
        _mockAi.Setup(s => s.GetTextEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);

        var service = new RagService(_mockChunker.Object, _mockAi.Object, _mockVectorDb.Object, _dbContext, _config, _mockUserService.Object, _mockLogger.Object);

        // Act
        await service.IngestDocumentAsync("Test content", "test.txt");

        // Assert
        var doc = await _dbContext.Documents.FirstAsync();
        Assert.Equal(userId, doc.UserId);
        
        _mockVectorDb.Verify(v => v.UpsertChunksAsync(It.IsAny<string>(), It.IsAny<IEnumerable<VectorData>>(), userId), Times.Once);
    }

    [Fact]
    public async Task QueryAsync_ShouldFilterByUserId()
    {
        // Arrange
        var userId = "user-999";
        _mockUserService.Setup(s => s.UserId).Returns(userId);
        _mockAi.Setup(s => s.GetTextEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[768]);
        _mockAi.Setup(s => s.GenerateAnswerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Mocked AI Answer");
        
        // FIX: Specify the limit explicitly to avoid CS0854
        _mockVectorDb.Setup(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), userId, It.IsAny<int>()))
            .ReturnsAsync(new List<ScoredChunkDto>());

        var service = new RagService(_mockChunker.Object, _mockAi.Object, _mockVectorDb.Object, _dbContext, _config, _mockUserService.Object, _mockLogger.Object);

        // Act
        await service.QueryAsync("What is RAG?", 3);

        // Assert
        _mockVectorDb.Verify(v => v.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), userId, 3), Times.Once);
    }

    [Fact]
    public async Task ClearCollectionAsync_ShouldOnlyAffectCurrentUser()
    {
        // Arrange
        var userA = "user-A";
        var userB = "user-B";

        // Seed data for User A
        _mockUserService.Setup(s => s.UserId).Returns(userA);
        _dbContext.Documents.Add(new Document { Id = Guid.NewGuid(), FileName = "docA.txt" });
        await _dbContext.SaveChangesAsync();

        // Seed data for User B
        _mockUserService.Setup(s => s.UserId).Returns(userB);
        _dbContext.Documents.Add(new Document { Id = Guid.NewGuid(), FileName = "docB.txt" });
        await _dbContext.SaveChangesAsync();

        // Switch back to User A for the actual test
        _mockUserService.Setup(s => s.UserId).Returns(userA);

        var service = new RagService(_mockChunker.Object, _mockAi.Object, _mockVectorDb.Object, _dbContext, _config, _mockUserService.Object, _mockLogger.Object);

        // Act
        await service.ClearCollectionAsync();

        // Assert
        // We need a fresh context or use IgnoreQueryFilters to see everything
        var allDocs = await _dbContext.Documents.IgnoreQueryFilters().ToListAsync();
        Assert.Single(allDocs);
        Assert.Equal(userB, allDocs[0].UserId);
    }
}
