namespace Diploma.Application.DTOs;

/// <summary>
/// DTO for a chat session summary in the research history sidebar
/// </summary>
public class ChatSessionDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public int MessageCount { get; set; }
    public int DocumentCount { get; set; }
    public string? SelectedModel { get; set; }
}

/// <summary>
/// Detailed DTO for a chat session including its messages
/// </summary>
public class ChatSessionDetailDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? SelectedModel { get; set; }
    public List<Guid> RelatedDocumentIds { get; set; } = new();
    public List<ChatMessageDto> Messages { get; set; } = new();
}

/// <summary>
/// Request model for creating a new chat session.
/// </summary>
public class CreateSessionRequest
{
    public string Title { get; set; } = "New Chat";
    public List<Guid>? SelectedDocumentIds { get; set; }
}

/// <summary>
/// Response model after creating a new chat session.
/// </summary>
public class CreateSessionResponse
{
    public Guid SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class ToggleDocumentBindingRequest
{
    public Guid SessionId { get; set; }
    public Guid DocumentId { get; set; }
}
