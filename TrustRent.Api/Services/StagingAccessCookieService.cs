using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using TrustRent.Modules.Admin.Contracts.DTOs;

namespace TrustRent.Api.Services;

public interface IStagingAccessCookieService
{
    void AppendSession(HttpContext ctx, StagingAccessUserSessionDto user);
    void ClearCookie(HttpContext ctx);
    StagingAccessCookieSession? ReadSession(HttpContext ctx);
}

public sealed record StagingAccessCookieSession(
    string Username,
    string DisplayName,
    DateTimeOffset ExpiresAt
);

public sealed class StagingAccessCookieService : IStagingAccessCookieService
{
    public const string CookieName = "trustrent_staging_access";

    private readonly IDataProtector _protector;
    private readonly IConfiguration _configuration;

    public StagingAccessCookieService(IDataProtectionProvider dataProtectionProvider, IConfiguration configuration)
    {
        _protector = dataProtectionProvider.CreateProtector("TrustRent.StagingAccess.Cookie.v1");
        _configuration = configuration;
    }

    public void AppendSession(HttpContext ctx, StagingAccessUserSessionDto user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(GetDurationMinutes());
        var protectedPayload = _protector.Protect(JsonSerializer.Serialize(new StagingAccessCookieSession(user.Username, user.DisplayName, expiresAt)));
        ctx.Response.Cookies.Append(CookieName, protectedPayload, BuildCookieOptions(ctx, expiresAt));
    }

    public void ClearCookie(HttpContext ctx)
    {
        ctx.Response.Cookies.Delete(CookieName, BuildCookieOptions(ctx, null));
    }

    public StagingAccessCookieSession? ReadSession(HttpContext ctx)
    {
        if (!ctx.Request.Cookies.TryGetValue(CookieName, out var protectedPayload) || string.IsNullOrWhiteSpace(protectedPayload))
        {
            return null;
        }

        try
        {
            var payload = _protector.Unprotect(protectedPayload);
            var session = JsonSerializer.Deserialize<StagingAccessCookieSession>(payload);
            if (session is null || session.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                return null;
            }

            return session;
        }
        catch
        {
            return null;
        }
    }

    private CookieOptions BuildCookieOptions(HttpContext ctx, DateTimeOffset? expiresAt)
    {
        var sameSite = ParseSameSite(_configuration["StagingAccessCookieSettings:SameSite"], SameSiteMode.Lax);
        var domain = _configuration["StagingAccessCookieSettings:Domain"] ?? _configuration["AuthCookieSettings:Domain"];
        var forceSecure = _configuration.GetValue<bool?>("StagingAccessCookieSettings:ForceSecure")
            ?? _configuration.GetValue<bool>("AuthCookieSettings:ForceSecure");

        return new CookieOptions
        {
            HttpOnly = true,
            Secure = forceSecure || sameSite == SameSiteMode.None || ctx.Request.IsHttps,
            SameSite = sameSite,
            Path = "/",
            Expires = expiresAt,
            IsEssential = true,
            Domain = string.IsNullOrWhiteSpace(domain) ? null : domain,
        };
    }

    private int GetDurationMinutes()
    {
        var configured = _configuration.GetValue<int?>("StagingAccessCookieSettings:DurationMinutes") ?? 20_160;
        return Math.Clamp(configured, 5, 20_160);
    }

    private static SameSiteMode ParseSameSite(string? rawValue, SameSiteMode fallback)
    {
        return Enum.TryParse<SameSiteMode>(rawValue, ignoreCase: true, out var sameSite)
            ? sameSite
            : fallback;
    }
}