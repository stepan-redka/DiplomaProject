namespace Diploma.Domain.Entities;

/// <summary>
/// Represents a text chunk stored in vector database
/// </summary>
public class DocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Foreign key for multi-tenancy isolation
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>
    /// Foreign key to parent document
    /// </summary>
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;
    
    public string Content { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
