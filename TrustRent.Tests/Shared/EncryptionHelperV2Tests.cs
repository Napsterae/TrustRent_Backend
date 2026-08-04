using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using TrustRent.Shared.Security;

namespace TrustRent.Tests.Shared;

[CollectionDefinition("EncryptionHelperV2", DisableParallelization = true)]
public class EncryptionHelperV2CollectionDefinition
{
}

/// <summary>
/// EncryptionHelperV2 keeps static, process-wide key state. These tests re-initialize it
/// with controlled keys, so the CollectionDefinition above guarantees they never run in
/// parallel with any other test collection (avoiding cross-test key races).
/// </summary>
[Collection("EncryptionHelperV2")]
public class EncryptionHelperV2Tests
{
    // Mirrors the bootstrap initialization in TestAssemblyBootstrap (all-zero 32-byte keys, no previous key).
    private static readonly byte[] BootstrapDataKey = new byte[32];
    private static readonly byte[] BootstrapBlindIndexKey = new byte[32];

    private static byte[] DeterministicKey(int seed) =>
        Enumerable.Range(seed, 32).Select(i => (byte)((i * 37 + 11) % 256)).ToArray();

    private static IConfiguration BuildConfig(byte[] dataKey, byte[]? dataKeyPrevious, byte[] blindIndexKey)
    {
        var values = new Dictionary<string, string?>
        {
            ["Encryption:V2:DataKey"] = Convert.ToBase64String(dataKey),
            ["Encryption:V2:BlindIndexKey"] = Convert.ToBase64String(blindIndexKey)
        };
        if (dataKeyPrevious != null)
            values["Encryption:V2:DataKeyPrevious"] = Convert.ToBase64String(dataKeyPrevious);
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static void RestoreBootstrapState()
    {
        EncryptionHelperV2.Initialize(BuildConfig(BootstrapDataKey, null, BootstrapBlindIndexKey));
    }

    /// <summary>
    /// Builds a legacy-format payload (nonce + ciphertext + tag, no version prefix)
    /// encrypted with the given key using the supplied nonce.
    /// </summary>
    private static string BuildLegacyPayload(byte[] key, byte[] nonce, string plaintext)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plainBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16
        using (var aes = new AesGcm(key))
        {
            aes.Encrypt(nonce, plainBytes, ciphertext, tag);
        }

        var combined = new byte[nonce.Length + ciphertext.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(ciphertext, 0, combined, nonce.Length, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length + ciphertext.Length, tag.Length);
        return Convert.ToBase64String(combined);
    }

    [Fact]
    public void Decrypt_LegacyPayloadWhoseNonceStartsWith0x01_NoPreviousKey_Decrypts()
    {
        var dataKey = DeterministicKey(1);
        try
        {
            EncryptionHelperV2.Initialize(BuildConfig(dataKey, null, DeterministicKey(2)));
            Assert.Equal(0, EncryptionHelperV2.CurrentKeyVersion);

            // Legacy payload whose random nonce happens to start with 0x01 — the byte that used
            // to be misclassified as a versioned prefix, making the row permanently undecryptable.
            var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12
            nonce[0] = 0x01;
            const string plaintext = "legacy payload with nonce starting 0x01";
            var payload = BuildLegacyPayload(dataKey, nonce, plaintext);

            Assert.Equal(plaintext, EncryptionHelperV2.Decrypt(payload));
        }
        finally
        {
            RestoreBootstrapState();
        }
    }

    [Fact]
    public void Encrypt_Decrypt_LegacyRoundtrip_NoPreviousKey()
    {
        var dataKey = DeterministicKey(3);
        try
        {
            EncryptionHelperV2.Initialize(BuildConfig(dataKey, null, DeterministicKey(4)));
            Assert.Equal(0, EncryptionHelperV2.CurrentKeyVersion);

            const string plaintext = "normal legacy roundtrip";
            var encrypted = EncryptionHelperV2.Encrypt(plaintext);
            Assert.Equal(plaintext, EncryptionHelperV2.Decrypt(encrypted));
        }
        finally
        {
            RestoreBootstrapState();
        }
    }

    [Fact]
    public void Encrypt_Decrypt_VersionedRoundtrip_WithPreviousKey()
    {
        var currentKey = DeterministicKey(5);
        var previousKey = DeterministicKey(6);
        try
        {
            EncryptionHelperV2.Initialize(BuildConfig(currentKey, previousKey, DeterministicKey(7)));
            Assert.Equal(1, EncryptionHelperV2.CurrentKeyVersion);

            const string plaintext = "versioned roundtrip after rotation";
            var encrypted = EncryptionHelperV2.Encrypt(plaintext);
            Assert.Equal(plaintext, EncryptionHelperV2.Decrypt(encrypted));
        }
        finally
        {
            RestoreBootstrapState();
        }
    }

    [Fact]
    public void Decrypt_LegacyPayloadWhoseNonceStartsWith0x01_WithPreviousKey_FallsBackToPreviousKey()
    {
        var currentKey = DeterministicKey(8);
        var previousKey = DeterministicKey(9);
        try
        {
            EncryptionHelperV2.Initialize(BuildConfig(currentKey, previousKey, DeterministicKey(10)));

            // Legacy data encrypted with the PREVIOUS key; its nonce starts with 0x01 so the
            // heuristic picks the versioned path first. The try-both-keys fallback must then
            // succeed with (previousKey, offset 0).
            var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
            nonce[0] = 0x01;
            const string plaintext = "legacy under previous key with nonce 0x01";
            var payload = BuildLegacyPayload(previousKey, nonce, plaintext);

            Assert.Equal(plaintext, EncryptionHelperV2.Decrypt(payload));
        }
        finally
        {
            RestoreBootstrapState();
        }
    }
}
