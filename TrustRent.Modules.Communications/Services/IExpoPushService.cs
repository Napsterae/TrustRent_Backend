namespace TrustRent.Modules.Communications.Services;

public interface IExpoPushService
{
    Task<IReadOnlyCollection<string>> SendNotificationAsync(
        IEnumerable<string> expoPushTokens,
        string title,
        string body,
        object? data = null,
        CancellationToken cancellationToken = default);
}