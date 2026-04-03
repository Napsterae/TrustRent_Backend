namespace TrustRent.Modules.Catalog.Models;

public class ApplicationHistory
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    
    // Who did the action?
    public Guid ActorId { get; set; }

    // Action that triggered the history entry (e.g., "Created", "TenantProposedDates", "LandlordCounterProposed", "VisitAccepted")
    public string Action { get; set; } = string.Empty;

    // Optional message from the actor
    public string? Message { get; set; }

    // The raw data or date proposed in this step, to keep history
    public string? EventData { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
