namespace Diploma.Domain;

/// <summary>
/// Represents a text chunk stored in vector database
/// </summary>
public class DocumentChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public string SourceDocument { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
