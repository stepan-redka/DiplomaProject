namespace Diploma.Application.Interfaces.AI;

public interface ISemanticCacheService
{
    /// <summary>
    /// Attempts to find a semantically similar response for a given question.
    /// </summary>
    Task<string?> GetCachedResponseAsync(string question, CancellationToken ct = default);

    /// <summary>
    /// Stores a question and its generated response in the semantic cache.
    /// </summary>
    Task SaveToCacheAsync(string question, string response, CancellationToken ct = default);
}
