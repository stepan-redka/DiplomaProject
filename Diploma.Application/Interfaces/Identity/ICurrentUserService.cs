namespace Diploma.Application.Interfaces.Identity;

public interface ICurrentUserService
{
    string? UserId { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
}