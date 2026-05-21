using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Communications.Contracts.Database;
using TrustRent.Modules.Communications.Models;
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
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();

            // Verify the user is a participant of this application
            var participants = await statusValidator.GetApplicationParticipantsAsync(applicationId);
            if (participants == null) return Results.NotFound();
            if (!IsApplicationChatParticipant(participants.Value, userId))
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
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();

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
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();

            var count = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
            return Results.Ok(new { UnreadCount = count });
        })
        .RequireAuthorization();

        // Marcar uma como lida
        group.MapPut("/notifications/{id:guid}/read", async (Guid id, ClaimsPrincipal user, CommunicationsDbContext db) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();

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
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();

            var unread = await db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
            foreach (var n in unread) n.IsRead = true;
            
            await db.SaveChangesAsync();

            return Results.NoContent();
        })
        .RequireAuthorization();

        group.MapPost("/notifications/devices/register", async (RegisterPushDeviceRequest request, ClaimsPrincipal user, CommunicationsDbContext db) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.ExpoPushToken)) return Results.BadRequest(new { Error = "ExpoPushToken é obrigatório." });

            var token = request.ExpoPushToken.Trim();
            var platform = string.IsNullOrWhiteSpace(request.Platform) ? "unknown" : request.Platform.Trim().ToLowerInvariant();

            var device = await db.PushDevices.FirstOrDefaultAsync(entry => entry.ExpoPushToken == token);
            if (device == null)
            {
                db.PushDevices.Add(new Models.PushDevice
                {
                    UserId = userId,
                    ExpoPushToken = token,
                    Platform = platform,
                    DeviceName = request.DeviceName?.Trim(),
                    AppVersion = request.AppVersion?.Trim(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    LastSeenAt = DateTime.UtcNow
                });
            }
            else
            {
                device.UserId = userId;
                device.Platform = platform;
                device.DeviceName = request.DeviceName?.Trim();
                device.AppVersion = request.AppVersion?.Trim();
                device.IsActive = true;
                device.LastSeenAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            return Results.Ok(new { Registered = true });
        })
        .RequireAuthorization();

        group.MapPost("/notifications/devices/unregister", async (UnregisterPushDeviceRequest request, ClaimsPrincipal user, CommunicationsDbContext db) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.ExpoPushToken)) return Results.BadRequest(new { Error = "ExpoPushToken é obrigatório." });

            var token = request.ExpoPushToken.Trim();
            var devices = await db.PushDevices
                .Where(device => device.UserId == userId && device.ExpoPushToken == token && device.IsActive)
                .ToListAsync();

            foreach (var device in devices)
            {
                device.IsActive = false;
                device.LastSeenAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .RequireAuthorization();

        group.MapGet("/legal-documents/{documentType}", async (string documentType, string? version, CommunicationsDbContext db, ICommunicationContentService content) =>
        {
            var normalizedType = NormalizeLegalDocumentType(documentType);
            if (normalizedType is null)
                return Results.NotFound();

            var document = await content.GetLegalDocumentAsync(normalizedType, version);
            if (document is null)
                return Results.NotFound();

            var versions = await db.LegalDocumentVersions
                .AsNoTracking()
                .Where(entry => entry.DocumentType == normalizedType)
                .OrderByDescending(entry => entry.PublishedAt)
                .Select(entry => new
                {
                    entry.Version,
                    entry.PublishedAt,
                    entry.IsCurrent,
                    entry.Title,
                    entry.Summary
                })
                .ToListAsync();

            if (versions.Count == 0)
            {
                versions =
                [
                    new
                    {
                        Version = document.Version,
                        PublishedAt = document.PublishedAt,
                        IsCurrent = true,
                        Title = document.Title,
                        Summary = document.Summary
                    }
                ];
            }

            return Results.Ok(new
            {
                document.DocumentType,
                document.Title,
                document.Version,
                document.Summary,
                document.ChangeSummary,
                document.BodyHtml,
                document.BodyText,
                document.IsCurrent,
                document.PublishedAt,
                Versions = versions
            });
        });
    }

    private static string? NormalizeLegalDocumentType(string? documentType)
    {
        var normalized = (documentType ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            LegalDocumentTypes.PrivacyPolicy => LegalDocumentTypes.PrivacyPolicy,
            LegalDocumentTypes.TermsOfUse => LegalDocumentTypes.TermsOfUse,
            "privacy-policy" => LegalDocumentTypes.PrivacyPolicy,
            "terms-of-use" => LegalDocumentTypes.TermsOfUse,
            _ => null
        };
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        return Guid.TryParse(value, out userId);
    }

    private static bool IsApplicationChatParticipant((Guid TenantId, Guid LandlordId, Guid? CoTenantUserId) participants, Guid userId)
        => participants.TenantId == userId
           || participants.LandlordId == userId
           || participants.CoTenantUserId == userId;

    private sealed record RegisterPushDeviceRequest(
        string ExpoPushToken,
        string Platform,
        string? DeviceName,
        string? AppVersion);

    private sealed record UnregisterPushDeviceRequest(string ExpoPushToken);
}
