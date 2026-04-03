namespace TrustRent.Modules.Catalog.Contracts.DTOs;

public class ApplicationHistoryDto
{
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? EventData { get; set; }
    public DateTime CreatedAt { get; set; }
}
