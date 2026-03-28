using System.Security.Cryptography;
using System.Text;

namespace TrustRent.Shared.Security;

public static class EncryptionHelper
{
    // AVISO: Num ambiente real, estas chaves devem vir do appsettings.json ou do Azure KeyVault!
    // Chave de 32 bytes (AES-256)
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("MinhaChaveSuperSecretaDe32Bytes!");
    // IV de 16 bytes fixo (Necessário para a encriptação ser determinística e permitir o IsUnique na Base de Dados)
    private static readonly byte[] IV = Encoding.UTF8.GetBytes("VetorFixoDe16Bts");

    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;
        using var aes = Aes.Create();
        aes.Key = Key;
        aes.IV = IV;
        var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return Convert.ToBase64String(encryptedBytes);
    }

    public static string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;
        using var aes = Aes.Create();
        aes.Key = Key;
        aes.IV = IV;
        var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        var cipherBytes = Convert.FromBase64String(cipherText);
        var decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(decryptedBytes);
    }
}