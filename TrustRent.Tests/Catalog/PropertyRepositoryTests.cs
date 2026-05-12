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
        Guid? tenantId = null)
    {
        return new Property
        {
            Id = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            Title = title,
            Price = price,
            PropertyType = "Apartamento",
            Typology = "T2",
            District = "Lisboa",
            Municipality = "Lisboa",
            Parish = "Avenidas Novas",
            IsPublic = isPublic,
            IsUnderMaintenance = isUnderMaintenance,
            IsBlocked = isBlocked,
            TenantId = tenantId,
            CreatedAt = createdAt,
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
}