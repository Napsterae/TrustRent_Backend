namespace TrustRent.Modules.Identity.Models;

public class EmailLoginCode
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? InvalidatedAt { get; set; }
    public int AttemptCount { get; set; }
    public string? RequestedFromIp { get; set; }
    public string? RequestedUserAgent { get; set; }
}