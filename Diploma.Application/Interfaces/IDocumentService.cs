using Diploma.Application.DTOs;

namespace Diploma.Application.Interfaces;

public interface IDocumentService
{
    Task<int> GetDocumentCountAsync(CancellationToken ct = default);
    Task<List<DocumentDto>> GetUserDocumentsAsync(CancellationToken ct = default);
    Task<bool> DeleteDocumentAsync(Guid documentId, string userId, CancellationToken ct = default);
    Task<bool> ClearCollectionAsync(string userId, CancellationToken ct = default);
    Task<List<StoredChunkInfo>> GetStoredChunksAsync(int limit = 500, CancellationToken ct = default);
    Task<int> DeleteChunksAsync(IEnumerable<string> chunkIds, string userId, CancellationToken ct = default);
}
