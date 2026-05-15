using Diploma.Application.DTOs;
using Diploma.Application.Interfaces;
using Diploma.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Diploma.Infrastructure.Services;

public class IntentResolver : IIntentResolver
{
    private readonly IAiService _aiService;
    private readonly IPromptRegistry _promptRegistry;
    private readonly ILogger<IntentResolver> _logger;

    public IntentResolver(
        IAiService aiService, 
        IPromptRegistry promptRegistry, 
        ILogger<IntentResolver> logger)
    {
        _aiService = aiService;
        _promptRegistry = promptRegistry;
        _logger = logger;
    }

    public async Task<QueryIntent> ResolveAsync(string question, bool isHighFidelity, bool hasDocuments, CancellationToken ct = default)
    {
        // 1. High-Fidelity Manual Override: Optimization for computational throughput
        if (isHighFidelity)
        {
            _logger.LogInformation("Intent: RESEARCH (High-Fidelity Manual Override bypassing semantic analysis)");
            return QueryIntent.Research;
        }

        // 2. Zero-Doc State: Semantic shortcut
        if (!hasDocuments)
        {
            _logger.LogInformation("Intent: GENERAL (Zero-Document state detected)");
            return QueryIntent.General;
        }

        // 3. Lazy Evaluation (Smart Routing): Using LLM as a Semantic Router
        _logger.LogDebug("Initiating semantic intent classification via micro-prompt...");
        var intentPrompt = _promptRegistry.GetIntentResolutionPrompt(question);

        try
        {
            var result = await _aiService.GenerateAnswerAsync(intentPrompt, ct: ct);
            var intent = result.Contains("RESEARCH", StringComparison.OrdinalIgnoreCase) 
                ? QueryIntent.Research 
                : QueryIntent.General;
                
            _logger.LogInformation("Intent classification: {Intent} (Resolved via LLM Semantic Router)", intent);
            return intent;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Semantic classification failed. Falling back to RESEARCH for data integrity.");
            return QueryIntent.Research;
        }
    }
}
