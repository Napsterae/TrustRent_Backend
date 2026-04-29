using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Admin.Authorization;
using TrustRent.Modules.Admin.Contracts;
using TrustRent.Modules.Admin.Contracts.Database;

namespace TrustRent.Modules.Admin.Endpoints;

public static class AdminAuditEndpoints
{
    public static void MapAdminAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin/audit");

        g.MapGet("/", async (HttpContext ctx, AdminDbContext db,
            string? action, string? entityType, string? entityId, Guid? adminUserId,
            DateTime? from, DateTime? to, int page = 1, int pageSize = 50) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var q = db.AuditLogs.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(action))     q = q.Where(x => x.Action.Contains(action));
            if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(x => x.EntityType == entityType);
            if (!string.IsNullOrWhiteSpace(entityId))   q = q.Where(x => x.EntityId == entityId);
            if (adminUserId.HasValue)                   q = q.Where(x => x.AdminUserId == adminUserId.Value);
            if (from.HasValue)                          q = q.Where(x => x.CreatedAt >= from.Value);
            if (to.HasValue)                            q = q.Where(x => x.CreatedAt <= to.Value);

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(x => new
                {
                    x.Id, x.AdminUserId, x.Action, x.EntityType, x.EntityId,
                    x.Reason, x.Ip, x.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(new { items, page, pageSize, totalCount = total, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.AuditRead));

        g.MapGet("/{id:guid}", async (Guid id, AdminDbContext db) =>
        {
            var entry = await db.AuditLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (entry is null) return Results.NotFound();
            return Results.Ok(entry);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.AuditRead));
    }
}
