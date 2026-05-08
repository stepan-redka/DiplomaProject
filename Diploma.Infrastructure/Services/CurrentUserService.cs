using Diploma.Application.Interfaces;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Diploma.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    public string? UserId { get; }
    public bool IsAdmin { get; }

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;
        UserId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        IsAdmin = user?.IsInRole("Admin") ?? false;
    }
}