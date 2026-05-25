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

    private static Guid? TryGetUserId(HttpContext ctx)
    {
        var raw = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? ctx.User.FindFirst("sub")?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public record CreateTicketRequest(string Subject, string Body, string? Category);
    public record CreateReportRequest(
        string Type,
        string Subject,
        string Body,
        string? Severity,
        string? PagePath,
        string? PageUrl,
        string? SourceChannel,
        string? Browser,
        string? OperatingSystem,
        string? DeviceType,
        bool DiagnosticsConsent,
        JsonElement? Template,
        JsonElement? SessionContext,
        JsonElement? EnvironmentContext,
        JsonElement? UserContext,
        JsonElement? AdditionalContext);
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
                .Where(t => t.Kind == SupportTicketKind.Support && t.OpenedByUserId == uid)
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
                .FirstOrDefaultAsync(x => x.Id == id && x.Kind == SupportTicketKind.Support && x.OpenedByUserId == uid);
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
                Kind = SupportTicketKind.Support,
                State = SupportTicketState.Open,
                Priority = SupportTicketPriority.Normal,
                SourceChannel = "support-center",
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
            var t = await db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id && x.Kind == SupportTicketKind.Support && x.OpenedByUserId == uid);
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

        var reports = app.MapGroup("/api/reports");

        reports.MapPost("/", async ([FromBody] CreateReportRequest req, HttpContext ctx, AdminDbContext db) =>
        {
            if (!TryParseReportKind(req.Type, out var kind) || kind == SupportTicketKind.Support)
                return Results.BadRequest(new { error = "Tipo de report inválido." });

            if (string.IsNullOrWhiteSpace(req.Subject) || string.IsNullOrWhiteSpace(req.Body))
                return Results.BadRequest(new { error = "Assunto e descrição obrigatórios." });

            if (kind == SupportTicketKind.ErrorReport && !req.DiagnosticsConsent)
                return Results.BadRequest(new { error = "É obrigatório consentimento para partilha dos dados técnicos no report de erro." });

            var openedByUserId = TryGetUserId(ctx);
            var createdAt = DateTime.UtcNow;
            var ticket = new SupportTicket
            {
                Id = Guid.NewGuid(),
                OpenedByUserId = openedByUserId,
                Subject = req.Subject.Trim(),
                Category = kind == SupportTicketKind.ErrorReport ? "report-error" : "feedback",
                Kind = kind,
                State = SupportTicketState.Open,
                Priority = ResolvePriority(req.Severity, kind),
                SourceChannel = TrimOrDefault(req.SourceChannel, "public-footer"),
                PagePath = TrimOrNull(req.PagePath),
                PageUrl = TrimOrNull(req.PageUrl),
                ClientBrowser = TrimOrNull(req.Browser),
                ClientOs = TrimOrNull(req.OperatingSystem),
                ClientDevice = TrimOrNull(req.DeviceType),
                DiagnosticsConsent = kind == SupportTicketKind.ErrorReport && req.DiagnosticsConsent,
                MetadataJson = SerializeReportMetadata(req, ctx, openedByUserId, createdAt),
                CreatedAt = createdAt
            };

            db.SupportTickets.Add(ticket);
            db.SupportTicketMessages.Add(new SupportTicketMessage
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                AuthorId = openedByUserId,
                IsAdmin = false,
                IsInternalNote = false,
                Body = req.Body.Trim(),
                CreatedAt = createdAt
            });

            await db.SaveChangesAsync();
            return Results.Created($"/api/admin/reports/{ticket.Id}", new { ticket.Id });
        });

        // ===== Endpoints admin =====
        var adm = app.MapGroup("/api/admin/tickets/support");

        adm.MapGet("/", async ([FromQuery] string? state, [FromQuery] Guid? assignedAdminId, [FromQuery] int page, [FromQuery] int pageSize, AdminDbContext db) =>
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 25 : (pageSize > 200 ? 200 : pageSize);
            var q = db.SupportTickets.AsNoTracking()
                .Where(t => t.Kind == SupportTicketKind.Support)
                .AsQueryable();
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
                .FirstOrDefaultAsync(x => x.Id == id && x.Kind == SupportTicketKind.Support);
            return t is null ? Results.NotFound() : Results.Ok(t);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.TicketsSupportRead));

        adm.MapPost("/{id:guid}/messages", async (Guid id, [FromBody] AddMessageRequest req, HttpContext ctx, AdminDbContext db, IAuditLogService audit) =>
        {
            if (string.IsNullOrWhiteSpace(req.Body)) return Results.BadRequest(new { error = "Mensagem vazia." });
            var t = await db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id && x.Kind == SupportTicketKind.Support);
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
            var t = await db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id && x.Kind == SupportTicketKind.Support);
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
            var t = await db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id && x.Kind == SupportTicketKind.Support);
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
            var t = await db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id && x.Kind == SupportTicketKind.Support);
            if (t is null) return Results.NotFound();
            var before = JsonSerializer.Serialize(new { t.Priority });
            t.Priority = p;
            t.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetUserId(ctx), "ticket.support.priority", "SupportTicket", id.ToString(), before, JsonSerializer.Serialize(new { t.Priority }), null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.TicketsSupportRespond));

        var admReports = app.MapGroup("/api/admin/reports");

        admReports.MapGet("/", async ([FromQuery] string? type, [FromQuery] string? state, [FromQuery] string? search, [FromQuery] int page, [FromQuery] int pageSize, AdminDbContext db) =>
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 25 : (pageSize > 200 ? 200 : pageSize);

            var q = db.SupportTickets.AsNoTracking()
                .Where(t => t.Kind != SupportTicketKind.Support)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(type) && TryParseReportKind(type, out var reportKind) && reportKind != SupportTicketKind.Support)
                q = q.Where(t => t.Kind == reportKind);

            if (!string.IsNullOrWhiteSpace(state) && Enum.TryParse<SupportTicketState>(state, true, out var parsedState))
                q = q.Where(t => t.State == parsedState);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                q = q.Where(t =>
                    t.Subject.ToLower().Contains(term) ||
                    (t.PagePath ?? string.Empty).ToLower().Contains(term) ||
                    (t.ClientBrowser ?? string.Empty).ToLower().Contains(term) ||
                    (t.ClientOs ?? string.Empty).ToLower().Contains(term));
            }

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new
                {
                    t.Id,
                    t.Kind,
                    t.OpenedByUserId,
                    t.Subject,
                    t.Category,
                    t.State,
                    t.Priority,
                    t.AssignedAdminId,
                    t.SourceChannel,
                    t.PagePath,
                    t.PageUrl,
                    t.ClientBrowser,
                    t.ClientOs,
                    t.ClientDevice,
                    t.DiagnosticsConsent,
                    t.CreatedAt,
                    t.UpdatedAt
                })
                .ToListAsync();

            return Results.Ok(new { items, page, pageSize, totalCount = total });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReportsRead));

        admReports.MapGet("/{id:guid}", async (Guid id, AdminDbContext db) =>
        {
            var t = await db.SupportTickets.AsNoTracking()
                .Include(x => x.Messages.OrderBy(m => m.CreatedAt))
                .FirstOrDefaultAsync(x => x.Id == id && x.Kind != SupportTicketKind.Support);
            return t is null ? Results.NotFound() : Results.Ok(t);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReportsRead));

        admReports.MapPost("/{id:guid}/notes", async (Guid id, [FromBody] AddMessageRequest req, HttpContext ctx, AdminDbContext db, IAuditLogService audit) =>
        {
            if (string.IsNullOrWhiteSpace(req.Body)) return Results.BadRequest(new { error = "Nota vazia." });
            var t = await db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id && x.Kind != SupportTicketKind.Support);
            if (t is null) return Results.NotFound();

            var adminId = GetUserId(ctx);
            db.SupportTicketMessages.Add(new SupportTicketMessage
            {
                Id = Guid.NewGuid(),
                TicketId = t.Id,
                AuthorId = adminId,
                IsAdmin = true,
                IsInternalNote = true,
                Body = req.Body.Trim(),
                CreatedAt = DateTime.UtcNow
            });
            t.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await audit.WriteAsync(adminId, "report.note", "SupportTicket", id.ToString(), null, null, null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReportsManage));

        admReports.MapPost("/{id:guid}/assign", async (Guid id, [FromBody] AssignRequest req, HttpContext ctx, AdminDbContext db, IAuditLogService audit) =>
        {
            var t = await db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id && x.Kind != SupportTicketKind.Support);
            if (t is null) return Results.NotFound();

            var before = JsonSerializer.Serialize(new { t.AssignedAdminId });
            t.AssignedAdminId = req.AdminId;
            t.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetUserId(ctx), "report.assign", "SupportTicket", id.ToString(), before, JsonSerializer.Serialize(new { t.AssignedAdminId }), null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReportsManage));

        admReports.MapPost("/{id:guid}/state", async (Guid id, [FromBody] StateRequest req, HttpContext ctx, AdminDbContext db, IAuditLogService audit) =>
        {
            if (!Enum.TryParse<SupportTicketState>(req.State, true, out var parsedState))
                return Results.BadRequest(new { error = "Estado inválido." });

            var t = await db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id && x.Kind != SupportTicketKind.Support);
            if (t is null) return Results.NotFound();

            var before = JsonSerializer.Serialize(new { t.State });
            t.State = parsedState;
            t.UpdatedAt = DateTime.UtcNow;
            t.ClosedAt = parsedState == SupportTicketState.Closed ? DateTime.UtcNow : null;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetUserId(ctx), "report.state", "SupportTicket", id.ToString(), before, JsonSerializer.Serialize(new { t.State }), null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReportsManage));

        admReports.MapPost("/{id:guid}/priority", async (Guid id, [FromBody] PriorityRequest req, HttpContext ctx, AdminDbContext db, IAuditLogService audit) =>
        {
            if (!Enum.TryParse<SupportTicketPriority>(req.Priority, true, out var parsedPriority))
                return Results.BadRequest(new { error = "Prioridade inválida." });

            var t = await db.SupportTickets.FirstOrDefaultAsync(x => x.Id == id && x.Kind != SupportTicketKind.Support);
            if (t is null) return Results.NotFound();

            var before = JsonSerializer.Serialize(new { t.Priority });
            t.Priority = parsedPriority;
            t.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetUserId(ctx), "report.priority", "SupportTicket", id.ToString(), before, JsonSerializer.Serialize(new { t.Priority }), null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReportsManage));
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string TrimOrDefault(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static bool TryParseReportKind(string? value, out SupportTicketKind kind)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "error":
            case "erro":
            case "bug":
            case "report-error":
                kind = SupportTicketKind.ErrorReport;
                return true;
            case "feedback":
            case "report-feedback":
                kind = SupportTicketKind.Feedback;
                return true;
            case "support":
                kind = SupportTicketKind.Support;
                return true;
            default:
                kind = SupportTicketKind.Support;
                return false;
        }
    }

    private static SupportTicketPriority ResolvePriority(string? severity, SupportTicketKind kind)
    {
        return severity?.Trim().ToLowerInvariant() switch
        {
            "critical" or "critica" or "crítica" => SupportTicketPriority.Urgent,
            "high" or "alta" => SupportTicketPriority.High,
            "medium" or "media" or "média" => SupportTicketPriority.Normal,
            "low" or "baixa" => SupportTicketPriority.Low,
            _ => kind == SupportTicketKind.ErrorReport ? SupportTicketPriority.High : SupportTicketPriority.Normal,
        };
    }

    private static string SerializeReportMetadata(CreateReportRequest req, HttpContext ctx, Guid? openedByUserId, DateTime createdAt)
    {
        var payload = new
        {
            request = new
            {
                type = req.Type,
                severity = req.Severity,
                pagePath = TrimOrNull(req.PagePath),
                pageUrl = TrimOrNull(req.PageUrl),
                sourceChannel = TrimOrDefault(req.SourceChannel, "public-footer"),
                browser = TrimOrNull(req.Browser),
                operatingSystem = TrimOrNull(req.OperatingSystem),
                deviceType = TrimOrNull(req.DeviceType),
                diagnosticsConsent = req.DiagnosticsConsent,
                template = req.Template,
                sessionContext = req.SessionContext,
                environmentContext = req.EnvironmentContext,
                userContext = req.UserContext,
                additionalContext = req.AdditionalContext,
            },
            server = new
            {
                receivedAtUtc = createdAt,
                traceId = ctx.TraceIdentifier,
                remoteIp = ctx.Connection.RemoteIpAddress?.ToString(),
                userAgent = ctx.Request.Headers.UserAgent.ToString(),
                referer = ctx.Request.Headers.Referer.ToString(),
                acceptLanguage = ctx.Request.Headers.AcceptLanguage.ToString(),
                openedByUserId,
            }
        };

        return JsonSerializer.Serialize(payload);
    }
}
