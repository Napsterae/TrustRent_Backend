namespace TrustRent.Shared.Contracts.Interfaces;

/// <summary>
/// Cross-module access: allows the Leasing module to read and update
/// Application/Property data that lives in the Catalog module.
/// </summary>
public interface ICatalogAccessService
{
    Task<ApplicationContext?> GetApplicationContextAsync(Guid applicationId);
    Task<PropertyContext?> GetPropertyContextAsync(Guid propertyId);
    Task UpdateApplicationStatusAsync(Guid applicationId, int newStatus, Guid actorId, string action, string? message = null);
    Task SetPropertyTenantAsync(Guid propertyId, Guid tenantId);
    Task RejectOtherApplicationsAsync(Guid propertyId, Guid excludeApplicationId, INotificationService notificationService);
}

public class ApplicationContext
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public Guid TenantId { get; set; }
    public int DurationMonths { get; set; }
    public int Status { get; set; }
    public Guid LandlordId { get; set; }
    
    // Property fields copied at lease creation time
    public decimal Price { get; set; }
    public decimal? Deposit { get; set; }
    public int AdvanceRentMonths { get; set; }
    public string? LeaseRegime { get; set; }
    public bool HasOfficialContract { get; set; }
    public bool AllowsRenewal { get; set; }
    public string CondominiumFeesPaidBy { get; set; } = "Inquilino";
    public string WaterPaidBy { get; set; } = "Inquilino";
    public string ElectricityPaidBy { get; set; } = "Inquilino";
    public string GasPaidBy { get; set; } = "Inquilino";
    
    // Property address info for contract generation
    public string? Street { get; set; }
    public string? DoorNumber { get; set; }
    public string? PostalCode { get; set; }
    public string? Parish { get; set; }
    public string? Municipality { get; set; }
    public string? District { get; set; }
    public string? Typology { get; set; }
    public string? MatrixArticle { get; set; }
    public string? PropertyFraction { get; set; }
    public string? UsageLicenseNumber { get; set; }
    public string? UsageLicenseDate { get; set; }
    public string? UsageLicenseIssuer { get; set; }
    public string? EnergyCertificateNumber { get; set; }
    public string? EnergyClass { get; set; }
    public string? PropertyTitle { get; set; }

    // Multi-parte (co-candidato + fiador)
    public Guid? CoTenantUserId { get; set; }
    public Guid? GuarantorUserId { get; set; }
    public Guid? GuarantorRecordId { get; set; }
    public string? GuarantorGuestEmail { get; set; }
    public string? GuarantorGuestName { get; set; }
    public string? GuarantorGuestAccessToken { get; set; }
}

public class PropertyContext
{
    public Guid Id { get; set; }
    public Guid LandlordId { get; set; }
    public string? Title { get; set; }
    public decimal Price { get; set; }
    public decimal? Deposit { get; set; }
    public int AdvanceRentMonths { get; set; }
}
