using Diploma.Application.DTOs;

namespace Diploma.Application.Interfaces;

/// <summary>
/// Interface for RAG operations
/// </summary>
public interface IRagService
{
    Task<IngestResponse> IngestDocumentAsync(string content, string documentName);
    Task<QueryResponse> QueryAsync(string question, int topK = 3);
    Task<bool> EnsureCollectionExistsAsync();
    Task<int> GetDocumentCountAsync();
    Task<bool> ClearCollectionAsync();
    Task<List<StoredChunkInfo>> GetStoredChunksAsync(int limit = 500);
    Task<int> DeleteChunksAsync(IEnumerable<string> chunkIds);
}
