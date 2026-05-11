using Diploma.Application.DTOs;

namespace Diploma.Application.Interfaces;

public interface IHealthService
{
    Task<SystemHealthDto> GetSystemHealthAsync(CancellationToken ct = default);
}

public class SystemHealthDto
{
    public ServiceStatus Database { get; set; } = new();
    public ServiceStatus VectorDb { get; set; } = new();
    public ServiceStatus AiService { get; set; } = new();
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;
}

public class ServiceStatus
{
    public string ServiceName { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public string Message { get; set; } = "Initializing...";
    public long LatencyMs { get; set; }
}
