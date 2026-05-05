using Diploma.Application.DTOs;
using Diploma.Application.Interfaces;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Diploma.Infrastructure.Persistence;

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
        var sw = Stopwatch.StartNew();
        var collections = await _client.ListCollectionsAsync();
        if (collections.Contains(collectionName))
        {
            return;
        }

        _logger.LogInformation("Creating Qdrant collection: {CollectionName} with size {VectorSize}", collectionName, vectorSize);
        
        try
        {
            await _client.CreateCollectionAsync(collectionName, new VectorParams
            {
                Size = (ulong)vectorSize,
                Distance = Distance.Cosine
            });
            sw.Stop();
            _logger.LogInformation("Collection {CollectionName} created successfully in {ElapsedMs}ms.", collectionName, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to create Qdrant collection {CollectionName} after {ElapsedMs}ms", collectionName, sw.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task UpsertChunksAsync(string collectionName, IEnumerable<VectorData> data, string userId)
    {
        var sw = Stopwatch.StartNew();
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

        try
        {
            await _client.UpsertAsync(collectionName, points);
            sw.Stop();
            _logger.LogInformation("Successfully upserted {Count} points for user {UserId} into {Collection} in {ElapsedMs}ms", 
                points.Count, userId, collectionName, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to upsert points into {Collection} for user {UserId} after {ElapsedMs}ms", 
                collectionName, userId, sw.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<IEnumerable<ScoredChunkDto>> SearchAsync(string collectionName, float[] embedding, string userId, int limit = 5)
    {
        var sw = Stopwatch.StartNew();
        // STRICT isolation: Filter by user_id
        var filter = new Filter
        {
            Must = { Conditions.MatchKeyword("user_id", userId) }
        };

        try
        {
            var results = await _client.SearchAsync(
                collectionName,
                embedding,
                filter: filter,
                limit: (ulong)limit
            );

            sw.Stop();
            _logger.LogDebug("Qdrant search for user {UserId} returned {Count} results in {ElapsedMs}ms", 
                userId, results.Count, sw.ElapsedMilliseconds);

            return results.Select(r => new ScoredChunkDto
            {
                ChunkId = Guid.Parse(r.Id.Uuid),
                DocumentId = Guid.TryParse(r.Payload["document_id"].StringValue, out var docId) ? docId : Guid.Empty,
                Content = r.Payload["content"].StringValue,
                Score = r.Score,
                Metadata = r.Payload.ToDictionary(p => p.Key, p => (object)p.Value.ToString())
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Qdrant search failed for user {UserId} after {ElapsedMs}ms", userId, sw.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task DeleteDocumentVectorsAsync(string collectionName, Guid documentId, string userId)
    {
        var sw = Stopwatch.StartNew();
        var filter = new Filter
        {
            Must = 
            { 
                Conditions.MatchKeyword("user_id", userId),
                Conditions.MatchKeyword("document_id", documentId.ToString())
            }
        };

        try
        {
            await _client.DeleteAsync(collectionName, filter);
            sw.Stop();
            _logger.LogInformation("Deleted vectors for document {DocumentId} and user {UserId} in {ElapsedMs}ms", 
                documentId, userId, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to delete vectors for document {DocumentId} and user {UserId} after {ElapsedMs}ms", 
                documentId, userId, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
