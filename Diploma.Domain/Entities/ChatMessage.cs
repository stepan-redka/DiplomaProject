using Diploma.Domain.Interfaces;

namespace Diploma.Domain.Entities;

/// <summary>
/// Individual message within a chat session
/// </summary>
public class ChatMessage : IMultiTenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Foreign key for multi-tenancy isolation
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>
    /// Foreign key to parent session
    /// </summary>
    public Guid ChatSessionId { get; set; }
    public ChatSession Session { get; set; } = null!;
    
    public string Role { get; set; } = "user"; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// For storing source citations/context used for this message
    /// </summary>
    public string? Metadata { get; set; }
}
