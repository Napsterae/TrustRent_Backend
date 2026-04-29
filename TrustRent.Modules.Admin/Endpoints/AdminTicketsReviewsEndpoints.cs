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
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Models;

namespace TrustRent.Modules.Admin.Endpoints;

public static class AdminTicketsReviewsEndpoints
{
    private static Guid GetAdminId(HttpContext ctx) =>
        Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? ctx.User.FindFirst("sub")!.Value);

    public record TicketStatusUpdate(string Status);
    public record ReviewModerationRequest(string Status, string? Reason);

    public static void MapAdminTicketsReviewsEndpoints(this IEndpointRouteBuilder app)
    {
        // ===== Tickets de manutenção =====
        var tk = app.MapGroup("/api/admin/tickets/maintenance");

        tk.MapGet("/", async ([FromQuery] string? status, [FromQuery] int page, [FromQuery] int pageSize, LeasingDbContext db) =>
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 25 : (pageSize > 200 ? 200 : pageSize);
            var q = db.Tickets.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TicketStatus>(status, true, out var s))
                q = q.Where(t => t.Status == s);
            var total = await q.CountAsync();
            var items = await q.OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(t => new { t.Id, t.LeaseId, t.TenantId, t.LandlordId, t.Title, t.Priority, t.Status, t.CreatedAt, t.ResolvedAt })
                .ToListAsync();
            return Results.Ok(new { items, page, pageSize, totalCount = total });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.TicketsMaintenanceRead));

        tk.MapPost("/{id:guid}/status", async (Guid id, [FromBody] TicketStatusUpdate req, LeasingDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            if (!Enum.TryParse<TicketStatus>(req.Status, true, out var s))
                return Results.BadRequest(new { error = "Status inválido." });
            var t = await db.Tickets.FirstOrDefaultAsync(x => x.Id == id);
            if (t is null) return Results.NotFound();
            var before = JsonSerializer.Serialize(new { t.Status });
            t.Status = s;
            if (s == TicketStatus.Resolved || s == TicketStatus.Closed) t.ResolvedAt = DateTime.UtcNow;
            t.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "ticket.maintenance.update_status", "Ticket", id.ToString(), before, JsonSerializer.Serialize(new { t.Status }), null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.TicketsMaintenanceIntervene));

        // ===== Reviews =====
        var rv = app.MapGroup("/api/admin/reviews");

        rv.MapGet("/", async ([FromQuery] string? status, [FromQuery] int page, [FromQuery] int pageSize, LeasingDbContext db) =>
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 25 : (pageSize > 200 ? 200 : pageSize);
            var q = db.Reviews.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReviewStatus>(status, true, out var s))
                q = q.Where(r => r.Status == s);
            var total = await q.CountAsync();
            var items = await q.OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(r => new { r.Id, r.ReviewerId, r.ReviewedUserId, r.LeaseId, r.Rating, r.Comment, r.Type, r.Status, r.CreatedAt, r.PublishedAt })
                .ToListAsync();
            return Results.Ok(new { items, page, pageSize, totalCount = total });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReviewsRead));

        rv.MapPost("/{id:guid}/moderate", async (Guid id, [FromBody] ReviewModerationRequest req, LeasingDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            if (!Enum.TryParse<ReviewStatus>(req.Status, true, out var s))
                return Results.BadRequest(new { error = "Status inválido." });
            var r = await db.Reviews.FirstOrDefaultAsync(x => x.Id == id);
            if (r is null) return Results.NotFound();
            var before = JsonSerializer.Serialize(new { r.Status });
            r.Status = s;
            if (s == ReviewStatus.Published) r.PublishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "review.moderate", "Review", id.ToString(), before, JsonSerializer.Serialize(new { r.Status }), req.Reason, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReviewsModerate));

        rv.MapDelete("/{id:guid}", async (Guid id, LeasingDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            var r = await db.Reviews.FirstOrDefaultAsync(x => x.Id == id);
            if (r is null) return Results.NotFound();
            var before = JsonSerializer.Serialize(r);
            db.Reviews.Remove(r);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "review.delete", "Review", id.ToString(), before, null, null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReviewsDelete));
    }
}
