namespace TrustRent.Modules.Catalog.Contracts.DTOs;

public class PropertyTenantManagementDto
{
    public PropertyManagedTenantDto? CurrentTenant { get; set; }
    public LeaseDto? CurrentLease { get; set; }
    public List<PropertyTenantHistoryEntryDto> TenantHistory { get; set; } = new();
}

public class PropertyManagedTenantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneCountryCode { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Nif { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public int TrustScore { get; set; }
    public bool IsIdentityVerified { get; set; }
    public bool IsNoDebtVerified { get; set; }
}

public class PropertyTenantHistoryEntryDto
{
    public Guid LeaseId { get; set; }
    public PropertyManagedTenantDto Tenant { get; set; } = new();
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ContractSignedAt { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal? Deposit { get; set; }
    public int AdvanceRentMonths { get; set; }
    public int DurationMonths { get; set; }
    public string ContractType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
}