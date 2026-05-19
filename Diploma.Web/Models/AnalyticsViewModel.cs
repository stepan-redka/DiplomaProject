using Diploma.Application.DTOs;

namespace Diploma.Web.Models;

public class AnalyticsViewModel
{
    // Tier 1: Infrastructure Telemetry
    public SystemHealthDto Health { get; set; } = new();

    // Tier 2: Research Summary
    public int TotalSessions { get; set; }
    public long StorageUsedBytes { get; set; }
    public string StorageUsedFormatted { get; set; } = "0 B";
    public int StorageUsedPercentage { get; set; }
    public int TotalDocuments { get; set; }
    public int TotalQueries { get; set; }

    // Tier 3: Scientific Insights (Chart Data)
    public List<ChartDataPoint> ModelLatency { get; set; } = new();
    public List<ChartDataPoint> IngestionEfficiency { get; set; } = new();
    public List<ChartDataPoint> RetrievalSimilarity { get; set; } = new();
    public List<ChartDataPoint> GenerationThroughput { get; set; } = new();
    public List<ChartDataPoint> KnowledgeDensity { get; set; } = new();
    public List<ChartDataPoint> StorageFootprint { get; set; } = new();
}
