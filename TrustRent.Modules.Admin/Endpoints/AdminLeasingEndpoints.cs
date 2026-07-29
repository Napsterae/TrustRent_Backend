using Hangfire;
using Hangfire.Storage.Monitoring;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Text.Json;
using TrustRent.Modules.Admin.Authorization;
using TrustRent.Modules.Admin.Contracts;
using TrustRent.Modules.Admin.Contracts.Interfaces;
using TrustRent.Modules.Admin.Models;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;
using Stripe;
using TrustRent.Modules.Admin.Contracts.Database;

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

        // GET /api/admin/leases/overdue — Active leases with no current-month rent
        leases.MapGet("/overdue", async (LeasingDbContext db) =>
        {
            var today = DateTime.UtcNow.Date;
            var currentMonthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            // Find active leases where no Succeeded MonthlyRent payment exists for the current billing period
            var activeLeases = await db.Leases
                .Where(l => l.Status == LeaseStatus.Active && l.EndDate > today)
                .ToListAsync();

            var leaseIds = activeLeases.Select(l => l.Id).ToList();
            var paidLeaseIds = await db.Payments
                .Where(p => leaseIds.Contains(p.LeaseId)
                    && p.Type == PaymentType.MonthlyRent
                    && p.CreatedAt >= currentMonthStart
                    && p.Status == PaymentStatus.Succeeded)
                .Select(p => p.LeaseId)
                .Distinct()
                .ToListAsync();

            var overdueLeases = activeLeases
                .Where(l => !paidLeaseIds.Contains(l.Id))
                .Select(l => new
                {
                    l.Id,
                    l.PropertyId,
                    l.TenantId,
                    l.LandlordId,
                    l.MonthlyRent,
                    l.StartDate,
                    l.EndDate,
                    l.CoTenantId,
                    DaysSinceStart = (today - l.StartDate).Days,
                    DueDay = l.StartDate.Day
                })
                .ToList();

            return Results.Ok(new { total = overdueLeases.Count, items = overdueLeases });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.LeasesRead));

        // GET /api/admin/leases/awaiting-payment — Leases stuck in AwaitingPayment
        leases.MapGet("/awaiting-payment", async (LeasingDbContext db, int olderThanDays = 7) =>
        {
            var cutoff = DateTime.UtcNow.AddDays(-olderThanDays);

            var leasesList = await db.Leases
                .Where(l => l.Status == LeaseStatus.AwaitingPayment && l.StartDate < cutoff)
                .OrderBy(l => l.StartDate)
                .Select(l => new
                {
                    l.Id,
                    l.PropertyId,
                    l.TenantId,
                    l.LandlordId,
                    l.MonthlyRent,
                    l.StartDate,
                    l.EndDate,
                    DaysWaiting = (DateTime.UtcNow - l.StartDate).Days
                })
                .ToListAsync();

            return Results.Ok(new { total = leasesList.Count, items = leasesList });
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

        payments.MapPost("/{id:guid}/mark-paid", async (Guid id, LeasingDbContext db, AdminDbContext adminDb, IAuditLogService audit, HttpContext ctx) =>
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

            adminDb.PaymentOperations.Add(new PaymentOperation
            {
                Id = Guid.NewGuid(),
                PaymentId = p.Id,
                LeaseId = p.LeaseId,
                AdminUserId = GetAdminId(ctx),
                OperationType = AdminPaymentOperationType.ManualMarkPaid,
                Amount = p.Amount,
                Currency = "EUR",
                IdempotencyKey = $"mark-paid-{p.Id}-{Guid.NewGuid():n}",
                Status = AdminPaymentOperationStatus.Succeeded,
                CreatedAt = DateTime.UtcNow
            });
            await adminDb.SaveChangesAsync();

            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsManualMarkPaid));

        payments.MapPost("/{id:guid}/refund", async (Guid id, [FromBody] RefundRequest dto, LeasingDbContext db, AdminDbContext adminDb, IAuditLogService audit, HttpContext ctx) =>
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
            // Append refund info to existing metadata instead of overwriting it
            var existingMetadata = string.IsNullOrEmpty(p.Metadata) ? new Dictionary<string, object>() : JsonSerializer.Deserialize<Dictionary<string, object>>(p.Metadata) ?? new Dictionary<string, object>();
            existingMetadata["refundAmount"] = dto.Amount;
            existingMetadata["refundReason"] = dto.Reason;
            existingMetadata["refundedAt"] = DateTime.UtcNow;
            p.Metadata = JsonSerializer.Serialize(existingMetadata);
            p.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "payment.refund", "Payment", id.ToString(), before, JsonSerializer.Serialize(new { p.Status, refundAmount = dto.Amount }), null, ctx);

            adminDb.PaymentOperations.Add(new PaymentOperation
            {
                Id = Guid.NewGuid(),
                PaymentId = p.Id,
                LeaseId = p.LeaseId,
                AdminUserId = GetAdminId(ctx),
                OperationType = AdminPaymentOperationType.Refund,
                Amount = dto.Amount,
                Currency = "EUR",
                IdempotencyKey = $"refund-{p.Id}-{Guid.NewGuid():n}",
                Status = AdminPaymentOperationStatus.Succeeded,
                CreatedAt = DateTime.UtcNow,
                Reason = dto.Reason
            });
            await adminDb.SaveChangesAsync();

            return Results.Ok(new { status = p.Status.ToString(), refundAmount = dto.Amount });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsRefund));

        // G15: Payment collection statistics for admin dashboard
        payments.MapGet("/stats", async (LeasingDbContext db) =>
        {
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

            var succeededCount = await db.Payments
                .Where(p => p.Type == PaymentType.MonthlyRent && p.Status == PaymentStatus.Succeeded && p.PaidAt >= thirtyDaysAgo)
                .CountAsync();
            var failedCount = await db.Payments
                .Where(p => p.Type == PaymentType.MonthlyRent && p.Status == PaymentStatus.Failed && p.CreatedAt >= thirtyDaysAgo)
                .CountAsync();
            var totalCollected30d = await db.Payments
                .Where(p => p.Type == PaymentType.MonthlyRent && p.Status == PaymentStatus.Succeeded && p.PaidAt >= thirtyDaysAgo)
                .SumAsync(p => (decimal?)p.Amount) ?? 0;
            var totalPending = await db.Payments
                .Where(p => p.Type == PaymentType.MonthlyRent && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Processing))
                .CountAsync();
            var refundedCount = await db.Payments
                .Where(p => p.Type == PaymentType.MonthlyRent && (p.Status == PaymentStatus.Refunded || p.Status == PaymentStatus.PartiallyRefunded))
                .CountAsync();

            var totalAttempts = succeededCount + failedCount;
            var collectionRate = totalAttempts > 0 ? (decimal)succeededCount / totalAttempts * 100 : 0;

            return Results.Ok(new
            {
                totalCollected30d,
                totalPending,
                succeededCount,
                failedCount,
                refundedCount,
                collectionRate = Math.Round(collectionRate, 1)
            });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsRead));

        payments.MapPost("/charge", async ([FromBody] ManualChargeRequest dto, LeasingDbContext db, AdminDbContext adminDb, IAuditLogService audit, HttpContext ctx) =>
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

            adminDb.PaymentOperations.Add(new PaymentOperation
            {
                Id = Guid.NewGuid(),
                PaymentId = newPayment.Id,
                LeaseId = newPayment.LeaseId,
                AdminUserId = GetAdminId(ctx),
                OperationType = AdminPaymentOperationType.ManualCharge,
                Amount = dto.Amount,
                Currency = "EUR",
                IdempotencyKey = $"charge-{newPayment.Id}-{Guid.NewGuid():n}",
                Status = AdminPaymentOperationStatus.Succeeded,
                CreatedAt = DateTime.UtcNow,
                Reason = dto.Reason
            });
            await adminDb.SaveChangesAsync();

            return Results.Created($"/api/admin/payments/{newPayment.Id}", newPayment);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsManualCharge));

        // GET /api/admin/payments/{id} — Full payment detail with lease info and retry history
        payments.MapGet("/{id:guid}", async (Guid id, LeasingDbContext db) =>
        {
            var payment = await db.Payments.FindAsync(id);
            if (payment == null) return Results.NotFound();

            var lease = await db.Leases.AsNoTracking().FirstOrDefaultAsync(l => l.Id == payment.LeaseId);

            // Find retry history — all payments for the same lease+tenant+billing period
            var periodStart = new DateTime(payment.CreatedAt.Year, payment.CreatedAt.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var periodEnd = periodStart.AddMonths(1);
            var retryHistory = await db.Payments
                .Where(p => p.LeaseId == payment.LeaseId && p.TenantId == payment.TenantId
                    && p.Type == PaymentType.MonthlyRent && p.CreatedAt >= periodStart && p.CreatedAt < periodEnd)
                .OrderBy(p => p.CreatedAt)
                .Select(p => new { p.Id, p.Status, p.Amount, p.RetryAttempt, p.FailureReason, p.CreatedAt, p.PaidAt })
                .ToListAsync();

            return Results.Ok(new
            {
                payment.Id,
                payment.LeaseId,
                payment.TenantId,
                payment.LandlordId,
                payment.StripePaymentIntentId,
                payment.Type,
                payment.Amount,
                payment.PlatformFee,
                payment.LandlordAmount,
                payment.RentAmount,
                payment.Status,
                payment.FailureReason,
                payment.RetryAttempt,
                payment.PaidAt,
                payment.CreatedAt,
                payment.UpdatedAt,
                payment.IdempotencyKey,
                Lease = lease == null ? null : new
                {
                    lease.Id,
                    lease.PropertyId,
                    lease.TenantId,
                    lease.LandlordId,
                    lease.MonthlyRent,
                    lease.Status,
                    lease.StartDate,
                    lease.EndDate,
                    lease.CoTenantId,
                    lease.TenantSharePercentage
                },
                RetryHistory = retryHistory
            });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsRead));

        // POST /api/admin/payments/{id}/reconcile — Sync payment status with Stripe
        payments.MapPost("/{id:guid}/reconcile", async (Guid id, LeasingDbContext db, IConfiguration config) =>
        {
            var payment = await db.Payments.FindAsync(id);
            if (payment == null) return Results.NotFound();

            if (string.IsNullOrEmpty(payment.StripePaymentIntentId) || payment.StripePaymentIntentId.StartsWith("pending_"))
                return Results.BadRequest("Payment has no valid Stripe PaymentIntent ID.");

            try
            {
                StripeConfiguration.ApiKey = config["Stripe:SecretKey"];
                var pi = await new PaymentIntentService().GetAsync(payment.StripePaymentIntentId);

                var stripeStatus = pi.Status;
                var previousStatus = payment.Status;

                payment.Status = stripeStatus switch
                {
                    "succeeded" => PaymentStatus.Succeeded,
                    "processing" => PaymentStatus.Processing,
                    "requires_action" => PaymentStatus.Pending,
                    "requires_payment_method" => PaymentStatus.Failed,
                    "canceled" => PaymentStatus.Failed,
                    _ => payment.Status
                };

                if (payment.Status == PaymentStatus.Succeeded && payment.PaidAt == null)
                    payment.PaidAt = DateTime.UtcNow;

                payment.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();

                return Results.Ok(new
                {
                    PreviousStatus = previousStatus.ToString(),
                    StripeStatus = stripeStatus,
                    CurrentStatus = payment.Status.ToString(),
                    Updated = previousStatus != payment.Status,
                    payment.Id,
                    payment.StripePaymentIntentId
                });
            }
            catch (StripeException)
            {
                return Results.BadRequest("Erro ao comunicar com o Stripe. Verifica o ID do pagamento e tenta novamente.");
            }
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsViewStripe));

        // GET /api/admin/payments/failed — Failed payments with lease context
        payments.MapGet("/failed", async (LeasingDbContext db, int page = 1, int pageSize = 25) =>
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);
            var query = db.Payments
                .Where(p => p.Status == PaymentStatus.Failed && p.Type == PaymentType.MonthlyRent)
                .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.Id,
                    p.LeaseId,
                    p.TenantId,
                    p.LandlordId,
                    p.Amount,
                    p.FailureReason,
                    p.RetryAttempt,
                    p.CreatedAt,
                    p.UpdatedAt,
                    p.StripePaymentIntentId
                })
                .ToListAsync();

            // Enrich with lease info
            var leaseIds = items.Select(p => p.LeaseId).Distinct().ToList();
            var leases = await db.Leases
                .Where(l => leaseIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id);

            var result = items.Select(p => new
            {
                p.Id,
                p.LeaseId,
                p.TenantId,
                p.LandlordId,
                p.Amount,
                p.FailureReason,
                p.RetryAttempt,
                p.CreatedAt,
                p.UpdatedAt,
                p.StripePaymentIntentId,
                LeaseInfo = leases.TryGetValue(p.LeaseId, out var lease) ? new
                {
                    lease.PropertyId,
                    lease.MonthlyRent,
                    lease.Status,
                    lease.StartDate,
                    lease.EndDate
                } : null,
                IsExhausted = p.RetryAttempt >= 3
            });

            return Results.Ok(new { totalCount = total, page, pageSize, items = result });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsRead));

        // POST /api/admin/payments/{id}/retry — Admin-triggered retry of failed monthly rent payment
        payments.MapPost("/{id:guid}/retry", async (Guid id, LeasingDbContext db, AdminDbContext adminDb, IStripePaymentService paymentService, HttpContext ctx) =>
        {
            var payment = await db.Payments.FindAsync(id);
            if (payment == null) return Results.NotFound();
            if (payment.Type != PaymentType.MonthlyRent) return Results.BadRequest("Only monthly rent payments can be retried.");
            if (payment.Status != PaymentStatus.Failed) return Results.BadRequest("Only failed payments can be retried.");

            var nextAttempt = payment.RetryAttempt + 1;
            var customAmount = payment.RentAmount > 0 ? (decimal?)payment.RentAmount : null;

            try
            {
                var result = await paymentService.CreateMonthlyRentPaymentAsync(
                    payment.LeaseId, payment.TenantId, payment.CreatedAt.Date, nextAttempt, customAmount);

                // Update the original failed payment's RetryAttempt so the failed-payments list
                // reflects the correct attempt count and exhaustion status.
                payment.RetryAttempt = nextAttempt;
                payment.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();

                adminDb.PaymentOperations.Add(new PaymentOperation
                {
                    Id = Guid.NewGuid(),
                    PaymentId = payment.Id,
                    LeaseId = payment.LeaseId,
                    AdminUserId = GetAdminId(ctx),
                    OperationType = AdminPaymentOperationType.Retry,
                    Amount = payment.Amount,
                    Currency = "EUR",
                    IdempotencyKey = $"retry-{payment.Id}-{Guid.NewGuid():n}",
                    Status = AdminPaymentOperationStatus.Succeeded,
                    CreatedAt = DateTime.UtcNow
                });
                await adminDb.SaveChangesAsync();

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(ex.Message);
            }
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsManualCharge));

        // GET /api/admin/payments/{id}/operations — Admin operations audit trail
        payments.MapGet("/{id:guid}/operations", async (Guid id, AdminDbContext adminDb) =>
        {
            var operations = await adminDb.PaymentOperations
                .Where(op => op.PaymentId == id)
                .OrderByDescending(op => op.CreatedAt)
                .Select(op => new
                {
                    op.Id,
                    op.PaymentId,
                    op.AdminUserId,
                    OperationType = op.OperationType.ToString(),
                    op.Amount,
                    op.Reason,
                    op.CreatedAt,
                    op.Status,
                    op.Error
                })
                .ToListAsync();

            return Results.Ok(operations);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsRead));

        // GET /api/admin/payments/{id}/scheduled-retries — Scheduled Hangfire retries for this payment
        payments.MapGet("/{id:guid}/scheduled-retries", async (Guid id, LeasingDbContext db, HttpContext ctx) =>
        {
            var payment = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (payment == null) return Results.NotFound();

            var monitoring = ctx.RequestServices.GetRequiredService<Hangfire.Storage.IMonitoringApi>();
            var scheduledJobs = monitoring.ScheduledJobs(0, 1000);
            var matchingJobs = scheduledJobs
                .Where(j => j.Value.Job.Method.Name == "RetryRentAsync"
                    && j.Value.Job.Args.Count >= 4
                    && j.Value.Job.Args[0]?.ToString() == payment.LeaseId.ToString())
                .Select(j => new
                {
                    jobId = j.Key,
                    scheduledAt = j.Value.EnqueueAt,
                    methodName = j.Value.Job.Method.Name,
                })
                .ToList();

            return Results.Ok(matchingJobs);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsRead));

        // DELETE /api/admin/payments/{id}/scheduled-retries — Cancel scheduled retries for this payment
        payments.MapDelete("/{id:guid}/scheduled-retries", async (Guid id, LeasingDbContext db, HttpContext ctx) =>
        {
            var payment = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (payment == null) return Results.NotFound();

            var monitoring = ctx.RequestServices.GetRequiredService<Hangfire.Storage.IMonitoringApi>();
            var backgroundJobClient = ctx.RequestServices.GetRequiredService<IBackgroundJobClient>();

            var scheduledJobs = monitoring.ScheduledJobs(0, 1000);
            var matchingJobs = scheduledJobs
                .Where(j => j.Value.Job.Method.Name == "RetryRentAsync"
                    && j.Value.Job.Args.Count >= 4
                    && j.Value.Job.Args[0]?.ToString() == payment.LeaseId.ToString())
                .ToList();

            foreach (var job in matchingJobs)
            {
                backgroundJobClient.Delete(job.Key);
            }

            return Results.Ok(new { deleted = matchingJobs.Count });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsManualCharge));

        // POST /api/admin/payments/{id}/resend-notification — Resend payment notification to tenant and landlord
        payments.MapPost("/{id:guid}/resend-notification", async (Guid id, LeasingDbContext db, INotificationService notification, IConfiguration config) =>
        {
            var payment = await db.Payments.FindAsync(id);
            if (payment == null) return Results.NotFound();

            var amount = payment.Amount.ToString("F2", System.Globalization.CultureInfo.GetCultureInfo("pt-PT")) + " €";
            var frontendUrl = config["Frontend:BaseUrl"] ?? "http://localhost:5173";
            var paymentUrl = $"{frontendUrl?.TrimEnd('/')}/payments/{payment.Id}";

            var message = payment.Status switch
            {
                TrustRent.Modules.Leasing.Models.PaymentStatus.Succeeded => $"Confirmação: pagamento de {amount} confirmado. Detalhes em: {paymentUrl}",
                TrustRent.Modules.Leasing.Models.PaymentStatus.Failed => $"Aviso: pagamento de {amount} falhou. Detalhes em: {paymentUrl}",
                TrustRent.Modules.Leasing.Models.PaymentStatus.Pending => $"Aviso: pagamento de {amount} requer autenticação. Detalhes em: {paymentUrl}",
                _ => $"Atualização do pagamento de {amount}. Detalhes em: {paymentUrl}"
            };

            await notification.SendNotificationAsync(payment.TenantId, "payment", message, payment.Id);
            await notification.SendNotificationAsync(payment.LandlordId, "payment", message, payment.Id);

            return Results.Ok(new { sent = true });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsRead));

        // GET /api/admin/payments/methods/expiring — Payment methods expiring in next 60 days
        payments.MapGet("/methods/expiring", async (LeasingDbContext db) =>
        {
            var now = DateTime.UtcNow;
            var twoMonthsLater = now.AddMonths(2);

            var methods = await db.TenantPaymentMethods
                .Where(pm => pm.CardExpMonth.HasValue && pm.CardExpYear.HasValue)
                .ToListAsync();

            var expiring = methods
                .Where(pm =>
                {
                    var expiryDate = new DateTime(pm.CardExpYear!.Value, pm.CardExpMonth!.Value, 1).AddMonths(1).AddDays(-1);
                    return expiryDate >= now && expiryDate <= twoMonthsLater;
                })
                .Select(pm => new
                {
                    pm.Id,
                    pm.UserId,
                    pm.DisplayName,
                    pm.CardBrand,
                    pm.CardLast4,
                    pm.CardExpMonth,
                    pm.CardExpYear,
                    pm.IsDefault,
                    ExpiryDate = new DateTime(pm.CardExpYear!.Value, pm.CardExpMonth!.Value, 1).AddMonths(1).AddDays(-1)
                })
                .ToList();

            return Results.Ok(new { total = expiring.Count, items = expiring });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsRead));

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

        // GET /api/admin/stripe/accounts/unhealthy — Stripe accounts with issues
        stripeAccounts.MapGet("/unhealthy", async (LeasingDbContext db) =>
        {
            var accounts = await db.StripeAccounts
                .Where(a => !a.ChargesEnabled || !a.PayoutsEnabled || !a.IsOnboardingComplete)
                .OrderBy(a => a.CreatedAt)
                .Select(a => new
                {
                    a.Id,
                    a.UserId,
                    a.StripeAccountId,
                    a.ChargesEnabled,
                    a.PayoutsEnabled,
                    a.IsOnboardingComplete,
                    a.IsDefault,
                    a.PropertyId,
                    a.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(new { total = accounts.Count, items = accounts });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsManageStripeAccounts));

        // ===== Users =====
        var users = app.MapGroup("/api/admin/users");

        // GET /api/admin/users/{userId}/payment-methods — User's saved payment methods
        users.MapGet("/{userId:guid}/payment-methods", async (Guid userId, LeasingDbContext db) =>
        {
            var methods = await db.TenantPaymentMethods
                .Where(pm => pm.UserId == userId)
                .OrderByDescending(pm => pm.IsDefault)
                .ThenByDescending(pm => pm.CreatedAt)
                .Select(pm => new
                {
                    pm.Id,
                    pm.UserId,
                    pm.Type,
                    pm.DisplayName,
                    pm.CardBrand,
                    pm.CardLast4,
                    pm.CardExpMonth,
                    pm.CardExpYear,
                    pm.IsDefault,
                    pm.CreatedAt
                })
                .ToListAsync();

            return Results.Ok(methods);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsRead));

        // ===== Webhooks =====
        var webhooks = app.MapGroup("/api/admin/webhooks");

        // GET /api/admin/webhooks — Paginated list of webhook events, filterable by type and date
        webhooks.MapGet("/", async ([FromQuery] string? type, [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] int page, [FromQuery] int pageSize, AdminDbContext adminDb) =>
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 25 : (pageSize > 200 ? 200 : pageSize);

            var query = adminDb.WebhookEvents.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(w => w.EventType == type);
            if (from.HasValue)
                query = query.Where(w => w.CreatedAt >= from.Value);
            if (to.HasValue)
                query = query.Where(w => w.CreatedAt <= to.Value);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(w => w.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(w => new
                {
                    w.Id,
                    w.StripeEventId,
                    w.EventType,
                    w.Status,
                    w.Error,
                    w.PayloadSummary,
                    w.CreatedAt,
                    w.ProcessedAt
                })
                .ToListAsync();

            return Results.Ok(new { items, page, pageSize, totalCount = total });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsRead));

        // POST /api/admin/webhooks/{id}/replay — Replay a specific webhook event
        webhooks.MapPost("/{id:guid}/replay", async (Guid id, AdminDbContext adminDb, IStripePaymentService paymentService, IStripeAccountService accountService) =>
        {
            var webhookEvent = await adminDb.WebhookEvents.FindAsync(id);
            if (webhookEvent == null) return Results.NotFound();

            webhookEvent.Status = "processing";
            await adminDb.SaveChangesAsync();

            try
            {
                var objectId = webhookEvent.PayloadSummary ?? string.Empty;

                switch (webhookEvent.EventType)
                {
                    case EventTypes.PaymentIntentSucceeded:
                        await paymentService.HandlePaymentSucceededAsync(objectId);
                        break;
                    case EventTypes.PaymentIntentPaymentFailed:
                        await paymentService.HandlePaymentFailedAsync(objectId, null);
                        break;
                    case EventTypes.PaymentIntentRequiresAction:
                        await paymentService.HandlePaymentRequiresActionAsync(objectId);
                        break;
                    case EventTypes.AccountUpdated:
                        await accountService.HandleAccountUpdatedWebhookAsync(objectId);
                        break;
                    case EventTypes.PaymentIntentProcessing:
                        await paymentService.HandlePaymentProcessingAsync(objectId);
                        break;
                    case EventTypes.PaymentIntentCanceled:
                        await paymentService.HandlePaymentCanceledAsync(objectId);
                        break;
                    case EventTypes.ChargeRefunded:
                        await paymentService.HandleChargeRefundedAsync(objectId, 0);
                        break;
                    default:
                        webhookEvent.Status = "failed";
                        webhookEvent.Error = $"Tipo de evento desconhecido: {webhookEvent.EventType}";
                        await adminDb.SaveChangesAsync();
                        return Results.BadRequest(new { error = webhookEvent.Error });
                }

                webhookEvent.Status = "processed";
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                await adminDb.SaveChangesAsync();

                return Results.Ok(new { id = webhookEvent.Id, status = webhookEvent.Status });
            }
            catch (Exception ex)
            {
                webhookEvent.Status = "failed";
                webhookEvent.Error = ex.Message;
                await adminDb.SaveChangesAsync();

                return Results.Ok(new { id = webhookEvent.Id, status = webhookEvent.Status, error = ex.Message });
            }
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PaymentsManualCharge));
    }
}
