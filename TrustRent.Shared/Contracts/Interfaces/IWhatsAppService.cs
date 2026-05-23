namespace TrustRent.Shared.Contracts.Interfaces;

public interface IWhatsAppService
{
    Task SendLoginCodeAsync(string phoneNumber, string code, CancellationToken cancellationToken = default);
    Task SendPhoneVerificationCodeAsync(string phoneNumber, string code, CancellationToken cancellationToken = default);
    Task SendNotificationAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
}