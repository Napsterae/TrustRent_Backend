using TrustRent.Shared.Models;

namespace TrustRent.Modules.Catalog.Models;

public class Application
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public Guid TenantId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool ShareProfile { get; set; }
    public bool WantsVisit { get; set; }
    
    public int DurationMonths { get; set; }
    
    public string TenantProposedDates { get; set; } = string.Empty; 
    
    public DateTime? LandlordProposedDate { get; set; }
    public DateTime? FinalVisitDate { get; set; }
    
    public ApplicationStatus Status { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public List<ApplicationHistory> History { get; set; } = new();
    public Property? Property { get; set; }
    
    public Guid? LeaseId { get; set; }
}
