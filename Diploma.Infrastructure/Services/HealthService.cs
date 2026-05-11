using Diploma.Application.Interfaces;
using Diploma.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Qdrant.Client;

namespace Diploma.Infrastructure.Services;

public class HealthService : IHealthService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly QdrantClient _qdrantClient;
    private readonly IAiService _aiService;

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
        health.Database = await CheckService("PostgreSQL", async () => 
        {
            return await _dbContext.Database.CanConnectAsync(ct);
        });

        // 2. Vector DB Health (Qdrant)
        health.VectorDb = await CheckService("Qdrant", async () => 
        {
            var collections = await _qdrantClient.ListCollectionsAsync(ct);
            return collections != null;
        });

        // 3. AI Service Health (Ollama/Gemini)
        health.AiService = await CheckService("AI Provider", async () => 
        {
            // Simple embedding check to verify API connectivity
            var test = await _aiService.GetTextEmbeddingAsync("health check", ct);
            return test != null && test.Length > 0;
        });

        return health;
    }

    private async Task<ServiceStatus> CheckService(string name, Func<Task<bool>> checkAction)
    {
        var status = new ServiceStatus { ServiceName = name };
        var sw = Stopwatch.StartNew();
        try
        {
            status.IsHealthy = await checkAction();
            status.Message = status.IsHealthy ? "Operational" : "Service Unreachable";
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
