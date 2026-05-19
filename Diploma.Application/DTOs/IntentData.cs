namespace Diploma.Application.DTOs;

/// <summary>
/// Represents a training sample for intent classification.
/// </summary>
public class IntentData
{
    public string Text { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
}
