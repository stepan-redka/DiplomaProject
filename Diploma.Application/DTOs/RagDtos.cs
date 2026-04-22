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
    public int TopK { get; set; } = 3;
}

/// <summary>
/// Response model for RAG query
/// </summary>
public class QueryResponse
{
    public string Answer { get; set; } = string.Empty;
    public List<RetrievedContext> Sources { get; set; } = new();
    public double ProcessingTimeMs { get; set; }
}

/// <summary>
/// Retrieved context from vector search
/// </summary>
public class RetrievedContext
{
    public string Content { get; set; } = string.Empty;
    public string SourceDocument { get; set; } = string.Empty;
    public double Score { get; set; }
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
