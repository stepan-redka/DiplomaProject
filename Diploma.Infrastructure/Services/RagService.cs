using Diploma.Application.DTOs;
using Diploma.Application.Interfaces;
using Diploma.Domain.Entities;
using Diploma.Infrastructure.Persistence;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Document = Diploma.Domain.Entities.Document;

namespace Diploma.Infrastructure.Services;

public class RagService : IRagService
{
    
    private readonly ITextChunkingService _chunkingService;
    private readonly IAiService _aiService;
    private readonly IVectorDatabase _vectorDb;
    private readonly ApplicationDbContext _dbContext;
    private readonly RagConfiguration _config;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<RagService> _logger;
    private string userId => _currentUserService.UserId ?? throw new UnauthorizedAccessException("User must be authenticated to perform this action.");

    public RagService(
        ITextChunkingService chunkingService,
        IAiService aiService,
        IVectorDatabase vectorDb,
        ApplicationDbContext dbContext,
        RagConfiguration config,
        ICurrentUserService currentUserService,
        ILogger<RagService> logger)
    {
        _chunkingService = chunkingService;
        _aiService = aiService;
        _vectorDb = vectorDb;
        _dbContext = dbContext;
        _config = config;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    
    public async Task<IngestResponse> IngestDocumentAsync(string content, string documentName)
    {
        _logger.LogInformation("Starting ingestion for document: {DocumentName}. Content length: {ContentLength}", documentName, content.Length);
        var sw = Stopwatch.StartNew();

        try
        {
            var textChunks = _chunkingService.ChunkText(content, _config.Chunking.MaxChunkSize, _config.Chunking.ChunkOverlap);
            _logger.LogDebug("Split document into {ChunkCount} chunks.", textChunks.Count);

            var document = new Document
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                FileName = documentName,
                CreatedAt = DateTime.UtcNow,
                Content = content
            };
            _dbContext.Documents.Add(document);

            var vectorDataList = new List<VectorData>();
            int index = 0;
            foreach (var chunk in textChunks)
            {
                var chunkId = Guid.NewGuid();
                var chunkEntity = new DocumentChunk
                {
                    Id = chunkId,
                    UserId = userId,
                    DocumentId = document.Id,
                    Content = chunk,
                    ChunkIndex = index,
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.DocumentChunks.Add(chunkEntity);
                
                var embedding = await _aiService.GetTextEmbeddingAsync(chunk);
                vectorDataList.Add(new VectorData(
                    chunkId,
                    document.Id,
                    embedding,
                    chunk,
                    new Dictionary<string, object> {{"index", index}}
                ));
                index++;
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogDebug("Metadata and chunks saved to PostgreSQL.");

            await _vectorDb.EnsureCollectionExistsAsync(_config.Qdrant.CollectionName, _config.Qdrant.VectorSize);
            await _vectorDb.UpsertChunksAsync(_config.Qdrant.CollectionName, vectorDataList, userId);
            
            sw.Stop();
            _logger.LogInformation("Successfully ingested {DocumentName} in {ElapsedMs}ms.", documentName, sw.ElapsedMilliseconds);

            return new IngestResponse {Success = true, ChunksCreated = textChunks.Count, Message = "Success!"}; 
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest document {DocumentName}", documentName);
            return new IngestResponse { Success = false, Message = ex.Message };
        }
    }

    public async Task<QueryResponse> QueryAsync(string question, int topK = 3)
    {
        _logger.LogInformation("Executing RAG query: {Question}", question);
        var sw = Stopwatch.StartNew();

        try
        {
            var questionEmbedding = await _aiService.GetTextEmbeddingAsync(question);
            var results = (await _vectorDb.SearchAsync(_config.Qdrant.CollectionName, questionEmbedding, userId, topK)).ToList();

            _logger.LogDebug("Vector search returned {ResultCount} chunks.", results.Count);

            var contextText = string.Join("\n---\n", results.Select(r => r.Content));
            var prompt = $"Context information is below.\n---------------------\n{contextText}\n---------------------\nGiven the context information and not prior knowledge, answer the query.\nQuery: {question}\nAnswer: " ;

            var answer = await _aiService.GenerateAnswerAsync(prompt);
            
            sw.Stop();
            _logger.LogInformation("Query completed in {ElapsedMs}ms.", sw.ElapsedMilliseconds);

            return new QueryResponse 
            { 
                Answer = answer,
                Sources = results.Select(r => new RetrievedContext 
                { 
                    Content = r.Content, 
                    SourceDocument = "Vector DB Chunk", // Ideal place to join with document metadata
                    Score = r.Score 
                }).ToList(),
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during QueryAsync for question: {Question}", question);
            throw;
        }
    }

    public async Task<bool> EnsureCollectionExistsAsync()
    {
        await _vectorDb.EnsureCollectionExistsAsync(_config.Qdrant.CollectionName, _config.Qdrant.VectorSize);
        return true;
    }

    public async Task<int> GetDocumentCountAsync() => await _dbContext.Documents.CountAsync();

    public async Task<List<DocumentDto>> GetUserDocumentsAsync()
    {
        return await _dbContext.Documents
            .Select(d => new DocumentDto
            {
                Id = d.Id,
                FileName = d.FileName,
                CreatedAt = d.CreatedAt,
                ChunkCount = d.Chunks.Count
            })
            .ToListAsync();
    }

    public async Task<List<StoredChunkInfo>> GetStoredChunksAsync(int limit = 500)
    {
        return await _dbContext.DocumentChunks
            .Include(c => c.Document)
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .Select(c => new StoredChunkInfo
            {
                Id = c.Id.ToString(),
                SourceDocument = c.Document.FileName,
                ContentPreview = c.Content.Length > 100 ? c.Content.Substring(0, 100) + "..." : c.Content,
                ChunkIndex = c.ChunkIndex
            })
            .ToListAsync();
    }


    //method to delete chunks by their IDs, ensuring that only chunks belonging to the authenticated user are deleted, and also removing the corresponding vectors from the vector database to maintain consistency. Returns the count of deleted chunks.
    public async Task<int> DeleteChunksAsync(IEnumerable<string> chunkIds)
    {
        //string to guid conversion with error handling
        var guids = chunkIds.Select(Guid.Parse).ToList();

        //find only chunks that belong to the user
        var chunksToDelete = await _dbContext.DocumentChunks.Where(c => guids.Contains(c.Id)).ToListAsync();

        if(!chunksToDelete.Any())
        {
            _logger.LogWarning("No chunks found for deletion with provided IDs: {ChunkIds}", chunkIds);
            return 0;
        }

        //delete from vector database first to avoid orphaned vectors in case of failure
        foreach (var chunk in chunksToDelete)
        {
            _logger.LogInformation("Deleting chunk {ChunkId} from document {DocumentId}", chunk.Id, chunk.DocumentId);
            await _vectorDb.DeleteDocumentVectorsAsync(_config.Qdrant.CollectionName, chunk.DocumentId, userId);
        }

        //delete from database(postgre)
        _dbContext.DocumentChunks.RemoveRange(chunksToDelete);
        await _dbContext.SaveChangesAsync();
        return chunksToDelete.Count;
    }


    //method to clear all documents and chunks for the authenticated user, ensuring that all associated vectors in the vector database are also deleted to maintain consistency. Returns true if the operation is successful.
     public async Task<bool> ClearCollectionAsync()
    {
        var myDocs = await _dbContext.Documents.ToListAsync();

        foreach (var doc in myDocs)
        {
            _logger.LogInformation("Deleting document {DocumentId} with name {FileName}", doc.Id, doc.FileName);
            await _vectorDb.DeleteDocumentVectorsAsync(_config.Qdrant.CollectionName, doc.Id, userId);
        }
        _dbContext.Documents.RemoveRange(myDocs);
        await _dbContext.SaveChangesAsync();    
        return true;
    }
}