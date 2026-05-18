using Diploma.Application.DTOs;

namespace Diploma.Application.Interfaces.AI;

public interface IRetrievalService
{
    Task<List<SourceCitation>> GetRelevantContextAsync(string question, string userId, int? topK = null, CancellationToken ct = default);
}
