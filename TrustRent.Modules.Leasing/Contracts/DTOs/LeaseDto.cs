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
}

public class ConfirmLeaseSignatureDto
{
    public string OtpCode { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
}

public class AcceptLeaseTermsDto
{
    public bool AcceptTerms { get; set; }
}

public class CancelLeaseDto
{
    public string Reason { get; set; } = string.Empty;
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
}
