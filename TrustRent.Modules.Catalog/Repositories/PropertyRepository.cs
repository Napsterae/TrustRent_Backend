using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Modules.Catalog.Models;

namespace TrustRent.Modules.Catalog.Repositories;

public class PropertyRepository : IPropertyRepository
{
    private readonly CatalogDbContext _context;

    public PropertyRepository(CatalogDbContext context) => _context = context;

    public async Task<Property?> GetByIdAsync(Guid id) =>
        await _context.Properties.FindAsync(id);

    public async Task<Property?> GetByIdWithImagesAsync(Guid id) =>
        await _context.Properties
            .Include(p => p.Images)
            .Include(p => p.Amenities).ThenInclude(pa => pa.Amenity)
            .Include(p => p.AcceptedPeriodicities)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Property?> GetByIdAndLandlordWithImagesAsync(Guid id, Guid landlordId) =>
        await _context.Properties
            .Include(p => p.Images)
            .Include(p => p.Amenities).ThenInclude(pa => pa.Amenity)
            .Include(p => p.AcceptedPeriodicities)
            .FirstOrDefaultAsync(p => p.Id == id && p.LandlordId == landlordId);

    public async Task<IEnumerable<Amenity>> GetAllAmenitiesAsync() =>
        await _context.Amenities.OrderBy(a => a.Category).ThenBy(a => a.Name).ToListAsync();

    public async Task<IEnumerable<Property>> GetByLandlordIdWithImagesAsync(Guid landlordId) =>
        await _context.Properties.Include(p => p.Images).Where(p => p.LandlordId == landlordId).OrderByDescending(p => p.CreatedAt).ToListAsync();

    public async Task<IEnumerable<Property>> GetByTenantIdWithImagesAsync(Guid tenantId) =>
        await _context.Properties.Include(p => p.Images).Where(p => p.TenantId == tenantId).OrderByDescending(p => p.CreatedAt).ToListAsync();

    public async Task AddAsync(Property property) =>
        await _context.Properties.AddAsync(property);

    public async Task AddImageAsync(PropertyImage image) =>
        await _context.PropertyImages.AddAsync(image);

    public void RemoveImages(IEnumerable<PropertyImage> images) =>
        _context.PropertyImages.RemoveRange(images);

    public async Task<(IEnumerable<Property> Items, int TotalCount)> SearchAsync(PropertySearchQuery query)
    {
        // Apenas listamos imóveis públicos que continuam disponíveis para arrendamento.
        var q = _context.Properties
            .Include(p => p.Images)
            .Where(p => p.IsPublic && p.TenantId == null);

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            q = q.Where(p => 
            p.Title.ToLower().Contains(query.SearchTerm.ToLower()) ||
            p.District.ToLower().Contains(query.SearchTerm.ToLower()));

        if (!string.IsNullOrWhiteSpace(query.Type) && query.Type != "Todos")
            q = q.Where(p => p.PropertyType == query.Type);

        if (!string.IsNullOrWhiteSpace(query.Typologies))
        {
            var types = query.Typologies.Split(',').Select(t => t.Trim()).ToList();
            q = q.Where(p => types.Contains(p.Typology));
        }

        if (!string.IsNullOrWhiteSpace(query.Locations))
        {
            var locs = query.Locations.Split(',').Select(l => l.Trim().ToLower()).ToList();

            q = q.Where(p =>
                locs.Contains(p.District.ToLower()) ||
                locs.Contains(p.Municipality.ToLower()) ||
                locs.Contains(p.Parish.ToLower())
            );
        }

        if (query.MinPrice.HasValue) q = q.Where(p => p.Price >= query.MinPrice.Value);
        if (query.MaxPrice.HasValue) q = q.Where(p => p.Price <= query.MaxPrice.Value);

        // Toggles de Comodidades
        if (query.HasElevator == true) q = q.Where(p => p.HasElevator);
        if (query.HasAirConditioning == true) q = q.Where(p => p.HasAirConditioning);
        if (query.HasGarage == true) q = q.Where(p => p.HasGarage);
        if (query.AllowsPets == true) q = q.Where(p => p.AllowsPets);
        if (query.IsFurnished == true) q = q.Where(p => p.IsFurnished);
        if (query.HasOfficialContract == true) q = q.Where(p => p.HasOfficialContract);

        var totalCount = await q.CountAsync();

        // Aplica a Paginação
        var items = await q.OrderByDescending(p => p.CreatedAt)
                           .Skip((query.Page - 1) * query.PageSize)
                           .Take(query.PageSize)
                           .ToListAsync();

        return (items, totalCount);
    }
}