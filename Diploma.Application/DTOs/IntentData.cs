using Microsoft.ML.Data;

namespace Diploma.Application.DTOs;

/// <summary>
/// Represents a training sample for intent classification.
/// </summary>
public class IntentData
{
    [LoadColumn(0)]
    public string Text { get; set; } = string.Empty;

    [LoadColumn(1)]
    public string Label { get; set; } = string.Empty;
}
