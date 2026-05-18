using Diploma.Application.DTOs;
using Diploma.Application.Interfaces.AI;
using Microsoft.Extensions.ML;

namespace Diploma.Infrastructure.Services.AI;

public class IntentMLClassifier : IIntentClassifier
{
    private readonly PredictionEnginePool<IntentData, IntentPrediction> _predictionPool;

    public IntentMLClassifier(PredictionEnginePool<IntentData, IntentPrediction> predictionPool)
    {
        _predictionPool = predictionPool;
    }

    public IntentPrediction Predict(IntentData data)
    {
        return _predictionPool.Predict(data);
    }
}
