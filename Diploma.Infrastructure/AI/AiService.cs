using Diploma.Application.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Diploma.Infrastructure.AI;

public class AiService : IAiService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<AiService> _logger;

    public AiService(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, 
        IChatCompletionService chatService,
        ILogger<AiService> logger)
    {
        _embeddingGenerator = embeddingGenerator;
        _chatService = chatService;
        _logger = logger;
    }

    // This method generates an embedding for the given text chunk using the configured embedding generator.
    public async Task<float[]> GetTextEmbeddingAsync(string text, CancellationToken ct = default)
    {
        _logger.LogDebug("Generating embedding for text (length: {Length})", text.Length);
        var sw = Stopwatch.StartNew();
        try
        {
            // Using the modern Microsoft.Extensions.AI approach
            var generatedEmbeddings = await _embeddingGenerator.GenerateAsync(new[] { text }, cancellationToken: ct);
            
            sw.Stop();
            if (generatedEmbeddings.Count > 0)
            {
                _logger.LogDebug("Generated embedding in {ElapsedMs}ms. Vector size: {VectorSize}", 
                    sw.ElapsedMilliseconds, generatedEmbeddings[0].Vector.Length);
                return generatedEmbeddings[0].Vector.ToArray();
            }

            _logger.LogWarning("Embedding generator returned no results after {ElapsedMs}ms", sw.ElapsedMilliseconds);
            return Array.Empty<float>();
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to generate embedding after {ElapsedMs}ms", sw.ElapsedMilliseconds);
            throw;
        }
    }

    // This method generates an answer to the given prompt using the configured chat completion service.
    public async Task<string> GenerateAnswerAsync(string prompt, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating AI answer for prompt (length: {Length})", prompt.Length);
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await _chatService.GetChatMessageContentAsync(prompt, cancellationToken: ct);
            sw.Stop();
            
            var content = result.Content ?? "I'm sorry, I couldn't generate an answer.";
            _logger.LogInformation("Successfully generated answer in {ElapsedMs}ms. Answer length: {AnswerLength}", 
                sw.ElapsedMilliseconds, content.Length);
            
            return content;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to generate AI answer after {ElapsedMs}ms", sw.ElapsedMilliseconds);
            throw;
        }
    }
}