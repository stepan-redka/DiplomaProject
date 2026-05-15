using Diploma.Application.DTOs;

namespace Diploma.Application.Interfaces;

public interface IHealthService
{
    Task<SystemHealthDto> GetSystemHealthAsync(CancellationToken ct = default);
}
