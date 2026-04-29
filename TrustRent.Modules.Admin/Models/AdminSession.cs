namespace TrustRent.Modules.Admin.Models;

public class AdminSession
{
    public Guid Id { get; set; }
    public Guid AdminUserId { get; set; }
    public string TokenId { get; set; } = string.Empty; // jti
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
}
