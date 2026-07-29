namespace TrustRent.Modules.Admin.Models;

public class WebhookEvent
{
    public Guid Id { get; set; }
    public string StripeEventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Status { get; set; } = "received"; // received, processed, failed
    public string? Error { get; set; }
    public string? PayloadSummary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}
