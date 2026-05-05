namespace Diploma.Application.Interfaces;

public interface IAiService
{
    //text into vectors
    Task<float[]> GetTextEmbeddingAsync(string text, CancellationToken ct = default);
    //final genetation step
    Task<string> GenerateAnswerAsync(string prompt, CancellationToken ct = default);

}