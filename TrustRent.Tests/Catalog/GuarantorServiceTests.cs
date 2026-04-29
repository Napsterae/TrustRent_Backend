using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Modules.Catalog.Services;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Identity.Models;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;

namespace TrustRent.Tests.Catalog;

public class GuarantorServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IUserService> _userServiceMock = new();
    private readonly Mock<INotificationService> _notificationMock = new();
    private readonly Mock<IEmailService> _emailMock = new();

    private (GuarantorService Service, CatalogDbContext Db, Application App, Property Prop) Setup(bool acceptsGuarantor = true, bool officialContract = true)
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new CatalogDbContext(options);

        var landlordId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var prop = new Property
        {
            Id = Guid.NewGuid(), LandlordId = landlordId, Title = "Test", Description = "d",
            Price = 800m, PropertyType = "Apartment", Typology = "T2", Area = 80m, Rooms = 2, Bathrooms = 1,
            Floor = "1", District = "Lx", Municipality = "Lx", Parish = "P", DoorNumber = "1", Street = "R",
            PostalCode = "1000-001", IsPublic = true,
            HasOfficialContract = officialContract, AcceptsGuarantor = acceptsGuarantor
        };
        db.Properties.Add(prop);
        var app = new Application
        {
            Id = Guid.NewGuid(), PropertyId = prop.Id, TenantId = tenantId,
            Message = "m", DurationMonths = 12, Status = ApplicationStatus.Pending, CreatedAt = DateTime.UtcNow
        };
        db.Applications.Add(app);
        db.SaveChanges();

        _userRepoMock.Setup(r => r.GetByIdAsync(tenantId))
            .ReturnsAsync(new User { Id = tenantId, Email = "tenant@t.pt", Name = "Tenant" });
        _userRepoMock.Setup(r => r.GetByIdAsync(landlordId))
            .ReturnsAsync(new User { Id = landlordId, Email = "landlord@t.pt", Name = "Landlord" });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:BaseUrl"] = "http://localhost:5173" })
            .Build();
        var svc = new GuarantorService(db, _userRepoMock.Object, _userServiceMock.Object, _notificationMock.Object, _emailMock.Object, config);
        return (svc, db, app, prop);
    }

    [Fact]
    public async Task RequestGuarantor_AsLandlord_TransitionsApplication()
    {
        var (svc, db, app, prop) = Setup();

        var result = await svc.RequestGuarantorAsync(app.Id, prop.LandlordId, new RequestGuarantorDto("Necessário fiador."));

        var refreshed = await db.Applications.FirstAsync(a => a.Id == app.Id);
        Assert.True(refreshed.IsGuarantorRequired);
        Assert.Equal(GuarantorRequirementStatus.Requested, refreshed.GuarantorRequirementStatus);
        Assert.Equal(ApplicationStatus.GuarantorRequested, refreshed.Status);
    }

    [Fact]
    public async Task RequestGuarantor_NonLandlord_Throws()
    {
        var (svc, _, app, _) = Setup();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.RequestGuarantorAsync(app.Id, Guid.NewGuid(), new RequestGuarantorDto("nope")));
    }

    [Fact]
    public async Task RequestGuarantor_PropertyDoesNotAcceptGuarantor_Throws()
    {
        var (svc, _, app, prop) = Setup(acceptsGuarantor: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.RequestGuarantorAsync(app.Id, prop.LandlordId, new RequestGuarantorDto(null)));
    }

    [Fact]
    public async Task WaiveGuarantor_RequiresReason()
    {
        var (svc, _, app, prop) = Setup();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.WaiveGuarantorAsync(app.Id, prop.LandlordId, new WaiveGuarantorDto("")));
    }
}
