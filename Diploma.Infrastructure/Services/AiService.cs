using Diploma.Application.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Diploma.Infrastructure.Services;

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