using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TrustRent.Modules.Admin.Authorization;
using TrustRent.Modules.Admin.Contracts;
using TrustRent.Modules.Admin.Contracts.Interfaces;
using TrustRent.Modules.Communications.Contracts.Database;
using TrustRent.Modules.Communications.Models;

namespace TrustRent.Modules.Admin.Endpoints;

public static class AdminCommunicationsEndpoints
{
    private static Guid GetAdminId(HttpContext ctx) =>
        Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? ctx.User.FindFirst("sub")!.Value);

    public record BroadcastRequest(string Title, string Body, string Audience, string Channel, DateTime? ScheduledAt);
    public record TemplateRequest(string Key, string Subject, string BodyHtml, string? BodyText, string Locale, string? Description);
    public record BannerRequest(string Title, string Body, string Severity, string Audience, bool IsActive, DateTime? StartsAt, DateTime? EndsAt);

    public static void MapAdminCommunicationsEndpoints(this IEndpointRouteBuilder app)
    {
        // ===== Broadcasts =====
        var bc = app.MapGroup("/api/admin/broadcasts");

        bc.MapGet("/", async (CommunicationsDbContext db) =>
        {
            var items = await db.Broadcasts.AsNoTracking().OrderByDescending(b => b.CreatedAt).ToListAsync();
            return Results.Ok(items);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsBroadcast));

        bc.MapPost("/", async ([FromBody] BroadcastRequest req, HttpContext ctx, CommunicationsDbContext db, IAuditLogService audit) =>
        {
            if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.Body))
                return Results.BadRequest(new { error = "Título e corpo obrigatórios." });
            var adminId = GetAdminId(ctx);
            var b = new Broadcast
            {
                Id = Guid.NewGuid(),
                Title = req.Title.Trim(),
                Body = req.Body,
                Audience = req.Audience ?? "all",
                Channel = req.Channel ?? "in_app",
                ScheduledAt = req.ScheduledAt,
                Status = req.ScheduledAt.HasValue ? "scheduled" : "draft",
                CreatedByAdminId = adminId,
                CreatedAt = DateTime.UtcNow
            };
            db.Broadcasts.Add(b);
            await db.SaveChangesAsync();
            await audit.WriteAsync(adminId, "broadcast.create", "Broadcast", b.Id.ToString(), null, JsonSerializer.Serialize(b), null, ctx);
            return Results.Created($"/api/admin/broadcasts/{b.Id}", b);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsBroadcast));

        bc.MapPost("/{id:guid}/send", async (Guid id, HttpContext ctx, CommunicationsDbContext db, IAuditLogService audit) =>
        {
            var b = await db.Broadcasts.FirstOrDefaultAsync(x => x.Id == id);
            if (b is null) return Results.NotFound();
            if (b.Status == "sent") return Results.BadRequest(new { error = "Já enviado." });
            b.Status = "sent";
            b.SentAt = DateTime.UtcNow;
            b.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "broadcast.send", "Broadcast", id.ToString(), null, JsonSerializer.Serialize(new { b.Status, b.SentAt }), null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsBroadcast));

        bc.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, CommunicationsDbContext db, IAuditLogService audit) =>
        {
            var b = await db.Broadcasts.FirstOrDefaultAsync(x => x.Id == id);
            if (b is null) return Results.NotFound();
            if (b.Status == "sent") return Results.BadRequest(new { error = "Não é possível eliminar broadcasts enviados." });
            db.Broadcasts.Remove(b);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "broadcast.delete", "Broadcast", id.ToString(), JsonSerializer.Serialize(b), null, null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsBroadcast));

        // ===== Templates =====
        var tp = app.MapGroup("/api/admin/templates");

        tp.MapGet("/", async (CommunicationsDbContext db) =>
        {
            var items = await db.EmailTemplates.AsNoTracking().OrderBy(t => t.Key).ThenBy(t => t.Locale).ToListAsync();
            return Results.Ok(items);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsTemplatesEdit));

        tp.MapPut("/", async ([FromBody] TemplateRequest req, HttpContext ctx, CommunicationsDbContext db, IAuditLogService audit) =>
        {
            if (string.IsNullOrWhiteSpace(req.Key)) return Results.BadRequest(new { error = "Key obrigatória." });
            var locale = string.IsNullOrWhiteSpace(req.Locale) ? "pt-PT" : req.Locale;
            var existing = await db.EmailTemplates.FirstOrDefaultAsync(t => t.Key == req.Key && t.Locale == locale);
            var before = existing is null ? null : JsonSerializer.Serialize(existing);
            if (existing is null)
            {
                existing = new EmailTemplate { Id = Guid.NewGuid(), Key = req.Key, Locale = locale };
                db.EmailTemplates.Add(existing);
            }
            existing.Subject = req.Subject;
            existing.BodyHtml = req.BodyHtml;
            existing.BodyText = req.BodyText;
            existing.Description = req.Description;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedByAdminId = GetAdminId(ctx);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "template.upsert", "EmailTemplate", existing.Id.ToString(), before, JsonSerializer.Serialize(existing), null, ctx);
            return Results.Ok(existing);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsTemplatesEdit));

        tp.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, CommunicationsDbContext db, IAuditLogService audit) =>
        {
            var t = await db.EmailTemplates.FirstOrDefaultAsync(x => x.Id == id);
            if (t is null) return Results.NotFound();
            db.EmailTemplates.Remove(t);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "template.delete", "EmailTemplate", id.ToString(), JsonSerializer.Serialize(t), null, null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsTemplatesEdit));

        // ===== Banners =====
        var bn = app.MapGroup("/api/admin/banners");

        bn.MapGet("/", async (CommunicationsDbContext db) =>
        {
            var items = await db.Banners.AsNoTracking().OrderByDescending(b => b.CreatedAt).ToListAsync();
            return Results.Ok(items);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsBannersEdit));

        bn.MapPost("/", async ([FromBody] BannerRequest req, HttpContext ctx, CommunicationsDbContext db, IAuditLogService audit) =>
        {
            if (string.IsNullOrWhiteSpace(req.Title)) return Results.BadRequest(new { error = "Título obrigatório." });
            var b = new Banner
            {
                Id = Guid.NewGuid(),
                Title = req.Title,
                Body = req.Body ?? string.Empty,
                Severity = req.Severity ?? "info",
                Audience = req.Audience ?? "all",
                IsActive = req.IsActive,
                StartsAt = req.StartsAt,
                EndsAt = req.EndsAt,
                CreatedByAdminId = GetAdminId(ctx),
                CreatedAt = DateTime.UtcNow
            };
            db.Banners.Add(b);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "banner.create", "Banner", b.Id.ToString(), null, JsonSerializer.Serialize(b), null, ctx);
            return Results.Created($"/api/admin/banners/{b.Id}", b);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsBannersEdit));

        bn.MapPut("/{id:guid}", async (Guid id, [FromBody] BannerRequest req, HttpContext ctx, CommunicationsDbContext db, IAuditLogService audit) =>
        {
            var b = await db.Banners.FirstOrDefaultAsync(x => x.Id == id);
            if (b is null) return Results.NotFound();
            var before = JsonSerializer.Serialize(b);
            b.Title = req.Title;
            b.Body = req.Body ?? string.Empty;
            b.Severity = req.Severity ?? "info";
            b.Audience = req.Audience ?? "all";
            b.IsActive = req.IsActive;
            b.StartsAt = req.StartsAt;
            b.EndsAt = req.EndsAt;
            b.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "banner.update", "Banner", id.ToString(), before, JsonSerializer.Serialize(b), null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsBannersEdit));

        bn.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, CommunicationsDbContext db, IAuditLogService audit) =>
        {
            var b = await db.Banners.FirstOrDefaultAsync(x => x.Id == id);
            if (b is null) return Results.NotFound();
            db.Banners.Remove(b);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "banner.delete", "Banner", id.ToString(), JsonSerializer.Serialize(b), null, null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.CommunicationsBannersEdit));
    }
}
