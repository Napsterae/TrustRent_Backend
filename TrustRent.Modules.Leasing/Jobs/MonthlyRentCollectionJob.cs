using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Shared.Models;

namespace TrustRent.Modules.Leasing.Jobs;

public interface IMonthlyRentCollectionJob
{
    Task ExecuteAsync();
    Task RetryRentAsync(Guid leaseId, Guid tenantId, DateTime billingPeriod, int attempt, decimal? customAmount = null);
}

public class MonthlyRentCollectionJob : IMonthlyRentCollectionJob
{
    private readonly LeasingDbContext _db;
    private readonly IStripePaymentService _paymentService;
    private readonly ILogger<MonthlyRentCollectionJob> _logger;

    public MonthlyRentCollectionJob(
        LeasingDbContext db,
        IStripePaymentService paymentService,
        ILogger<MonthlyRentCollectionJob> logger)
    {
        _db = db;
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var today = DateTime.UtcNow.Date;
        _logger.LogInformation("Starting monthly rent collection for {Date}", today);

        // Collect rent on the same day-of-month as lease start date.
        // Handle months with fewer days: a lease starting on the 31st collects on the
        // last day of shorter months (e.g. Feb 28/29, Apr 30).
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);

        var dueLeases = await _db.Leases
            .Where(l => l.Status == LeaseStatus.Active
                && l.EndDate > today)
            .ToListAsync();

        // Filter in-memory for the day-of-month match with edge-case handling
        var dueToday = dueLeases.Where(l =>
        {
            var effectiveDay = Math.Min(l.StartDate.Day, daysInMonth);
            return effectiveDay == today.Day;
        }).ToList();

        _logger.LogInformation("Found {Count} leases due for rent collection (of {Total} active leases)",
            dueToday.Count, dueLeases.Count);

        foreach (var lease in dueToday)
        {
            try
            {
                if (lease.TenantSharePercentage.HasValue && lease.CoTenantId.HasValue)
                {
                    // Split payment: charge each tenant their share
                    var tenantAmount = lease.MonthlyRent * (lease.TenantSharePercentage.Value / 100m);
                    var coTenantAmount = lease.MonthlyRent - tenantAmount;
                    await _paymentService.CreateMonthlyRentPaymentAsync(lease.Id, lease.TenantId, today, 0, tenantAmount);
                    await _paymentService.CreateMonthlyRentPaymentAsync(lease.Id, lease.CoTenantId.Value, today, 0, coTenantAmount);
                }
                else
                {
                    await _paymentService.CreateMonthlyRentPaymentAsync(lease.Id, lease.TenantId, today);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect rent for lease {LeaseId}", lease.Id);
            }
        }

        _logger.LogInformation("Monthly rent collection completed");
    }

    /// <summary>
    /// Retry a failed monthly rent payment. Called by Hangfire after a scheduled retry delay.
    /// Passes the attempt number so CreateMonthlyRentPaymentAsync generates a distinct
    /// idempotency key (e.g. rent_..._r1) — each retry creates a fresh PaymentIntent.
    /// The billing-period-wide success check prevents duplicate charges if a prior attempt
    /// already succeeded.
    /// </summary>
    public async Task RetryRentAsync(Guid leaseId, Guid tenantId, DateTime billingPeriod, int attempt, decimal? customAmount = null)
    {
        _logger.LogInformation("Retrying monthly rent collection for lease {LeaseId} (attempt {Attempt}, period {Period})",
            leaseId, attempt, billingPeriod.ToString("yyyy-MM"));

        try
        {
            var result = await _paymentService.CreateMonthlyRentPaymentAsync(leaseId, tenantId, billingPeriod, attempt, customAmount);
            if (result != null)
            {
                _logger.LogInformation("Retry {Attempt} succeeded for lease {LeaseId}", attempt, leaseId);
            }
            else
            {
                _logger.LogInformation("Retry {Attempt} skipped for lease {LeaseId} — payment already succeeded or not needed", attempt, leaseId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Retry {Attempt} failed for lease {LeaseId}", attempt, leaseId);
            // If the Stripe payment fails, the HandlePaymentFailedAsync webhook handler
            // will schedule the next retry. If CreateMonthlyRentPaymentAsync itself throws
            // (e.g., no payment method), the retry chain ends — the tenant must update
            // their payment method and pay on demand.
        }
    }
}
