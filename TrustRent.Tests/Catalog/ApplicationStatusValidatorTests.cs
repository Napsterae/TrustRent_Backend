using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Modules.Catalog.Services;
using TrustRent.Shared.Models;

namespace TrustRent.Tests.Catalog;

public class ApplicationStatusValidatorTests
{
    private CatalogDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CatalogDbContext(options);
    }

    [Fact]
    public async Task IsApplicationChatLockedAsync_RejectedApplication_ReturnsTrue()
    {
        using var context = CreateContext();
        var app = new Application
        {
            Id = Guid.NewGuid(),
            PropertyId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Status = ApplicationStatus.Rejected
        };
        context.Applications.Add(app);
        await context.SaveChangesAsync();

        var sut = new ApplicationStatusValidator(context);
        var result = await sut.IsApplicationChatLockedAsync(app.Id);

        Assert.True(result);
    }

    [Fact]
    public async Task IsApplicationChatLockedAsync_AcceptedApplication_ReturnsTrue()
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

        var sut = new ApplicationStatusValidator(context);
        var result = await sut.IsApplicationChatLockedAsync(app.Id);

        Assert.True(result);
    }

    [Fact]
    public async Task IsApplicationChatLockedAsync_PendingApplication_ReturnsFalse()
    {
        using var context = CreateContext();
        var app = new Application
        {
            Id = Guid.NewGuid(),
            PropertyId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Status = ApplicationStatus.Pending
        };
        context.Applications.Add(app);
        await context.SaveChangesAsync();

        var sut = new ApplicationStatusValidator(context);
        var result = await sut.IsApplicationChatLockedAsync(app.Id);

        Assert.False(result);
    }

    [Fact]
    public async Task IsApplicationChatLockedAsync_NonExistentApplication_ReturnsTrue()
    {
        using var context = CreateContext();
        var sut = new ApplicationStatusValidator(context);

        var result = await sut.IsApplicationChatLockedAsync(Guid.NewGuid());

        Assert.True(result); // Security: locked if not found
    }

    [Fact]
    public async Task GetApplicationParticipantsAsync_ValidApplication_ReturnsTuple()
    {
        using var context = CreateContext();
        var landlordId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var property = new Property
        {
            Id = Guid.NewGuid(),
            LandlordId = landlordId,
            Title = "Test Property",
            Description = "Test",
            Price = 500,
            PropertyType = "Apartment",
            Typology = "T2",
            Area = 80,
            Rooms = 2,
            Bathrooms = 1,
            Floor = "1",
            Street = "Rua X",
            District = "Lisboa",
            Municipality = "Lisboa",
            Parish = "Arroios",
            DoorNumber = "1",
            PostalCode = "1000-001"
        };
        context.Properties.Add(property);

        var app = new Application
        {
            Id = Guid.NewGuid(),
            PropertyId = property.Id,
            TenantId = tenantId,
            Status = ApplicationStatus.Pending,
            Property = property
        };
        context.Applications.Add(app);
        await context.SaveChangesAsync();

        var sut = new ApplicationStatusValidator(context);
        var result = await sut.GetApplicationParticipantsAsync(app.Id);

        Assert.NotNull(result);
        Assert.Equal(tenantId, result!.Value.TenantId);
        Assert.Equal(landlordId, result.Value.LandlordId);
    }

    [Fact]
    public async Task GetApplicationParticipantsAsync_NonExistent_ReturnsNull()
    {
        using var context = CreateContext();
        var sut = new ApplicationStatusValidator(context);

        var result = await sut.GetApplicationParticipantsAsync(Guid.NewGuid());

        Assert.Null(result);
    }
}
