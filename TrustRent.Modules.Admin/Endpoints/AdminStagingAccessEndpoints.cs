using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using System.Security.Claims;
using System.Text.Json;
using TrustRent.Modules.Admin.Authorization;
using TrustRent.Modules.Admin.Contracts;
using TrustRent.Modules.Admin.Contracts.DTOs;
using TrustRent.Modules.Admin.Contracts.Interfaces;

namespace TrustRent.Modules.Admin.Endpoints;

public static class AdminStagingAccessEndpoints
{
    private static Guid GetAdminId(HttpContext ctx) =>
        Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? ctx.User.FindFirst("sub")!.Value);

    public static void MapAdminStagingAccessEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/staging-access");

        group.MapGet("/", async (IWebHostEnvironment env, IStagingAccessService stagingAccessService, CancellationToken ct) =>
        {
            if (!env.IsStaging())
            {
                return Results.Ok(new StagingAccessAdminStateDto(false, false, false, []));
            }

            var users = await stagingAccessService.ListUsersAsync(ct);
            var simulationsEnabled = await stagingAccessService.GetSimulationOverrideAsync(ct) ?? true;
            return Results.Ok(new StagingAccessAdminStateDto(true, true, simulationsEnabled, users));
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.SettingsRead));

        group.MapPost("/users", async ([FromBody] CreateStagingAccessUserRequest request, IWebHostEnvironment env, IStagingAccessService stagingAccessService, IAuditLogService audit, HttpContext ctx, CancellationToken ct) =>
        {
            if (!env.IsStaging()) return Results.NotFound();

            try
            {
                var created = await stagingAccessService.CreateUserAsync(request, GetAdminId(ctx), ct);
                await audit.WriteAsync(
                    GetAdminId(ctx),
                    "staging_access.user.create",
                    "StagingAccessUser",
                    created.Username,
                    null,
                    JsonSerializer.Serialize(new { created.Username, created.DisplayName, created.IsActive }),
                    null,
                    ctx.Connection.RemoteIpAddress?.ToString(),
                    ctx.Request.Headers.UserAgent.ToString(),
                    ctx.TraceIdentifier,
                    ct);

                return Results.Ok(created);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.SettingsEdit));

        group.MapPut("/users/{username}", async (string username, [FromBody] UpdateStagingAccessUserRequest request, IWebHostEnvironment env, IStagingAccessService stagingAccessService, IAuditLogService audit, HttpContext ctx, CancellationToken ct) =>
        {
            if (!env.IsStaging()) return Results.NotFound();

            try
            {
                var before = await stagingAccessService.GetUserAsync(username, ct);
                var updated = await stagingAccessService.UpdateUserAsync(username, request, GetAdminId(ctx), ct);
                await audit.WriteAsync(
                    GetAdminId(ctx),
                    "staging_access.user.update",
                    "StagingAccessUser",
                    updated.Username,
                    JsonSerializer.Serialize(before is null ? null : new { before.Username, before.DisplayName, before.IsActive }),
                    JsonSerializer.Serialize(new { updated.Username, updated.DisplayName, updated.IsActive }),
                    null,
                    ctx.Connection.RemoteIpAddress?.ToString(),
                    ctx.Request.Headers.UserAgent.ToString(),
                    ctx.TraceIdentifier,
                    ct);

                return Results.Ok(updated);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.SettingsEdit));

        group.MapPut("/users/{username}/password", async (string username, [FromBody] ResetStagingAccessUserPasswordRequest request, IWebHostEnvironment env, IStagingAccessService stagingAccessService, IAuditLogService audit, HttpContext ctx, CancellationToken ct) =>
        {
            if (!env.IsStaging()) return Results.NotFound();

            try
            {
                var user = await stagingAccessService.GetUserAsync(username, ct);
                await stagingAccessService.ResetPasswordAsync(username, request.Password, GetAdminId(ctx), ct);
                await audit.WriteAsync(
                    GetAdminId(ctx),
                    "staging_access.user.reset_password",
                    "StagingAccessUser",
                    username,
                    JsonSerializer.Serialize(user is null ? null : new { user.Username, user.DisplayName, user.IsActive }),
                    JsonSerializer.Serialize(user is null ? null : new { user.Username, user.DisplayName, user.IsActive, PasswordReset = true }),
                    null,
                    ctx.Connection.RemoteIpAddress?.ToString(),
                    ctx.Request.Headers.UserAgent.ToString(),
                    ctx.TraceIdentifier,
                    ct);

                return Results.Ok(new { message = "Password atualizada." });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.SettingsEdit));

        group.MapDelete("/users/{username}", async (string username, IWebHostEnvironment env, IStagingAccessService stagingAccessService, IAuditLogService audit, HttpContext ctx, CancellationToken ct) =>
        {
            if (!env.IsStaging()) return Results.NotFound();

            try
            {
                var before = await stagingAccessService.GetUserAsync(username, ct);
                await stagingAccessService.DeleteUserAsync(username, GetAdminId(ctx), ct);
                await audit.WriteAsync(
                    GetAdminId(ctx),
                    "staging_access.user.delete",
                    "StagingAccessUser",
                    username,
                    JsonSerializer.Serialize(before is null ? null : new { before.Username, before.DisplayName, before.IsActive }),
                    null,
                    null,
                    ctx.Connection.RemoteIpAddress?.ToString(),
                    ctx.Request.Headers.UserAgent.ToString(),
                    ctx.TraceIdentifier,
                    ct);

                return Results.NoContent();
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.SettingsEdit));

        group.MapPut("/simulations", async ([FromBody] UpdateStagingSimulationsRequest request, IWebHostEnvironment env, IStagingAccessService stagingAccessService, IAuditLogService audit, HttpContext ctx, CancellationToken ct) =>
        {
            if (!env.IsStaging()) return Results.NotFound();

            var before = await stagingAccessService.GetSimulationOverrideAsync(ct);
            var enabled = await stagingAccessService.SetSimulationOverrideAsync(request.Enabled, GetAdminId(ctx), ct);
            await audit.WriteAsync(
                GetAdminId(ctx),
                "staging_access.simulations.update",
                "PlatformSetting",
                "staging.simulations.enabled",
                JsonSerializer.Serialize(new { Enabled = before ?? true, HadExplicitValue = before.HasValue }),
                JsonSerializer.Serialize(new { Enabled = enabled, HadExplicitValue = true }),
                null,
                ctx.Connection.RemoteIpAddress?.ToString(),
                ctx.Request.Headers.UserAgent.ToString(),
                ctx.TraceIdentifier,
                ct);

            return Results.Ok(new { enabled });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.SettingsEdit));
    }
}