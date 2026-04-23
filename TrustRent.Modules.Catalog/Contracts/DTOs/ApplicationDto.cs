using TrustRent.Shared.Contracts.DTOs;

namespace TrustRent.Modules.Catalog.Contracts.DTOs;

public class ApplicationDto
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;
    public string? PropertyImageUrl { get; set; }
    public Guid TenantId { get; set; }
    public string TenantName { get; set; } = string.Empty;
    public string? TenantAvatarUrl { get; set; }
    public int TenantReviewScore { get; set; }
    public Guid LandlordId { get; set; }
    public string LandlordName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool ShareProfile { get; set; }
    public bool WantsVisit { get; set; }
    public int DurationMonths { get; set; }
    public List<string> TenantProposedDates { get; set; } = new();
    public DateTime? LandlordProposedDate { get; set; }
    public DateTime? FinalVisitDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<ApplicationHistoryDto> History { get; set; } = new();
    public LeaseDto? Lease { get; set; }

    // Validação de rendimentos via IA
    public bool IsIncomeValidationRequested { get; set; }
    public DateTime? IncomeValidationRequestedAt { get; set; }
    public bool IsIncomeVerified { get; set; }
    public string? IncomeRangeCode { get; set; }
    public string? IncomeRangeLabel { get; set; }
    public DateTime? IncomeValidatedAt { get; set; }
}
