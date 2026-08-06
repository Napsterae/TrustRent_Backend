using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace TrustRent.Shared.Security;

/// <summary>
/// V2 encryption helper using AES-256-GCM (authenticated encryption) with per-record random nonces.
/// Supports key versioning for rotation:
///   - Version 0 = original key, no version byte prefix (backward-compatible with Phases 1-3 data).
///   - Version 1+ = rotated keys, 1-byte version prefix (0x01, 0x02, ...) before nonce.
/// Also provides HMAC-SHA256 blind indexes for equality/unique-constraint lookups.
///
/// Config keys:
///   "Encryption:V2:DataKey"          — current data key (Base64 32 bytes)
///   "Encryption:V2:DataKeyPrevious"  — previous data key (Base64 32 bytes, optional)
///   "Encryption:V2:BlindIndexKey"    — blind index key (Base64 32 bytes, never rotated)
/// </summary>
public static class EncryptionHelperV2
{
    private static byte[]? _dataKey;
    private static byte[]? _dataKeyPrevious;
    private static byte[]? _blindIndexKey;

    /// <summary>
    /// Current key version:
    ///   0 = original key (no version prefix in ciphertext)
    ///   1 = first rotation (prefix 0x01)
    /// </summary>
    public static int CurrentKeyVersion { get; private set; }

    /// <summary>
    /// True when a previous key is configured (rotation window is open).
    /// </summary>
    public static bool HasPreviousKey => _dataKeyPrevious != null;

    /// <summary>
    /// Must be called at startup to initialize encryption keys from configuration.
    /// DataKey: 32 bytes (256 bits) for AES-256-GCM.
    /// DataKeyPrevious: 32 bytes, optional — set during key rotation window.
    /// BlindIndexKey: 32 bytes (256 bits) for HMAC-SHA256 blind indexes.
    /// All keys must be provided as Base64-encoded strings.
    /// </summary>
    public static void Initialize(IConfiguration configuration)
    {
        var dataKeyBase64 = configuration["Encryption:V2:DataKey"];
        var dataKeyPreviousBase64 = configuration["Encryption:V2:DataKeyPrevious"];
        var blindIndexKeyBase64 = configuration["Encryption:V2:BlindIndexKey"];

        if (string.IsNullOrWhiteSpace(dataKeyBase64))
            throw new InvalidOperationException("Encryption:V2:DataKey must be set in configuration (Base64-encoded 32-byte key).");
        if (string.IsNullOrWhiteSpace(blindIndexKeyBase64))
            throw new InvalidOperationException("Encryption:V2:BlindIndexKey must be set in configuration (Base64-encoded 32-byte key).");

        _dataKey = Convert.FromBase64String(dataKeyBase64);
        _blindIndexKey = Convert.FromBase64String(blindIndexKeyBase64);

        if (_dataKey.Length != 32)
            throw new InvalidOperationException("Encryption:V2:DataKey must be exactly 32 bytes (256 bits) when Base64-decoded.");
        if (_blindIndexKey.Length != 32)
            throw new InvalidOperationException("Encryption:V2:BlindIndexKey must be exactly 32 bytes (256 bits) when Base64-decoded.");

        if (!string.IsNullOrWhiteSpace(dataKeyPreviousBase64))
        {
            _dataKeyPrevious = Convert.FromBase64String(dataKeyPreviousBase64);
            if (_dataKeyPrevious.Length != 32)
                throw new InvalidOperationException("Encryption:V2:DataKeyPrevious must be exactly 32 bytes (256 bits) when Base64-decoded.");
            CurrentKeyVersion = 1;
        }
        else
        {
            _dataKeyPrevious = null;
            CurrentKeyVersion = 0;
        }
    }

    /// <summary>
    /// Encrypts plaintext using AES-256-GCM.
    /// When CurrentKeyVersion is 0 (original key), produces legacy format (no version prefix).
    /// When CurrentKeyVersion &gt;= 1, prepends a 1-byte version prefix.
    /// </summary>
    public static string Encrypt(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText ?? string.Empty;
        EnsureInitialized();

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plainBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

        using var aesGcm = new AesGcm(_dataKey!);
        aesGcm.Encrypt(nonce, plainBytes, ciphertext, tag);

        if (CurrentKeyVersion == 0)
        {
            // Legacy format (backward compatible with Phases 1-3): nonce + ciphertext + tag
            var combined = new byte[nonce.Length + ciphertext.Length + tag.Length];
            Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
            Buffer.BlockCopy(ciphertext, 0, combined, nonce.Length, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, combined, nonce.Length + ciphertext.Length, tag.Length);
            return Convert.ToBase64String(combined);
        }

        // Versioned format: version(1) + nonce + ciphertext + tag
        var versioned = new byte[1 + nonce.Length + ciphertext.Length + tag.Length];
        versioned[0] = (byte)CurrentKeyVersion;
        Buffer.BlockCopy(nonce, 0, versioned, 1, nonce.Length);
        Buffer.BlockCopy(ciphertext, 0, versioned, 1 + nonce.Length, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, versioned, 1 + nonce.Length + ciphertext.Length, tag.Length);
        return Convert.ToBase64String(versioned);
    }

    /// <summary>
    /// Decrypts a payload that may be legacy (no version prefix, Phases 1-3)
    /// or versioned (1-byte version prefix, post-rotation).
    /// Uses try-both-keys approach to avoid false positives from the first-byte heuristic.
    /// </summary>
    public static string? Decrypt(string? cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;
        EnsureInitialized();

        var combined = Convert.FromBase64String(cipherText);
        var nonceSize = AesGcm.NonceByteSizes.MaxSize; // 12
        var tagSize = AesGcm.TagByteSizes.MaxSize;     // 16

        // Invariant: versioned payloads (1-byte version prefix, CurrentKeyVersion >= 1) can
        // only exist after a key rotation, and a rotation is only possible when DataKeyPrevious
        // was configured (Initialize sets CurrentKeyVersion = 0 when it is absent). With no
        // previous key, Encrypt only ever produces the legacy format (nonce + ciphertext + tag),
        // so every payload must be treated as legacy (offset 0, _dataKey) — the first-byte
        // heuristic is skipped entirely. This is required because a legacy random nonce may
        // legitimately start with 0x01; misclassifying it as versioned made ~1/256 of rows
        // permanently undecryptable (first try fails, no second key to fall back to).
        if (_dataKeyPrevious == null)
        {
            return DecryptWithKey(combined, 0, _dataKey ?? throw new CryptographicException("No key available for legacy ciphertext."));
        }

        var minVersionedLength = 1 + nonceSize + tagSize; // 29 bytes
        var looksVersioned = combined.Length >= minVersionedLength && combined[0] == 0x01;

        // Determine which key/offset combination to try first, then try the fallback.
        // This avoids the 1/256 false-positive rate of the heuristic when a legacy
        // nonce byte happens to be 0x01.
        (byte[] key, int offset) firstTry;
        (byte[] key, int offset)? secondTry = null;

        if (looksVersioned)
        {
            // Heuristic says versioned (0x01 prefix). Try current key with offset=1 first.
            firstTry = (_dataKey ?? _dataKeyPrevious ?? throw new CryptographicException("No key available for versioned ciphertext."), 1);
            // Fallback: legacy format with the previous key (the original key before rotation).
            if (_dataKeyPrevious != null)
                secondTry = (_dataKeyPrevious, 0);
        }
        else
        {
            // Heuristic says legacy (no version prefix), or payload too short for versioning.
            // During rotation window the legacy data was encrypted with the previous key.
            // After rotation (previous key removed) the only key is the current key.
            firstTry = (_dataKeyPrevious ?? _dataKey ?? throw new CryptographicException("No key available for legacy ciphertext."), 0);
            // Fallback: versioned format with the current key (if previous key was set and firstTry fails).
            if (_dataKeyPrevious != null && _dataKey != null)
                secondTry = (_dataKey, 1);
        }

        // Try the first choice
        try
        {
            return DecryptWithKey(combined, firstTry.offset, firstTry.key);
        }
        catch (CryptographicException) when (secondTry.HasValue)
        {
            // First attempt failed and we have a fallback — try it
        }

        // Try the fallback
        try
        {
            return DecryptWithKey(combined, secondTry!.Value.offset, secondTry!.Value.key);
        }
        catch (CryptographicException)
        {
            throw new CryptographicException("Decryption failed with both available keys. The data may be corrupt or encrypted with a key that is no longer available.");
        }
    }

    /// <summary>
    /// Decrypts raw combined bytes (nonce + ciphertext + tag) starting at the given offset
    /// using the specified AES-256-GCM key.
    /// </summary>
    private static string DecryptWithKey(byte[] combined, int offset, byte[] key)
    {
        var nonceSize = AesGcm.NonceByteSizes.MaxSize; // 12
        var tagSize = AesGcm.TagByteSizes.MaxSize;     // 16

        if (combined.Length - offset < nonceSize + tagSize)
            throw new CryptographicException("Ciphertext payload is too short to contain nonce + tag.");

        var nonce = new byte[nonceSize];
        var tag = new byte[tagSize];
        var ciphertext = new byte[combined.Length - offset - nonceSize - tagSize];

        Buffer.BlockCopy(combined, offset, nonce, 0, nonceSize);
        Buffer.BlockCopy(combined, offset + nonceSize, ciphertext, 0, ciphertext.Length);
        Buffer.BlockCopy(combined, offset + nonceSize + ciphertext.Length, tag, 0, tagSize);

        var plainBytes = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(key);
        aesGcm.Decrypt(nonce, ciphertext, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// Computes a deterministic blind index (HMAC-SHA256) for equality lookups and unique constraints.
    /// The blind index key is SEPARATE from the data encryption key and is NEVER rotated.
    /// </summary>
    public static string? ComputeBlindIndex(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return null;
        EnsureInitialized();

        using var hmac = new HMACSHA256(_blindIndexKey!);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(plainText));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string NormalizeEmail(string? email)
    {
        if (string.IsNullOrEmpty(email)) return string.Empty;
        return email.Trim().ToLowerInvariant();
    }

    public static string NormalizeNif(string? nif)
    {
        if (string.IsNullOrEmpty(nif)) return string.Empty;
        return new string(nif.Where(char.IsDigit).ToArray());
    }

    public static string NormalizePhone(string? phone)
    {
        if (string.IsNullOrEmpty(phone)) return string.Empty;
        return new string(phone.Where(char.IsDigit).ToArray());
    }

    private static void EnsureInitialized()
    {
        if (_dataKey == null || _blindIndexKey == null)
            throw new InvalidOperationException("EncryptionHelperV2 has not been initialized. Call Initialize(configuration) at startup.");
    }
}
