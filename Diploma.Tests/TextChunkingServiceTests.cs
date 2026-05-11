using Diploma.Infrastructure.Services;
using Xunit;

namespace Diploma.Tests;

public class TextChunkingServiceTests
{
    private readonly TextChunkingService _service;

    public TextChunkingServiceTests()
    {
        _service = new TextChunkingService();
    }

    [Fact]
    public void ChunkText_ShouldSplitText_WhenTextIsLongerThanMaxChunkSize()
    {
        // Arrange
        var text = "This is a long sentence that should definitely be split into several smaller chunks based on the character limit provided.";
        int maxChunkSize = 20;
        int overlap = 0;

        // Act
        var result = _service.ChunkText(text, maxChunkSize, overlap);

        // Assert
        Assert.NotEmpty(result);
        Assert.True(result.Count > 1, "Text should be split into multiple chunks");
        Assert.All(result, chunk => Assert.True(chunk.Length <= maxChunkSize, $"Chunk '{chunk}' exceeds max size of {maxChunkSize}"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void ChunkText_ShouldReturnEmpty_WhenTextIsInvalid(string invalidText)
    {
        // Act
        var result = _service.ChunkText(invalidText, 100, 0);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ChunkText_ShouldRespectOverlap()
    {
        // Arrange
        var text = "FirstChunkContentThatIsLong SecondChunkContentThatIsLong ThirdChunkContentThatIsLong";
        int maxChunkSize = 25;
        int overlap = 10;

        // Act
        var result = _service.ChunkText(text, maxChunkSize, overlap);

        // Assert
        Assert.True(result.Count > 1);
        
        // Verify that some text from the end of chunk 0 exists in chunk 1
        var firstChunk = result[0];
        var secondChunk = result[1];
        
        // Check for shared content (overlap)
        // We look for a subset of the first chunk inside the second
        var endOfFirst = firstChunk.Substring(firstChunk.Length - 5);
        Assert.Contains(endOfFirst, secondChunk);
    }

    [Fact]
    public void ChunkText_ShouldNotCreateInfiniteLoop_WhenOverlapIsTooLarge()
    {
        // Arrange
        var text = "Some text to chunk for testing infinite loops";
        int maxChunkSize = 10;
        int overlap = 20; // Overlap larger than chunk size is dangerous

        // Act
        var result = _service.ChunkText(text, maxChunkSize, overlap);

        // Assert
        Assert.NotEmpty(result);
        // The service should have corrected the overlap internally and finished
    }
}
