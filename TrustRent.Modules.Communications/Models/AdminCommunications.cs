namespace TrustRent.Modules.Communications.Models;

public class Broadcast
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Audience { get; set; } = "all"; // all|tenants|landlords
    public string Channel { get; set; } = "in_app"; // in_app|email|both
    public DateTime? ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
    public string Status { get; set; } = "draft"; // draft|scheduled|sent|cancelled
    public Guid CreatedByAdminId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public int? RecipientCount { get; set; }
}

public class EmailTemplate
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;     // unique
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public string? BodyText { get; set; }
    public string Locale { get; set; } = "pt-PT";
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedByAdminId { get; set; }
}

public class Banner
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Severity { get; set; } = "info"; // info|warning|danger|success
    public string Audience { get; set; } = "all";   // all|tenants|landlords|admins
    public bool IsActive { get; set; } = true;
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public Guid CreatedByAdminId { get; set; }
}
