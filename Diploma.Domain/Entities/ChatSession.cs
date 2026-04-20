namespace Diploma.Domain.Entities;

/// <summary>
/// Represents a chat session for a user
/// </summary>
public class ChatSession
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
    /// Navigation property for chat messages
    /// </summary>
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
