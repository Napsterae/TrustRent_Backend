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
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Models;

namespace TrustRent.Modules.Admin.Endpoints;

public static class AdminLeasingEndpoints
{
    private static Guid GetAdminId(HttpContext ctx) =>
        Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? ctx.User.FindFirst("sub")!.Value);

    public record TerminateLeaseRequest(string Reason);
    public record ReasonRequest(string? Reason);
    public record RefundRequest(decimal Amount, string Reason);
    public record ManualChargeRequest(string LeaseId, decimal Amount, string Reason);

    public static void MapAdminLeasingEndpoints(this IEndpointRouteBuilder app)
    {
        // ===== Applications =====
        var apps = app.MapGroup("/api/admin/applications");

        apps.MapGet("/", async ([FromQuery] string? status, [FromQuery] Guid? partyId, [FromQuery] int page, [FromQuery] int pageSize, CatalogDbContext db) =>
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 25 : (pageSize > 200 ? 200 : pageSize);
            var query = db.Applications.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TrustRent.Shared.Models.ApplicationStatus>(status, true, out var s))
                query = query.Where(a => a.Status == s);
            if (partyId.HasValue)
                query = query.Where(a => a.TenantId == partyId.Value || a.CoTenantUserId == partyId.Value);
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

        apps.MapPost("/{id:guid}/cancel", async (Guid id, [FromBody] ReasonRequest? req, CatalogDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            var a = await db.Applications.FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return Results.NotFound();
            var before = JsonSerializer.Serialize(new { a.Status });
            a.Status = TrustRent.Shared.Models.ApplicationStatus.Rejected;
            a.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "application.cancel", "Application", id.ToString(), before, JsonSerializer.Serialize(new { a.Status }), req?.Reason, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ApplicationsCancel));

        // ===== Leases =====
        var leases = app.MapGroup("/api/admin/leases");

        leases.MapGet("/", async ([FromQuery] Guid? partyId, [FromQuery] int page, [FromQuery] int pageSize, LeasingDbContext db) =>
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 25 : (pageSize > 200 ? 200 : pageSize);
            var query = db.Leases.AsNoTracking().AsQueryable();
            if (partyId.HasValue)
                query = query.Where(l => l.TenantId == partyId.Value
                    || l.LandlordId == partyId.Value
                    || l.CoTenantId == partyId.Value
                    || l.GuarantorUserId == partyId.Value);
            var total = await query.CountAsync();
            var items = await query.OrderByDescending(l => l.StartDate)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(l => new { l.Id, l.PropertyId, l.TenantId, l.LandlordId, l.StartDate, l.EndDate, l.MonthlyRent, l.ContractType, l.IsRegisteredWithTaxAuthority })
                .ToListAsync();
            return Results.Ok(new { items, page, pageSize, totalCount = total });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.LeasesRead));

        leases.MapGet("/{id:guid}", async (Guid id, LeasingDbContext db) =>
        {
            var l = await db.Leases
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.Id,
                    x.PropertyId,
                    x.TenantId,
                    x.LandlordId,
                    x.ApplicationId,
                    x.CoTenantId,
                    x.GuarantorUserId,
                    x.RequiredSignaturesCount,
                    x.StartDate,
                    x.EndDate,
                    x.MonthlyRent,
                    x.Deposit,
                    x.ContractType,
                    x.IsRegisteredWithTaxAuthority,
                    x.TaxRegistrationDate,
                    x.TaxRegistrationReference,
                    x.LandlordSigned,
                    x.LandlordSignatureVerified,
                    x.TenantSigned,
                    x.TenantSignatureVerified,
                    Status = x.Status.ToString(),
                    x.CreatedAt,
                    Signatures = x.Signatures
                        .OrderBy(s => s.SequenceOrder)
                        .Select(s => new
                        {
                            s.Id,
                            s.UserId,
                            s.Role,
                            s.SequenceOrder,
                            s.Signed,
                            s.SignatureVerified,
                        })
                        .ToList(),
                })
                .FirstOrDefaultAsync();

            return l is null
                ? Results.NotFound(new { error = "Contrato não encontrado." })
                : Results.Ok(l);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.LeasesRead));

        // ===== Payments =====
        var payments = app.MapGroup("/api/admin/payments");

        payments.MapGet("/", async ([FromQuery] string? status, [FromQuery] Guid? partyId, [FromQuery] int page, [FromQuery] int pageSize, LeasingDbContext db) =>
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 25 : (pageSize > 200 ? 200 : pageSize);
            var query = db.Payments.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TrustRent.Modules.Leasing.Models.PaymentStatus>(status, true, out var s))
                query = query.Where(p => p.Status == s);
            if (partyId.HasValue)
                query = query.Where(p => p.TenantId == partyId.Value || p.LandlordId == partyId.Value);
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
            if (p.Status == TrustRent.Modules.Leasing.Models.PaymentStatus.Succeeded)
                return Results.NoContent();
            if (p.Status is TrustRent.Modules.Leasing.Models.PaymentStatus.Refunded or TrustRent.Modules.Leasing.Models.PaymentStatus.PartiallyRefunded)
                return Results.BadRequest("Não é possível marcar como pago um pagamento reembolsado.");
            var before = JsonSerializer.Serialize(new { p.Status });
            p.Status = TrustRent.Modules.Leasing.Models.PaymentStatus.Succeeded;
            p.PaidAt = DateTime.UtcNow;
            p.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "payment.manual_mark_paid", "Payment", id.ToString(), before, JsonSerializer.Serialize(new { p.Status }), null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsManualMarkPaid));

        payments.MapPost("/{id:guid}/refund", async (Guid id, [FromBody] RefundRequest dto, LeasingDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            var p = await db.Payments.FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();
            if (p.Status is not TrustRent.Modules.Leasing.Models.PaymentStatus.Succeeded)
                return Results.BadRequest("Só é possível reembolsar pagamentos com sucesso.");
            if (dto.Amount <= 0 || dto.Amount > p.Amount)
                return Results.BadRequest("Valor de reembolso inválido.");
            var before = JsonSerializer.Serialize(new { p.Status, p.Metadata });
            p.Status = dto.Amount == p.Amount
                ? TrustRent.Modules.Leasing.Models.PaymentStatus.Refunded
                : TrustRent.Modules.Leasing.Models.PaymentStatus.PartiallyRefunded;
            p.Metadata = JsonSerializer.Serialize(new { refundAmount = dto.Amount, refundReason = dto.Reason });
            p.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "payment.refund", "Payment", id.ToString(), before, JsonSerializer.Serialize(new { p.Status, refundAmount = dto.Amount }), null, ctx);
            return Results.Ok(new { status = p.Status.ToString(), refundAmount = dto.Amount });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsRefund));

        payments.MapPost("/charge", async ([FromBody] ManualChargeRequest dto, LeasingDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            if (!Guid.TryParse(dto.LeaseId, out var leaseId))
                return Results.BadRequest("ID de locação inválido.");
            if (dto.Amount <= 0)
                return Results.BadRequest("Valor inválido.");

            var lease = await db.Leases.AsNoTracking().FirstOrDefaultAsync(x => x.Id == leaseId);
            if (lease is null) return Results.NotFound("Locação não encontrada.");
            var before = JsonSerializer.Serialize(new { action = "manual_charge", amount = dto.Amount });
            var newPayment = new TrustRent.Modules.Leasing.Models.Payment
            {
                Id = Guid.NewGuid(),
                LeaseId = lease.Id,
                TenantId = lease.TenantId,
                LandlordId = lease.LandlordId,
                Type = TrustRent.Modules.Leasing.Models.PaymentType.InitialPayment,
                Amount = dto.Amount,
                PlatformFee = 0,
                LandlordAmount = dto.Amount,
                RentAmount = dto.Amount,
                Currency = "eur",
                Status = TrustRent.Modules.Leasing.Models.PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Metadata = JsonSerializer.Serialize(new { source = "admin_manual_charge", reason = dto.Reason }),
            };
            db.Payments.Add(newPayment);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "payment.manual_charge", "Payment", newPayment.Id.ToString(), before, JsonSerializer.Serialize(new { paymentId = newPayment.Id }), null, ctx);
            return Results.Created($"/api/admin/payments/{newPayment.Id}", newPayment);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsManualCharge));

        // ===== Stripe Accounts =====
        var stripeAccounts = app.MapGroup("/api/admin/stripe/accounts");

        stripeAccounts.MapGet("/", async ([FromQuery] int page, [FromQuery] int pageSize, LeasingDbContext db) =>
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 25 : (pageSize > 200 ? 200 : pageSize);
            var total = await db.StripeAccounts.CountAsync();
            var items = await db.StripeAccounts.AsNoTracking()
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(a => new { a.Id, a.UserId, a.StripeAccountId, a.ChargesEnabled, a.PayoutsEnabled, a.IsOnboardingComplete, a.IsDefault, a.PropertyId, a.CreatedAt })
                .ToListAsync();
            return Results.Ok(new { items, page, pageSize, totalCount = total });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsManageStripeAccounts));

        stripeAccounts.MapGet("/{id:guid}/user", async (Guid id, LeasingDbContext db) =>
        {
            var account = await db.StripeAccounts.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            if (account is null) return Results.NotFound();
            return Results.Ok(new {
                account.Id, account.StripeAccountId, account.ChargesEnabled, account.PayoutsEnabled,
                account.IsOnboardingComplete, account.IsDefault, account.PropertyId, account.CreatedAt,
                userId = account.UserId
            });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsManageStripeAccounts));

        stripeAccounts.MapPost("/{id:guid}/refresh", async (Guid id, IStripeAccountService stripeAccountService) =>
        {
            try
            {
                await stripeAccountService.RefreshAccountStatusAsync(id);
                var updated = await stripeAccountService.GetAccountByIdAsync(id);
                return Results.Ok(updated);
            }
            catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsManageStripeAccounts));
    }
}
