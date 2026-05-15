using Diploma.Application.DTOs;

namespace Diploma.Application.Interfaces;

/// <summary>
/// Interface for RAG operations
/// </summary>
public interface IRagService
{
    Task<IngestResponse> IngestDocumentAsync(string content, string documentName, CancellationToken ct = default);
    Task<QueryResponse> QueryAsync(string question, Guid? sessionId = null, int? topK = null, Diploma.Domain.Enums.QueryIntent? intent = null, string? modelName = null, CancellationToken ct = default);
    Task<bool> EnsureCollectionExistsAsync(CancellationToken ct = default);
    Task<int> GetDocumentCountAsync(CancellationToken ct = default);
    Task<List<DocumentDto>> GetUserDocumentsAsync(CancellationToken ct = default);
    Task<bool> ClearCollectionAsync(CancellationToken ct = default);
    Task<bool> DeleteDocumentAsync(Guid documentId, CancellationToken ct = default);
    Task<List<StoredChunkInfo>> GetStoredChunksAsync(int limit = 500, CancellationToken ct = default);
    Task<int> DeleteChunksAsync(IEnumerable<string> chunkIds, CancellationToken ct = default);
    
    // Chat History & Feedback
    Task<List<ChatMessageDto>> GetChatHistoryAsync(int limit = 50, CancellationToken ct = default);
    Task<bool> SetFeedbackAsync(Guid messageId, int effectiveness, CancellationToken ct = default);

    // Research Analytics
    Task<int> GetTotalQueriesAsync(CancellationToken ct = default);
    Task<long> GetStorageUsedAsync(CancellationToken ct = default);
    Task<ResearchAnalyticsDto> GetAnalyticsAsync(CancellationToken ct = default);
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
