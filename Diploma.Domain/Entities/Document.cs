using Diploma.Domain.Enums;
using Diploma.Domain.Interfaces;

namespace Diploma.Domain.Entities;

/// <summary>
/// Represents a document uploaded and processed by the system
/// </summary>
public class Document : IMultiTenant
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
    /// Processing status for background ingestion
    /// </summary>
    public IngestionStatus Status { get; set; } = IngestionStatus.Pending;

    /// <summary>
    /// Detailed error if ingestion fails
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Time taken to parse and ingest the document in milliseconds.
    /// </summary>
    public double ProcessingTimeMs { get; set; }

    /// <summary>
    /// Actual file size in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Navigation property to chunks
    /// </summary>
    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}
