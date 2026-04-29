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
    public string CurrentUserRole { get; set; } = string.Empty;
    public bool IsCurrentUserCoTenant { get; set; }
    public bool IsCurrentUserGuarantor { get; set; }

    // Validação de rendimentos via IA
    public bool IsIncomeValidationRequested { get; set; }
    public DateTime? IncomeValidationRequestedAt { get; set; }
    public bool IsIncomeVerified { get; set; }
    public string? IncomeRangeCode { get; set; }
    public string? IncomeRangeLabel { get; set; }
    public DateTime? IncomeValidatedAt { get; set; }

    // Tipo de relação laboral + método de validação efetivamente usado.
    // EmploymentType: "Employee" | "SelfEmployed"
    // IncomeValidationMethod: "Payslips" | "PayslipsWithEmployerDeclaration" | "ActivityWithGreenReceipts"
    public string? EmploymentType { get; set; }
    public string? IncomeValidationMethod { get; set; }
    public int? PayslipsProvidedCount { get; set; }
    public string? EmployerName { get; set; }
    public string? EmployerNif { get; set; }
    public DateTime? EmploymentStartDate { get; set; }

    // ===== Co-candidato (joint application) =====
    public bool IsJointApplication { get; set; }
    public Guid? CoTenantUserId { get; set; }
    public string? CoTenantName { get; set; }
    public string? CoTenantAvatarUrl { get; set; }
    public DateTime? CoTenantJoinedAt { get; set; }

    public bool IsCoTenantIncomeValidationRequested { get; set; }
    public DateTime? CoTenantIncomeValidationRequestedAt { get; set; }
    public bool IsCoTenantIncomeVerified { get; set; }
    public string? CoTenantIncomeRangeCode { get; set; }
    public string? CoTenantIncomeRangeLabel { get; set; }
    public DateTime? CoTenantIncomeValidatedAt { get; set; }
    public string? CoTenantEmploymentType { get; set; }
    public string? CoTenantIncomeValidationMethod { get; set; }
    public int? CoTenantPayslipsProvidedCount { get; set; }
    public string? CoTenantEmployerName { get; set; }
    public string? CoTenantEmployerNif { get; set; }
    public DateTime? CoTenantEmploymentStartDate { get; set; }

    public List<CoTenantInviteDto> CoTenantInvites { get; set; } = new();
    public CoTenantInviteDto? PendingCoTenantInvite { get; set; }

    // ===== Fiador =====
    public bool IsGuarantorRequired { get; set; }
    public DateTime? GuarantorRequestedAt { get; set; }
    public string? GuarantorRequestNote { get; set; }
    public string GuarantorRequirementStatus { get; set; } = "NotRequested";
    public Guid? GuarantorId { get; set; }
    public List<GuarantorSummaryDto> Guarantors { get; set; } = new();
}
