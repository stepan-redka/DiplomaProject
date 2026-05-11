namespace Diploma.Application.Interfaces;

public interface IAiService
{
    //text into vectors
    Task<float[]> GetTextEmbeddingAsync(string text, CancellationToken ct = default);
    
    // Batch processing for efficiency
    Task<List<float[]>> GetTextEmbeddingsAsync(IEnumerable<string> texts, CancellationToken ct = default);
    
    //final genetation step
    Task<string> GenerateAnswerAsync(string prompt, CancellationToken ct = default);

}