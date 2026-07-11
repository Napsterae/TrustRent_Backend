using System.Security.Cryptography;
using System.Text;

namespace TrustRent.Shared.Security;

public static class IpHashHelper
{
    // Salt loaded from configuration — same approach as EncryptionHelper keys.
    // Using a static salt is acceptable here because the goal is pseudonymization
    // (preventing reversal without the salt), not per-record uniqueness.
    private static byte[]? _salt;

    public static void Initialize(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        var saltBase64 = configuration["Encryption:V2:BlindIndexKey"];
        // Reuse the blind index key as the salt source — it's already a 32-byte secret.
        // This avoids adding a new config key while still providing a secret salt.
        if (string.IsNullOrWhiteSpace(saltBase64))
        {
            // Fallback for dev/test: use a fixed salt
            _salt = Encoding.UTF8.GetBytes("trustrent-ip-hash-salt-v1");
            return;
        }
        _salt = Convert.FromBase64String(saltBase64);
    }

    /// <summary>
    /// Hashes an IP address with a salted SHA256 for pseudonymization.
    /// Returns a 64-character lowercase hex string, or null if input is null/empty.
    /// The same IP always produces the same hash (deterministic) — enables audit correlation.
    /// The hash cannot be reversed to the original IP without the salt.
    /// </summary>
    public static string? Hash(string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress)) return null;

        EnsureInitialized();

        using var sha256 = SHA256.Create();
        var input = Encoding.UTF8.GetBytes(ipAddress.Trim());
        var salted = new byte[_salt!.Length + input.Length];
        Buffer.BlockCopy(_salt, 0, salted, 0, _salt.Length);
        Buffer.BlockCopy(input, 0, salted, _salt.Length, input.Length);
        var hash = sha256.ComputeHash(salted);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void EnsureInitialized()
    {
        if (_salt == null)
            throw new InvalidOperationException("IpHashHelper has not been initialized. Call Initialize(configuration) at startup.");
    }
}
