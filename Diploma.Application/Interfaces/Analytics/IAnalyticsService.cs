using Diploma.Application.DTOs;

namespace Diploma.Application.Interfaces.Analytics;

public interface IAnalyticsService
{
    Task<ResearchAnalyticsDto> GetAnalyticsAsync(string userId, CancellationToken ct = default);
    Task<int> GetTotalQueriesAsync(string userId, CancellationToken ct = default);
    Task<long> GetStorageUsedAsync(string userId, CancellationToken ct = default);
}
