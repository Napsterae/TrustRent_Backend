using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace TrustRent.Shared.Security;

public static class EncryptionHelper
{
    private static byte[]? _key;
    private static byte[]? _iv;

    /// <summary>
    /// Must be called at startup to initialize encryption keys from configuration.
    /// Keys should be stored in appsettings.json under "Encryption:Key" (32 chars) and "Encryption:IV" (16 chars).
    /// </summary>
    public static void Initialize(IConfiguration configuration)
    {
        var key = configuration["Encryption:Key"];
        var iv = configuration["Encryption:IV"];
        
        if (string.IsNullOrEmpty(key) || key.Length != 32)
            throw new InvalidOperationException("Encryption:Key must be exactly 32 characters (256 bits) in configuration.");
        if (string.IsNullOrEmpty(iv) || iv.Length != 16)
            throw new InvalidOperationException("Encryption:IV must be exactly 16 characters (128 bits) in configuration.");
        
        _key = Encoding.UTF8.GetBytes(key);
        _iv = Encoding.UTF8.GetBytes(iv);
    }

    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;
        EnsureInitialized();
        using var aes = Aes.Create();
        aes.Key = _key!;
        aes.IV = _iv!;
        var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return Convert.ToBase64String(encryptedBytes);
    }

    public static string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;
        EnsureInitialized();
        using var aes = Aes.Create();
        aes.Key = _key!;
        aes.IV = _iv!;
        var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        var cipherBytes = Convert.FromBase64String(cipherText);
        var decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(decryptedBytes);
    }
    
    private static void EnsureInitialized()
    {
        if (_key == null || _iv == null)
            throw new InvalidOperationException("EncryptionHelper has not been initialized. Call Initialize(configuration) at startup.");
    }
}