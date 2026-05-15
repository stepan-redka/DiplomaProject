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
    public List<ModelLatencyDto> ModelLatency { get; set; } = new();
    public List<IngestionEfficiencyDto> IngestionEfficiency { get; set; } = new();
    public List<SemanticPrecisionDto> SemanticPrecision { get; set; } = new();
    public List<ThroughputDto> GenerationThroughput { get; set; } = new();
    public List<CorrelationDto> MathHumanCorrelation { get; set; } = new();
    public List<KnowledgeDensityDto> KnowledgeDensity { get; set; } = new();
}

public class ModelLatencyDto
{
    public string ModelName { get; set; } = "";
    public double AverageLatencyMs { get; set; }
}

public class IngestionEfficiencyDto
{
    public long FileSizeBytes { get; set; }
    public double ProcessingTimeMs { get; set; }
    public string FileName { get; set; } = "";
}

public class SemanticPrecisionDto
{
    public DateTime Timestamp { get; set; }
    public double MaxScore { get; set; }
}

public class ThroughputDto
{
    public string ModelName { get; set; } = "";
    public double TokensPerSec { get; set; }
}

public class CorrelationDto
{
    public double SimilarityScore { get; set; }
    public int UserEffectiveness { get; set; } // 0, 1, 2
}

public class KnowledgeDensityDto
{
    public string DocumentName { get; set; } = "";
    public int ChunkCount { get; set; }
}
