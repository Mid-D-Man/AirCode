namespace AirCode.Domain.Interfaces;

// Interface for adding basic security attributes
public interface ISecureEntity
{
    string SecurityToken { get; init; }
    DateTime LastModified { get; init; }
    string ModifiedBy { get; init; }
}