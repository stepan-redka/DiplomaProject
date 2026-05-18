using Diploma.Application.DTOs;
using Diploma.Application.Interfaces.AI;
using Diploma.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Diploma.Infrastructure.Services.AI;

public class IntentResolver : IIntentResolver
{
    private readonly IIntentClassifier _classifier;
    private readonly ILogger<IntentResolver> _logger;

    public IntentResolver(
        IIntentClassifier classifier, 
        ILogger<IntentResolver> logger)
    {
        _classifier = classifier;
        _logger = logger;
    }

    public async Task<QueryIntent> ResolveAsync(string question, bool isHighFidelity, bool hasDocuments, CancellationToken ct = default)
    {
        // 1. Manual Override: High Fidelity Logic
        if (isHighFidelity)
        {
            _logger.LogInformation("Intent: RESEARCH (Manual High-Fidelity Override)");
            return QueryIntent.Research;
        }

        // 2. Safe Fallback: Zero-Doc State
        if (!hasDocuments)
        {
            _logger.LogInformation("Intent: GENERAL (No documents available for RAG)");
            return QueryIntent.General;
        }

        // 3. Autonomous Mode: Local ML Inference via IIntentClassifier
        try
        {
            var prediction = _classifier.Predict(new IntentData { Text = question });
            
            // Map semantic labels from CLINC150-trained model
            var intent = prediction.PredictedLabel.ToUpper() switch
            {
                "RESEARCH" => QueryIntent.Research,
                "GENERAL" => QueryIntent.General,
                _ => QueryIntent.General
            };

            _logger.LogInformation("Intent Resolved: {Intent} (Label: {Label}, Confidence: {Score})", 
                intent, prediction.PredictedLabel, prediction.Score?.Max() ?? 0);
            
            return intent;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local ML classification failed. Defaulting to RESEARCH for safety.");
            return QueryIntent.Research;
        }
    }
}
