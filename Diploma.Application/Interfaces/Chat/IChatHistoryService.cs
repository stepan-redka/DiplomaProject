using Diploma.Application.DTOs;

namespace Diploma.Application.Interfaces.Chat;

/// <summary>
/// Service for managing user chat sessions and research history.
/// </summary>
public interface IChatHistoryService
{
    /// <summary>
    /// Retrieves all chat sessions for the current user.
    /// </summary>
    Task<List<ChatSessionDto>> GetUserSessionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves full details (including messages) for a specific session.
    /// </summary>
    Task<ChatSessionDetailDto?> GetSessionDetailsAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new research session.
    /// </summary>
    Task<Guid> CreateSessionAsync(string title, IEnumerable<Guid>? documentIds = null, CancellationToken ct = default);

    /// <summary>
    /// Deletes a research session and all associated messages.
    /// </summary>
    Task<bool> DeleteSessionAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Updates the title of an existing session.
    /// </summary>
    Task<bool> UpdateSessionTitleAsync(Guid sessionId, string newTitle, CancellationToken ct = default);
}
