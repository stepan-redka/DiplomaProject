using Diploma.Application.DTOs;

namespace Diploma.Web.Models;

public class SidebarViewModel
{
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
    public bool IsAuthenticated { get; set; }
    public Guid? ActiveSessionId { get; set; }
    public int StorageUsedPercentage { get; set; }
    public List<ChatSessionDto> RecentSessions { get; set; } = new();
    public IEnumerable<DocumentDto> Documents { get; set; } = new List<DocumentDto>();
}
