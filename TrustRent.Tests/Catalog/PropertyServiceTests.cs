using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Moq;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Modules.Catalog.Services;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;

namespace TrustRent.Tests.Catalog;

public class PropertyServiceTests
{
    private readonly Mock<ICatalogUnitOfWork> _uowMock;
    private readonly Mock<IBackgroundJobClient> _bgJobsMock;
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<ILeasingAccessService> _leasingAccessMock;
    private readonly Mock<IPropertyRepository> _propertyRepoMock;

    public PropertyServiceTests()
    {
        _uowMock = new Mock<ICatalogUnitOfWork>();
        _bgJobsMock = new Mock<IBackgroundJobClient>();
        _userServiceMock = new Mock<IUserService>();
        _leasingAccessMock = new Mock<ILeasingAccessService>();
        _propertyRepoMock = new Mock<IPropertyRepository>();
        _uowMock.Setup(u => u.Properties).Returns(_propertyRepoMock.Object);
    }

    private (PropertyService Service, CatalogDbContext Context) CreateServiceWithContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new CatalogDbContext(options);

        return (new PropertyService(_uowMock.Object, _bgJobsMock.Object, context, _userServiceMock.Object, _leasingAccessMock.Object), context);
    }

    private PropertyService CreateService() => CreateServiceWithContext().Service;

    private static CreatePropertyDto CreateValidPropertyDto() => new()
    {
        Title = "Test",
        Description = "Descricao",
        Price = 500m,
        PropertyType = "Apartamento",
        Typology = "T1",
        Area = 50m,
        Rooms = 1,
        Bathrooms = 1,
        Floor = "1",
        District = "Lisboa",
        Municipality = "Lisboa",
        Parish = "Arroios",
        DoorNumber = "1",
        Street = "Rua Teste",
        PostalCode = "1000-001",
        AdvanceRentMonths = 0,
        LeaseRegime = "PermanentHousing"
    };

    private static List<int> CreateValidAcceptedPeriodicities() => new() { 36 };

    // --- GetPropertyByIdAsync ---

    [Fact]
    public async Task GetPropertyByIdAsync_Existing_ReturnsProperty()
    {
        var propertyId = Guid.NewGuid();
        var property = new Property
        {
            Id = propertyId,
            Title = "Test",
            LandlordId = Guid.NewGuid()
        };
        _propertyRepoMock.Setup(r => r.GetByIdWithImagesAsync(propertyId)).ReturnsAsync(property);

        var service = CreateService();
        var result = await service.GetPropertyByIdAsync(propertyId);

        Assert.NotNull(result);
        Assert.Equal(propertyId, result!.Id);
    }

    [Fact]
    public async Task GetPropertyByIdAsync_NonExistent_ReturnsNull()
    {
        _propertyRepoMock.Setup(r => r.GetByIdWithImagesAsync(It.IsAny<Guid>())).ReturnsAsync((Property?)null);

        var service = CreateService();
        var result = await service.GetPropertyByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    // --- GetPropertiesByLandlordAsync ---

    [Fact]
    public async Task GetPropertiesByLandlordAsync_ReturnsPropertySummaries()
    {
        var landlordId = Guid.NewGuid();
        var properties = new List<Property>
        {
            new() { Id = Guid.NewGuid(), Title = "Prop 1", LandlordId = landlordId, Images = new List<PropertyImage> { new() { Id = Guid.NewGuid(), Url = "http://test.com/1.jpg", IsMain = true } } },
            new() { Id = Guid.NewGuid(), Title = "Prop 2", LandlordId = landlordId, Images = new List<PropertyImage>() }
        };
        _propertyRepoMock.Setup(r => r.GetByLandlordIdWithImagesAsync(landlordId)).ReturnsAsync(properties);

        var service = CreateService();
        var result = (await service.GetPropertiesByLandlordAsync(landlordId)).ToList();

        Assert.Equal(2, result.Count);
    }

    // --- GetAllAmenitiesAsync ---

    [Fact]
    public async Task GetAllAmenitiesAsync_ReturnsAmenities()
    {
        var amenities = new List<Amenity>
        {
            new() { Id = Guid.NewGuid(), Name = "Wi-Fi", IconName = "wifi", Category = "Tech" },
            new() { Id = Guid.NewGuid(), Name = "Parking", IconName = "car", Category = "Facility" }
        };
        _propertyRepoMock.Setup(r => r.GetAllAmenitiesAsync()).ReturnsAsync(amenities);

        var service = CreateService();
        var result = (await service.GetAllAmenitiesAsync()).ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task SearchPropertiesAsync_WhenUserHasApplication_AnnotatesPropertySearchDto()
    {
        var (service, context) = CreateServiceWithContext();
        var tenantId = Guid.NewGuid();
        var propertyId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();

        _propertyRepoMock
            .Setup(r => r.SearchAsync(It.IsAny<PropertySearchQuery>(), It.IsAny<IReadOnlyCollection<Guid>>()))
            .ReturnsAsync((new[]
            {
                new Property
                {
                    Id = propertyId,
                    Title = "Test",
                    Municipality = "Lisboa",
                    Parish = "Arroios",
                    Price = 950m,
                    PropertyType = "Apartamento",
                    Typology = "T2",
                    Area = 80m,
                    Rooms = 2,
                    Bathrooms = 1,
                    AllowsPets = true,
                    HasOfficialContract = true,
                    Images = new List<PropertyImage>()
                }
            }, 1));

        context.Applications.Add(new Application
        {
            Id = applicationId,
            PropertyId = propertyId,
            TenantId = tenantId,
            Message = "Aplicacao ativa",
            DurationMonths = 12,
            Status = ApplicationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var result = await service.SearchPropertiesAsync(new PropertySearchQuery(), tenantId);
        var item = Assert.Single(result.Items);

        Assert.True(item.HasSubmittedApplication);
        Assert.True(item.HasActiveApplication);
        Assert.Equal(applicationId, item.ExistingApplicationId);
        Assert.Equal("Pending", item.ExistingApplicationStatus);
    }

    // --- ValidateFinancialTerms (tested through CreatePropertyAsync) ---

    [Fact]
    public async Task CreatePropertyAsync_PersistsPropertyBeforeEnqueueingUploadJob()
    {
        var service = CreateService();
        var landlordId = Guid.NewGuid();
        var dto = CreateValidPropertyDto();

        var propertyId = await service.CreatePropertyAsync(
            landlordId,
            dto,
            Enumerable.Empty<FileDto>(),
            new List<string>(),
            0,
            Enumerable.Empty<FileDto>(),
            acceptedPeriodicities: CreateValidAcceptedPeriodicities());

        Assert.NotEqual(Guid.Empty, propertyId);
        _propertyRepoMock.Verify(r => r.AddAsync(It.Is<Property>(p => p.Id == propertyId && p.LandlordId == landlordId && p.IsUnderMaintenance)), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        _bgJobsMock.Verify(bg => bg.Create(It.IsAny<Job>(), It.IsAny<IState>()), Times.Once);
    }

    [Fact]
    public async Task CreatePropertyAsync_WithAcceptedPeriodicities_AddsThemToProperty()
    {
        var service = CreateService();
        var landlordId = Guid.NewGuid();
        Property? capturedProperty = null;

        _propertyRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Property>()))
            .Callback<Property>(property => capturedProperty = property)
            .Returns(Task.CompletedTask);

        var dto = new CreatePropertyDto
        {
            Title = "Test",
            Description = "Descricao",
            Price = 850m,
            AdvanceRentMonths = 0,
            LeaseRegime = "PermanentHousing"
        };

        await service.CreatePropertyAsync(
            landlordId,
            dto,
            Enumerable.Empty<FileDto>(),
            new List<string>(),
            0,
            Enumerable.Empty<FileDto>(),
            acceptedPeriodicities: new List<int> { 36, 48, 60 });

        Assert.NotNull(capturedProperty);
        Assert.Equal(new[] { 36, 48, 60 }, capturedProperty!.AcceptedPeriodicities.Select(p => p.DurationMonths).OrderBy(months => months));
    }

    [Fact]
    public async Task CreatePropertyAsync_WithoutLeaseRegime_ThrowsException()
    {
        var service = CreateService();
        var dto = CreateValidPropertyDto();
        dto.LeaseRegime = string.Empty;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePropertyAsync(Guid.NewGuid(), dto,
                Enumerable.Empty<FileDto>(),
                new List<string>(), 0,
                Enumerable.Empty<FileDto>(),
                acceptedPeriodicities: CreateValidAcceptedPeriodicities()));

        Assert.Contains("habitacao permanente", ex.Message);
    }

    [Fact]
    public async Task CreatePropertyAsync_WithoutAcceptedPeriodicities_ThrowsException()
    {
        var service = CreateService();
        var dto = CreateValidPropertyDto();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePropertyAsync(Guid.NewGuid(), dto,
                Enumerable.Empty<FileDto>(),
                new List<string>(), 0,
                Enumerable.Empty<FileDto>(),
                acceptedPeriodicities: new List<int>()));

        Assert.Contains("periodicidade", ex.Message);
    }

    [Fact]
    public async Task UpdatePropertyAsync_UpdatesAcceptedPeriodicitiesCollection()
    {
        var service = CreateService();
        var propertyId = Guid.NewGuid();
        var landlordId = Guid.NewGuid();
        var property = new Property
        {
            Id = propertyId,
            LandlordId = landlordId,
            Title = "Original",
            Description = "Descricao original",
            Price = 900m,
            PropertyType = "Apartamento",
            Typology = "T2",
            Area = 75m,
            Rooms = 2,
            Bathrooms = 1,
            Floor = "2",
            District = "Lisboa",
            Municipality = "Lisboa",
            Parish = "Arroios",
            Street = "Rua do Teste",
            DoorNumber = "10",
            PostalCode = "1000-001",
            AcceptedPeriodicities = new List<PropertyPeriodicity>
            {
                new() { Id = Guid.NewGuid(), PropertyId = propertyId, DurationMonths = 36 },
                new() { Id = Guid.NewGuid(), PropertyId = propertyId, DurationMonths = 48 }
            }
        };

        _propertyRepoMock
            .Setup(r => r.GetByIdAndLandlordWithImagesAsync(propertyId, landlordId))
            .ReturnsAsync(property);

        var dto = new CreatePropertyDto
        {
            Title = "Atualizado",
            Description = "Descricao atualizada",
            Price = 950m,
            PropertyType = "Apartamento",
            Typology = "T2",
            Area = 80m,
            Rooms = 2,
            Bathrooms = 1,
            Floor = "3",
            Street = "Rua do Teste",
            District = "Lisboa",
            Municipality = "Lisboa",
            Parish = "Arroios",
            DoorNumber = "10",
            PostalCode = "1000-001",
            AdvanceRentMonths = 0,
            LeaseRegime = "PermanentHousing"
        };

        await service.UpdatePropertyAsync(
            propertyId,
            landlordId,
            dto,
            Enumerable.Empty<FileDto>(),
            new List<string>(),
            new List<Guid>(),
            -1,
            null,
            acceptedPeriodicities: new List<int> { 36, 60 });

        Assert.Equal(new[] { 36, 60 }, property.AcceptedPeriodicities.Select(p => p.DurationMonths).OrderBy(months => months));
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreatePropertyAsync_AdvanceRentTooHigh_ThrowsException()
    {
        var service = CreateService();
        var dto = CreateValidPropertyDto();
        dto.AdvanceRentMonths = 3; // Max is 2

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePropertyAsync(Guid.NewGuid(), dto,
                Enumerable.Empty<FileDto>(),
                new List<string>(), 0,
                Enumerable.Empty<FileDto>(),
                acceptedPeriodicities: CreateValidAcceptedPeriodicities()));
    }

    [Fact]
    public async Task CreatePropertyAsync_NegativeDeposit_ThrowsException()
    {
        var service = CreateService();
        var dto = CreateValidPropertyDto();
        dto.Deposit = -100m;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePropertyAsync(Guid.NewGuid(), dto,
                Enumerable.Empty<FileDto>(),
                new List<string>(), 0,
                Enumerable.Empty<FileDto>(),
                acceptedPeriodicities: CreateValidAcceptedPeriodicities()));
    }

    [Fact]
    public async Task CreatePropertyAsync_DepositExceedsTwoMonthsRent_ThrowsException()
    {
        var service = CreateService();
        var dto = CreateValidPropertyDto();
        dto.Deposit = 1100m; // Max = 500 * 2 = 1000

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePropertyAsync(Guid.NewGuid(), dto,
                Enumerable.Empty<FileDto>(),
                new List<string>(), 0,
                Enumerable.Empty<FileDto>(),
                acceptedPeriodicities: CreateValidAcceptedPeriodicities()));
    }

    [Fact]
    public async Task CreatePropertyAsync_ZeroPriceWithDeposit_ThrowsException()
    {
        var service = CreateService();
        var dto = CreateValidPropertyDto();
        dto.Price = 0m;
        dto.Deposit = 500m;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePropertyAsync(Guid.NewGuid(), dto,
                Enumerable.Empty<FileDto>(),
                new List<string>(), 0,
                Enumerable.Empty<FileDto>(),
                acceptedPeriodicities: CreateValidAcceptedPeriodicities()));
    }
}
