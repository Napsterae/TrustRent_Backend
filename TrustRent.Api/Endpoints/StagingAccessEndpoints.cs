using Microsoft.AspNetCore.Mvc;
using TrustRent.Api.Services;
using TrustRent.Modules.Admin.Contracts.DTOs;
using TrustRent.Modules.Admin.Contracts.Interfaces;

namespace TrustRent.Api.Endpoints;

public static class StagingAccessEndpoints
{
    public record StagingAccessLoginRequest(string Username, string Password);

    public record StagingAccessSessionResponse(
        bool AccessEnabled,
        bool HasAccess,
        bool SimulationsEnabled,
        bool HasConfiguredUsers,
        StagingAccessUserSessionDto? User
    );

    public static void MapStagingAccessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/staging-access");

        group.MapGet("/session", async (HttpContext ctx, IWebHostEnvironment env, IStagingAccessService stagingAccessService, IStagingAccessCookieService cookieService) =>
        {
            var simulationsEnabled = await ResolveSimulationsEnabledAsync(env, stagingAccessService, ctx.RequestAborted);
            if (!env.IsStaging())
            {
                return Results.Ok(new StagingAccessSessionResponse(false, true, simulationsEnabled, false, null));
            }

            var hasConfiguredUsers = await stagingAccessService.CountActiveUsersAsync(ctx.RequestAborted) > 0;
            var session = cookieService.ReadSession(ctx);
            if (session is null)
            {
                cookieService.ClearCookie(ctx);
                return Results.Ok(new StagingAccessSessionResponse(true, false, simulationsEnabled, hasConfiguredUsers, null));
            }

            var user = await stagingAccessService.GetActiveUserAsync(session.Username, ctx.RequestAborted);
            if (user is null)
            {
                cookieService.ClearCookie(ctx);
                return Results.Ok(new StagingAccessSessionResponse(true, false, simulationsEnabled, hasConfiguredUsers, null));
            }

            return Results.Ok(new StagingAccessSessionResponse(true, true, simulationsEnabled, hasConfiguredUsers, user));
        });

        group.MapPost("/login", async ([FromBody] StagingAccessLoginRequest request, HttpContext ctx, IWebHostEnvironment env, IStagingAccessService stagingAccessService, IStagingAccessCookieService cookieService) =>
        {
            if (!env.IsStaging()) return Results.NotFound();

            var user = await stagingAccessService.ValidateCredentialsAsync(request.Username, request.Password, ctx.RequestAborted);
            if (user is null)
            {
                return Results.Json(new { error = "Credenciais inválidas." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            cookieService.AppendSession(ctx, user);

            return Results.Ok(new StagingAccessSessionResponse(
                true,
                true,
                await ResolveSimulationsEnabledAsync(env, stagingAccessService, ctx.RequestAborted),
                true,
                user));
        }).RequireRateLimiting("auth");

        group.MapPost("/logout", (HttpContext ctx, IStagingAccessCookieService cookieService) =>
        {
            cookieService.ClearCookie(ctx);
            return Results.Ok(new { message = "Acesso staging terminado." });
        });
    }

    private static async Task<bool> ResolveSimulationsEnabledAsync(IWebHostEnvironment env, IStagingAccessService stagingAccessService, CancellationToken ct)
    {
        if (env.IsDevelopment()) return true;
        if (!env.IsStaging()) return false;
        return await stagingAccessService.GetSimulationOverrideAsync(ct) ?? true;
    }
}