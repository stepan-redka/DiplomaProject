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
    public List<ChatMessageDto> Messages { get; set; } = new();
}
