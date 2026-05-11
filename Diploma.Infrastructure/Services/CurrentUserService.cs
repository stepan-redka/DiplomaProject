using Diploma.Application.Interfaces;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Diploma.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private string? _manualUserId;

    public string? UserId => _manualUserId ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    public bool IsAdmin => _httpContextAccessor.HttpContext?.User?.IsInRole("Admin") ?? false;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Allows setting the UserId manually for background processing contexts.
    /// </summary>
    public void SetManualUserId(string userId)
    {
        _manualUserId = userId;
    }
}