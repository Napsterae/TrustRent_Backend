using System.Diagnostics;

namespace TrustRent.Shared;

/// <summary>
/// Central ActivitySource for WeKaza custom telemetry spans.
/// Used to instrument critical business paths: Stripe webhooks, signing webhooks, lease state transitions.
/// </summary>
public static class Telemetry
{
    public static readonly ActivitySource Source = new("WeKaza.Business", "1.0.0");
}
