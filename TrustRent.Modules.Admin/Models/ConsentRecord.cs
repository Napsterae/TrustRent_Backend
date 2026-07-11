namespace TrustRent.Modules.Admin.Models;

public class ConsentRecord
{
    public Guid Id { get; set; }
    public Guid? OpenedByUserId { get; set; }
    public string? SessionId { get; set; }
    public string ConsentStatus { get; set; } = string.Empty; // granted | refused | withdrawn
    public string PurposesJson { get; set; } = string.Empty; // {"error_telemetry":true}
    public string Mechanism { get; set; } = string.Empty; // accept_all | reject_all | withdrawn_via_preferences
    public string? BannerVersion { get; set; }
    public string? PrivacyPolicyVersion { get; set; }
    public string? SourceUrl { get; set; }
    public string? Language { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}
