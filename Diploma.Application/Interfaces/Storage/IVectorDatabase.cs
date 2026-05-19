using Diploma.Application.DTOs;

namespace Diploma.Application.Interfaces.Storage;

public interface IVectorDatabase
{
    /// <summary>
    /// Ensures the collection exists in the vector database.
    /// </summary>
    Task EnsureCollectionExistsAsync(string collectionName, int vectorSize, CancellationToken ct = default);

    /// <summary>
    /// Creates a payload index for optimized filtering (e.g., by userId).
    /// </summary>
    Task CreatePayloadIndexAsync(string collectionName, string fieldName, CancellationToken ct = default);

    /// <summary>
    /// Saves a batch of text chunks with their embeddings, scoped to a specific user.
    /// </summary>
    Task UpsertChunksAsync(string collectionName, IEnumerable<VectorData> data, string userId, CancellationToken ct = default);

    /// <summary>
    /// Searches for similar text chunks, strictly filtered by the active user's ID and optionally by specific document IDs.
    /// </summary>
    Task<List<DocumentChunkDto>> SearchAsync(
        string collectionName,
        ReadOnlyMemory<float> vector,
        int limit,
        List<Guid>? allowedDocumentIds,
        CancellationToken ct,
        IReadOnlyDictionary<string, string>? requiredPayloadMatches = null);

    /// <summary>
    /// Deletes all vectors associated with a specific document for a user.
    /// </summary>
    Task DeleteDocumentVectorsAsync(string collectionName, Guid documentId, string userId, CancellationToken ct = default);

    /// <summary>
    /// Deletes all vectors for a specific user.
    /// </summary>
    Task DeleteUserVectorsAsync(string collectionName, string userId, CancellationToken ct = default);
}

public record VectorData(Guid ChunkId, Guid DocumentId, float[] Embedding, string Content, Dictionary<string, object>? Metadata = null);
