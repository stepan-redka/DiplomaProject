using Diploma.Application.Interfaces.AI;
using Diploma.Application.Interfaces.Analytics;
using Diploma.Application.DTOs;
using Diploma.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Qdrant.Client;
using System.Globalization;

namespace Diploma.Infrastructure.Services.Analytics;

public class HealthService : IHealthService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly QdrantClient _qdrantClient;
    private readonly IAiService _aiService;

    // Base paths for telemetry, prioritized for host-mounted volumes
    private static readonly string ProcPath = Directory.Exists("/host/proc") ? "/host/proc" : "/proc";
    private static readonly string RootPath = Directory.Exists("/host/root") ? "/host/root" : "/";

    public HealthService(ApplicationDbContext dbContext, QdrantClient qdrantClient, IAiService aiService)
    {
        _dbContext = dbContext;
        _qdrantClient = qdrantClient;
        _aiService = aiService;
    }

    public async Task<SystemHealthDto> GetSystemHealthAsync(CancellationToken ct = default)
    {
        var health = new SystemHealthDto();
        
        // 1. Database Health (PostgreSQL)
        health.Database = await CheckService("PostgreSQL", async (token) => 
        {
            return await _dbContext.Database.CanConnectAsync(token);
        }, 3000, ct);

        // 2. Vector DB Health (Qdrant)
        health.VectorDb = await CheckService("Qdrant", async (token) => 
        {
            var collections = await _qdrantClient.ListCollectionsAsync(token);
            return collections != null;
        }, 3000, ct);

        // 3. AI Service Health (Ollama)
        health.AiService = await CheckService("AI Provider", async (token) => 
        {
            var test = await _aiService.GetTextEmbeddingAsync("health check", token);
            return test != null && test.Length > 0;
        }, 5000, ct);

        // 4. Host Server Telemetry (Dynamic)
        health.HostServer = await GetHostStatusAsync();

        return health;
    }

    private async Task<HostStatus> GetHostStatusAsync()
    {
        var status = new HostStatus { DiskIoStatus = "Nominal" };
        
        try
        {
            // RAM Telemetry via /proc/meminfo
            if (File.Exists($"{ProcPath}/meminfo"))
            {
                var memInfo = await File.ReadAllLinesAsync($"{ProcPath}/meminfo");
                double totalKb = 0, availKb = 0;
                
                foreach (var line in memInfo)
                {
                    if (line.StartsWith("MemTotal:")) totalKb = ParseKb(line);
                    if (line.StartsWith("MemAvailable:")) availKb = ParseKb(line);
                }

                status.RamTotalGb = Math.Round(totalKb / 1024 / 1024, 2);
                status.RamUsedGb = Math.Round((totalKb - availKb) / 1024 / 1024, 2);
            }

            // CPU Telemetry via /proc/stat (Delta Calculation)
            if (File.Exists($"{ProcPath}/stat"))
            {
                var (total1, idle1) = await ReadCpuStatsAsync();
                await Task.Delay(150); // High-fidelity sampling window
                var (total2, idle2) = await ReadCpuStatsAsync();

                double totalDelta = total2 - total1;
                double idleDelta = idle2 - idle1;

                if (totalDelta > 0)
                {
                    status.CpuLoadPercentage = Math.Round(100 * (1.0 - (idleDelta / totalDelta)), 1);
                }
            }

            // Disk Status via DriveInfo (Checking root)
            var drive = new DriveInfo(RootPath);
            if (drive.IsReady)
            {
                double freePercent = (double)drive.AvailableFreeSpace / drive.TotalSize;
                status.DiskIoStatus = freePercent < 0.1 ? "Critical (Low Space)" : "Nominal";
            }
        }
        catch
        {
            status.DiskIoStatus = "Degraded (Telemetry Fault)";
        }

        return status;
    }

    private async Task<(long Total, long Idle)> ReadCpuStatsAsync()
    {
        try
        {
            var lines = await File.ReadAllLinesAsync($"{ProcPath}/stat");
            var cpuLine = lines.FirstOrDefault(l => l.StartsWith("cpu "));
            if (cpuLine == null) return (0, 0);

            var parts = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // Indices: 0=cpu, 1=user, 2=nice, 3=system, 4=idle, 5=iowait, 6=irq, 7=softirq, 8=steal
            long user = long.Parse(parts[1]);
            long nice = long.Parse(parts[2]);
            long system = long.Parse(parts[3]);
            long idle = long.Parse(parts[4]);
            long iowait = long.Parse(parts[5]);
            long irq = long.Parse(parts[6]);
            long softirq = long.Parse(parts[7]);
            long steal = long.Parse(parts[8]);

            long totalIdle = idle + iowait;
            long totalActive = user + nice + system + irq + softirq + steal;
            return (totalIdle + totalActive, totalIdle);
        }
        catch
        {
            return (0, 0);
        }
    }

    private double ParseKb(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && double.TryParse(parts[1], CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }
        return 0;
    }

    private async Task<ServiceStatus> CheckService(string name, Func<CancellationToken, Task<bool>> checkAction, int timeoutMs, CancellationToken ct)
    {
        var status = new ServiceStatus { ServiceName = name };
        var sw = Stopwatch.StartNew();
        
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        try
        {
            status.IsHealthy = await checkAction(cts.Token);
            status.Message = status.IsHealthy ? "Operational" : "Service Unreachable";
        }
        catch (OperationCanceledException)
        {
            status.IsHealthy = false;
            status.Message = "Request Timeout (Service busy/slow)";
        }
        catch (Exception ex)
        {
            status.IsHealthy = false;
            status.Message = ex.Message;
        }
        finally
        {
            sw.Stop();
            status.LatencyMs = sw.ElapsedMilliseconds;
        }
        return status;
    }
}
