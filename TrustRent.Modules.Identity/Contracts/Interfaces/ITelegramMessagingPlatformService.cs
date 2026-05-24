using TrustRent.Modules.Identity.Models;

namespace TrustRent.Modules.Identity.Contracts.Interfaces;

public interface ITelegramMessagingPlatformService
{
    Task SyncPendingUpdatesAsync(CancellationToken ct = default);
    Task<TelegramPhoneVerificationStartResult> StartPhoneVerificationAsync(Guid userId, string phoneNumber, CancellationToken ct = default);
    Task<TelegramPhoneVerificationStatusResult> GetPhoneVerificationStatusAsync(Guid userId, CancellationToken ct = default);
    Task SendLoginCodeAsync(User user, string code, CancellationToken ct = default);
    Task SendNotificationAsync(User user, string message, CancellationToken ct = default);
}

public sealed record TelegramPhoneVerificationStartResult(
    string Message,
    DateTime ExpiresAtUtc,
    string? DeepLinkUrl,
    string BotUsername,
    bool AwaitingContactShare);

public sealed record TelegramPhoneVerificationStatusResult(
    bool IsConfigured,
    bool HasStartedConversation,
    bool AwaitingContactShare,
    bool IsPhoneNumberVerified,
    string Message,
    string? DeepLinkUrl,
    DateTime? ExpiresAtUtc,
    string? TelegramUsername);