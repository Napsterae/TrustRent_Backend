namespace TrustRent.Shared.Contracts.DTOs;

public class LeaseDto
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LandlordId { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid? CoTenantId { get; set; }
    public Guid? GuarantorUserId { get; set; }
    public Guid? GuarantorRecordId { get; set; }
    public int RequiredSignaturesCount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int DurationMonths { get; set; }
    public bool AllowsRenewal { get; set; }
    public DateTime? RenewalDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal? Deposit { get; set; }
    public int AdvanceRentMonths { get; set; }
    public string? LeaseRegime { get; set; }
    public string ContractType { get; set; } = string.Empty;
    public string CondominiumFeesPaidBy { get; set; } = string.Empty;
    public string WaterPaidBy { get; set; } = string.Empty;
    public string ElectricityPaidBy { get; set; } = string.Empty;
    public string GasPaidBy { get; set; } = string.Empty;
    public string? ContractFilePath { get; set; }
    public DateTime? ContractGeneratedAt { get; set; }
    public DateTime? ContractSignedAt { get; set; }
    public bool LandlordSigned { get; set; }
    public DateTime? LandlordSignedAt { get; set; }
    public string? LandlordSignatureCertSubject { get; set; }
    public bool LandlordSignatureVerified { get; set; }
    public bool TenantSigned { get; set; }
    public DateTime? TenantSignedAt { get; set; }
    public string? TenantSignatureCertSubject { get; set; }
    public bool TenantSignatureVerified { get; set; }
    public bool IsRegisteredWithTaxAuthority { get; set; }
    public DateTime? TaxRegistrationDate { get; set; }
    public string? TaxRegistrationReference { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<LeaseHistoryDto> History { get; set; } = new();
}

public class LeaseHistoryDto
{
    public Guid Id { get; set; }
    public Guid ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? EventData { get; set; }
    public DateTime CreatedAt { get; set; }
}
