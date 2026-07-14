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

    public async Task<(IEnumerable<Property> Items, int TotalCount)> SearchAsync(PropertySearchQuery query, IReadOnlyCollection<Guid>? excludedPropertyIds = null)
    {
        var page = query.EffectivePage;
        var pageSize = query.EffectivePageSize;

        // Apenas listamos imóveis públicos, ativos e realmente disponíveis para arrendamento.
        var q = _context.Properties
            .Include(p => p.Images)
            .Where(p => p.IsPublic && !p.IsUnderMaintenance && !p.IsBlocked && p.TenantId == null);

        if (excludedPropertyIds is { Count: > 0 })
            q = q.Where(p => !excludedPropertyIds.Contains(p.Id));

        // --- Full-text search ---
        // On PostgreSQL: use websearch_to_tsquery with configurable language stemming + ranking via SearchVector.
        // On InMemory (tests): fall back to case-insensitive Contains on Title + District + Municipality + Parish.
        // Language is configurable via FTS_LANGUAGE env var, defaults to 'portuguese'.
        var ftsLanguage = Environment.GetEnvironmentVariable("FTS_LANGUAGE") ?? "portuguese";

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            if (IsNpgsqlProvider)
            {
                var tsQuery = EF.Functions.WebSearchToTsQuery(ftsLanguage, query.SearchTerm);
                q = q.Where(p => p.SearchVector!.Matches(tsQuery));
            }
            else
            {
                var term = query.SearchTerm.ToLower();
                q = q.Where(p =>
                    p.Title.ToLower().Contains(term) ||
                    p.District.ToLower().Contains(term) ||
                    p.Municipality.ToLower().Contains(term) ||
                    p.Parish.ToLower().Contains(term));
            }
        }

        // --- Geo radius search (bounding box approximation) ---
        // Works with both PostgreSQL and InMemory. Uses simple lat/lng range filter.
        // Accurate enough for thousands of properties; can upgrade to PostGIS later if needed.
        if (query.Latitude.HasValue && query.Longitude.HasValue && query.RadiusKm.HasValue)
        {
            var lat = query.Latitude.Value;
            var lng = query.Longitude.Value;
            var radiusKm = query.RadiusKm.Value;

            // 1 degree of latitude ≈ 111 km. Convert radius to degree range.
            var latDelta = radiusKm / 111.0;
            // Longitude degrees shrink as latitude approaches the poles.
            var lngDelta = radiusKm / (111.0 * Math.Cos(lat * Math.PI / 180.0));

            q = q.Where(p =>
                p.Latitude >= lat - latDelta && p.Latitude <= lat + latDelta &&
                p.Longitude >= lng - lngDelta && p.Longitude <= lng + lngDelta);
        }

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

        // On PostgreSQL with FTS, sort by relevance (ts_rank) when no explicit sort is requested.
        // Otherwise fall back to the existing sort logic.
        var effectiveSort = query.EffectiveSort;
        if (effectiveSort == "recent" && !string.IsNullOrWhiteSpace(query.SearchTerm) && IsNpgsqlProvider)
        {
            var tsQuery = EF.Functions.WebSearchToTsQuery(ftsLanguage, query.SearchTerm!);
            var orderedQuery = q
                .Select(p => new { Property = p, Rank = p.SearchVector!.Rank(tsQuery) })
                .OrderByDescending(x => x.Rank)
                .ThenByDescending(x => x.Property.CreatedAt)
                .Select(x => x.Property);

            var items = await orderedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        var sortedQuery = effectiveSort switch
        {
            "price_asc" => q.OrderBy(p => p.Price).ThenByDescending(p => p.CreatedAt),
            "price_desc" => q.OrderByDescending(p => p.Price).ThenByDescending(p => p.CreatedAt),
            _ => q.OrderByDescending(p => p.CreatedAt)
        };

        var results = await sortedQuery.Skip((page - 1) * pageSize)
                                       .Take(pageSize)
                                       .ToListAsync();

        return (results, totalCount);
    }

    private bool IsNpgsqlProvider =>
        _context.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";
}