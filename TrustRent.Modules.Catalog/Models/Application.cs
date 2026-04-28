using TrustRent.Modules.Catalog.Models.ReferenceData;
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

    // ===== Validação de rendimentos via IA (recibos de vencimento) =====
    // O senhorio pode pedir, após visita, que o inquilino valide rendimentos.
    // Não armazenamos ficheiros nem valor exato — apenas a faixa salarial calculada.
    public bool IsIncomeValidationRequested { get; set; }
    public DateTime? IncomeValidationRequestedAt { get; set; }
    public Guid? IncomeRangeId { get; set; }
    public DateTime? IncomeValidatedAt { get; set; }
    public SalaryRange? IncomeRange { get; set; }

    // Tipo de relação laboral declarado e método de validação efetivamente usado.
    // Empregador / atividade: extraídos dos documentos validados e expostos a senhorio + inquilino.
    public EmploymentType? EmploymentType { get; set; }
    public IncomeValidationMethod? IncomeValidationMethod { get; set; }
    public int? PayslipsProvidedCount { get; set; }
    public string? EmployerName { get; set; }    // empresa OU CAE principal (independentes)
    public string? EmployerNif { get; set; }     // NIF da empresa (independentes: não preenchido)
    public DateTime? EmploymentStartDate { get; set; }
}
