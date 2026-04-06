using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Models;

namespace TrustRent.Modules.Catalog.Contracts.Interfaces;

public interface IPropertyRepository
{
    Task<Property?> GetByIdAsync(Guid id);
    Task<Property?> GetByIdWithImagesAsync(Guid id);
    Task<Property?> GetByIdAndLandlordWithImagesAsync(Guid id, Guid landlordId);
    Task<IEnumerable<Property>> GetByLandlordIdWithImagesAsync(Guid landlordId);
    Task<IEnumerable<Property>> GetByTenantIdWithImagesAsync(Guid tenantId);
    Task AddAsync(Property property);
    Task AddImageAsync(PropertyImage image);
    void RemoveImages(IEnumerable<PropertyImage> images);
    Task<(IEnumerable<Property> Items, int TotalCount)> SearchAsync(PropertySearchQuery query);
    Task<IEnumerable<Amenity>> GetAllAmenitiesAsync();
}