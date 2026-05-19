using Diploma.Application.DTOs;
using Diploma.Application.Interfaces.AI;
using Diploma.Application.Interfaces.Chat;
using Diploma.Application.Interfaces.Identity;
using Diploma.Domain.Entities;
using Diploma.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Diploma.Infrastructure.Services.Chat;

public class ChatHistoryService : IChatHistoryService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISemanticCacheService _semanticCacheService;
    private readonly ILogger<ChatHistoryService> _logger;

    public ChatHistoryService(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        ISemanticCacheService semanticCacheService,
        ILogger<ChatHistoryService> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _semanticCacheService = semanticCacheService;
        _logger = logger;
    }

    public async Task<List<ChatSessionDto>> GetUserSessionsAsync(CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return new List<ChatSessionDto>();

        return await _dbContext.ChatSessions
            .OrderByDescending(s => s.LastUpdatedAt)
            .Select(s => new ChatSessionDto
            {
                Id = s.Id,
                Title = s.Title,
                CreatedAt = s.CreatedAt,
                LastUpdatedAt = s.LastUpdatedAt,
                MessageCount = s.Messages.Count,
                DocumentCount = s.RelatedDocumentIds.Count,
                SelectedModel = s.SelectedModel
            })
            .ToListAsync(ct);
    }

    public async Task<ChatSessionDetailDto?> GetSessionDetailsAsync(Guid sessionId, CancellationToken ct = default)
    {
        try
        {
            var session = await _dbContext.ChatSessions
                .Include(s => s.Messages)
                .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

            if (session == null) return null;

            var deserializeOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            return new ChatSessionDetailDto
            {
                Id = session.Id,
                Title = session.Title,
                CreatedAt = session.CreatedAt,
                SelectedModel = session.SelectedModel,
                RelatedDocumentIds = session.RelatedDocumentIds,
                Messages = session.Messages.OrderBy(m => m.CreatedAt).Select(m =>
                {
                    List<SourceCitation> sources;
                    try
                    {
                        sources = !string.IsNullOrEmpty(m.Metadata)
                            ? JsonSerializer.Deserialize<List<SourceCitation>>(m.Metadata, deserializeOptions) ?? new List<SourceCitation>()
                            : new List<SourceCitation>();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize metadata for message {MessageId}", m.Id);
                        sources = new List<SourceCitation>();
                    }

                    return new ChatMessageDto
                    {
                        Id = m.Id,
                        Role = m.Role,
                        Content = m.Content,
                        CreatedAt = m.CreatedAt,
                        Effectiveness = (int)m.Effectiveness,
                        ModelName = m.ModelName,
                        ProcessingTimeMs = m.ProcessingTimeMs,
                        TokenCount = m.TokenCount,
                        Sources = sources
                    };
                }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error while retrieving session details for {SessionId}", sessionId);
            throw; // Re-throw to allow controller to handle 500 error correctly
        }
    }

    public async Task<Guid> CreateSessionAsync(string title, IEnumerable<Guid>? documentIds = null, CancellationToken ct = default)
    {
        var validatedDocumentIds = await GetValidDocumentIdsAsync(documentIds, ct);
        var session = new ChatSession
        {
            Title = title,
            RelatedDocumentIds = validatedDocumentIds
        };

        _dbContext.ChatSessions.Add(session);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created new research session: {SessionId} (Title: {Title})", session.Id, title);
        return session.Id;
    }

    public async Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return false;

        // SECURITY: Use FirstOrDefaultAsync with explicit UserId predicate.
        var session = await _dbContext.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct);
        if (session == null) return false;

        _dbContext.ChatSessions.Remove(session);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted research session: {SessionId}", sessionId);
        return true;
    }

    public async Task<bool> UpdateSessionTitleAsync(Guid sessionId, string newTitle, CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return false;

        // SECURITY: Explicit UserId predicate to prevent FindAsync cache bypass.
        var session = await _dbContext.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct);
        if (session == null) return false;

        session.Title = newTitle;
        session.LastUpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ToggleDocumentBindingAsync(Guid sessionId, Guid documentId, CancellationToken ct = default)
    {
        if (!await IsValidDocumentIdAsync(documentId, ct))
        {
            _logger.LogWarning("Ignored invalid or foreign document binding request for document {DocumentId}", documentId);
            return false;
        }

        var session = await _dbContext.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session == null)
        {
            _logger.LogWarning("Attempted to toggle binding for non-existent session {SessionId}", sessionId);
            return false;
        }

        var isBound = session.RelatedDocumentIds.Contains(documentId);

        if (isBound)
        {
            session.RelatedDocumentIds.Remove(documentId);
            _logger.LogInformation("Unbound document {DocumentId} from session {SessionId}", documentId, sessionId);
        }
        else
        {
            session.RelatedDocumentIds.Add(documentId);
            _logger.LogInformation("Bound document {DocumentId} to session {SessionId}", documentId, sessionId);
        }

        session.LastUpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        // Invalidate cache
        await _semanticCacheService.ClearCacheAsync(ct);

        return !isBound;
    }

    private async Task<List<Guid>> GetValidDocumentIdsAsync(IEnumerable<Guid>? documentIds, CancellationToken ct)
    {
        var requestedIds = documentIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList() ?? new List<Guid>();

        if (!requestedIds.Any()) return new List<Guid>();

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return new List<Guid>();

        return await _dbContext.Documents
            .IgnoreQueryFilters()
            .Where(d => requestedIds.Contains(d.Id) && (d.UserId == userId || d.UserId == "public"))
            .Select(d => d.Id)
            .ToListAsync(ct);
    }

    private async Task<bool> IsValidDocumentIdAsync(Guid documentId, CancellationToken ct)
    {
        if (documentId == Guid.Empty) return false;

        var userId = _currentUserService.UserId;
        if (string.IsNullOrEmpty(userId)) return false;

        return await _dbContext.Documents
            .IgnoreQueryFilters()
            .AnyAsync(d => d.Id == documentId && (d.UserId == userId || d.UserId == "public"), ct);
    }
}
