using Diploma.Application.DTOs;

namespace Diploma.Web.Models;

public class DashboardViewModel
{
    public string? UserName { get; set; }
    public IEnumerable<DocumentDto> Documents { get; set; } = new List<DocumentDto>();
    public int TotalDocuments { get; set; }
    public int TotalChunks { get; set; }
    public int TotalQueries { get; set; }
    public int TotalSessions { get; set; }
    public long StorageUsedBytes { get; set; }
    public string StorageUsedFormatted { get; set; } = "0 B";
    public double StorageLimitMB { get; set; } = 1024; // 1GB default limit for thesis context
    public bool IsAuthenticated { get; set; }
    
    // Session Management
    public Guid? ActiveSessionId { get; set; }
    public List<ChatSessionDto> RecentSessions { get; set; } = new();

    public int StorageUsedPercentage => (int)Math.Min(100, (StorageUsedBytes / (1024.0 * 1024.0) / StorageLimitMB) * 100);

    public SidebarViewModel Sidebar => new SidebarViewModel
    {
        UserName = UserName ?? (IsAuthenticated ? "Principal Investigator" : "Guest Researcher"),
        UserEmail = IsAuthenticated ? UserName : null, // Assuming UserName is email for now or needs to be set separately
        IsAuthenticated = IsAuthenticated,
        ActiveSessionId = ActiveSessionId,
        RecentSessions = RecentSessions,
        Documents = Documents,
        StorageUsedPercentage = StorageUsedPercentage
    };
}
