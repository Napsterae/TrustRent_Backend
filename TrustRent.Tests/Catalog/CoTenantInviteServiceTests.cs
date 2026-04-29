using Microsoft.EntityFrameworkCore;
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

public class CoTenantInviteServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IUserService> _userServiceMock = new();
    private readonly Mock<INotificationService> _notificationMock = new();
    private readonly Mock<IEmailService> _emailMock = new();

    private (CoTenantInviteService Service, CatalogDbContext Db, Application App) Setup(string? coTenantEmail = "co@test.pt", Guid? coTenantUserId = null)
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new CatalogDbContext(options);

        var landlordId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var property = new Property
        {
            Id = Guid.NewGuid(), LandlordId = landlordId, Title = "Test", Description = "d",
            Price = 800m, PropertyType = "Apartment", Typology = "T2", Area = 80m, Rooms = 2, Bathrooms = 1,
            Floor = "1", District = "Lx", Municipality = "Lx", Parish = "P", DoorNumber = "1", Street = "R",
            PostalCode = "1000-001", IsPublic = true,
            HasOfficialContract = true, AcceptsGuarantor = true,
        };
        db.Properties.Add(property);
        var app = new Application
        {
            Id = Guid.NewGuid(), PropertyId = property.Id, TenantId = tenantId,
            Message = "m", DurationMonths = 12, Status = ApplicationStatus.Pending, CreatedAt = DateTime.UtcNow
        };
        db.Applications.Add(app);
        db.SaveChanges();

        if (coTenantEmail != null)
        {
            _userRepoMock.Setup(r => r.GetByEmailAsync(It.Is<string>(e => e == coTenantEmail)))
                .ReturnsAsync(new User { Id = coTenantUserId ?? Guid.NewGuid(), Email = coTenantEmail, Name = "Co Tenant" });
        }
        _userRepoMock.Setup(r => r.GetByEmailAsync(It.Is<string>(e => e != coTenantEmail)))
            .ReturnsAsync((User?)null);
        _userRepoMock.Setup(r => r.GetByIdAsync(tenantId))
            .ReturnsAsync(new User { Id = tenantId, Email = "principal@test.pt", Name = "Principal" });
        _userRepoMock.Setup(r => r.GetByIdAsync(landlordId))
            .ReturnsAsync(new User { Id = landlordId, Email = "landlord@test.pt", Name = "Landlord" });
        _userServiceMock.Setup(s => s.GetPublicProfileAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(new PublicUserProfileDto(tenantId, "Inviter", null, null, 0, false, false, null, null));

        var svc = new CoTenantInviteService(db, _userRepoMock.Object, _userServiceMock.Object, _notificationMock.Object, _emailMock.Object);
        return (svc, db, app);
    }

    [Fact]
    public async Task CreateInvite_FromPrincipalTenant_CreatesPendingInvite()
    {
        var (svc, db, app) = Setup();

        var dto = new CreateCoTenantInviteDto("co@test.pt");
        var result = await svc.CreateInviteAsync(app.Id, app.TenantId, dto, sourceIp: null);

        Assert.NotNull(result);
        Assert.Equal("Pending", result.Status);
        Assert.Single(db.ApplicationCoTenantInvites);
        _emailMock.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CreateInvite_FromOtherUser_Throws()
    {
        var (svc, _, app) = Setup();
        var someoneElse = Guid.NewGuid();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.CreateInviteAsync(app.Id, someoneElse, new CreateCoTenantInviteDto("co@test.pt"), null));
    }

    [Fact]
    public async Task CreateInvite_DuplicateSameEmail_ReturnsExistingInvite()
    {
        var (svc, _, app) = Setup();
        var first = await svc.CreateInviteAsync(app.Id, app.TenantId, new CreateCoTenantInviteDto("co@test.pt"), null);

        var second = await svc.CreateInviteAsync(app.Id, app.TenantId, new CreateCoTenantInviteDto("co@test.pt"), null);

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task CreateInvite_UserNotRegistered_ThrowsKeyNotFound()
    {
        var (svc, _, app) = Setup(coTenantEmail: null);

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            svc.CreateInviteAsync(app.Id, app.TenantId, new CreateCoTenantInviteDto("missing@test.pt"), null));
        Assert.Equal("user_not_registered", ex.Message);
    }

    [Fact]
    public async Task AcceptInvite_AssignsCoTenantToApplication()
    {
        var coUserId = Guid.NewGuid();
        var (svc, db, app) = Setup(coTenantUserId: coUserId);
        var invite = await svc.CreateInviteAsync(app.Id, app.TenantId, new CreateCoTenantInviteDto("co@test.pt"), null);

        await svc.AcceptInviteAsync(invite.Id, coUserId);

        var refreshed = await db.Applications.FirstAsync(a => a.Id == app.Id);
        Assert.Equal(coUserId, refreshed.CoTenantUserId);
    }
}
