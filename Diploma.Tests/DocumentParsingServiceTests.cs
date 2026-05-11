using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Diploma.Application.Interfaces;
using Diploma.Domain.Entities;
using Diploma.Infrastructure.Parsers;
using Diploma.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Diploma.Tests;

public class DocumentParsingServiceTests
{
    private readonly Mock<ILogger<DocumentParsingService>> _mockLogger;

    public DocumentParsingServiceTests()
    {
        _mockLogger = new Mock<ILogger<DocumentParsingService>>();
    }

    [Fact]
    public async Task ParseDocumentAsync_ShouldSelectCorrectParser()
    {
        // Arrange
        var mockPdfParser = new Mock<IDocumentParser>();
        mockPdfParser.Setup(p => p.IsSupported("test.pdf")).Returns(true);
        mockPdfParser.Setup(p => p.ParseAsync(It.IsAny<Stream>(), "test.pdf"))
            .ReturnsAsync(new ParsedDocument { Success = true, Content = "PDF Content" });

        var mockWordParser = new Mock<IDocumentParser>();
        mockWordParser.Setup(p => p.IsSupported("test.pdf")).Returns(false);

        var parsers = new List<IDocumentParser> { mockPdfParser.Object, mockWordParser.Object };
        var service = new DocumentParsingService(parsers, _mockLogger.Object);

        // Act
        var result = await service.ParseDocumentAsync(new MemoryStream(), "test.pdf");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("PDF Content", result.Content);
        mockPdfParser.Verify(p => p.ParseAsync(It.IsAny<Stream>(), "test.pdf"), Times.Once);
    }

    [Fact]
    public async Task ParseDocumentAsync_ShouldUseFallback_WhenNoSpecializedParserFound()
    {
        // Arrange
        var mockPdfParser = new Mock<IDocumentParser>();
        mockPdfParser.Setup(p => p.IsSupported(It.IsAny<string>())).Returns(false);

        // Fallback parser is a real class in your project, but we can mock it here for the interface
        var mockFallback = new Mock<FallbackTextParser>(new Mock<ILogger<FallbackTextParser>>().Object);
        // Note: FallbackTextParser is usually the last resort.
        
        var parsers = new List<IDocumentParser> { mockPdfParser.Object, new FallbackTextParser(new Mock<ILogger<FallbackTextParser>>().Object) };
        var service = new DocumentParsingService(parsers, _mockLogger.Object);

        // Act
        // FallbackTextParser.IsSupported usually returns true for everything or is used as last resort
        var result = await service.ParseDocumentAsync(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("raw text")), "unknown.xyz");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("raw text", result.Content);
    }
}
