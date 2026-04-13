namespace TrustRent.Modules.Leasing.Contracts.DTOs;

public class TicketDto
{
    public Guid Id { get; set; }
    public Guid LeaseId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LandlordId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public List<TicketCommentDto> Comments { get; set; } = new();
    public List<TicketAttachmentDto> Attachments { get; set; } = new();
}
