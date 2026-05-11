namespace TrustRent.Shared.Infrastructure;

public static class PublicGatewaySurface
{
    public const string ApiPrefix = "/api";
    public const string ApiCatchAll = "/api/{**catchall}";
    public const string ChatHub = "/api/chathub";
    public const string NotificationHub = "/api/notificationhub";
    public const string StripeWebhook = "/api/stripe/webhook";
    public const string Health = "/health";
    public const string HealthLive = "/health/live";
    public const string HealthReady = "/health/ready";
}