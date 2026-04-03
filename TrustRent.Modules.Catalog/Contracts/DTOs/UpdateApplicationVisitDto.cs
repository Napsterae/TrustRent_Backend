namespace TrustRent.Modules.Catalog.Contracts.DTOs;

public class UpdateApplicationVisitDto
{
    public string Action { get; set; } = string.Empty; // "Accept", "CounterPropose", "Reject", "TenantCounterPropose"
    public DateTime? LandlordProposedDate { get; set; }
    public string? SelectedTenantDate { get; set; } // The exact date from TenantProposedDates the Landlord wants to accept
    public List<string>? TenantProposedDates { get; set; }
}
