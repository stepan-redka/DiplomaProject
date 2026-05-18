using Diploma.Application.Interfaces.Identity;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Diploma.Infrastructure.Services.Identity;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private string? _manualUserId;
    private string? _cachedUserId;
    private bool? _cachedIsAdmin;

    public string? UserId 
    {
        get
        {
            if (_manualUserId != null) return _manualUserId;
            if (_cachedUserId != null) return _cachedUserId;

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return null;

            // 1. Try Authenticated User
            var authenticatedId = httpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(authenticatedId))
            {
                _cachedUserId = authenticatedId;
                return _cachedUserId;
            }

            // 2. Try Guest Cookie (SAD-PATH: Avoid Session to prevent DataProtection circularity)
            var guestId = httpContext.Request.Cookies["GuestUserId"];
            if (string.IsNullOrEmpty(guestId))
            {
                guestId = "guest_" + Guid.NewGuid().ToString();
                try
                {
                    httpContext.Response.Cookies.Append("GuestUserId", guestId, new CookieOptions 
                    { 
                        HttpOnly = true, 
                        Secure = true, 
                        SameSite = SameSiteMode.Strict,
                        Expires = DateTimeOffset.UtcNow.AddDays(30),
                        IsEssential = true 
                    });
                }
                catch { /* Response started */ }
            }

            _cachedUserId = guestId;
            return _cachedUserId;
        }
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
    
    public bool IsAdmin 
    {
        get
        {
            if (_cachedIsAdmin.HasValue) return _cachedIsAdmin.Value;
            _cachedIsAdmin = _httpContextAccessor.HttpContext?.User?.HasClaim(ClaimTypes.Role, "Admin") ?? false;
            return _cachedIsAdmin.Value;
        }
    }

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void SetManualUserId(string userId)
    {
        _manualUserId = userId;
    }
}