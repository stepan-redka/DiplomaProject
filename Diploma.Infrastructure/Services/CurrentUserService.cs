using Diploma.Application.Interfaces;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Diploma.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private string? _manualUserId;

    public string? UserId 
    {
        get
        {
            if (_manualUserId != null) return _manualUserId;

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            // 1. Try Authenticated User
            var authenticatedId = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(authenticatedId)) return authenticatedId;

            // 2. Try Guest Session ID
            var guestId = httpContext.Session.GetString("GuestUserId");
            if (string.IsNullOrEmpty(guestId))
            {
                guestId = "guest_" + Guid.NewGuid().ToString();
                httpContext.Session.SetString("GuestUserId", guestId);
            }

            return guestId;
        }
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
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