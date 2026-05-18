using Diploma.Application.DTOs;
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
    private readonly ILogger<ChatHistoryService> _logger;

    public ChatHistoryService(
        ApplicationDbContext dbContext, 
        ICurrentUserService currentUserService,
        ILogger<ChatHistoryService> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
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
        var session = new ChatSession
        {
            Title = title,
            RelatedDocumentIds = documentIds?.ToList() ?? new List<Guid>()
        };

        _dbContext.ChatSessions.Add(session);
        await _dbContext.SaveChangesAsync(ct);
        
        _logger.LogInformation("Created new research session: {SessionId} (Title: {Title})", session.Id, title);
        return session.Id;
    }

    public async Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _dbContext.ChatSessions.FindAsync(new object[] { sessionId }, ct);
        if (session == null) return false;

        _dbContext.ChatSessions.Remove(session);
        await _dbContext.SaveChangesAsync(ct);
        
        _logger.LogInformation("Deleted research session: {SessionId}", sessionId);
        return true;
    }

    public async Task<bool> UpdateSessionTitleAsync(Guid sessionId, string newTitle, CancellationToken ct = default)
    {
        var session = await _dbContext.ChatSessions.FindAsync(new object[] { sessionId }, ct);
        if (session == null) return false;

        session.Title = newTitle;
        session.LastUpdatedAt = DateTime.UtcNow;
        
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }
}
