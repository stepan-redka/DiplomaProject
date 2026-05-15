using Diploma.Domain.Enums;

namespace Diploma.Application.Interfaces;

public interface IIntentResolver
{
    /// <summary>
    /// Resolves the intent of the user's question.
    /// </summary>
    /// <param name="question">The question string.</param>
    /// <param name="isHighFidelity">Manual override for full RAG flow.</param>
    /// <param name="hasDocuments">Whether the user has documents in their repository.</param>
    /// <param name="ct">CancellationToken.</param>
    Task<QueryIntent> ResolveAsync(string question, bool isHighFidelity, bool hasDocuments, CancellationToken ct = default);
}
