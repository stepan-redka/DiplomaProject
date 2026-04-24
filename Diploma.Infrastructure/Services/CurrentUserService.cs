using Diploma.Application.Interfaces;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Diploma.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    public string? UserId { get; }

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        // Extract user ID from JWT claims
        UserId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}