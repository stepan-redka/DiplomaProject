using Diploma.Domain.Interfaces;

namespace Diploma.Domain.Entities;

public class UserPreference : IMultiTenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public int TopK { get; set; } = 3;
    public double Temperature { get; set; } = 0.7;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}
