using Diploma.Application.DTOs;
using Diploma.Application.Interfaces.Analytics;
using Diploma.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace Diploma.Infrastructure.Services.Analytics;

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
        var assistantMessages = await _dbContext.ChatMessages
            .Include(m => m.Session)
            .Where(m => m.UserId == userId && m.Role == "assistant")
            .ToListAsync(ct);

        // 1. Model Latency
        analytics.ModelLatency = assistantMessages
            .Where(m => m.ProcessingTimeMs > 0)
            .GroupBy(m => ResolveModelName(m.ModelName, m.Session.SelectedModel))
            .Select(g => new ChartDataPoint
            {
                Label = g.Key,
                Value = Math.Round(g.Average(m => m.ProcessingTimeMs), 2)
            })
            .ToList();

        // 2. Ingestion time by document type/name
        analytics.IngestionEfficiency = await _dbContext.Documents
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(20)
            .Select(d => new ChartDataPoint
            {
                Label = $"{d.Type}: {d.FileName}",
                Value = Math.Round(d.ProcessingTimeMs, 2)
            })
            .ToListAsync(ct);

        // 3. Retrieval similarity trend (last 20 grounded answers)
        var lastMessages = assistantMessages
            .Where(m => m.Metadata != null)
            .OrderByDescending(m => m.CreatedAt)
            .Take(20)
            .ToList();

        foreach (var msg in lastMessages)
        {
            try
            {
                var sources = JsonSerializer.Deserialize<List<SourceCitation>>(msg.Metadata!);
                if (sources != null && sources.Any())
                {
                    analytics.RetrievalSimilarity.Add(new ChartDataPoint
                    {
                        Label = msg.CreatedAt.ToString("HH:mm:ss"),
                        Value = Math.Round(sources.Max(s => s.Score), 4)
                    });
                }
            }
            catch { /* Ignore corrupt metadata */ }
        }

        analytics.RetrievalSimilarity.Reverse();

        // 4. Generation Throughput
        analytics.GenerationThroughput = assistantMessages
            .Where(m => m.ProcessingTimeMs > 0 && (m.TokenCount > 0 || !string.IsNullOrWhiteSpace(m.Content)))
            .GroupBy(m => ResolveModelName(m.ModelName, m.Session.SelectedModel))
            .Select(g => new ChartDataPoint
            {
                Label = g.Key,
                Value = Math.Round(g.Average(m => ResolveTokenCount(m.TokenCount, m.Content) / (m.ProcessingTimeMs / 1000.0)), 2)
            })
            .ToList();

        // 5. Knowledge density and storage footprint by document
        var densityDocuments = await _dbContext.Documents
            .Where(d => d.UserId == userId)
            .Select(d => new
            {
                d.FileName,
                d.Type,
                d.FileSizeBytes,
                d.Content,
                ChunkCount = d.Chunks.Count
            })
            .ToListAsync(ct);

        analytics.KnowledgeDensity = densityDocuments
            .Select(d => new ChartDataPoint
            {
                Label = $"{d.Type}: {d.FileName}",
                Value = d.ChunkCount > 0 ? d.ChunkCount : EstimateChunkCount(d.Content)
            })
            .ToList();

        analytics.StorageFootprint = densityDocuments
            .Select(d =>
            {
                var contentBytes = string.IsNullOrEmpty(d.Content) ? 0 : Encoding.UTF8.GetByteCount(d.Content);
                var chunkCount = d.ChunkCount > 0 ? d.ChunkCount : EstimateChunkCount(d.Content);
                var vectorBytes = chunkCount * 1536L * sizeof(float);
                var footprintBytes = Math.Max(Math.Max(d.FileSizeBytes, contentBytes), vectorBytes);

                return new ChartDataPoint
                {
                    Label = $"{d.Type}: {d.FileName}",
                    Value = Math.Round(footprintBytes / 1024.0, 2)
                };
            })
            .ToList();

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
        var documents = await _dbContext.Documents
            .Where(d => d.UserId == userId)
            .Select(d => new
            {
                d.FileSizeBytes,
                d.Content,
                ChunkCount = d.Chunks.Count
            })
            .ToListAsync(ct);

        return documents.Sum(d =>
        {
            var contentBytes = string.IsNullOrEmpty(d.Content) ? 0 : Encoding.UTF8.GetByteCount(d.Content);
            var chunkCount = d.ChunkCount > 0 ? d.ChunkCount : EstimateChunkCount(d.Content);
            var vectorBytes = chunkCount * 1536L * sizeof(float);
            return Math.Max(Math.Max(d.FileSizeBytes, contentBytes), vectorBytes);
        });
    }

    private static int EstimateChunkCount(string? content)
    {
        if (string.IsNullOrWhiteSpace(content) || content == "[Processing...]") return 0;

        const int estimatedChunkSize = 1000;
        return Math.Max(1, (int)Math.Ceiling(content.Length / (double)estimatedChunkSize));
    }

    private static string ResolveModelName(string? messageModel, string? sessionModel)
    {
        if (!string.IsNullOrWhiteSpace(messageModel)) return messageModel;
        if (!string.IsNullOrWhiteSpace(sessionModel)) return sessionModel;
        return "Unknown";
    }

    private static int ResolveTokenCount(int storedTokenCount, string content)
    {
        if (storedTokenCount > 0) return storedTokenCount;
        if (string.IsNullOrWhiteSpace(content)) return 0;
        return Math.Max(1, content.Length / 4);
    }
}
