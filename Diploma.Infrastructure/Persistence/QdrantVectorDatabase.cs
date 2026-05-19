using Diploma.Application.DTOs;
using Diploma.Application.Interfaces.Storage;
using Diploma.Application.Interfaces.Identity;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Grpc.Core;

namespace Diploma.Infrastructure.Persistence;

public class QdrantVectorDatabase : IVectorDatabase
{
    private readonly QdrantClient _client;
    private readonly ICurrentUserService _currentUserService;
    private readonly RagConfiguration _config;
    private readonly ILogger<QdrantVectorDatabase> _logger;

    public QdrantVectorDatabase(
        QdrantClient client,
        ICurrentUserService currentUserService,
        RagConfiguration config,
        ILogger<QdrantVectorDatabase> logger)
    {
        _client = client;
        _currentUserService = currentUserService;
        _config = config;
        _logger = logger;
    }

    public async Task EnsureCollectionExistsAsync(string collectionName, int vectorSize, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var collections = await _client.ListCollectionsAsync(ct);
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
            }, cancellationToken: ct);

            // PERFORMANCE OPTIMIZATION: Create index on user_id for multi-tenant isolation performance
            await CreatePayloadIndexAsync(collectionName, "user_id", ct);

            // PERFORMANCE OPTIMIZATION: Create index on document_id for scoped research performance
            await CreatePayloadIndexAsync(collectionName, "document_id", ct);

            sw.Stop();
            _logger.LogInformation("Collection {CollectionName} created successfully with indexes in {ElapsedMs}ms.", collectionName, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to create Qdrant collection {CollectionName} after {ElapsedMs}ms", collectionName, sw.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task CreatePayloadIndexAsync(string collectionName, string fieldName, CancellationToken ct = default)
    {
        _logger.LogInformation("Ensuring payload index for field {FieldName} in collection {CollectionName}", fieldName, collectionName);
        try
        {
            await _client.CreatePayloadIndexAsync(collectionName, fieldName, PayloadSchemaType.Keyword, cancellationToken: ct);
            _logger.LogInformation("Payload index for {FieldName} confirmed.", fieldName);
        }
        catch (Exception ex)
        {
            // Qdrant might throw if index already exists, which is fine for "Ensure" semantics
            _logger.LogDebug(ex, "Payload index creation for {FieldName} note: {Message}", fieldName, ex.Message);
        }
    }

    public async Task UpsertChunksAsync(string collectionName, IEnumerable<VectorData> data, string userId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var allPoints = data.Select(d =>
        {
            var point = new PointStruct
            {
                Id = d.ChunkId,
                Vectors = d.Embedding,
                Payload =
                {
                    ["user_id"] = userId,
                    ["document_id"] = d.DocumentId.ToString(),
                    ["content"] = d.Content
                }
            };

            if (d.Metadata != null)
            {
                foreach (var meta in d.Metadata)
                {
                    point.Payload[meta.Key] = meta.Value?.ToString() ?? string.Empty;
                }
            }
            return point;
        }).ToList();

        // PERFORMANCE OPTIMIZATION: Batch upserts to prevent timeouts and handle large payloads
        const int batchSize = 100;
        int totalUpserted = 0;

        try
        {
            for (int i = 0; i < allPoints.Count; i += batchSize)
            {
                var batch = allPoints.Skip(i).Take(batchSize).ToList();
                await _client.UpsertAsync(collectionName, batch, cancellationToken: ct);
                totalUpserted += batch.Count;
                _logger.LogDebug("Upserted batch of {BatchCount} points. Total: {TotalUpserted}/{AllCount}",
                    batch.Count, totalUpserted, allPoints.Count);
            }

            sw.Stop();
            _logger.LogInformation("Successfully upserted {Count} points for user {UserId} into {Collection} in {ElapsedMs}ms",
                allPoints.Count, userId, collectionName, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to upsert points into {Collection} for user {UserId} after {ElapsedMs}ms",
                collectionName, userId, sw.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task<List<DocumentChunkDto>> SearchAsync(
        string collectionName,
        ReadOnlyMemory<float> vector,
        int limit,
        List<Guid>? allowedDocumentIds,
        CancellationToken ct,
        IReadOnlyDictionary<string, string>? requiredPayloadMatches = null)
    {
        var sw = Stopwatch.StartNew();
        var userId = _currentUserService.UserId ?? "system";

        // STRICT isolation: Filter by user_id
        var filter = new Filter
        {
            Must = { Conditions.MatchKeyword("user_id", userId) }
        };

        // Task 2: SCOPED isolation: Filter by specific document IDs if provided
        if (allowedDocumentIds != null && allowedDocumentIds.Any())
        {
            var docIdStrings = allowedDocumentIds.Select(id => id.ToString()).ToList();

            // Constructing a strict MatchAny condition for document_id using Keywords for multiple strings
            var docIdMatch = new Match { Keywords = new RepeatedStrings() };
            foreach (var id in docIdStrings)
            {
                docIdMatch.Keywords.Strings.Add(id);
            }

            filter.Must.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = "document_id",
                    Match = docIdMatch
                }
            });

            _logger.LogDebug("Qdrant search scoped to {DocCount} documents.", allowedDocumentIds.Count);
        }

        if (requiredPayloadMatches != null)
        {
            foreach (var match in requiredPayloadMatches)
            {
                filter.Must.Add(Conditions.MatchKeyword(match.Key, match.Value));
            }
        }

        try
        {
            var results = await _client.SearchAsync(
                collectionName,
                vector,
                filter: filter,
                limit: (ulong)limit,
                cancellationToken: ct
            );

            sw.Stop();
            _logger.LogDebug("Qdrant search for user {UserId} returned {Count} results in {ElapsedMs}ms",
                userId, results.Count, sw.ElapsedMilliseconds);

            return results.Select(r => new DocumentChunkDto
            {
                ChunkId = Guid.TryParse(r.Id.Uuid, out var cid) ? cid : Guid.Empty,
                DocumentId = r.Payload.TryGetValue("document_id", out var docVal) && Guid.TryParse(docVal.StringValue, out var docId) ? docId : Guid.Empty,
                Content = r.Payload.TryGetValue("content", out var contentVal) ? contentVal.StringValue : string.Empty,
                Score = r.Score,
                Metadata = r.Payload.ToDictionary(
                    p => p.Key, 
                    p => p.Value.KindCase switch 
                    {
                        Value.KindOneofCase.StringValue => (object)p.Value.StringValue,
                        Value.KindOneofCase.IntegerValue => (object)p.Value.IntegerValue,
                        Value.KindOneofCase.DoubleValue => (object)p.Value.DoubleValue,
                        Value.KindOneofCase.BoolValue => (object)p.Value.BoolValue,
                        _ => (object)p.Value.ToString()
                    })
            }).ToList();
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            _logger.LogWarning("Qdrant collection {Collection} not found. Returning empty results.", collectionName);
            return new List<DocumentChunkDto>();
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Qdrant search failed for user {UserId} after {ElapsedMs}ms", userId, sw.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task DeleteDocumentVectorsAsync(string collectionName, Guid documentId, string userId, CancellationToken ct = default)
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
            await _client.DeleteAsync(collectionName, filter, cancellationToken: ct);
            sw.Stop();
            _logger.LogInformation("Deleted vectors for document {DocumentId} and user {UserId} in {ElapsedMs}ms",
                documentId, userId, sw.ElapsedMilliseconds);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            _logger.LogWarning("Qdrant collection {Collection} not found during document vector deletion. Skipping.", collectionName);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to delete vectors for document {DocumentId} and user {UserId} after {ElapsedMs}ms",
                documentId, userId, sw.ElapsedMilliseconds);
            throw;
        }
    }

    public async Task DeleteUserVectorsAsync(string collectionName, string userId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var filter = new Filter
        {
            Must = { Conditions.MatchKeyword("user_id", userId) }
        };

        try
        {
            await _client.DeleteAsync(collectionName, filter, cancellationToken: ct);
            sw.Stop();
            _logger.LogInformation("Deleted all vectors for user {UserId} in {ElapsedMs}ms",
                userId, sw.ElapsedMilliseconds);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            _logger.LogWarning("Qdrant collection {Collection} not found during user vector deletion. Skipping.", collectionName);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Failed to delete vectors for user {UserId} after {ElapsedMs}ms",
                userId, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
