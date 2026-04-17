namespace TrustRent.Modules.Leasing.Models;

public class LeaseHistory
{
    public Guid Id { get; set; }
    public Guid LeaseId { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? EventData { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navegação
    public Lease? Lease { get; set; }
}
