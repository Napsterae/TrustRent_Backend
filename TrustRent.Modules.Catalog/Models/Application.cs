namespace TrustRent.Modules.Catalog.Models;

public class Application
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public Guid TenantId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool ShareProfile { get; set; }
    public bool WantsVisit { get; set; }
    
    public int DurationMonths { get; set; } // Adicionado
    
    // Lista de datas no formato string "YYYY-MM-DD" separadas por virgula ou JSON (para simplificar, array mapeado em json ou string delimitada)
    public string TenantProposedDates { get; set; } = string.Empty; 
    
    public DateTime? LandlordProposedDate { get; set; }
    public DateTime? FinalVisitDate { get; set; }
    
    public ApplicationStatus Status { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public List<ApplicationHistory> History { get; set; } = new();
    public Property? Property { get; set; }
    
    public Lease? Lease { get; set; } // Adicionado
}

public enum ApplicationStatus
{
    Pending,
    VisitCounterProposed,
    VisitAccepted,
    InterestConfirmed,
    Accepted,
    Rejected,
    LeaseStartDateProposed,
    LeaseStartDateConfirmed,
    ContractPendingSignature,
    AwaitingPayment,
    LeaseActive
}
