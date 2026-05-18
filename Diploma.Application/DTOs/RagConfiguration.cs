namespace Diploma.Application.DTOs;

/// <summary>
/// Configuration for RAG system components
/// </summary>
public class RagConfiguration
{
    public QdrantSettings Qdrant { get; set; } = new();
    public OllamaSettings Ollama { get; set; } = new();
    public GeminiSettings Gemini { get; set; } = new();
    public ChunkingSettings Chunking { get; set; } = new();
}

/// <summary>
/// Qdrant vector database settings
/// </summary>
public class QdrantSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6334;
    public string CollectionName { get; set; } = "documents";
    public string CacheCollectionName { get; set; } = "cached_queries";
    public int VectorSize { get; set; } = 768; // nomic-embed-text dimension
    public int DefaultTopK { get; set; } = 5;
    public double SimilarityThreshold { get; set; } = 0.40;
    public double CacheSimilarityThreshold { get; set; } = 0.95;
}

/// <summary>
/// Ollama embedding service settings
/// </summary>
public class OllamaSettings
{
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public string ChatModel { get; set; } = "llama3.2:3b";
}

/// <summary>
/// Google Gemini API settings
/// </summary>
public class GeminiSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string ModelId { get; set; } = "gemini-2.0-flash";
}

/// <summary>
/// Text chunking settings
/// </summary>
public class ChunkingSettings
{
    public int MaxChunkSize { get; set; } = 500;
    public int ChunkOverlap { get; set; } = 100;
}
