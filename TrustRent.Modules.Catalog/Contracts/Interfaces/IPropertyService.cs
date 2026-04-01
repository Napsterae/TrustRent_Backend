using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Models;

namespace TrustRent.Modules.Catalog.Contracts.Interfaces;

public interface IPropertyService
{
    Task<Guid> CreatePropertyAsync(Guid landlordId, CreatePropertyDto dto, IEnumerable<FileDto> images, IList<string> imageCategories, int mainImageIndex, IEnumerable<FileDto> documents);
    Task<IEnumerable<PropertySummaryDto>> GetPropertiesByLandlordAsync(Guid landlordId);
    Task<Property?> GetPropertyByIdAsync(Guid propertyId);
    Task UpdatePropertyAsync(Guid propertyId, Guid landlordId, CreatePropertyDto dto, IEnumerable<FileDto> newImages, IList<string> imageCategories, IList<Guid> retainedImageIds, int mainImageIndex, Guid? mainRetainedImageId);
    Task<PagedResult<PropertySearchDto>> SearchPropertiesAsync(PropertySearchQuery query);
}