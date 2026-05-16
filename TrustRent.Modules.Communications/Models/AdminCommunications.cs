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
    public string Key { get; set; } = string.Empty;     // template family/use case key
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public string? BodyText { get; set; }
    public string Locale { get; set; } = "pt-PT";
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public bool IsSystemDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByAdminId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedByAdminId { get; set; }
}

public static class LegalDocumentTypes
{
    public const string PrivacyPolicy = "privacy_policy";
    public const string TermsOfUse = "terms_of_use";

    public static readonly string[] All =
    [
        PrivacyPolicy,
        TermsOfUse
    ];
}

public class LegalDocumentVersion
{
    public Guid Id { get; set; }
    public string DocumentType { get; set; } = LegalDocumentTypes.PrivacyPolicy;
    public string Version { get; set; } = "1.0.0";
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string ChangeSummary { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public string? BodyText { get; set; }
    public bool IsCurrent { get; set; }
    public bool NotifyUsers { get; set; }
    public Guid? BasedOnVersionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByAdminId { get; set; }
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public Guid? PublishedByAdminId { get; set; }
    public DateTime? NotificationRequestedAt { get; set; }
    public Guid? NotificationRequestedByAdminId { get; set; }
    public DateTime? NotificationSentAt { get; set; }
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
