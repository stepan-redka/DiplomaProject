using Diploma.Application.Interfaces.AI;
using Diploma.Application.DTOs;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Diploma.Infrastructure.Services.AI;

public class AiService : IAiService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IChatCompletionService _chatService;
    private readonly RagConfiguration _config;
    private readonly ILogger<AiService> _logger;
    private readonly HttpClient _httpClient;

    public static readonly string[] SupportedModels = { "llama3.1", "qwen2.5:7b", "phi3.5" };

    public AiService(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, 
        IChatCompletionService chatService,
        RagConfiguration config,
        ILogger<AiService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _embeddingGenerator = embeddingGenerator;
        _chatService = chatService;
        _config = config;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task<float[]> GetTextEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var results = await GetTextEmbeddingsAsync(new[] { text }, ct);
        return results.FirstOrDefault() ?? Array.Empty<float>();
    }

    public async Task<List<float[]>> GetTextEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default)
    {
        var textList = texts.ToList();
        if (!textList.Any()) return new List<float[]>();

        _logger.LogDebug("Generating embeddings for {Count} text chunks", textList.Count);
        var sw = Stopwatch.StartNew();

        try
        {
            var generatedEmbeddings = await _embeddingGenerator.GenerateAsync(textList, cancellationToken: ct);
            sw.Stop();

            _logger.LogDebug("Generated {Count} embeddings in {ElapsedMs}ms", 
                generatedEmbeddings.Count, sw.ElapsedMilliseconds);

            return generatedEmbeddings.Select(e => e.Vector.ToArray()).ToList();
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to generate batch embeddings after {ElapsedMs}ms", sw.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<string> GenerateAnswerAsync(string prompt, string? modelName = null, CancellationToken ct = default)
    {
        // THESIS NOTE: Dynamic Dispatch allows for comparative analysis of SLM 
        // reasoning capabilities within the same RAG pipeline context.
        var effectiveModel = !string.IsNullOrEmpty(modelName) ? modelName : _config.Ollama.ChatModel;

        // Proactive availability check
        if (!await IsModelAvailableAsync(effectiveModel, ct))
        {
            _logger.LogWarning("Model {ModelName} is not available in Ollama. Falling back to default: {DefaultModel}", 
                effectiveModel, _config.Ollama.ChatModel);
            effectiveModel = _config.Ollama.ChatModel;
        }

        _logger.LogInformation("Generating AI answer using model: {ModelName} (Prompt length: {Length})", 
            effectiveModel, prompt.Length);
            
        var sw = Stopwatch.StartNew();
        try
        {
            var executionSettings = new Microsoft.SemanticKernel.PromptExecutionSettings
            {
                ModelId = effectiveModel
            };

            var result = await _chatService.GetChatMessageContentAsync(prompt, executionSettings, cancellationToken: ct);
            sw.Stop();
            
            var content = result.Content ?? "I'm sorry, I couldn't generate an answer.";
            _logger.LogInformation("Successfully generated answer in {ElapsedMs}ms. Answer length: {AnswerLength}", 
                sw.ElapsedMilliseconds, content.Length);
            
            return content;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to generate AI answer after {ElapsedMs}ms using model {Model}", 
                sw.ElapsedMilliseconds, effectiveModel);
            throw;
        }
    }

    public async Task<bool> IsModelAvailableAsync(string modelName, CancellationToken ct = default)
    {
        try
        {
            var endpoint = _config.Ollama.Endpoint.TrimEnd('/');
            var response = await _httpClient.GetFromJsonAsync<OllamaTagsResponse>(
                $"{endpoint}/api/tags", ct);
            
            if (response?.Models == null) return false;

            // Ollama often uses 'model:latest' format, handle both exact and base match
            return response.Models.Any(m => 
                m.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase) || 
                m.Name.Equals($"{modelName}:latest", StringComparison.OrdinalIgnoreCase) ||
                m.Name.StartsWith(modelName + ":", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check model availability for {ModelName} at {_Endpoint}", 
                modelName, _config.Ollama.Endpoint);
            return false;
        }
    }

    private class OllamaTagsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModelInfo> Models { get; set; } = new();
    }

    private class OllamaModelInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}