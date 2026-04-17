using TrustRent.Modules.Leasing.Models;

namespace TrustRent.Modules.Leasing.Contracts.Interfaces;

public interface IContractGenerationService
{
    Task<string> GenerateContractPdfAsync(Lease lease, string landlordName, string landlordNif,
        string landlordAddress, string tenantName, string tenantNif, string tenantAddress,
        ContractPropertyInfo propertyInfo);

    Task<byte[]> GetContractBytesAsync(string contractFilePath);
}

/// <summary>
/// Property info needed for contract generation, passed from Catalog via cross-module access.
/// </summary>
public class ContractPropertyInfo
{
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
}
