using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;

namespace TrustRent.Api.Endpoints;

public static class CoTenantInviteEndpoints
{
    public static void MapCoTenantInviteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();

        // Criar convite (candidato principal -> co-candidato)
        group.MapPost("/applications/{applicationId:guid}/cotenant-invites",
            async (Guid applicationId, [FromBody] CreateCoTenantInviteDto dto, ICoTenantInviteService svc, HttpContext http, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            try
            {
                var ip = http.Connection.RemoteIpAddress?.ToString();
                var invite = await svc.CreateInviteAsync(applicationId, userId, dto, ip);
                return Results.Ok(invite);
            }
            catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException e) { return Results.BadRequest(new { error = e.Message }); }
            catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
        }).RequireRateLimiting("cotenantInvites");

        // Listar convites pendentes para o utilizador atual (dashboard)
        group.MapGet("/me/cotenant-invites", async (ICoTenantInviteService svc, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            return Results.Ok(await svc.GetPendingInvitesForUserAsync(userId));
        });

        // Listar convites de uma candidatura (participantes/senhorio)
        group.MapGet("/applications/{applicationId:guid}/cotenant-invites",
            async (Guid applicationId, ICoTenantInviteService svc, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            try { return Results.Ok(await svc.GetInvitesForApplicationAsync(applicationId, userId)); }
            catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
        });

        // Aceitar
        group.MapPost("/cotenant-invites/{inviteId:guid}/accept",
            async (Guid inviteId, ICoTenantInviteService svc, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            try { return Results.Ok(await svc.AcceptInviteAsync(inviteId, userId)); }
            catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException e) { return Results.BadRequest(new { error = e.Message }); }
        });

        // Recusar
        group.MapPost("/cotenant-invites/{inviteId:guid}/decline",
            async (Guid inviteId, [FromBody] RespondCoTenantInviteDto dto, ICoTenantInviteService svc, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            try { return Results.Ok(await svc.DeclineInviteAsync(inviteId, userId, dto ?? new RespondCoTenantInviteDto())); }
            catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException e) { return Results.BadRequest(new { error = e.Message }); }
        });

        // Cancelar (pelo emissor)
        group.MapPost("/cotenant-invites/{inviteId:guid}/cancel",
            async (Guid inviteId, ICoTenantInviteService svc, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            try { return Results.Ok(await svc.CancelInviteAsync(inviteId, userId)); }
            catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException e) { return Results.BadRequest(new { error = e.Message }); }
        });
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        var s = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(s, out userId);
    }
}
