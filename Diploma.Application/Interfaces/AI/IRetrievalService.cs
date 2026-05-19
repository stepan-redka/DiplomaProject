using Diploma.Application.DTOs;

namespace Diploma.Application.Interfaces.AI;

public interface IRetrievalService
{
    /// <summary>
    /// Retrieves relevant document chunks based on semantic similarity.
    /// Automatically applies user isolation and optional document pre-filtering.
    /// </summary>
    Task<List<SourceCitation>> GetRelevantContextAsync(string question, int? topK = null, List<Guid>? allowedDocumentIds = null, CancellationToken ct = default);
}
