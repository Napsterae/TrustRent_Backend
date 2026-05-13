namespace TrustRent.Modules.Identity.Contracts.Interfaces;

public interface ILoginCodeService
{
    Task<LoginCodeDispatchResult> SendLoginCodeAsync(string email, string? sourceIp, string? userAgent, CancellationToken ct = default);
    Task<string> VerifyLoginCodeAsync(string email, string code, CancellationToken ct = default);
}

public sealed record LoginCodeDispatchResult(string MaskedEmail, DateTime ExpiresAtUtc);