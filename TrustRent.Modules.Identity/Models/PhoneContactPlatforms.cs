namespace TrustRent.Modules.Identity.Models;

public static class PhoneContactPlatforms
{
    public const string Telegram = "telegram";
    public const string WhatsApp = "whatsapp";

    public static string Normalize(string? value, string fallback = Telegram)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            Telegram => Telegram,
            WhatsApp => WhatsApp,
            _ => fallback
        };
    }

    public static bool IsSupported(string? value)
        => value is not null && Normalize(value, fallback: string.Empty) is Telegram or WhatsApp;

    public static string DisplayName(string? value)
        => Normalize(value) switch
        {
            WhatsApp => "WhatsApp",
            _ => "Telegram"
        };
}