namespace Diploma.Application.DTOs;

public class SystemHealthDto
{
    public ServiceStatus Database { get; set; } = new();
    public ServiceStatus VectorDb { get; set; } = new();
    public ServiceStatus AiService { get; set; } = new();
    public HostStatus HostServer { get; set; } = new();
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

    public bool IsHealthy => Database.IsHealthy && VectorDb.IsHealthy && AiService.IsHealthy;
}

public class ServiceStatus
{
    public string ServiceName { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public string Message { get; set; } = string.Empty;
    public long LatencyMs { get; set; }
}

public class HostStatus
{
    public double CpuLoadPercentage { get; set; }
    public double RamUsedGb { get; set; }
    public double RamTotalGb { get; set; }
    public double RamUsagePercentage => RamTotalGb > 0 ? (RamUsedGb / RamTotalGb) * 100 : 0;
    public string DiskIoStatus { get; set; } = "Nominal";
}
