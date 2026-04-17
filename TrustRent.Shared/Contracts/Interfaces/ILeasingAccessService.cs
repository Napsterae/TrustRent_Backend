using TrustRent.Shared.Contracts.DTOs;

namespace TrustRent.Shared.Contracts.Interfaces;

/// <summary>
/// Cross-module access: allows the Catalog module to read Lease data
/// that lives in the Leasing module (reverse of ICatalogAccessService).
/// </summary>
public interface ILeasingAccessService
{
    Task<LeaseDto?> GetLeaseByApplicationIdAsync(Guid applicationId);
    Task<Dictionary<Guid, LeaseDto>> GetLeasesByApplicationIdsAsync(IEnumerable<Guid> applicationIds);
    Task<List<LeaseDto>> GetLeasesByPropertyIdAsync(Guid propertyId);
}
