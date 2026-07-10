using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Models;

namespace TrustRent.Modules.Admin.Endpoints;

public static class AgentReportsEndpoints
{
    public static void MapAgentReportsEndpoints(this IEndpointRouteBuilder app)
    {
        var agent = app.MapGroup("/api/agent/reports");

        agent.AddEndpointFilter(async (efi, next) =>
        {
            var config = efi.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var apiKey = config["Hermes:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Results.Json(new { error = "Agent API desativado." }, statusCode: 404);
            }

            var provided = efi.HttpContext.Request.Headers["X-Api-Key"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(provided) || !string.Equals(provided.Trim(), apiKey.Trim(), StringComparison.Ordinal))
            {
                return Results.Json(new { error = "API key inválida." }, statusCode: 401);
            }

            return await next(efi);
        });

        agent.MapGet("/", async (
            [FromQuery] string? type,
            [FromQuery] string? state,
            [FromQuery] bool? archived,
            [FromQuery] string? search,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            AdminDbContext db) =>
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 25 : (pageSize > 200 ? 200 : pageSize);

            var q = db.SupportTickets.AsNoTracking()
                .Where(t => t.Kind != SupportTicketKind.Support)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(type) && SupportTicketsEndpoints.TryParseReportKind(type, out var reportKind) && reportKind != SupportTicketKind.Support)
                q = q.Where(t => t.Kind == reportKind);

            if (!string.IsNullOrWhiteSpace(state) && Enum.TryParse<SupportTicketState>(state, true, out var parsedState))
                q = q.Where(t => t.State == parsedState);

            q = archived.HasValue ? q.Where(t => t.IsArchived == archived.Value) : q.Where(t => !t.IsArchived);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                q = q.Where(t =>
                    t.Subject.ToLower().Contains(term) ||
                    (t.PagePath ?? string.Empty).ToLower().Contains(term) ||
                    (t.ClientBrowser ?? string.Empty).ToLower().Contains(term) ||
                    (t.ClientOs ?? string.Empty).ToLower().Contains(term));
            }

            if (from.HasValue) q = q.Where(t => t.CreatedAt >= from.Value);
            if (to.HasValue) q = q.Where(t => t.CreatedAt <= to.Value);

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
                    t.IsArchived,
                    t.ArchivedAt,
                    t.ArchivedByAdminId,
                    t.CreatedAt,
                    t.UpdatedAt,
                    t.ClosedAt
                })
                .ToListAsync();

            return Results.Ok(new { items, page, pageSize, totalCount = total });
        });

        agent.MapGet("/{id:guid}", async (Guid id, AdminDbContext db) =>
        {
            var t = await db.SupportTickets.AsNoTracking()
                .Include(x => x.Messages.OrderBy(m => m.CreatedAt))
                .FirstOrDefaultAsync(x => x.Id == id && x.Kind != SupportTicketKind.Support);

            if (t is null) return Results.NotFound();

            var result = new
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
                t.IsArchived,
                t.ArchivedAt,
                t.ArchivedByAdminId,
                t.MetadataJson,
                t.CreatedAt,
                t.UpdatedAt,
                t.ClosedAt,
                Messages = t.Messages.Select(m => new
                {
                    m.Id,
                    m.AuthorId,
                    m.IsAdmin,
                    m.IsInternalNote,
                    m.Body,
                    m.CreatedAt
                }).ToList()
            };

            return Results.Ok(result);
        });
    }
}
