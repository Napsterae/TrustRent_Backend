using System.Text.Json.Serialization;

namespace TrustRent.Modules.Admin.Models;

public enum SupportTicketState
{
    Open = 0,
    PendingUser = 1,
    PendingAdmin = 2,
    Resolved = 3,
    Closed = 4
}

public enum SupportTicketPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}

public enum SupportTicketKind
{
    Support = 0,
    ErrorReport = 1,
    Feedback = 2
}

public class SupportTicket
{
    public Guid Id { get; set; }
    public Guid? OpenedByUserId { get; set; } // public user (identity.Users.Id)
    public string Subject { get; set; } = string.Empty;
    public string Category { get; set; } = "general";
    public SupportTicketKind Kind { get; set; } = SupportTicketKind.Support;
    public SupportTicketState State { get; set; } = SupportTicketState.Open;
    public SupportTicketPriority Priority { get; set; } = SupportTicketPriority.Normal;
    public Guid? AssignedAdminId { get; set; }
    public string SourceChannel { get; set; } = "support-center";
    public string? PagePath { get; set; }
    public string? PageUrl { get; set; }
    public string? ClientBrowser { get; set; }
    public string? ClientOs { get; set; }
    public string? ClientDevice { get; set; }
    public bool DiagnosticsConsent { get; set; }
    public bool IsArchived { get; set; } = false;
    public DateTime? ArchivedAt { get; set; }
    public Guid? ArchivedByAdminId { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public ICollection<SupportTicketMessage> Messages { get; set; } = new List<SupportTicketMessage>();
}

public class SupportTicketMessage
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    [JsonIgnore]
    public SupportTicket? Ticket { get; set; }
    public Guid? AuthorId { get; set; } // user OR admin
    public bool IsAdmin { get; set; }
    public bool IsInternalNote { get; set; } = false;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
