namespace TrustRent.Modules.Identity.Contracts.Interfaces;

public interface IPhoneLoginCodeService
{
    Task<PhoneLoginCodeDispatchResult> SendLoginCodeAsync(string phoneNumber, string? sourceIp, string? userAgent, CancellationToken ct = default);
    Task<string> VerifyLoginCodeAsync(string phoneNumber, string code, CancellationToken ct = default);
}

public sealed record PhoneLoginCodeDispatchResult(string MaskedPhoneNumber, string Platform, DateTime ExpiresAtUtc);