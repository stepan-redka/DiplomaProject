using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Diploma.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Diploma.Application.DTOs;
using Diploma.Application.Interfaces.Analytics;
using Diploma.Application.Interfaces.Chat;
using Diploma.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace Diploma.Web.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHealthService _healthService;
    private readonly IRagService _ragService;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        ApplicationDbContext dbContext,
        IHealthService healthService,
        IRagService ragService,
        UserManager<IdentityUser> userManager,
        ILogger<AdminController> logger)
    {
        _dbContext = dbContext;
        _healthService = healthService;
        _ragService = ragService;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        _logger.LogInformation("Proactive admin dashboard accessed.");

        var health = await _healthService.GetSystemHealthAsync();

        var stats = new AdminStatsDto
        {
            Health = health,
            TotalUsers = await _dbContext.Users.CountAsync(),
            TotalDocuments = await _dbContext.Documents.IgnoreQueryFilters().CountAsync(),
            TotalChunks = await _dbContext.DocumentChunks.IgnoreQueryFilters().CountAsync(),

            // User stats with Lockout status
            UserManagement = await _dbContext.Users
                .Select(u => new UserManagementDto
                {
                    UserId = u.Id,
                    Email = u.Email ?? "Unknown",
                    IsLockedOut = u.LockoutEnd > DateTimeOffset.UtcNow,
                    DocumentCount = _dbContext.Documents.IgnoreQueryFilters().Count(d => d.UserId == u.Id)
                }).ToListAsync(),

            // Failed Ingestions Telemetry
            FailedIngestions = await _dbContext.Documents
                .IgnoreQueryFilters()
                .Where(d => d.Status == IngestionStatus.Failed)
                .OrderByDescending(d => d.CreatedAt)
                .Take(15)
                .Select(d => new FailedIngestionDto
                {
                    DocumentId = d.Id,
                    FileName = d.FileName,
                    UserId = d.UserId,
                    Timestamp = d.CreatedAt,
                    ErrorMessage = d.ErrorMessage ?? "Unknown Error"
                }).ToListAsync()
        };

        return View(stats);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleUserStatus(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        if (user.LockoutEnd > DateTimeOffset.UtcNow)
        {
            // Unlock
            await _userManager.SetLockoutEndDateAsync(user, null);
            _logger.LogInformation("Admin unlocked user: {UserId}", userId);
        }
        else
        {
            // Lock indefinitely (99 years)
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(99));
            _logger.LogInformation("Admin locked user: {UserId}", userId);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WipeUserVectors(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest("UserId is required.");
        }

        _logger.LogWarning("Admin {AdminId} initiated emergency vector wipe for user: {TargetUserId}",
            User.Identity?.Name, userId);

        // SECURITY FIX: Bypassing scoped IRagService to ensure we target the specific user
        var userDocs = await _dbContext.Documents
            .IgnoreQueryFilters()
            .Where(d => d.UserId == userId)
            .ToListAsync();

        if (userDocs.Count > 0)
        {
            _dbContext.Documents.RemoveRange(userDocs);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("SQL wipe complete for user: {TargetUserId}. Removed {Count} documents.", userId, userDocs.Count);
        }

        return RedirectToAction(nameof(Index));
    }
}

public class AdminStatsDto
{
    public SystemHealthDto Health { get; set; } = new();
    public int TotalUsers { get; set; }
    public int TotalDocuments { get; set; }
    public int TotalChunks { get; set; }
    public List<UserManagementDto> UserManagement { get; set; } = new();
    public List<FailedIngestionDto> FailedIngestions { get; set; } = new();
}

public class UserManagementDto
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsLockedOut { get; set; }
    public int DocumentCount { get; set; }
}

public class FailedIngestionDto
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
