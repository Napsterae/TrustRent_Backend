namespace TrustRent.Modules.Leasing.Models;

public class Ticket
{
    public Guid Id { get; set; }
    public Guid LeaseId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LandlordId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public TicketStatus Status { get; set; } = TicketStatus.Open;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public List<TicketComment> Comments { get; set; } = new();
    public List<TicketAttachment> Attachments { get; set; } = new();
}

public enum TicketStatus
{
    Open,
    InProgress,
    Resolved,
    Closed
}

public enum TicketPriority
{
    Low,
    Medium,
    High,
    Critical
}
