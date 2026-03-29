using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Contracts.Database;
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
        await _context.Properties.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Property?> GetByIdAndLandlordWithImagesAsync(Guid id, Guid landlordId) =>
        await _context.Properties.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id && p.LandlordId == landlordId);

    public async Task<IEnumerable<Property>> GetByLandlordIdWithImagesAsync(Guid landlordId) =>
        await _context.Properties.Include(p => p.Images).Where(p => p.LandlordId == landlordId).OrderByDescending(p => p.CreatedAt).ToListAsync();

    public async Task AddAsync(Property property) =>
        await _context.Properties.AddAsync(property);

    public async Task AddImageAsync(PropertyImage image) =>
        await _context.PropertyImages.AddAsync(image);

    public void RemoveImages(IEnumerable<PropertyImage> images) =>
        _context.PropertyImages.RemoveRange(images);
}