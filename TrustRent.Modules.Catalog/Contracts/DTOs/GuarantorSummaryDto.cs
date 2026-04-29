namespace TrustRent.Modules.Catalog.Contracts.DTOs;

/// <summary>
/// Resumo do fiador exposto ao senhorio e às partes da candidatura.
/// Nunca contém ficheiros nem NIF completo.
/// </summary>
public class GuarantorSummaryDto
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? UserAvatarUrl { get; set; }
    public string GuestEmail { get; set; } = string.Empty;
    public string? GuestEmailMasked { get; set; }
    public string? GuestName { get; set; }
    public string? GuestPhoneNumber { get; set; }
    public string? GuestAccessUrl { get; set; }
    public string? PropertyTitle { get; set; }
    public string? PropertyAddress { get; set; }
    public decimal? MonthlyRent { get; set; }

    public string InviteStatus { get; set; } = string.Empty;
    public string RequirementStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    public bool IsIdentityVerified { get; set; }
    public DateTime? IdentityVerifiedAt { get; set; }

    public string? IncomeRangeCode { get; set; }
    public string? IncomeRangeLabel { get; set; }
    public DateTime? IncomeValidatedAt { get; set; }
    public string? EmploymentType { get; set; }
    public string? IncomeValidationMethod { get; set; }
    public int? PayslipsProvidedCount { get; set; }
    public string? EmployerName { get; set; }
    public string? EmployerNifMasked { get; set; }
    public DateTime? EmploymentStartDate { get; set; }

    public string? LandlordRequestNote { get; set; }
    public string? RejectionReason { get; set; }
}

public record CreateGuarantorInviteDto(string Email);

public record RequestGuarantorDto(string? Note = null);

public record GuarantorDecisionDto(string? Reason = null);

public record WaiveGuarantorDto(string Reason);
