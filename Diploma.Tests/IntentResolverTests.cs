using Diploma.Application.DTOs;
using Diploma.Application.Interfaces.AI;
using Diploma.Domain.Enums;
using Diploma.Infrastructure.Services.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Diploma.Tests;

public class IntentResolverTests
{
    private readonly Mock<ILogger<IntentResolver>> _mockLogger;
    private readonly Mock<IIntentClassifier> _mockClassifier;

    public IntentResolverTests()
    {
        _mockLogger = new Mock<ILogger<IntentResolver>>();
        _mockClassifier = new Mock<IIntentClassifier>();
    }

    [Fact]
    public async Task ResolveAsync_Level0_HighFidelity_ReturnsResearch()
    {
        // Arrange
        var resolver = new IntentResolver(_mockClassifier.Object, _mockLogger.Object);

        // Act
        var result = await resolver.ResolveAsync("hi", true, true);

        // Assert
        Assert.Equal(QueryIntent.Research, result);
        _mockClassifier.Verify(p => p.Predict(It.IsAny<IntentData>()), Times.Never());
    }

    [Fact]
    public async Task ResolveAsync_Level1_NoDocuments_ReturnsGeneral()
    {
        // Arrange
        var resolver = new IntentResolver(_mockClassifier.Object, _mockLogger.Object);

        // Act
        var result = await resolver.ResolveAsync("What is the weather?", false, false);

        // Assert
        Assert.Equal(QueryIntent.General, result);
        _mockClassifier.Verify(p => p.Predict(It.IsAny<IntentData>()), Times.Never());
    }

    [Fact]
    public async Task ResolveAsync_Level2_MLInference_ResearchLabel_ReturnsResearch()
    {
        // Arrange
        _mockClassifier.Setup(p => p.Predict(It.IsAny<IntentData>()))
            .Returns(new IntentPrediction { PredictedLabel = "RESEARCH", Score = new[] { 0.9f } });
        
        var resolver = new IntentResolver(_mockClassifier.Object, _mockLogger.Object);

        // Act
        var result = await resolver.ResolveAsync("Analyze the market trends", false, true);

        // Assert
        Assert.Equal(QueryIntent.Research, result);
        _mockClassifier.Verify(p => p.Predict(It.IsAny<IntentData>()), Times.Once());
    }

    [Fact]
    public async Task ResolveAsync_Level2_MLInference_GeneralLabel_ReturnsGeneral()
    {
        // Arrange
        _mockClassifier.Setup(p => p.Predict(It.IsAny<IntentData>()))
            .Returns(new IntentPrediction { PredictedLabel = "GENERAL", Score = new[] { 0.8f } });
        
        var resolver = new IntentResolver(_mockClassifier.Object, _mockLogger.Object);

        // Act
        var result = await resolver.ResolveAsync("Hello bot", false, true);

        // Assert
        Assert.Equal(QueryIntent.General, result);
    }

    [Fact]
    public async Task ResolveAsync_MLInference_Error_ReturnsResearchSafetyFallback()
    {
        // Arrange
        _mockClassifier.Setup(p => p.Predict(It.IsAny<IntentData>()))
            .Throws(new Exception("ML Engine Error"));
        
        var resolver = new IntentResolver(_mockClassifier.Object, _mockLogger.Object);

        // Act
        var result = await resolver.ResolveAsync("Something complex", false, true);

        // Assert
        Assert.Equal(QueryIntent.Research, result);
    }
}
