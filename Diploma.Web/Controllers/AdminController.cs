using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Diploma.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Diploma.Application.DTOs;

namespace Diploma.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<AdminController> _logger;

    public AdminController(ApplicationDbContext dbContext, ILogger<AdminController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        _logger.LogInformation("Admin dashboard accessed.");

        var stats = new AdminStatsDto
        {
            TotalUsers = await _dbContext.Users.CountAsync(),
            TotalDocuments = await _dbContext.Documents.IgnoreQueryFilters().CountAsync(),
            TotalChunks = await _dbContext.DocumentChunks.IgnoreQueryFilters().CountAsync(),
            DocumentsByUser = await _dbContext.Documents
                .IgnoreQueryFilters()
                .GroupBy(d => d.UserId)
                .Select(g => new UserDocCountDto { UserId = g.Key, Count = g.Count() })
                .ToListAsync(),
            
            // --- Chart Data: Daily Uploads (Last 7 Days) ---
            UploadsHistory = await _dbContext.Documents
                .IgnoreQueryFilters()
                .Where(d => d.CreatedAt >= DateTime.UtcNow.AddDays(-7))
                .GroupBy(d => d.CreatedAt.Date)
                .OrderBy(g => g.Key)
                .Select(g => new DailyUploadDto 
                { 
                    Date = g.Key.ToString("MMM dd"), 
                    Count = g.Count() 
                })
                .ToListAsync(),

            // --- Chart Data: Document Type Distribution ---
            TypeDistribution = await _dbContext.Documents
                .IgnoreQueryFilters()
                .GroupBy(d => d.FileName.Substring(d.FileName.LastIndexOf(".")).ToLower())
                .Select(g => new DocTypeDistributionDto
                {
                    Extension = g.Key,
                    Count = g.Count()
                })
                .ToListAsync()
        };

        return View(stats);
    }
}

public class AdminStatsDto
{
    public int TotalUsers { get; set; }
    public int TotalDocuments { get; set; }
    public int TotalChunks { get; set; }
    public List<UserDocCountDto> DocumentsByUser { get; set; } = new();
    public List<DailyUploadDto> UploadsHistory { get; set; } = new();
    public List<DocTypeDistributionDto> TypeDistribution { get; set; } = new();
}

public class UserDocCountDto
{
    public string UserId { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DailyUploadDto
{
    public string Date { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DocTypeDistributionDto
{
    public string Extension { get; set; } = string.Empty;
    public int Count { get; set; }
}
