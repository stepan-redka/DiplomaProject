using Diploma.Domain.Enums;

namespace Diploma.Application.Interfaces.AI;

public interface IIntentResolver
{
    /// <summary>
    /// Resolves the intent of the user's question using a hybrid ML.NET and manual override logic.
    /// </summary>
    /// <param name="question">The question string.</param>
    /// <param name="isHighFidelity">Manual override to force Research mode (bypasses ML inference).</param>
    /// <param name="hasDocuments">Whether the user has documents in their repository.</param>
    /// <param name="ct">CancellationToken.</param>
    Task<QueryIntent> ResolveAsync(string question, bool isHighFidelity, bool hasDocuments, CancellationToken ct = default);
}
