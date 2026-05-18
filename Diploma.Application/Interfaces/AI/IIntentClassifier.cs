using Diploma.Application.DTOs;

namespace Diploma.Application.Interfaces.AI;

/// <summary>
/// Wrapper for local ML intent classification to enable unit testing.
/// </summary>
public interface IIntentClassifier
{
    IntentPrediction Predict(IntentData data);
}
