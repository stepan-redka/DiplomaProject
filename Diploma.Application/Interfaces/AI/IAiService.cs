namespace Diploma.Application.Interfaces.AI;

public interface IAiService
{
    //text into vectors
    Task<float[]> GetTextEmbeddingAsync(string text, CancellationToken ct = default);

    // Batch processing for efficiency
    Task<List<float[]>> GetTextEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default);

    //final genetation step
    Task<string> GenerateAnswerAsync(string prompt, string? modelName = null, CancellationToken ct = default);

    /// <summary>
    /// Checks if a specific model is available in the local Ollama instance.
    /// </summary>
    Task<bool> IsModelAvailableAsync(string modelName, CancellationToken ct = default);
}