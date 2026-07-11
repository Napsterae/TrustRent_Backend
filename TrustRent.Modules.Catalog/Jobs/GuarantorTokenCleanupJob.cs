using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Shared.Models;

namespace TrustRent.Modules.Catalog.Jobs;

public class GuarantorTokenCleanupJob
{
    private readonly CatalogDbContext _db;
    private readonly ILogger<GuarantorTokenCleanupJob> _logger;

    public GuarantorTokenCleanupJob(CatalogDbContext db, ILogger<GuarantorTokenCleanupJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RunCleanupAsync()
    {
        var now = DateTime.UtcNow;

        // Nullify tokens for guarantors who have submitted data or whose invite has expired
        var submittedGuarantors = await _db.Guarantors
            .Where(g => g.GuestAccessToken != null
                        && g.GuestAccessToken != string.Empty
                        && (g.InviteStatus == GuarantorInviteStatus.Accepted
                            || g.ExpiresAt < now))
            .ToListAsync();

        foreach (var g in submittedGuarantors)
        {
            g.GuestAccessToken = string.Empty;
        }

        if (submittedGuarantors.Count > 0)
        {
            _logger.LogInformation("Nullified {Count} guarantor access tokens (data submitted or expired)", submittedGuarantors.Count);
        }

        await _db.SaveChangesAsync();
    }
}
