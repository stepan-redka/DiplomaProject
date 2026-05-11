using Diploma.Application.DTOs;

namespace Diploma.Web.Models;

public class DashboardViewModel
{
    public IEnumerable<DocumentDto> Documents { get; set; } = new List<DocumentDto>();
    public int TotalDocuments { get; set; }
    public int TotalChunks { get; set; }
    public int TotalQueries { get; set; }
    public string StorageUsedFormatted { get; set; } = "0 B";
    public bool IsAuthenticated { get; set; }
}
