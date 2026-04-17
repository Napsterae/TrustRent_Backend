using Microsoft.EntityFrameworkCore;
using TrustRent.Api.Services;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Shared.Models;

namespace TrustRent.Tests.Api;

public class CatalogAccessServiceTests
{
    private CatalogDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CatalogDbContext(options);
    }

    private Property CreateTestProperty(Guid? landlordId = null) => new()
    {
        Id = Guid.NewGuid(),
        LandlordId = landlordId ?? Guid.NewGuid(),
        Title = "Apartamento T2",
        Description = "Apartamento centro",
        Price = 800m,
        PropertyType = "Apartment",
        Typology = "T2",
        Area = 90,
        Rooms = 2,
        Bathrooms = 1,
        Floor = "3",
        Street = "Rua Augusta",
        District = "Lisboa",
        Municipality = "Lisboa",
        Parish = "Santa Maria Maior",
        DoorNumber = "45",
        PostalCode = "1100-001",
        Deposit = 1600m,
        AdvanceRentMonths = 1,
        HasOfficialContract = true,
        AllowsRenewal = true
    };

    [Fact]
    public async Task GetApplicationContextAsync_ValidApplication_ReturnsContext()
    {
        using var context = CreateContext();
        var property = CreateTestProperty();
        context.Properties.Add(property);

        var app = new Application
        {
            Id = Guid.NewGuid(),
            PropertyId = property.Id,
            TenantId = Guid.NewGuid(),
            DurationMonths = 12,
            Status = ApplicationStatus.Accepted,
            Property = property
        };
        context.Applications.Add(app);
        await context.SaveChangesAsync();

        var sut = new CatalogAccessService(context);
        var result = await sut.GetApplicationContextAsync(app.Id);

        Assert.NotNull(result);
        Assert.Equal(app.Id, result!.Id);
        Assert.Equal(property.LandlordId, result.LandlordId);
        Assert.Equal(property.Price, result.Price);
        Assert.Equal(property.Deposit, result.Deposit);
        Assert.Equal(12, result.DurationMonths);
    }

    [Fact]
    public async Task GetApplicationContextAsync_NonExistent_ReturnsNull()
    {
        using var context = CreateContext();
        var sut = new CatalogAccessService(context);

        var result = await sut.GetApplicationContextAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPropertyContextAsync_ValidProperty_ReturnsContext()
    {
        using var context = CreateContext();
        var property = CreateTestProperty();
        context.Properties.Add(property);
        await context.SaveChangesAsync();

        var sut = new CatalogAccessService(context);
        var result = await sut.GetPropertyContextAsync(property.Id);

        Assert.NotNull(result);
        Assert.Equal(property.Id, result!.Id);
        Assert.Equal(property.Title, result.Title);
        Assert.Equal(property.Price, result.Price);
    }

    [Fact]
    public async Task GetPropertyContextAsync_NonExistent_ReturnsNull()
    {
        using var context = CreateContext();
        var sut = new CatalogAccessService(context);

        var result = await sut.GetPropertyContextAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateApplicationStatusAsync_ValidApplication_UpdatesStatusAndHistory()
    {
        using var context = CreateContext();
        var app = new Application
        {
            Id = Guid.NewGuid(),
            PropertyId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Status = ApplicationStatus.Accepted
        };
        context.Applications.Add(app);
        await context.SaveChangesAsync();

        var actorId = Guid.NewGuid();
        var sut = new CatalogAccessService(context);
        await sut.UpdateApplicationStatusAsync(app.Id, (int)ApplicationStatus.LeaseActive, actorId, "Activated", "Lease is now active");

        var updated = await context.Applications.Include(a => a.History).FirstAsync(a => a.Id == app.Id);
        Assert.Equal(ApplicationStatus.LeaseActive, updated.Status);
        Assert.NotNull(updated.UpdatedAt);
        Assert.Single(updated.History);
        Assert.Equal("Activated", updated.History[0].Action);
    }

    [Fact]
    public async Task UpdateApplicationStatusAsync_NonExistent_DoesNotThrow()
    {
        using var context = CreateContext();
        var sut = new CatalogAccessService(context);

        await sut.UpdateApplicationStatusAsync(Guid.NewGuid(), (int)ApplicationStatus.Rejected, Guid.NewGuid(), "test");
        // Should not throw
    }

    [Fact]
    public async Task SetPropertyTenantAsync_ValidProperty_UpdatesTenantAndDelists()
    {
        using var context = CreateContext();
        var property = CreateTestProperty();
        property.IsPublic = true;
        context.Properties.Add(property);
        await context.SaveChangesAsync();

        var tenantId = Guid.NewGuid();
        var sut = new CatalogAccessService(context);
        await sut.SetPropertyTenantAsync(property.Id, tenantId);

        var updated = await context.Properties.FindAsync(property.Id);
        Assert.Equal(tenantId, updated!.TenantId);
        Assert.False(updated.IsPublic);
    }

    [Fact]
    public async Task SetPropertyTenantAsync_NonExistent_DoesNotThrow()
    {
        using var context = CreateContext();
        var sut = new CatalogAccessService(context);

        await sut.SetPropertyTenantAsync(Guid.NewGuid(), Guid.NewGuid());
    }
}
