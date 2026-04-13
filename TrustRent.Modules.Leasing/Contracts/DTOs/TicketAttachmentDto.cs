namespace TrustRent.Modules.Leasing.Contracts.DTOs;

public class TicketAttachmentDto
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string StorageUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}
