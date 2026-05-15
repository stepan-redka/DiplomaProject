using Diploma.Application.DTOs;
using Diploma.Application.Interfaces;
using Diploma.Domain.Enums;
using Diploma.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Diploma.Tests;

public class IntentResolverTests
{
    private readonly Mock<IAiService> _mockAi;
    private readonly Mock<IPromptRegistry> _mockPromptRegistry;
    private readonly Mock<ILogger<IntentResolver>> _mockLogger;

    public IntentResolverTests()
    {
        _mockAi = new Mock<IAiService>();
        _mockPromptRegistry = new Mock<IPromptRegistry>();
        _mockLogger = new Mock<ILogger<IntentResolver>>();
    }

    [Fact]
    public async Task ResolveAsync_HighFidelity_ReturnsResearch()
    {
        // Arrange
        var resolver = new IntentResolver(_mockAi.Object, _mockPromptRegistry.Object, _mockLogger.Object);

        // Act
        var result = await resolver.ResolveAsync("hi", true, false);

        // Assert
        Assert.Equal(QueryIntent.Research, result);
    }

    [Fact]
    public async Task ResolveAsync_NoDocuments_ReturnsGeneral()
    {
        // Arrange
        var resolver = new IntentResolver(_mockAi.Object, _mockPromptRegistry.Object, _mockLogger.Object);

        // Act
        var result = await resolver.ResolveAsync("What is the weather?", false, false);

        // Assert
        Assert.Equal(QueryIntent.General, result);
    }

    [Fact]
    public async Task ResolveAsync_SemanticClassification_Research()
    {
        // Arrange
        _mockAi.Setup(s => s.GenerateAnswerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("RESEARCH");
        
        var resolver = new IntentResolver(_mockAi.Object, _mockPromptRegistry.Object, _mockLogger.Object);

        // Act
        var result = await resolver.ResolveAsync("Tell me about physics", false, true);

        // Assert
        Assert.Equal(QueryIntent.Research, result);
    }
}
