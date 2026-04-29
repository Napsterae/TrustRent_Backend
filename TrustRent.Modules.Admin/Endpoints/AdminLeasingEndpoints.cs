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
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.Database;

namespace TrustRent.Modules.Admin.Endpoints;

public static class AdminLeasingEndpoints
{
    private static Guid GetAdminId(HttpContext ctx) =>
        Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? ctx.User.FindFirst("sub")!.Value);

    public record TerminateLeaseRequest(string Reason);
    public record RefundRequest(decimal Amount, string Reason);

    public static void MapAdminLeasingEndpoints(this IEndpointRouteBuilder app)
    {
        // ===== Applications =====
        var apps = app.MapGroup("/api/admin/applications");

        apps.MapGet("/", async ([FromQuery] string? status, [FromQuery] int page, [FromQuery] int pageSize, CatalogDbContext db) =>
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 25 : (pageSize > 200 ? 200 : pageSize);
            var query = db.Applications.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TrustRent.Shared.Models.ApplicationStatus>(status, true, out var s))
                query = query.Where(a => a.Status == s);
            var total = await query.CountAsync();
            var items = await query.OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(a => new { a.Id, a.PropertyId, a.TenantId, a.Status, a.DurationMonths, a.CreatedAt, a.UpdatedAt, a.LeaseId })
                .ToListAsync();
            return Results.Ok(new { items, page, pageSize, totalCount = total });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ApplicationsRead));

        apps.MapGet("/{id:guid}", async (Guid id, CatalogDbContext db) =>
        {
            var a = await db.Applications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return a is null ? Results.NotFound() : Results.Ok(a);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ApplicationsRead));

        apps.MapPost("/{id:guid}/cancel", async (Guid id, CatalogDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            var a = await db.Applications.FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();
            var before = JsonSerializer.Serialize(new { a.Status });
            a.Status = TrustRent.Shared.Models.ApplicationStatus.Rejected;
            a.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "application.cancel", "Application", id.ToString(), before, JsonSerializer.Serialize(new { a.Status }), null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ApplicationsCancel));

        // ===== Leases =====
        var leases = app.MapGroup("/api/admin/leases");

        leases.MapGet("/", async ([FromQuery] int page, [FromQuery] int pageSize, LeasingDbContext db) =>
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 25 : (pageSize > 200 ? 200 : pageSize);
            var total = await db.Leases.CountAsync();
            var items = await db.Leases.AsNoTracking().OrderByDescending(l => l.StartDate)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(l => new { l.Id, l.PropertyId, l.TenantId, l.LandlordId, l.StartDate, l.EndDate, l.MonthlyRent, l.ContractType, l.IsRegisteredWithTaxAuthority })
                .ToListAsync();
            return Results.Ok(new { items, page, pageSize, totalCount = total });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.LeasesRead));

        leases.MapGet("/{id:guid}", async (Guid id, LeasingDbContext db) =>
        {
            var l = await db.Leases.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            return l is null ? Results.NotFound() : Results.Ok(l);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.LeasesRead));

        // ===== Payments =====
        var payments = app.MapGroup("/api/admin/payments");

        payments.MapGet("/", async ([FromQuery] string? status, [FromQuery] int page, [FromQuery] int pageSize, LeasingDbContext db) =>
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 25 : (pageSize > 200 ? 200 : pageSize);
            var query = db.Payments.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TrustRent.Modules.Leasing.Models.PaymentStatus>(status, true, out var s))
                query = query.Where(p => p.Status == s);
            var total = await query.CountAsync();
            var items = await query.OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(p => new { p.Id, p.LeaseId, p.TenantId, p.LandlordId, p.Type, p.Amount, p.Status, p.CreatedAt, p.PaidAt, p.StripePaymentIntentId })
                .ToListAsync();
            return Results.Ok(new { items, page, pageSize, totalCount = total });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsRead));

        payments.MapPost("/{id:guid}/mark-paid", async (Guid id, LeasingDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            var p = await db.Payments.FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();
            var before = JsonSerializer.Serialize(new { p.Status });
            p.Status = TrustRent.Modules.Leasing.Models.PaymentStatus.Succeeded;
            p.PaidAt = DateTime.UtcNow;
            p.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "payment.manual_mark_paid", "Payment", id.ToString(), before, JsonSerializer.Serialize(new { p.Status }), null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsManualMarkPaid));
    }
}
