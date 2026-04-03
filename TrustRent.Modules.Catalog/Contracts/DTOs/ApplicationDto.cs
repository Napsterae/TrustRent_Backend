using TrustRent.Modules.Catalog.Models;

namespace TrustRent.Modules.Catalog.Contracts.DTOs;

public class ApplicationDto
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LandlordId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool ShareProfile { get; set; }
    public bool WantsVisit { get; set; }
    public List<string> TenantProposedDates { get; set; } = new();
    public DateTime? LandlordProposedDate { get; set; }
    public DateTime? FinalVisitDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<ApplicationHistoryDto> History { get; set; } = new();
}
