using TrustRent.Modules.Catalog.Contracts.DTOs;

namespace TrustRent.Modules.Catalog.Contracts.Interfaces;

public interface IPropertyService
{
    Task<Guid> CreatePropertyAsync(Guid landlordId, CreatePropertyDto dto, IEnumerable<FileDto> images, IEnumerable<FileDto> documents);
    Task<IEnumerable<PropertySummaryDto>> GetPropertiesByLandlordAsync(Guid landlordId);
}