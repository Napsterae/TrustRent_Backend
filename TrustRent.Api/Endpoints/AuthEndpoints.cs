using Microsoft.AspNetCore.Mvc;
using TrustRent.Modules.Identity.Contracts.Interfaces;

namespace TrustRent.Api.Endpoints;

public static class AuthEndpoints
{
    public const string AuthCookieName = "trustrent_auth";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", async ([FromBody] RegisterRequest request, IAuthService authService, HttpContext ctx, IConfiguration cfg) =>
        {
            try
            {
                var token = await authService.RegisterAsync(request.Name, request.Email, request.Password);
                AppendAuthCookie(ctx, token, cfg);
                return Results.Ok(new { Token = token });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        }).RequireRateLimiting("auth");

        group.MapPost("/login", async ([FromBody] LoginRequest request, IAuthService authService, HttpContext ctx, IConfiguration cfg) =>
        {
            try
            {
                var token = await authService.LoginAsync(request.Email, request.Password);
                AppendAuthCookie(ctx, token, cfg);
                return Results.Ok(new { Token = token });
            }
            catch
            {
                return Results.Json(new { Error = "Credenciais invalidas." }, statusCode: 401);
            }
        }).RequireRateLimiting("auth");

        group.MapPost("/logout", (HttpContext ctx) =>
        {
            ctx.Response.Cookies.Delete(AuthCookieName, new CookieOptions
            {
                Path = "/",
                Secure = ctx.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                HttpOnly = true
            });
            return Results.Ok(new { Message = "Sessao terminada." });
        });
    }

    private static void AppendAuthCookie(HttpContext ctx, string token, IConfiguration cfg)
    {
        var days = int.TryParse(cfg["JwtSettings:ExpiryDays"], out var d) ? d : 7;

        ctx.Response.Cookies.Append(AuthCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(days),
            IsEssential = true
        });
    }
}

public record RegisterRequest(string Name, string Email, string Password);
public record LoginRequest(string Email, string Password);
