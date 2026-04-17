namespace TrustRent.Modules.Leasing.Contracts.DTOs;

public class TicketListItemDto
{
    public Guid Id { get; set; }
    public Guid LeaseId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int CommentCount { get; set; }
    public int AttachmentCount { get; set; }

    // Tenant info
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string? TenantProfilePictureUrl { get; set; }
}
