namespace TrustRent.Modules.Identity.Contracts.Interfaces;

public interface IWhatsAppCodeService
{
    Task<WhatsAppCodeDispatchResult> SendLoginCodeAsync(string phoneNumber, string? sourceIp, string? userAgent, CancellationToken ct = default);
    Task<string> VerifyLoginCodeAsync(string phoneNumber, string code, CancellationToken ct = default);
    Task<WhatsAppCodeDispatchResult> SendPhoneVerificationCodeAsync(Guid userId, string phoneNumber, string? sourceIp, string? userAgent, CancellationToken ct = default);
    Task VerifyPhoneVerificationCodeAsync(Guid userId, string phoneNumber, string code, CancellationToken ct = default);
}

public sealed record WhatsAppCodeDispatchResult(string MaskedPhoneNumber, DateTime ExpiresAtUtc);