using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TrustRent.Modules.Admin.Authorization;
using TrustRent.Modules.Admin.Contracts;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Contracts.Interfaces;
using TrustRent.Modules.Admin.Models;

namespace TrustRent.Modules.Admin.Endpoints;

public static class SupportTicketsEndpoints
{
    private static Guid GetUserId(HttpContext ctx) =>
        Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? ctx.User.FindFirst("sub")!.Value);

    public record CreateTicketRequest(string Subject, string Body, string? Category);
    public record AddMessageRequest(string Body, bool IsInternalNote = false);
    public record AssignRequest(Guid? AdminId);
    public record StateRequest(string State);
    public record PriorityRequest(string Priority);

    public static void MapSupportTicketsEndpoints(this IEndpointRouteBuilder app)
    {
        // ===== Endpoints públicos (utilizador autenticado normal) =====
        var pub = app.MapGroup("/api/support/tickets")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme });

        pub.MapGet("/", async (HttpContext ctx, AdminDbContext db) =>
        {
            var uid = GetUserId(ctx);
            var items = await db.SupportTickets.AsNoTracking()
                .Where(t => t.OpenedByUserId == uid)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new { t.Id, t.Subject, t.Category, t.State, t.Priority, t.CreatedAt, t.UpdatedAt })
                .ToListAsync();
            return Results.Ok(items);
        });

        pub.MapGet("/{id:guid}", async (Guid id, HttpContext ctx, AdminDbContext db) =>
        {
            var uid = GetUserId(ctx);
            var t = await db.SupportTickets.AsNoTracking()
                .Include(x => x.Messages.Where(m => !m.IsInternalNote).OrderBy(m => m.CreatedAt))
                .FirstOrDefaultAsync(x => x.Id == id && x.OpenedByUserId == uid);
            return t is null ? Results.NotFound() : Results.Ok(t);
        });

        pub.MapPost("/", async ([FromBody] CreateTicketRequest req, HttpContext ctx, AdminDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Subject) || string.IsNullOrWhiteSpace(req.Body))
                return Results.BadRequest(new { error = "Assunto e mensagem obrigatórios." });
            var uid = GetUserId(ctx);
            var t = new SupportTicket
            {
                Id = Guid.NewGuid(),
                OpenedByUserId = uid,
                Subject = req.Subject.Trim(),
                Category = string.IsNullOrWhiteSpace(req.Category) ? "general" : req.Category.Trim(),
                State = SupportTicketState.Open,
                Priority = SupportTicketPriority.Normal,
                CreatedAt = DateTime.UtcNow
            };
            db.SupportTickets.Add(t);
            db.SupportTicketMessages.Add(new SupportTicketMessage
            {
                Id = Guid.NewGuid(),
                TicketId = t.Id,
                AuthorId = uid,
                IsAdmin = false,
                IsInternalNote = false,
                Body = req.Body.Trim(),
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            return Results.Created($"/api/support/tickets/{t.Id}", new { t.Id });
        });

        pub.MapPost("/{id:guid}/messages", async (Guid id, [FromBody] AddMessageRequest req, HttpContext ctx, AdminDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Body)) return Results.BadRequest(new { error = "Mensagem vazia." });
            var uid = GetUserId(ctx);
            var t = await db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id && x.OpenedByUserId == uid);
            if (t is null) return Results.NotFound();
            if (t.State == SupportTicketState.Closed) return Results.BadRequest(new { error = "Ticket fechado." });
            db.SupportTicketMessages.Add(new SupportTicketMessage
            {
                Id = Guid.NewGuid(),
                TicketId = t.Id,
                AuthorId = uid,
                IsAdmin = false,
                IsInternalNote = false,
                Body = req.Body.Trim(),
                CreatedAt = DateTime.UtcNow
            });
            t.State = SupportTicketState.PendingAdmin;
            t.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ===== Endpoints admin =====
        var adm = app.MapGroup("/api/admin/tickets/support");

        adm.MapGet("/", async ([FromQuery] string? state, [FromQuery] Guid? assignedAdminId, [FromQuery] int page, [FromQuery] int pageSize, AdminDbContext db) =>
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 25 : (pageSize > 200 ? 200 : pageSize);
            var q = db.SupportTickets.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(state) && Enum.TryParse<SupportTicketState>(state, true, out var s))
                q = q.Where(t => t.State == s);
            if (assignedAdminId.HasValue) q = q.Where(t => t.AssignedAdminId == assignedAdminId.Value);
            var total = await q.CountAsync();
            var items = await q.OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(t => new { t.Id, t.OpenedByUserId, t.Subject, t.Category, t.State, t.Priority, t.AssignedAdminId, t.CreatedAt, t.UpdatedAt })
                .ToListAsync();
            return Results.Ok(new { items, page, pageSize, totalCount = total });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.TicketsSupportRead));

        adm.MapGet("/{id:guid}", async (Guid id, AdminDbContext db) =>
        {
            var t = await db.SupportTickets.AsNoTracking()
                .Include(x => x.Messages.OrderBy(m => m.CreatedAt))
                .FirstOrDefaultAsync(x => x.Id == id);
            return t is null ? Results.NotFound() : Results.Ok(t);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.TicketsSupportRead));

        adm.MapPost("/{id:guid}/messages", async (Guid id, [FromBody] AddMessageRequest req, HttpContext ctx, AdminDbContext db, IAuditLogService audit) =>
        {
            if (string.IsNullOrWhiteSpace(req.Body)) return Results.BadRequest(new { error = "Mensagem vazia." });
            var t = await db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id);
            if (t is null) return Results.NotFound();
            var adminId = GetUserId(ctx);
            db.SupportTicketMessages.Add(new SupportTicketMessage
            {
                Id = Guid.NewGuid(),
                TicketId = t.Id,
                AuthorId = adminId,
                IsAdmin = true,
                IsInternalNote = req.IsInternalNote,
                Body = req.Body.Trim(),
                CreatedAt = DateTime.UtcNow
            });
            if (!req.IsInternalNote) t.State = SupportTicketState.PendingUser;
            t.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await audit.WriteAsync(adminId, req.IsInternalNote ? "ticket.support.note" : "ticket.support.respond", "SupportTicket", id.ToString(), null, null, null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.TicketsSupportRespond));

        adm.MapPost("/{id:guid}/assign", async (Guid id, [FromBody] AssignRequest req, HttpContext ctx, AdminDbContext db, IAuditLogService audit) =>
        {
            var t = await db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id);
            if (t is null) return Results.NotFound();
            var before = JsonSerializer.Serialize(new { t.AssignedAdminId });
            t.AssignedAdminId = req.AdminId;
            t.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetUserId(ctx), "ticket.support.assign", "SupportTicket", id.ToString(), before, JsonSerializer.Serialize(new { t.AssignedAdminId }), null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.TicketsSupportAssign));

        adm.MapPost("/{id:guid}/state", async (Guid id, [FromBody] StateRequest req, HttpContext ctx, AdminDbContext db, IAuditLogService audit) =>
        {
            if (!Enum.TryParse<SupportTicketState>(req.State, true, out var s))
                return Results.BadRequest(new { error = "Estado inválido." });
            var t = await db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id);
            if (t is null) return Results.NotFound();
            var before = JsonSerializer.Serialize(new { t.State });
            t.State = s;
            t.UpdatedAt = DateTime.UtcNow;
            if (s == SupportTicketState.Closed) t.ClosedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            var perm = s == SupportTicketState.Closed ? "ticket.support.close" : "ticket.support.update_state";
            await audit.WriteAsync(GetUserId(ctx), perm, "SupportTicket", id.ToString(), before, JsonSerializer.Serialize(new { t.State }), null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.TicketsSupportClose));

        adm.MapPost("/{id:guid}/priority", async (Guid id, [FromBody] PriorityRequest req, HttpContext ctx, AdminDbContext db, IAuditLogService audit) =>
        {
            if (!Enum.TryParse<SupportTicketPriority>(req.Priority, true, out var p))
                return Results.BadRequest(new { error = "Prioridade inválida." });
            var t = await db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id);
            if (t is null) return Results.NotFound();
            var before = JsonSerializer.Serialize(new { t.Priority });
            t.Priority = p;
            t.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetUserId(ctx), "ticket.support.priority", "SupportTicket", id.ToString(), before, JsonSerializer.Serialize(new { t.Priority }), null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.TicketsSupportRespond));
    }
}
