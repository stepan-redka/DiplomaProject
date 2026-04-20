using Diploma.Domain.Enums;

namespace Diploma.Domain.Entities;

/// <summary>
/// Result of document parsing
/// </summary>
public class ParsedDocument
{
    public bool Success { get; set; }
    public string? UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DocumentType Type { get; set; }
    public string? ErrorMessage { get; set; }
    public int CharacterCount => Content.Length;
}
