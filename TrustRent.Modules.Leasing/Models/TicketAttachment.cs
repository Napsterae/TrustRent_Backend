namespace TrustRent.Modules.Leasing.Models;

public class TicketAttachment
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string StorageUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Ticket? Ticket { get; set; }
}
