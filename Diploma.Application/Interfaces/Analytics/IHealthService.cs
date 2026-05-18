using Diploma.Application.DTOs;

namespace Diploma.Application.Interfaces.Analytics;

public interface IHealthService
{
    Task<SystemHealthDto> GetSystemHealthAsync(CancellationToken ct = default);
}
