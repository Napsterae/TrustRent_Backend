using Microsoft.EntityFrameworkCore;
using TrustRent.Api.Services;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Shared.Models;

namespace TrustRent.Tests.Api;

public class CatalogUserContactAccessServiceTests
{
    private (CatalogDbContext Catalog, LeasingDbContext Leasing) CreateContexts()
    {
        var catalogOptions = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var leasingOptions = new DbContextOptionsBuilder<LeasingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return (new CatalogDbContext(catalogOptions), new LeasingDbContext(leasingOptions));
    }

    [Fact]
    public async Task CanViewDirectContactAsync_SameUser_ReturnsTrue()
    {
        var (catalog, leasing) = CreateContexts();
        var sut = new CatalogUserContactAccessService(catalog, leasing);
        var userId = Guid.NewGuid();

        var result = await sut.CanViewDirectContactAsync(userId, userId);

        Assert.True(result);
        catalog.Dispose();
        leasing.Dispose();
    }

    [Fact]
    public async Task CanViewDirectContactAsync_EmptyGuids_ReturnsFalse()
    {
        var (catalog, leasing) = CreateContexts();
        var sut = new CatalogUserContactAccessService(catalog, leasing);

        var result = await sut.CanViewDirectContactAsync(Guid.Empty, Guid.NewGuid());

        Assert.False(result);
        catalog.Dispose();
        leasing.Dispose();
    }

    [Fact]
    public async Task CanViewDirectContactAsync_SharedLease_ReturnsTrue()
    {
        var (catalog, leasing) = CreateContexts();
        var tenantId = Guid.NewGuid();
        var landlordId = Guid.NewGuid();

        leasing.Leases.Add(new Lease
        {
            Id = Guid.NewGuid(),
            PropertyId = Guid.NewGuid(),
            TenantId = tenantId,
            LandlordId = landlordId,
            ApplicationId = Guid.NewGuid(),
            MonthlyRent = 500m,
            DurationMonths = 12,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(12),
            Status = LeaseStatus.Active
        });
        await leasing.SaveChangesAsync();

        var sut = new CatalogUserContactAccessService(catalog, leasing);
        var result = await sut.CanViewDirectContactAsync(tenantId, landlordId);

        Assert.True(result);
        catalog.Dispose();
        leasing.Dispose();
    }

    [Fact]
    public async Task CanViewDirectContactAsync_CancelledLease_NoApplication_ReturnsFalse()
    {
        var (catalog, leasing) = CreateContexts();
        var tenantId = Guid.NewGuid();
        var landlordId = Guid.NewGuid();

        leasing.Leases.Add(new Lease
        {
            Id = Guid.NewGuid(),
            PropertyId = Guid.NewGuid(),
            TenantId = tenantId,
            LandlordId = landlordId,
            ApplicationId = Guid.NewGuid(),
            MonthlyRent = 500m,
            DurationMonths = 12,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(12),
            Status = LeaseStatus.Cancelled
        });
        await leasing.SaveChangesAsync();

        var sut = new CatalogUserContactAccessService(catalog, leasing);
        var result = await sut.CanViewDirectContactAsync(tenantId, landlordId);

        Assert.False(result);
        catalog.Dispose();
        leasing.Dispose();
    }

    [Fact]
    public async Task CanViewDirectContactAsync_ActiveApplication_ReturnsTrue()
    {
        var (catalog, leasing) = CreateContexts();
        var tenantId = Guid.NewGuid();
        var landlordId = Guid.NewGuid();

        var property = new Property
        {
            Id = Guid.NewGuid(),
            LandlordId = landlordId,
            Title = "Test",
            Description = "Test",
            Price = 500,
            PropertyType = "Apartment",
            Typology = "T1",
            Area = 50,
            Rooms = 1,
            Bathrooms = 1,
            Floor = "1",
            Street = "Rua X",
            District = "Lisboa",
            Municipality = "Lisboa",
            Parish = "Arroios",
            DoorNumber = "1",
            PostalCode = "1000-001"
        };
        catalog.Properties.Add(property);

        catalog.Applications.Add(new Application
        {
            Id = Guid.NewGuid(),
            PropertyId = property.Id,
            TenantId = tenantId,
            Status = ApplicationStatus.Pending
        });
        await catalog.SaveChangesAsync();

        var sut = new CatalogUserContactAccessService(catalog, leasing);
        var result = await sut.CanViewDirectContactAsync(tenantId, landlordId);

        Assert.True(result);
        catalog.Dispose();
        leasing.Dispose();
    }

    [Fact]
    public async Task CanViewDirectContactAsync_NoRelationship_ReturnsFalse()
    {
        var (catalog, leasing) = CreateContexts();
        var sut = new CatalogUserContactAccessService(catalog, leasing);

        var result = await sut.CanViewDirectContactAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result);
        catalog.Dispose();
        leasing.Dispose();
    }
}
