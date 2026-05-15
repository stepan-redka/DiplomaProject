using Diploma.Application.DTOs;

namespace Diploma.Application.Interfaces;

public interface IRetrievalService
{
    Task<List<SourceCitation>> GetRelevantContextAsync(string question, string userId, int? topK = null, CancellationToken ct = default);
}
