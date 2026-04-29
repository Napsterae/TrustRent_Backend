using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

public class ApplicationServiceTests
{
    private readonly Mock<INotificationService> _notificationMock;
    private readonly Mock<ILeasingAccessService> _leasingAccessMock;
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;

    public ApplicationServiceTests()
    {
        _notificationMock = new Mock<INotificationService>();
        _leasingAccessMock = new Mock<ILeasingAccessService>();
        _userServiceMock = new Mock<IUserService>();
        _userRepositoryMock = new Mock<IUserRepository>();
    }

    private (ApplicationService Service, CatalogDbContext Context) CreateService()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new CatalogDbContext(options);

        var service = new ApplicationService(context, _notificationMock.Object, _leasingAccessMock.Object, _userServiceMock.Object, _userRepositoryMock.Object, new ServiceCollection().BuildServiceProvider());
        return (service, context);
    }

    private Property CreateTestProperty(Guid? landlordId = null)
    {
        return new Property
        {
            Id = Guid.NewGuid(),
            LandlordId = landlordId ?? Guid.NewGuid(),
            Title = "Test Property",
            Description = "A test property",
            Price = 800m,
            PropertyType = "Apartment",
            Typology = "T2",
            Area = 80m,
            Rooms = 2,
            Bathrooms = 1,
            Floor = "3",
            District = "Lisboa",
            Municipality = "Lisboa",
            Parish = "Arroios",
            DoorNumber = "1A",
            Street = "Rua Test",
            PostalCode = "1000-001",
            IsPublic = true,
            IsUnderMaintenance = false
        };
    }

    // --- SubmitApplicationAsync ---

    [Fact]
    public async Task SubmitApplicationAsync_ValidData_CreatesApplication()
    {
        var (service, context) = CreateService();
        var property = CreateTestProperty();
        context.Properties.Add(property);
        await context.SaveChangesAsync();

        var tenantId = Guid.NewGuid();
        var dto = new SubmitApplicationDto
        {
            Message = "I'm interested in this property",
            WantsVisit = true,
            DurationMonths = 12,
            SelectedDates = new List<string> { "2025-02-01", "2025-02-05" }
        };

        var result = await service.SubmitApplicationAsync(property.Id, tenantId, dto);

        Assert.NotNull(result);
        Assert.Equal("Pending", result.Status);
        Assert.Equal(property.Id, result.PropertyId);

        // Verify notification sent to landlord
        _notificationMock.Verify(n => n.SendNotificationAsync(
            property.LandlordId, "application", It.IsAny<string>(), It.IsAny<Guid>()), Times.Once);

        context.Dispose();
    }

    [Fact]
    public async Task SubmitApplicationAsync_PropertyNotFound_ThrowsException()
    {
        var (service, context) = CreateService();

        var dto = new SubmitApplicationDto { Message = "Test", DurationMonths = 12 };

        await Assert.ThrowsAsync<Exception>(
            () => service.SubmitApplicationAsync(Guid.NewGuid(), Guid.NewGuid(), dto));

        context.Dispose();
    }

    [Fact]
    public async Task SubmitApplicationAsync_PropertyNotPublic_ThrowsException()
    {
        var (service, context) = CreateService();
        var property = CreateTestProperty();
        property.IsPublic = false;
        context.Properties.Add(property);
        await context.SaveChangesAsync();

        var dto = new SubmitApplicationDto { Message = "Test", DurationMonths = 12 };

        var ex = await Assert.ThrowsAsync<Exception>(
            () => service.SubmitApplicationAsync(property.Id, Guid.NewGuid(), dto));
        Assert.Contains("não está disponível", ex.Message);

        context.Dispose();
    }

    [Fact]
    public async Task SubmitApplicationAsync_PropertyAlreadyRented_ThrowsException()
    {
        var (service, context) = CreateService();
        var property = CreateTestProperty();
        property.TenantId = Guid.NewGuid(); // Already has a tenant
        context.Properties.Add(property);
        await context.SaveChangesAsync();

        var dto = new SubmitApplicationDto { Message = "Test", DurationMonths = 12 };

        var ex = await Assert.ThrowsAsync<Exception>(
            () => service.SubmitApplicationAsync(property.Id, Guid.NewGuid(), dto));
        Assert.Contains("arrendado", ex.Message);

        context.Dispose();
    }

    [Fact]
    public async Task SubmitApplicationAsync_LandlordApplyingToOwnProperty_ThrowsException()
    {
        var (service, context) = CreateService();
        var landlordId = Guid.NewGuid();
        var property = CreateTestProperty(landlordId: landlordId);
        context.Properties.Add(property);
        await context.SaveChangesAsync();

        var dto = new SubmitApplicationDto { Message = "Test", DurationMonths = 12 };

        var ex = await Assert.ThrowsAsync<Exception>(
            () => service.SubmitApplicationAsync(property.Id, landlordId, dto));
        Assert.Contains("proprietário", ex.Message);

        context.Dispose();
    }

    // --- GetApplicationByIdAsync ---

    [Fact]
    public async Task GetApplicationByIdAsync_ExistingApplication_ReturnsDto()
    {
        var (service, context) = CreateService();
        var property = CreateTestProperty();
        context.Properties.Add(property);

        var application = new Application
        {
            Id = Guid.NewGuid(),
            PropertyId = property.Id,
            TenantId = Guid.NewGuid(),
            Message = "Test",
            DurationMonths = 12,
            Status = ApplicationStatus.Pending
        };
        context.Applications.Add(application);
        await context.SaveChangesAsync();

        _leasingAccessMock.Setup(l => l.GetLeaseByApplicationIdAsync(application.Id))
            .ReturnsAsync((TrustRent.Shared.Contracts.DTOs.LeaseDto?)null);

        var result = await service.GetApplicationByIdAsync(application.Id, application.TenantId);

        Assert.NotNull(result);
        Assert.Equal(application.Id, result!.Id);

        context.Dispose();
    }

    [Fact]
    public async Task GetApplicationByIdAsync_NonExistent_ReturnsNull()
    {
        var (service, context) = CreateService();

        var result = await service.GetApplicationByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);

        context.Dispose();
    }

    // --- GetApplicationsForPropertyAsync ---

    [Fact]
    public async Task GetApplicationsForPropertyAsync_MultipleApps_ReturnsAll()
    {
        var (service, context) = CreateService();
        var property = CreateTestProperty();
        context.Properties.Add(property);

        context.Applications.AddRange(
            new Application { Id = Guid.NewGuid(), PropertyId = property.Id, TenantId = Guid.NewGuid(), Message = "App 1", DurationMonths = 12, Status = ApplicationStatus.Pending },
            new Application { Id = Guid.NewGuid(), PropertyId = property.Id, TenantId = Guid.NewGuid(), Message = "App 2", DurationMonths = 6, Status = ApplicationStatus.Pending }
        );
        await context.SaveChangesAsync();

        _leasingAccessMock.Setup(l => l.GetLeasesByApplicationIdsAsync(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, TrustRent.Shared.Contracts.DTOs.LeaseDto>());

        var result = (await service.GetApplicationsForPropertyAsync(property.Id, property.LandlordId)).ToList();

        Assert.Equal(2, result.Count);

        context.Dispose();
    }

    // --- GetApplicationsForTenantAsync ---

    [Fact]
    public async Task GetApplicationsForTenantAsync_ReturnsOnlyTenantApps()
    {
        var (service, context) = CreateService();
        var tenantId = Guid.NewGuid();
        var property = CreateTestProperty();
        context.Properties.Add(property);

        context.Applications.AddRange(
            new Application { Id = Guid.NewGuid(), PropertyId = property.Id, TenantId = tenantId, Message = "Mine", DurationMonths = 12, Status = ApplicationStatus.Pending },
            new Application { Id = Guid.NewGuid(), PropertyId = property.Id, TenantId = Guid.NewGuid(), Message = "Other", DurationMonths = 12, Status = ApplicationStatus.Pending }
        );
        await context.SaveChangesAsync();

        _leasingAccessMock.Setup(l => l.GetLeasesByApplicationIdsAsync(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, TrustRent.Shared.Contracts.DTOs.LeaseDto>());

        var result = (await service.GetApplicationsForTenantAsync(tenantId)).ToList();

        Assert.Single(result);

        context.Dispose();
    }

    [Fact]
    public async Task GetApplicationsForTenantAsync_IncludesCoTenantApps()
    {
        var (service, context) = CreateService();
        var tenantId = Guid.NewGuid();
        var coTenantId = Guid.NewGuid();
        var property = CreateTestProperty();
        context.Properties.Add(property);

        context.Applications.AddRange(
            new Application { Id = Guid.NewGuid(), PropertyId = property.Id, TenantId = tenantId, CoTenantUserId = coTenantId, Message = "Joint", DurationMonths = 12, Status = ApplicationStatus.Pending },
            new Application { Id = Guid.NewGuid(), PropertyId = property.Id, TenantId = Guid.NewGuid(), Message = "Other", DurationMonths = 12, Status = ApplicationStatus.Pending }
        );
        await context.SaveChangesAsync();

        _leasingAccessMock.Setup(l => l.GetLeasesByApplicationIdsAsync(It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(new Dictionary<Guid, TrustRent.Shared.Contracts.DTOs.LeaseDto>());

        var result = (await service.GetApplicationsForTenantAsync(coTenantId)).ToList();

        Assert.Single(result);
        Assert.Equal(coTenantId, result[0].CoTenantUserId);

        context.Dispose();
    }

    [Fact]
    public async Task UpdateVisitStatus_CoTenantCannotConfirmInterest()
    {
        var (service, context) = CreateService();
        var coTenantId = Guid.NewGuid();
        var property = CreateTestProperty();
        context.Properties.Add(property);
        var application = new Application
        {
            Id = Guid.NewGuid(),
            PropertyId = property.Id,
            TenantId = Guid.NewGuid(),
            CoTenantUserId = coTenantId,
            Message = "Joint",
            DurationMonths = 12,
            Status = ApplicationStatus.VisitAccepted
        };
        context.Applications.Add(application);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.UpdateVisitStatusAsync(application.Id, coTenantId, new UpdateApplicationVisitDto { Action = "TenantConfirmInterest" }));

        context.Dispose();
    }

    [Fact]
    public async Task GetApplicationByIdAsync_GuarantorAcceptedButNotApproved_Throws()
    {
        var (service, context) = CreateService();
        var guarantorUserId = Guid.NewGuid();
        var property = CreateTestProperty();
        context.Properties.Add(property);
        var application = new Application
        {
            Id = Guid.NewGuid(),
            PropertyId = property.Id,
            TenantId = Guid.NewGuid(),
            Message = "Needs guarantor",
            DurationMonths = 12,
            Status = ApplicationStatus.GuarantorReview,
            GuarantorRequirementStatus = GuarantorRequirementStatus.LandlordReviewing
        };
        application.Guarantors.Add(new Guarantor
        {
            Id = Guid.NewGuid(),
            ApplicationId = application.Id,
            UserId = guarantorUserId,
            InvitedByUserId = application.TenantId,
            InviteStatus = GuarantorInviteStatus.Accepted,
            ExpiresAt = DateTime.UtcNow.AddDays(5)
        });
        context.Applications.Add(application);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetApplicationByIdAsync(application.Id, guarantorUserId));

        context.Dispose();
    }
}
