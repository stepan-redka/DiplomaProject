using Diploma.Application.DTOs;
using Diploma.Application.Interfaces;
using Diploma.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Diploma.Infrastructure.Services;

public class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IVectorDatabase _vectorDb;
    private readonly RagConfiguration _config;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        ApplicationDbContext dbContext, 
        IVectorDatabase vectorDb, 
        RagConfiguration config,
        ILogger<DocumentService> logger)
    {
        _dbContext = dbContext;
        _vectorDb = vectorDb;
        _config = config;
        _logger = logger;
    }

    public async Task<int> GetDocumentCountAsync(CancellationToken ct = default) => await _dbContext.Documents.CountAsync(ct);

    public async Task<List<DocumentDto>> GetUserDocumentsAsync(CancellationToken ct = default)
    {
        return await _dbContext.Documents
            .Select(d => new DocumentDto 
            { 
                Id = d.Id, 
                FileName = d.FileName, 
                CreatedAt = d.CreatedAt, 
                ChunkCount = d.Chunks.Count, 
                FileSizeBytes = d.FileSizeBytes, 
                ProcessingTimeMs = d.ProcessingTimeMs 
            })
            .ToListAsync(ct);
    }

    public async Task<bool> DeleteDocumentAsync(Guid documentId, string userId, CancellationToken ct = default)
    {
        var document = await _dbContext.Documents.FirstOrDefaultAsync(d => d.Id == documentId && d.UserId == userId, ct);
        if (document == null) return false;

        await _vectorDb.DeleteDocumentVectorsAsync(_config.Qdrant.CollectionName, documentId, userId, ct);
        _dbContext.Documents.Remove(document);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ClearCollectionAsync(string userId, CancellationToken ct = default)
    {
        await _vectorDb.DeleteUserVectorsAsync(_config.Qdrant.CollectionName, userId, ct);
        var documents = await _dbContext.Documents.ToListAsync(ct);
        _dbContext.Documents.RemoveRange(documents);
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<StoredChunkInfo>> GetStoredChunksAsync(int limit = 500, CancellationToken ct = default)
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
            .ToListAsync(ct);
    }

    public async Task<int> DeleteChunksAsync(IEnumerable<string> chunkIds, string userId, CancellationToken ct = default)
    {
        var guids = chunkIds.Select(Guid.Parse).ToList();
        var chunksToDelete = await _dbContext.DocumentChunks.Where(c => guids.Contains(c.Id)).ToListAsync(ct);
        if (!chunksToDelete.Any()) return 0;

        foreach (var group in chunksToDelete.GroupBy(c => c.DocumentId)) 
        {
            await _vectorDb.DeleteDocumentVectorsAsync(_config.Qdrant.CollectionName, group.Key, userId, ct);
        }

        _dbContext.DocumentChunks.RemoveRange(chunksToDelete);
        await _dbContext.SaveChangesAsync(ct);
        return chunksToDelete.Count;
    }
}
