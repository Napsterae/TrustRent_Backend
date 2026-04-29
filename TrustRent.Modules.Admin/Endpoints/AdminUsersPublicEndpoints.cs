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
using TrustRent.Modules.Identity.Contracts.Database;

namespace TrustRent.Modules.Admin.Endpoints;

public static class AdminUsersPublicEndpoints
{
    private static Guid GetAdminId(HttpContext ctx) =>
        Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? ctx.User.FindFirst("sub")!.Value);

    public record SuspendRequest(string Reason);
    public record AnonymizeRequest(string Reason);

    public static void MapAdminUsersPublicEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin/users");

        g.MapGet("/", async ([FromQuery] string? q, [FromQuery] bool? suspended, [FromQuery] int page, [FromQuery] int pageSize, IdentityDbContext db) =>
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 25 : (pageSize > 200 ? 200 : pageSize);
            var query = db.Users.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var like = $"%{q.Trim().ToLower()}%";
                query = query.Where(u => EF.Functions.ILike(u.Email, like) || EF.Functions.ILike(u.Name, like));
            }
            if (suspended.HasValue) query = query.Where(u => u.IsSuspended == suspended.Value);
            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(u => new
                {
                    u.Id, u.Name, u.Email, u.IsSuspended, u.SuspendedAt, u.SuspendedReason,
                    u.AnonymizedAt, u.IsIdentityVerified, u.IsNoDebtVerified, u.TrustScore, u.CreatedAt
                })
                .ToListAsync();
            return Results.Ok(new { items, page, pageSize, totalCount = total });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.UsersRead));

        g.MapGet("/{id:guid}", async (Guid id, IdentityDbContext db) =>
        {
            var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return u is null ? Results.NotFound() : Results.Ok(u);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.UsersRead));

        g.MapPost("/{id:guid}/suspend", async (Guid id, [FromBody] SuspendRequest req, IdentityDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (u is null) return Results.NotFound();
            if (u.IsSuspended) return Results.BadRequest(new { error = "Já suspenso." });
            var before = JsonSerializer.Serialize(new { u.IsSuspended, u.SuspendedReason });
            u.IsSuspended = true;
            u.SuspendedAt = DateTime.UtcNow;
            u.SuspendedReason = req.Reason;
            u.SuspendedByAdminId = GetAdminId(ctx);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "user.suspend", "User", id.ToString(), before, JsonSerializer.Serialize(new { u.IsSuspended, u.SuspendedReason }), req.Reason, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.UsersSuspend));

        g.MapPost("/{id:guid}/unsuspend", async (Guid id, IdentityDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (u is null) return Results.NotFound();
            if (!u.IsSuspended) return Results.BadRequest(new { error = "Não está suspenso." });
            var before = JsonSerializer.Serialize(new { u.IsSuspended });
            u.IsSuspended = false;
            u.SuspendedAt = null;
            u.SuspendedReason = null;
            u.SuspendedByAdminId = null;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "user.unsuspend", "User", id.ToString(), before, JsonSerializer.Serialize(new { u.IsSuspended }), null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.UsersUnsuspend));

        g.MapPost("/{id:guid}/anonymize", async (Guid id, [FromBody] AnonymizeRequest req, IdentityDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (u is null) return Results.NotFound();
            if (u.AnonymizedAt.HasValue) return Results.BadRequest(new { error = "Já anonimizado." });
            var before = JsonSerializer.Serialize(new { u.Name, u.Email, u.Nif, u.PhoneNumber });
            var anonId = $"anon-{id:N}";
            u.Name = "Utilizador anonimizado";
            u.Email = $"{anonId}@anonymous.local";
            u.Nif = null;
            u.CitizenCardNumber = null;
            u.Address = null;
            u.PostalCode = null;
            u.PhoneCountryCode = null;
            u.PhoneNumber = null;
            u.ProfilePictureUrl = null;
            u.AnonymizedAt = DateTime.UtcNow;
            u.AnonymizedByAdminId = GetAdminId(ctx);
            u.IsSuspended = true;
            u.SuspendedAt = u.SuspendedAt ?? DateTime.UtcNow;
            u.SuspendedReason = u.SuspendedReason ?? "Anonimizado (RGPD)";
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "user.anonymize", "User", id.ToString(), before, null, req.Reason, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.UsersAnonymize));
    }
}
