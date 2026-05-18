using Microsoft.ML.Data;

namespace Diploma.Application.DTOs;

/// <summary>
/// Represents the output of the intent classification model.
/// </summary>
public class IntentPrediction
{
    [ColumnName("PredictedLabel")]
    public string PredictedLabel { get; set; } = string.Empty;

    public float[] Score { get; set; } = [];
}
