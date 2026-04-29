using OtpNet;

namespace TrustRent.Modules.Admin.Services;

internal static class TotpHelper
{
    public static string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    public static string BuildOtpAuthUri(string issuer, string accountEmail, string secret)
    {
        var encIssuer = Uri.EscapeDataString(issuer);
        var encAccount = Uri.EscapeDataString(accountEmail);
        return $"otpauth://totp/{encIssuer}:{encAccount}?secret={secret}&issuer={encIssuer}&algorithm=SHA1&digits=6&period=30";
    }

    public static bool VerifyCode(string base32Secret, string code)
    {
        if (string.IsNullOrWhiteSpace(base32Secret) || string.IsNullOrWhiteSpace(code)) return false;
        try
        {
            var bytes = Base32Encoding.ToBytes(base32Secret);
            var totp = new Totp(bytes);
            return totp.VerifyTotp(code.Trim(), out _, new VerificationWindow(previous: 1, future: 1));
        }
        catch
        {
            return false;
        }
    }
}
