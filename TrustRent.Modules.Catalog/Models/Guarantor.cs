using TrustRent.Modules.Catalog.Models.ReferenceData;
using TrustRent.Shared.Models;

namespace TrustRent.Modules.Catalog.Models;

/// <summary>
/// Fiador associado a uma candidatura. Pode ser convidado externo por token;
/// co-candidatos continuam a exigir conta de utilizador.
/// </summary>
public class Guarantor
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public Application? Application { get; set; }

    public Guid? UserId { get; set; }             // opcional: conta TrustRent associada ao fiador
    public Guid InvitedByUserId { get; set; }     // candidato principal que convidou

    public string GuestEmail { get; set; } = string.Empty;
    public string? GuestName { get; set; }
    public string? GuestPhoneNumber { get; set; }
    public string? GuestAddress { get; set; }
    public string? GuestPostalCode { get; set; }
    public string GuestAccessToken { get; set; } = string.Empty;
    public DateTime? GuestTokenIssuedAt { get; set; }
    public DateTime? GuestTokenLastUsedAt { get; set; }
    public string? CreatedFromIp { get; set; }

    public GuarantorInviteStatus InviteStatus { get; set; } = GuarantorInviteStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);

    public string? LandlordRequestNote { get; set; }
    public string? RejectionReason { get; set; }
    public string? DeclineReason { get; set; }

    // ===== KYC contextual =====
    public bool IsIdentityVerified { get; set; }
    public DateTime? IdentityVerifiedAt { get; set; }
    public string? IdentityMatchEvidenceHash { get; set; }
    public bool IsAddressVerified { get; set; }
    public DateTime? AddressVerifiedAt { get; set; }

    // ===== Validação de rendimentos =====
    public Guid? IncomeRangeId { get; set; }
    public SalaryRange? IncomeRange { get; set; }
    public DateTime? IncomeValidatedAt { get; set; }
    public EmploymentType? EmploymentType { get; set; }
    public IncomeValidationMethod? IncomeValidationMethod { get; set; }
    public int? PayslipsProvidedCount { get; set; }
    public string? EmployerName { get; set; }
    public string? EmployerNif { get; set; }
    public DateTime? EmploymentStartDate { get; set; }
}
