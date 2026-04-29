namespace TrustRent.Modules.Leasing.Contracts.DTOs;

public class InitiateLeaseProcedureDto
{
    public DateTime ProposedStartDate { get; set; }
}

public class ConfirmLeaseStartDateDto
{
    public DateTime StartDate { get; set; }
}

public class RequestLeaseSignatureDto
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string? ChallengeToken { get; set; }
}

public class ConfirmLeaseSignatureDto
{
    public string OtpCode { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
}

public class AcceptLeaseTermsDto
{
    public bool AcceptTerms { get; set; }
    public string? AcceptedDocumentHash { get; set; }
}

public class CancelLeaseDto
{
    public string Reason { get; set; } = string.Empty;
}

public class RegisterTaxDto
{
    public string? Reference { get; set; }
}

public class LeaseSignatureStatusDto
{
    public Guid LeaseId { get; set; }
    public bool ProcessInitiated { get; set; }
    public bool LandlordSigned { get; set; }
    public bool LandlordSignatureVerified { get; set; }
    public DateTime? LandlordSignedAt { get; set; }
    public string? LandlordSignatureCertSubject { get; set; }
    public bool TenantSigned { get; set; }
    public bool TenantSignatureVerified { get; set; }
    public DateTime? TenantSignedAt { get; set; }
    public string? TenantSignatureCertSubject { get; set; }
    public string ContractType { get; set; } = string.Empty;
    public string LeaseStatus { get; set; } = string.Empty;
    public string DocumentStatus { get; set; } = string.Empty;

    // ===== Multi-parte =====
    public int RequiredSignaturesCount { get; set; }
    public int SignedCount { get; set; }
    public List<LeaseSignatoryDto> Signatories { get; set; } = new();
}

public class LeaseSignatoryDto
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = string.Empty; // Landlord | Tenant | CoTenant | Guarantor
    public int SequenceOrder { get; set; }
    public bool Signed { get; set; }
    public DateTime? SignedAt { get; set; }
    public bool SignatureVerified { get; set; }
    public string? SignatureCertSubject { get; set; }
    public DateTime? AcceptedTermsAt { get; set; }
}
