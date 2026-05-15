using Diploma.Application.DTOs;
using Diploma.Application.Interfaces;
using Diploma.Domain.Enums;
using Diploma.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Diploma.Infrastructure.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly ApplicationDbContext _dbContext;

    public AnalyticsService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ResearchAnalyticsDto> GetAnalyticsAsync(string userId, CancellationToken ct = default)
    {
        var analytics = new ResearchAnalyticsDto();

        // 1. Model Latency
        analytics.ModelLatency = await _dbContext.ChatMessages
            .Where(m => m.UserId == userId && m.Role == "assistant" && m.ModelName != null)
            .GroupBy(m => m.ModelName)
            .Select(g => new ModelLatencyData
            {
                ModelName = g.Key!,
                AvgLatencyMs = g.Average(m => m.ProcessingTimeMs)
            })
            .ToListAsync(ct);

        // 2. Ingestion Efficiency
        analytics.IngestionEfficiency = await _dbContext.Documents
            .Where(d => d.UserId == userId)
            .Select(d => new IngestionData
            {
                FileSizeBytes = d.FileSizeBytes,
                ProcessingTimeMs = d.ProcessingTimeMs,
                FileName = d.FileName
            })
            .ToListAsync(ct);

        // 3. Semantic Precision (Last 20)
        var lastMessages = await _dbContext.ChatMessages
            .Where(m => m.UserId == userId && m.Role == "assistant" && m.Metadata != null)
            .OrderByDescending(m => m.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        foreach (var msg in lastMessages)
        {
            try
            {
                var sources = JsonSerializer.Deserialize<List<SourceCitation>>(msg.Metadata!);
                if (sources != null && sources.Any())
                {
                    analytics.SemanticPrecision.Add(new PrecisionData
                    {
                        Timestamp = msg.CreatedAt,
                        Score = sources.Max(s => s.Score)
                    });
                }
            }
            catch { /* Ignore corrupt metadata */ }
        }

        // 4. Generation Throughput
        analytics.GenerationThroughput = await _dbContext.ChatMessages
            .Where(m => m.UserId == userId && m.Role == "assistant" && m.ProcessingTimeMs > 0 && m.TokenCount > 0)
            .GroupBy(m => m.ModelName)
            .Select(g => new ThroughputData
            {
                ModelName = g.Key ?? "Unknown",
                TokensPerSec = g.Average(m => m.TokenCount / (m.ProcessingTimeMs / 1000.0))
            })
            .ToListAsync(ct);

        // 5. Math-to-Human Correlation
        foreach (var msg in lastMessages)
        {
            try
            {
                var sources = JsonSerializer.Deserialize<List<SourceCitation>>(msg.Metadata!);
                if (sources != null && sources.Any())
                {
                    analytics.MathHumanCorrelation.Add(new CorrelationData
                    {
                        Score = sources.Max(s => s.Score),
                        Feedback = (int)msg.Effectiveness
                    });
                }
            }
            catch { }
        }

        // 6. Knowledge Density
        analytics.KnowledgeDensity = await _dbContext.Documents
            .Where(d => d.UserId == userId)
            .Select(d => new DensityData
            {
                DocName = d.FileName,
                Chunks = d.Chunks.Count
            })
            .ToListAsync(ct);

        return analytics;
    }

    public async Task<int> GetTotalQueriesAsync(string userId, CancellationToken ct = default)
    {
        return await _dbContext.ChatMessages
            .Where(m => m.UserId == userId && m.Role == "user")
            .CountAsync(ct);
    }

    public async Task<long> GetStorageUsedAsync(string userId, CancellationToken ct = default)
    {
        return await _dbContext.Documents
            .Where(d => d.UserId == userId)
            .SumAsync(d => (long)d.Content.Length, ct);
    }
}
