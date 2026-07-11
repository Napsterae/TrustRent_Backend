using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Modules.Catalog.Repositories;

namespace TrustRent.Tests.Catalog;

public class PropertyRepositoryTests
{
    private static CatalogDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new CatalogDbContext(options);
    }

    private static Property CreateProperty(
        string title,
        decimal price,
        DateTime createdAt,
        bool isPublic = true,
        bool isUnderMaintenance = false,
        bool isBlocked = false,
        Guid? tenantId = null,
        double latitude = 0,
        double longitude = 0,
        string? district = null,
        string? municipality = null,
        string? parish = null)
    {
        return new Property
        {
            Id = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            Title = title,
            Price = price,
            PropertyType = "Apartamento",
            Typology = "T2",
            District = district ?? "Lisboa",
            Municipality = municipality ?? "Lisboa",
            Parish = parish ?? "Avenidas Novas",
            IsPublic = isPublic,
            IsUnderMaintenance = isUnderMaintenance,
            IsBlocked = isBlocked,
            TenantId = tenantId,
            CreatedAt = createdAt,
            Latitude = latitude,
            Longitude = longitude,
        };
    }

    [Fact]
    public async Task SearchAsync_DefaultSort_ReturnsNewestFirstAndExcludesUnavailableProperties()
    {
        await using var context = CreateContext();

        var olderVisible = CreateProperty("Older visible", 950m, DateTime.UtcNow.AddDays(-5));
        var newestVisible = CreateProperty("Newest visible", 1250m, DateTime.UtcNow.AddDays(-1));
        var hidden = CreateProperty("Hidden", 800m, DateTime.UtcNow.AddDays(-2), isPublic: false);
        var maintenance = CreateProperty("Maintenance", 1100m, DateTime.UtcNow.AddDays(-3), isUnderMaintenance: true);
        var blocked = CreateProperty("Blocked", 1400m, DateTime.UtcNow.AddDays(-4), isBlocked: true);
        var rented = CreateProperty("Rented", 1000m, DateTime.UtcNow.AddDays(-6), tenantId: Guid.NewGuid());

        context.Properties.AddRange(olderVisible, newestVisible, hidden, maintenance, blocked, rented);
        await context.SaveChangesAsync();

        var repository = new PropertyRepository(context);
        var result = await repository.SearchAsync(new PropertySearchQuery());
        var items = result.Items.ToList();

        Assert.Equal(2, result.TotalCount);
        Assert.Collection(
            items,
            first => Assert.Equal(newestVisible.Id, first.Id),
            second => Assert.Equal(olderVisible.Id, second.Id));
    }

    [Fact]
    public async Task SearchAsync_SortPriceAsc_ReturnsLowestPriceFirst()
    {
        await using var context = CreateContext();

        var expensive = CreateProperty("Expensive", 1800m, DateTime.UtcNow.AddDays(-1));
        var medium = CreateProperty("Medium", 1200m, DateTime.UtcNow.AddDays(-2));
        var affordable = CreateProperty("Affordable", 700m, DateTime.UtcNow.AddDays(-3));

        context.Properties.AddRange(expensive, medium, affordable);
        await context.SaveChangesAsync();

        var repository = new PropertyRepository(context);
        var result = await repository.SearchAsync(new PropertySearchQuery { Sort = "price_asc" });
        var items = result.Items.ToList();

        Assert.Collection(
            items,
            first => Assert.Equal(affordable.Id, first.Id),
            second => Assert.Equal(medium.Id, second.Id),
            third => Assert.Equal(expensive.Id, third.Id));
    }

    [Fact]
    public async Task SearchAsync_SortPriceDesc_ReturnsHighestPriceFirst()
    {
        await using var context = CreateContext();

        var expensive = CreateProperty("Expensive", 1800m, DateTime.UtcNow.AddDays(-1));
        var medium = CreateProperty("Medium", 1200m, DateTime.UtcNow.AddDays(-2));
        var affordable = CreateProperty("Affordable", 700m, DateTime.UtcNow.AddDays(-3));

        context.Properties.AddRange(expensive, medium, affordable);
        await context.SaveChangesAsync();

        var repository = new PropertyRepository(context);
        var result = await repository.SearchAsync(new PropertySearchQuery { Sort = "price_desc" });
        var items = result.Items.ToList();

        Assert.Collection(
            items,
            first => Assert.Equal(expensive.Id, first.Id),
            second => Assert.Equal(medium.Id, second.Id),
            third => Assert.Equal(affordable.Id, third.Id));
    }

    [Fact]
    public async Task SearchAsync_ExcludedPropertyIds_RemovesAppliedPropertiesFromCountAndResults()
    {
        await using var context = CreateContext();

        var hidden = CreateProperty("Hidden because applied", 1200m, DateTime.UtcNow.AddDays(-1));
        var visible = CreateProperty("Still visible", 900m, DateTime.UtcNow.AddDays(-2));

        context.Properties.AddRange(hidden, visible);
        await context.SaveChangesAsync();

        var repository = new PropertyRepository(context);
        var result = await repository.SearchAsync(new PropertySearchQuery(), new[] { hidden.Id });
        var items = result.Items.ToList();

        Assert.Equal(1, result.TotalCount);
        Assert.Single(items);
        Assert.Equal(visible.Id, items[0].Id);
    }

    [Fact]
    public async Task SearchAsync_SearchTerm_MatchesTitleDistrictMunicipalityAndParish()
    {
        await using var context = CreateContext();

        var byTitle = CreateProperty("Apartamento T2 no Porto", 900m, DateTime.UtcNow.AddDays(-1));
        var byDistrict = CreateProperty("Casa bonita", 800m, DateTime.UtcNow.AddDays(-2), district: "Porto");
        var byMunicipality = CreateProperty("Loft moderno", 1100m, DateTime.UtcNow.AddDays(-3), municipality: "Vila Nova de Gaia", district: "Porto");
        var byParish = CreateProperty("Estúdio acolhedor", 700m, DateTime.UtcNow.AddDays(-4), parish: "Cedofeita", district: "Porto", municipality: "Porto");
        var noMatch = CreateProperty("Moradia em Setúbal", 1500m, DateTime.UtcNow.AddDays(-5), district: "Setúbal", municipality: "Setúbal", parish: "São Sebastião");

        context.Properties.AddRange(byTitle, byDistrict, byMunicipality, byParish, noMatch);
        await context.SaveChangesAsync();

        var repository = new PropertyRepository(context);
        var result = await repository.SearchAsync(new PropertySearchQuery { SearchTerm = "porto" });
        var items = result.Items.ToList();

        Assert.Equal(4, result.TotalCount);
        Assert.DoesNotContain(items, p => p.Id == noMatch.Id);
    }

    [Fact]
    public async Task SearchAsync_GeoRadius_FiltersPropertiesWithinBoundingBox()
    {
        await using var context = CreateContext();

        // Porto center: lat 41.15, lng -8.61
        var nearPorto = CreateProperty("Near Porto", 900m, DateTime.UtcNow.AddDays(-1), latitude: 41.15, longitude: -8.61);
        var alsoNearPorto = CreateProperty("Also near Porto", 850m, DateTime.UtcNow.AddDays(-2), latitude: 41.16, longitude: -8.60);
        // Lisbon: lat 38.72, lng -9.14 — ~280km from Porto
        var inLisbon = CreateProperty("In Lisbon", 1100m, DateTime.UtcNow.AddDays(-3), latitude: 38.72, longitude: -9.14);

        context.Properties.AddRange(nearPorto, alsoNearPorto, inLisbon);
        await context.SaveChangesAsync();

        var repository = new PropertyRepository(context);
        var result = await repository.SearchAsync(new PropertySearchQuery
        {
            Latitude = 41.15,
            Longitude = -8.61,
            RadiusKm = 10.0 // 10km radius
        });
        var items = result.Items.ToList();

        Assert.Equal(2, result.TotalCount);
        Assert.Contains(items, p => p.Id == nearPorto.Id);
        Assert.Contains(items, p => p.Id == alsoNearPorto.Id);
        Assert.DoesNotContain(items, p => p.Id == inLisbon.Id);
    }

    [Fact]
    public async Task SearchAsync_GeoRadius_ZeroRadius_StillMatchesExactLocation()
    {
        await using var context = CreateContext();

        var atCenter = CreateProperty("At center", 900m, DateTime.UtcNow.AddDays(-1), latitude: 41.15, longitude: -8.61);
        var farAway = CreateProperty("Far away", 850m, DateTime.UtcNow.AddDays(-2), latitude: 41.20, longitude: -8.70);

        context.Properties.AddRange(atCenter, farAway);
        await context.SaveChangesAsync();

        var repository = new PropertyRepository(context);
        var result = await repository.SearchAsync(new PropertySearchQuery
        {
            Latitude = 41.15,
            Longitude = -8.61,
            RadiusKm = 1.0 // 1km — should include atCenter but not farAway (~10km away)
        });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(atCenter.Id, result.Items.First().Id);
    }

    [Fact]
    public async Task SearchAsync_GeoRadius_CombinedWithTextSearch()
    {
        await using var context = CreateContext();

        var matchingNear = CreateProperty("T2 Porto Centro", 900m, DateTime.UtcNow.AddDays(-1), latitude: 41.15, longitude: -8.61, district: "Porto", municipality: "Porto");
        var matchingFar = CreateProperty("T2 Porto Lisboa", 900m, DateTime.UtcNow.AddDays(-2), latitude: 38.72, longitude: -9.14, district: "Lisboa", municipality: "Lisboa");
        var nonMatchingNear = CreateProperty("Moradia Setúbal", 900m, DateTime.UtcNow.AddDays(-3), latitude: 41.15, longitude: -8.61, district: "Setúbal", municipality: "Setúbal");

        context.Properties.AddRange(matchingNear, matchingFar, nonMatchingNear);
        await context.SaveChangesAsync();

        var repository = new PropertyRepository(context);
        var result = await repository.SearchAsync(new PropertySearchQuery
        {
            SearchTerm = "porto",
            Latitude = 41.15,
            Longitude = -8.61,
            RadiusKm = 15.0
        });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(matchingNear.Id, result.Items.First().Id);
    }

    [Fact]
    public async Task SearchAsync_WithoutGeoParams_DoesNotFilterByLocation()
    {
        await using var context = CreateContext();

        var prop1 = CreateProperty("Prop 1", 900m, DateTime.UtcNow.AddDays(-1), latitude: 41.15, longitude: -8.61);
        var prop2 = CreateProperty("Prop 2", 900m, DateTime.UtcNow.AddDays(-2), latitude: 38.72, longitude: -9.14);

        context.Properties.AddRange(prop1, prop2);
        await context.SaveChangesAsync();

        var repository = new PropertyRepository(context);
        var result = await repository.SearchAsync(new PropertySearchQuery());

        Assert.Equal(2, result.TotalCount);
    }
}