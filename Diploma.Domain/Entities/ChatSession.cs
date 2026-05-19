using Diploma.Domain.Enums;
using Diploma.Domain.Interfaces;

namespace Diploma.Domain.Entities;

/// <summary>
/// Represents a chat session for a user
/// </summary>
public class ChatSession : IMultiTenant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key to Identity User
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    public string Title { get; set; } = "New Chat";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The IDs of documents that were active/used during this research session.
    /// This ensures reproducibility of AI findings.
    /// </summary>
    public List<Guid> RelatedDocumentIds { get; set; } = new List<Guid>();

    /// <summary>
    /// The specific AI model used for this session (e.g., llama3.1, phi3.5).
    /// </summary>
    public string? SelectedModel { get; set; }

    /// <summary>
    /// Navigation property for chat messages
    /// </summary>
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
