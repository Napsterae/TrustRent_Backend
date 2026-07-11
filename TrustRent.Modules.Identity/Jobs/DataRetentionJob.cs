using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrustRent.Modules.Identity.Contracts.Database;

namespace TrustRent.Modules.Identity.Jobs;

public class DataRetentionJob
{
    private readonly IdentityDbContext _db;
    private readonly ILogger<DataRetentionJob> _logger;

    public DataRetentionJob(IdentityDbContext db, ILogger<DataRetentionJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task RunCleanupAsync()
    {
        _logger.LogInformation("Starting data retention cleanup job");
        var now = DateTime.UtcNow;

        // 1. Purge expired email login codes (30 days after expiry)
        var emailCutoff = now.AddDays(-30);
        var expiredEmailCodes = await _db.EmailLoginCodes
            .Where(x => x.ExpiresAt < emailCutoff)
            .ToListAsync();
        if (expiredEmailCodes.Count > 0)
        {
            _db.EmailLoginCodes.RemoveRange(expiredEmailCodes);
            _logger.LogInformation("Purging {Count} expired email login codes", expiredEmailCodes.Count);
        }

        // 2. Purge expired WhatsApp one-time codes (30 days after expiry)
        var expiredWhatsAppCodes = await _db.WhatsAppOneTimeCodes
            .Where(x => x.ExpiresAt < emailCutoff)
            .ToListAsync();
        if (expiredWhatsAppCodes.Count > 0)
        {
            _db.WhatsAppOneTimeCodes.RemoveRange(expiredWhatsAppCodes);
            _logger.LogInformation("Purging {Count} expired WhatsApp codes", expiredWhatsAppCodes.Count);
        }

        // 3. Clear expired Telegram pending verification data (7 days after expiry)
        var telegramCutoff = now.AddDays(-7);
        var usersWithExpiredTelegram = await _db.Users
            .Where(u => u.TelegramPendingVerificationExpiresAt.HasValue
                        && u.TelegramPendingVerificationExpiresAt < telegramCutoff
                        && (u.TelegramPendingVerificationToken != null
                            || u.TelegramPendingExpectedPhoneNumber != null
                            || u.TelegramPendingVerificationError != null))
            .ToListAsync();

        foreach (var user in usersWithExpiredTelegram)
        {
            user.TelegramPendingVerificationToken = null;
            user.TelegramPendingExpectedPhoneNumber = null;
            user.TelegramPendingVerificationError = null;
            user.TelegramPendingVerificationExpiresAt = null;
        }
        if (usersWithExpiredTelegram.Count > 0)
        {
            _logger.LogInformation("Clearing expired Telegram verification data for {Count} users", usersWithExpiredTelegram.Count);
        }

        // 4. Clear pending phone numbers that have been verified or are stale (30 days old)
        var stalePhoneCutoff = now.AddDays(-30);
        var usersWithStalePendingPhone = await _db.Users
            .Where(u => u.PendingPhoneNumber != null
                        && u.PhoneNumberVerifiedAt.HasValue
                        && u.PhoneNumberVerifiedAt < stalePhoneCutoff)
            .ToListAsync();

        foreach (var user in usersWithStalePendingPhone)
        {
            user.PendingPhoneNumber = null;
            user.PendingPhoneCountryCode = null;
            user.PendingPhoneContactPlatform = null;
        }
        if (usersWithStalePendingPhone.Count > 0)
        {
            _logger.LogInformation("Clearing stale pending phone data for {Count} users", usersWithStalePendingPhone.Count);
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Data retention cleanup job completed");
    }
}
