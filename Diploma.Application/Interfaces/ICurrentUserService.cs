namespace Diploma.Application.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    bool IsAdmin { get; }
}