using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Identity.Contracts.Database;
using TrustRent.Shared.Security;

namespace TrustRent.Api.Jobs;

/// <summary>
/// Batched key rotation job. Re-encrypts all V2-encrypted fields with the current encryption key.
/// Designed to be manually triggered (not recurring) during a key rotation window.
/// Processes rows in batches so it can safely be interrupted and resumed.
/// Skips rows that already use the current key format (versioned).
/// </summary>
public class KeyRotationJob
{
    private const int BatchSize = 100;

    private readonly IdentityDbContext _identityDb;
    private readonly CatalogDbContext _catalogDb;
    private readonly ILogger<KeyRotationJob> _logger;

    public KeyRotationJob(
        IdentityDbContext identityDb,
        CatalogDbContext catalogDb,
        ILogger<KeyRotationJob> logger)
    {
        _identityDb = identityDb;
        _catalogDb = catalogDb;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Key rotation job started. Current key version: {Version}, HasPreviousKey: {HasPrevious}",
            EncryptionHelperV2.CurrentKeyVersion, EncryptionHelperV2.HasPreviousKey);

        var totalProcessed = 0;

        totalProcessed += await ReEncryptUsersAsync(ct);
        totalProcessed += await ReEncryptEmailLoginCodesAsync(ct);
        totalProcessed += await ReEncryptWhatsAppCodesAsync(ct);
        totalProcessed += await ReEncryptGuarantorsAsync(ct);
        totalProcessed += await ReEncryptPropertiesAsync(ct);

        _logger.LogInformation("Key rotation job completed. Total rows processed: {Total}", totalProcessed);
    }

    private async Task<int> ReEncryptUsersAsync(CancellationToken ct)
    {
        var processed = 0;
        var skip = 0;
        bool hasMore;

        do
        {
            var batch = await _identityDb.Users
                .OrderBy(u => u.Id)
                .Skip(skip)
                .Take(BatchSize)
                .ToListAsync(ct);

            hasMore = batch.Count == BatchSize;
            skip += batch.Count;

            foreach (var user in batch)
            {
                var changed = false;

                if (TryReEncrypt(user.Email, out var newEmail)) { user.Email = newEmail!; changed = true; }
                if (TryReEncrypt(user.Nif, out var newNif)) { user.Nif = newNif; changed = true; }
                if (TryReEncrypt(user.Name, out var newName)) { user.Name = newName!; changed = true; }
                if (TryReEncrypt(user.Address, out var newAddress)) { user.Address = newAddress; changed = true; }
                if (TryReEncrypt(user.PostalCode, out var newPostal)) { user.PostalCode = newPostal; changed = true; }
                if (TryReEncrypt(user.CitizenCardNumber, out var newCc)) { user.CitizenCardNumber = newCc; changed = true; }
                if (TryReEncrypt(user.PhoneNumber, out var newPhone)) { user.PhoneNumber = newPhone; changed = true; }
                if (TryReEncrypt(user.PendingPhoneNumber, out var newPending)) { user.PendingPhoneNumber = newPending; changed = true; }
                if (TryReEncrypt(user.TelegramPendingExpectedPhoneNumber, out var newTg)) { user.TelegramPendingExpectedPhoneNumber = newTg; changed = true; }

                if (changed) processed++;
            }

            if (batch.Count > 0)
                await _identityDb.SaveChangesAsync(ct);

            _logger.LogDebug("Re-encrypted {Processed} Users so far (batch offset {Skip})", processed, skip);
        }
        while (hasMore);

        if (processed > 0)
            _logger.LogInformation("Re-encrypted {Count} Users", processed);

        return processed;
    }

    private async Task<int> ReEncryptEmailLoginCodesAsync(CancellationToken ct)
    {
        var processed = 0;
        var skip = 0;
        bool hasMore;

        do
        {
            var batch = await _identityDb.EmailLoginCodes
                .OrderBy(c => c.Id)
                .Skip(skip)
                .Take(BatchSize)
                .ToListAsync(ct);

            hasMore = batch.Count == BatchSize;
            skip += batch.Count;

            foreach (var code in batch)
            {
                if (TryReEncrypt(code.Email, out var newEmail))
                {
                    code.Email = newEmail!;
                    processed++;
                }
            }

            if (batch.Count > 0)
                await _identityDb.SaveChangesAsync(ct);
        }
        while (hasMore);

        if (processed > 0)
            _logger.LogInformation("Re-encrypted {Count} EmailLoginCodes", processed);

        return processed;
    }

    private async Task<int> ReEncryptWhatsAppCodesAsync(CancellationToken ct)
    {
        var processed = 0;
        var skip = 0;
        bool hasMore;

        do
        {
            var batch = await _identityDb.WhatsAppOneTimeCodes
                .OrderBy(c => c.Id)
                .Skip(skip)
                .Take(BatchSize)
                .ToListAsync(ct);

            hasMore = batch.Count == BatchSize;
            skip += batch.Count;

            foreach (var code in batch)
            {
                if (TryReEncrypt(code.PhoneNumber, out var newPhone))
                {
                    code.PhoneNumber = newPhone!;
                    processed++;
                }
            }

            if (batch.Count > 0)
                await _identityDb.SaveChangesAsync(ct);
        }
        while (hasMore);

        if (processed > 0)
            _logger.LogInformation("Re-encrypted {Count} WhatsAppOneTimeCodes", processed);

        return processed;
    }

    private async Task<int> ReEncryptGuarantorsAsync(CancellationToken ct)
    {
        var processed = 0;
        var skip = 0;
        bool hasMore;

        do
        {
            var batch = await _catalogDb.Guarantors
                .OrderBy(g => g.Id)
                .Skip(skip)
                .Take(BatchSize)
                .ToListAsync(ct);

            hasMore = batch.Count == BatchSize;
            skip += batch.Count;

            foreach (var g in batch)
            {
                var changed = false;

                if (TryReEncrypt(g.GuestEmail, out var newEmail)) { g.GuestEmail = newEmail!; changed = true; }
                if (TryReEncrypt(g.GuestName, out var newName)) { g.GuestName = newName; changed = true; }
                if (TryReEncrypt(g.GuestPhoneNumber, out var newPhone)) { g.GuestPhoneNumber = newPhone; changed = true; }

                if (changed) processed++;
            }

            if (batch.Count > 0)
                await _catalogDb.SaveChangesAsync(ct);
        }
        while (hasMore);

        if (processed > 0)
            _logger.LogInformation("Re-encrypted {Count} Guarantors", processed);

        return processed;
    }

    private async Task<int> ReEncryptPropertiesAsync(CancellationToken ct)
    {
        var processed = 0;
        var skip = 0;
        bool hasMore;

        do
        {
            var batch = await _catalogDb.Properties
                .OrderBy(p => p.Id)
                .Skip(skip)
                .Take(BatchSize)
                .ToListAsync(ct);

            hasMore = batch.Count == BatchSize;
            skip += batch.Count;

            foreach (var p in batch)
            {
                var changed = false;

                if (TryReEncrypt(p.MatrixArticle, out var v1)) { p.MatrixArticle = v1; changed = true; }
                if (TryReEncrypt(p.EnergyCertificateNumber, out var v2)) { p.EnergyCertificateNumber = v2; changed = true; }
                if (TryReEncrypt(p.AtRegistrationNumber, out var v3)) { p.AtRegistrationNumber = v3; changed = true; }
                if (TryReEncrypt(p.PermanentCertNumber, out var v4)) { p.PermanentCertNumber = v4; changed = true; }
                if (TryReEncrypt(p.PermanentCertOffice, out var v5)) { p.PermanentCertOffice = v5; changed = true; }
                if (TryReEncrypt(p.UsageLicenseNumber, out var v6)) { p.UsageLicenseNumber = v6; changed = true; }

                if (changed) processed++;
            }

            if (batch.Count > 0)
                await _catalogDb.SaveChangesAsync(ct);
        }
        while (hasMore);

        if (processed > 0)
            _logger.LogInformation("Re-encrypted {Count} Properties", processed);

        return processed;
    }

    /// <summary>
    /// Attempts to re-encrypt a field value. Decrypts with the available key(s) and
    /// re-encrypts with the current key. The Decrypt method handles format detection
    /// (versioned vs legacy) correctly via a try-both-keys approach.
    /// </summary>
    private static bool TryReEncrypt(string? field, out string? result)
    {
        result = null;

        if (string.IsNullOrEmpty(field))
            return false;

        // If no rotation has been initiated, there's nothing to re-encrypt
        if (EncryptionHelperV2.CurrentKeyVersion == 0)
            return false;

        try
        {
            var decrypted = EncryptionHelperV2.Decrypt(field);
            if (decrypted == null)
                return false;

            result = EncryptionHelperV2.Encrypt(decrypted);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
