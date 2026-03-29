namespace TrustRent.Shared.Contracts.Interfaces;

public interface INotificationService
{
    Task SendNotificationAsync(Guid userId, string type, string message);
}