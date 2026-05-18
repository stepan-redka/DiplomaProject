namespace Diploma.Application.DTOs;

/// <summary>
/// Request model for document ingestion
/// </summary>
public class IngestRequest
{
    public string Content { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
}

/// <summary>
/// Request model for RAG query
/// </summary>
public class QueryRequest
{
    public string Question { get; set; } = string.Empty;
    public Guid? SessionId { get; set; }
    public int TopK { get; set; } = 3;
    public Diploma.Domain.Enums.QueryIntent? Intent { get; set; }
    public string? SelectedModel { get; set; }
    public bool IsHighFidelity { get; set; }
}

/// <summary>
/// Response model for RAG query
/// </summary>
public class QueryResponse
{
    public Guid MessageId { get; set; }
    public Guid SessionId { get; set; }
    public string? SessionTitle { get; set; }
    public string Answer { get; set; } = string.Empty;
    public List<SourceCitation> Sources { get; set; } = new();
    public double ProcessingTimeMs { get; set; }
}

/// <summary>
/// Professional Source Citation DTO for transparency
/// </summary>
public class SourceCitation
{
    public string Content { get; set; } = string.Empty;
    public string SourceDocument { get; set; } = string.Empty;
    public double Score { get; set; }
}

/// <summary>
/// Request to set user feedback on a message
/// </summary>
public class SetFeedbackRequest
{
    public Guid MessageId { get; set; }
    public int Effectiveness { get; set; } // 0=Neutral, 1=Positive, 2=Negative
}

/// <summary>
/// Response for ingestion operation
/// </summary>
public class IngestResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ChunksCreated { get; set; }
}

/// <summary>
/// Request model for deleting specific chunks
/// </summary>
public class DeleteChunksRequest
{
    public string[] ChunkIds { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Info about a stored chunk for display
/// </summary>
public class StoredChunkInfo
{
    public string Id { get; set; } = "";
    public string SourceDocument { get; set; } = "";
    public string ContentPreview { get; set; } = "";
    public int ChunkIndex { get; set; }
}

public class ScoredChunkDto
{
    public Guid ChunkId { get; set; }
    public Guid DocumentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public float Score { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// DTO for displaying document information in the UI
/// </summary>
public class DocumentDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int ChunkCount { get; set; }
    public long FileSizeBytes { get; set; }
    public double ProcessingTimeMs { get; set; }
}

public class ChatMessageDto
{
    public Guid Id { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int Effectiveness { get; set; }
    public List<SourceCitation> Sources { get; set; } = new();

    /// <summary>
    /// The AI model used for this message
    /// </summary>
    public string? ModelName { get; set; }

    /// <summary>
    /// Generation time in milliseconds
    /// </summary>
    public double ProcessingTimeMs { get; set; }

    /// <summary>
    /// Estimated or actual token count
    /// </summary>
    public int TokenCount { get; set; }
}

public class ResearchAnalyticsDto
{
    public List<ModelLatencyData> ModelLatency { get; set; } = new();
    public List<IngestionData> IngestionEfficiency { get; set; } = new();
    public List<PrecisionData> SemanticPrecision { get; set; } = new();
    public List<ThroughputData> GenerationThroughput { get; set; } = new();
    public List<CorrelationData> MathHumanCorrelation { get; set; } = new();
    public List<DensityData> KnowledgeDensity { get; set; } = new();
}

public class ModelLatencyData { public string ModelName { get; set; } = ""; public double AvgLatencyMs { get; set; } }
public class IngestionData { public long FileSizeBytes { get; set; } public double ProcessingTimeMs { get; set; } public string FileName { get; set; } = ""; }
public class PrecisionData { public DateTime Timestamp { get; set; } public double Score { get; set; } }
public class ThroughputData { public string ModelName { get; set; } = ""; public double TokensPerSec { get; set; } }
public class CorrelationData { public double Score { get; set; } public int Feedback { get; set; } }
public class DensityData { public string DocName { get; set; } = ""; public int Chunks { get; set; } }
