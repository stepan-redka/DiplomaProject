using Diploma.Application.DTOs;
using Diploma.Application.Interfaces;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Microsoft.Extensions.Logging;

namespace Diploma.Infrastructure.AI;

public class QdrantVectorDatabase : IVectorDatabase
{
    private readonly QdrantClient _client;
    private readonly ILogger<QdrantVectorDatabase> _logger;

    public QdrantVectorDatabase(QdrantClient client, ILogger<QdrantVectorDatabase> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task EnsureCollectionExistsAsync(string collectionName, int vectorSize)
    {
        var collections = await _client.ListCollectionsAsync();
        if (collections.Contains(collectionName))
        {
            return;
        }

        _logger.LogInformation("Creating Qdrant collection: {CollectionName} with size {VectorSize}", collectionName, vectorSize);
        
        await _client.CreateCollectionAsync(collectionName, new VectorParams
        {
            Size = (ulong)vectorSize,
            Distance = Distance.Cosine
        });
    }

    public async Task UpsertChunksAsync(string collectionName, IEnumerable<VectorData> data, string userId)
    {
        var points = data.Select(d => new PointStruct
        {
            Id = d.ChunkId,
            Vectors = d.Embedding,
            Payload =
            {
                ["user_id"] = userId,
                ["document_id"] = d.DocumentId.ToString(),
                ["content"] = d.Content
            }
        }).ToList();

        // Add additional metadata if present
        foreach (var (point, sourceData) in points.Zip(data))
        {
            if (sourceData.Metadata != null)
            {
                foreach (var meta in sourceData.Metadata)
                {
                    point.Payload[meta.Key] = meta.Value?.ToString() ?? string.Empty;
                }
            }
        }

        await _client.UpsertAsync(collectionName, points);
        _logger.LogDebug("Upserted {Count} points for user {UserId} into {Collection}", points.Count, userId, collectionName);
    }

    public async Task<IEnumerable<ScoredChunkDto>> SearchAsync(string collectionName, float[] embedding, string userId, int limit = 5)
    {
        // STRICT isolation: Filter by user_id
        var filter = new Filter
        {
            Must = { Conditions.MatchKeyword("user_id", userId) }
        };

        var results = await _client.SearchAsync(
            collectionName,
            embedding,
            filter: filter,
            limit: (ulong)limit
        );

        return results.Select(r => new ScoredChunkDto
        {
            ChunkId = Guid.Parse(r.Id.Uuid),
            DocumentId = Guid.TryParse(r.Payload["document_id"].StringValue, out var docId) ? docId : Guid.Empty,
            Content = r.Payload["content"].StringValue,
            Score = r.Score,
            Metadata = r.Payload.ToDictionary(p => p.Key, p => (object)p.Value.ToString())
        });
    }

    public async Task DeleteDocumentVectorsAsync(string collectionName, Guid documentId, string userId)
    {
        var filter = new Filter
        {
            Must = 
            { 
                Conditions.MatchKeyword("user_id", userId),
                Conditions.MatchKeyword("document_id", documentId.ToString())
            }
        };

        await _client.DeleteAsync(collectionName, filter);
        _logger.LogInformation("Deleted vectors for document {DocumentId} and user {UserId}", documentId, userId);
    }
}
