using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Models;

namespace TrustRent.Modules.Catalog.Contracts.Interfaces;

public interface IPropertyService
{
    Task<Guid> CreatePropertyAsync(Guid landlordId, CreatePropertyDto dto, IEnumerable<FileDto> images, IList<string> imageCategories, int mainImageIndex, IEnumerable<FileDto> documents, IList<Guid>? amenityIds = null);
    Task<IEnumerable<PropertySummaryDto>> GetPropertiesByLandlordAsync(Guid landlordId);
    Task<Property?> GetPropertyByIdAsync(Guid propertyId);
    Task UpdatePropertyAsync(Guid propertyId, Guid landlordId, CreatePropertyDto dto, IEnumerable<FileDto> newImages, IList<string> imageCategories, IList<Guid> retainedImageIds, int mainImageIndex, Guid? mainRetainedImageId, IList<Guid>? amenityIds = null);
    Task<PagedResult<PropertySearchDto>> SearchPropertiesAsync(PropertySearchQuery query);
    Task<IEnumerable<PropertySummaryDto>> GetPropertiesByTenantAsync(Guid tenantId);
    Task<IEnumerable<Amenity>> GetAllAmenitiesAsync();
}