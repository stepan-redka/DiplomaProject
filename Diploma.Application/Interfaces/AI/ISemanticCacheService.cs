namespace Diploma.Application.Interfaces.AI;

public interface ISemanticCacheService
{
    /// <summary>
    /// Attempts to find a semantically similar response for a given question.
    /// </summary>
    Task<string?> GetCachedResponseAsync(string question, List<Guid>? allowedDocumentIds = null, CancellationToken ct = default);

    /// <summary>
    /// Stores a question and its generated response in the semantic cache.
    /// </summary>
    Task SaveToCacheAsync(string question, string response, List<Guid>? allowedDocumentIds = null, CancellationToken ct = default);

    /// <summary>
    /// Clears all cache entries for the current user.
    /// </summary>
    Task ClearCacheAsync(CancellationToken ct = default);
}
