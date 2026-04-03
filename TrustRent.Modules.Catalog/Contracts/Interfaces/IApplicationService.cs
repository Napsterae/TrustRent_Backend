using TrustRent.Modules.Catalog.Contracts.DTOs;

namespace TrustRent.Modules.Catalog.Contracts.Interfaces;

public interface IApplicationService
{
    Task<ApplicationDto> SubmitApplicationAsync(Guid propertyId, Guid tenantId, SubmitApplicationDto dto);
    Task<ApplicationDto?> GetApplicationByIdAsync(Guid applicationId, Guid userId);
    Task<IEnumerable<ApplicationDto>> GetApplicationsForPropertyAsync(Guid propertyId, Guid landlordId);
    Task<IEnumerable<ApplicationDto>> GetApplicationsForTenantAsync(Guid tenantId);
    Task<ApplicationDto> UpdateVisitStatusAsync(Guid applicationId, Guid userId, UpdateApplicationVisitDto dto);
}
