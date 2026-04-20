using Diploma.Domain.Enums;

namespace Diploma.Domain.Entities;

/// <summary>
/// Represents a document uploaded and processed by the system
/// </summary>
public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Foreign key to Identity User
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DocumentType Type { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Navigation property to chunks
    /// </summary>
    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}
