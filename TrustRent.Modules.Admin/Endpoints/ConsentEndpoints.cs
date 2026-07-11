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
using TrustRent.Modules.Admin.Models;

namespace TrustRent.Modules.Admin.Endpoints;

public static class ConsentEndpoints
{
    private static Guid? TryGetUserId(HttpContext ctx)
    {
        var raw = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? ctx.User.FindFirst("sub")?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public record CreateConsentRequest(
        string ConsentStatus,           // granted | refused | withdrawn
        string Mechanism,               // accept_all | reject_all | withdrawn_via_preferences
        string? SessionId,
        string? BannerVersion,
        string? PrivacyPolicyVersion,
        string? SourceUrl,
        string? Language,
        JsonElement? Purposes);         // {"error_telemetry": true}

    public static void MapConsentEndpoints(this IEndpointRouteBuilder app)
    {
        // ===== Public endpoint (anonymous-capable, resolves user from cookie if present) =====
        var pub = app.MapGroup("/api/consent");

        pub.MapPost("/", async ([FromBody] CreateConsentRequest req, HttpContext ctx, AdminDbContext db) =>
        {
            // Validate
            var status = req.ConsentStatus?.Trim().ToLowerInvariant();
            if (status != "granted" && status != "refused" && status != "withdrawn")
                return Results.BadRequest(new { error = "Estado de consentimento inválido." });

            if (string.IsNullOrWhiteSpace(req.Mechanism))
                return Results.BadRequest(new { error = "Mecanismo de consentimento obrigatório." });

            var userId = TryGetUserId(ctx);
            var now = DateTime.UtcNow;

            var record = new ConsentRecord
            {
                Id = Guid.NewGuid(),
                OpenedByUserId = userId,
                SessionId = TrimOrNull(req.SessionId),
                ConsentStatus = status,
                PurposesJson = req.Purposes.HasValue ? req.Purposes.Value.GetRawText() : "{}",
                Mechanism = req.Mechanism.Trim(),
                BannerVersion = TrimOrNull(req.BannerVersion),
                PrivacyPolicyVersion = TrimOrNull(req.PrivacyPolicyVersion),
                SourceUrl = TrimOrNull(req.SourceUrl),
                Language = TrimOrNull(req.Language),
                UserAgent = TrimOrNull(ctx.Request.Headers.UserAgent.ToString()),
                IpAddress = ctx.Connection.RemoteIpAddress?.ToString(),
                CreatedAt = now
            };

            db.ConsentRecords.Add(record);
            await db.SaveChangesAsync();
            return Results.Created($"/api/consent/{record.Id}", new { record.Id });
        });

        // ===== Admin endpoints =====
        var adm = app.MapGroup("/api/admin/consent-records");

        adm.MapGet("/", async ([FromQuery] string? search, [FromQuery] string? status, [FromQuery] int page, [FromQuery] int pageSize, AdminDbContext db) =>
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 25 : (pageSize > 200 ? 200 : pageSize);

            var q = db.ConsentRecords.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(c => c.ConsentStatus == status);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                q = q.Where(c =>
                    (c.SessionId ?? string.Empty).ToLower().Contains(term) ||
                    (c.SourceUrl ?? string.Empty).ToLower().Contains(term) ||
                    (c.UserAgent ?? string.Empty).ToLower().Contains(term));
            }

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(c => new
                {
                    c.Id,
                    c.OpenedByUserId,
                    c.SessionId,
                    c.ConsentStatus,
                    c.Mechanism,
                    c.BannerVersion,
                    c.PrivacyPolicyVersion,
                    c.SourceUrl,
                    c.Language,
                    c.UserAgent,
                    c.IpAddress,
                    c.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(new { items, page, pageSize, totalCount = total });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReportsRead));

        adm.MapGet("/{id:guid}", async (Guid id, AdminDbContext db) =>
        {
            var c = await db.ConsentRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return c is null ? Results.NotFound() : Results.Ok(c);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReportsRead));
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
