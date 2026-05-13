using Microsoft.AspNetCore.Mvc;
using TrustRent.Modules.Identity.Contracts.Interfaces;

namespace TrustRent.Api.Endpoints;

public static class AuthEndpoints
{
    public const string AuthCookieName = "trustrent_auth";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/request-code", async ([FromBody] RequestLoginCodeRequest request, ILoginCodeService loginCodeService, HttpContext ctx) =>
        {
            try
            {
                var result = await loginCodeService.SendLoginCodeAsync(
                    request.Email,
                    ctx.Connection.RemoteIpAddress?.ToString(),
                    ctx.Request.Headers.UserAgent.ToString(),
                    ctx.RequestAborted);

                return Results.Ok(new
                {
                    Message = "Se o email for válido, enviámos um código de acesso.",
                    MaskedEmail = result.MaskedEmail,
                    ExpiresAtUtc = result.ExpiresAtUtc
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        }).RequireRateLimiting("auth");

        group.MapPost("/verify-code", async ([FromBody] VerifyLoginCodeRequest request, ILoginCodeService loginCodeService, IAuthService authService, HttpContext ctx, IConfiguration cfg) =>
        {
            try
            {
                var verifiedEmail = await loginCodeService.VerifyLoginCodeAsync(request.Email, request.Code, ctx.RequestAborted);
                var token = await authService.SignInWithEmailAsync(verifiedEmail);
                AppendAuthCookie(ctx, token, cfg);
                return Results.Ok(new { Token = token });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Json(new { Error = "Código inválido ou expirado." }, statusCode: 401);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        }).RequireRateLimiting("auth");

        group.MapPost("/logout", (HttpContext ctx, IConfiguration cfg) =>
        {
            ctx.Response.Cookies.Delete(AuthCookieName, BuildAuthCookieOptions(ctx, null, cfg));
            return Results.Ok(new { Message = "Sessao terminada." });
        });
    }

    private static void AppendAuthCookie(HttpContext ctx, string token, IConfiguration cfg)
    {
        var days = int.TryParse(cfg["JwtSettings:ExpiryDays"], out var d) ? d : 7;

        ctx.Response.Cookies.Append(AuthCookieName, token, BuildAuthCookieOptions(ctx, DateTimeOffset.UtcNow.AddDays(days), cfg));
    }

    private static CookieOptions BuildAuthCookieOptions(HttpContext ctx, DateTimeOffset? expiresAt, IConfiguration cfg)
    {
        var sameSite = ParseSameSite(cfg["AuthCookieSettings:SameSite"], SameSiteMode.Lax);
        var domain = cfg["AuthCookieSettings:Domain"];
        var forceSecure = cfg.GetValue<bool>("AuthCookieSettings:ForceSecure");

        return new CookieOptions
        {
            HttpOnly = true,
            Secure = forceSecure || sameSite == SameSiteMode.None || ctx.Request.IsHttps,
            SameSite = sameSite,
            Path = "/",
            Expires = expiresAt,
            IsEssential = true,
            Domain = string.IsNullOrWhiteSpace(domain) ? null : domain
        };
    }

    private static SameSiteMode ParseSameSite(string? rawValue, SameSiteMode fallback)
    {
        return Enum.TryParse<SameSiteMode>(rawValue, ignoreCase: true, out var sameSite)
            ? sameSite
            : fallback;
    }
}

public record RequestLoginCodeRequest(string Email);
public record VerifyLoginCodeRequest(string Email, string Code);
