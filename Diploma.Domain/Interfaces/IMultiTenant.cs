namespace Diploma.Domain.Interfaces;

public interface IMultiTenant
{
    // This is the "Label" we will use for the "Lock"
    string UserId { get; set; }
}