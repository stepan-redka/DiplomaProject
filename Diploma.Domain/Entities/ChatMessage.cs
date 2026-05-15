using Diploma.Domain.Interfaces;
using Diploma.Domain.Enums;

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
    /// User feedback on AI response quality
    /// </summary>
    public MessageEffectiveness Effectiveness { get; set; } = MessageEffectiveness.Neutral;

    /// <summary>
    /// For storing source citations/context used for this message
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// The AI model used to generate this specific message.
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// Time taken to generate the response in milliseconds.
    /// </summary>
    public double ProcessingTimeMs { get; set; }

    /// <summary>
    /// Number of tokens (estimated) in the generated answer.
    /// </summary>
    public int TokenCount { get; set; }
}
