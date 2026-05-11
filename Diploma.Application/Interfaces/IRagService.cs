using Diploma.Application.DTOs;

namespace Diploma.Application.Interfaces;

/// <summary>
/// Interface for RAG operations
/// </summary>
public interface IRagService
{
    Task<IngestResponse> IngestDocumentAsync(string content, string documentName, CancellationToken ct = default);
    Task<QueryResponse> QueryAsync(string question, int topK = 3, CancellationToken ct = default);
    Task<bool> EnsureCollectionExistsAsync(CancellationToken ct = default);
    Task<int> GetDocumentCountAsync(CancellationToken ct = default);
    Task<List<DocumentDto>> GetUserDocumentsAsync(CancellationToken ct = default);
    Task<bool> ClearCollectionAsync(CancellationToken ct = default);
    Task<List<StoredChunkInfo>> GetStoredChunksAsync(int limit = 500, CancellationToken ct = default);
    Task<int> DeleteChunksAsync(IEnumerable<string> chunkIds, CancellationToken ct = default);
    
    // Chat History & Feedback
    Task<List<ChatMessageDto>> GetChatHistoryAsync(int limit = 50, CancellationToken ct = default);
    Task<bool> SetFeedbackAsync(Guid messageId, int effectiveness, CancellationToken ct = default);

    // Research Analytics
    Task<int> GetTotalQueriesAsync(CancellationToken ct = default);
    Task<long> GetStorageUsedAsync(CancellationToken ct = default);
}
