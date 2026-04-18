using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Communications.Contracts.Database;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Modules.Communications.Endpoints;

public static class CommunicationsEndpoints
{
    public static void MapCommunicationsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags("Communications");

        // --- CHAT ENDPOINTS ---
        group.MapGet("/applications/{applicationId:guid}/chat", async (Guid applicationId, ClaimsPrincipal user, CommunicationsDbContext db, IApplicationStatusValidator statusValidator) =>
        {
            var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return Results.Unauthorized();
            var userId = Guid.Parse(userIdStr);

            // Verify the user is a participant of this application
            var participants = await statusValidator.GetApplicationParticipantsAsync(applicationId);
            if (participants == null) return Results.NotFound();
            if (participants.Value.TenantId != userId && participants.Value.LandlordId != userId)
                return Results.Forbid();

            var messages = await db.Messages
                .Where(m => m.ContextId == applicationId && m.ContextType == Models.MessageContextType.Application)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new {
                    m.Id,
                    m.SenderId,
                    m.Content,
                    m.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(messages);
        })
        .RequireAuthorization();

        // --- NOTIFICATION ENDPOINTS ---
        
        // Listar notificações do utilizador (Top 50 recentes)
        group.MapGet("/notifications", async (ClaimsPrincipal user, CommunicationsDbContext db) =>
        {
            var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return Results.Unauthorized();
            var userId = Guid.Parse(userIdStr);

            var notifications = await db.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .ToListAsync();

            return Results.Ok(notifications);
        })
        .RequireAuthorization();

        // Contagem de não lidas (Badge)
        group.MapGet("/notifications/unread-count", async (ClaimsPrincipal user, CommunicationsDbContext db) =>
        {
            var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return Results.Unauthorized();
            var userId = Guid.Parse(userIdStr);

            var count = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
            return Results.Ok(new { UnreadCount = count });
        })
        .RequireAuthorization();

        // Marcar uma como lida
        group.MapPut("/notifications/{id:guid}/read", async (Guid id, ClaimsPrincipal user, CommunicationsDbContext db) =>
        {
            var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return Results.Unauthorized();
            var userId = Guid.Parse(userIdStr);

            var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
            if (notification == null) return Results.NotFound();

            notification.IsRead = true;
            await db.SaveChangesAsync();

            return Results.NoContent();
        })
        .RequireAuthorization();

        // Marcar todas como lidas
        group.MapPut("/notifications/mark-all-read", async (ClaimsPrincipal user, CommunicationsDbContext db) =>
        {
            var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return Results.Unauthorized();
            var userId = Guid.Parse(userIdStr);

            var unread = await db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
            foreach (var n in unread) n.IsRead = true;
            
            await db.SaveChangesAsync();

            return Results.NoContent();
        })
        .RequireAuthorization();
    }
}
