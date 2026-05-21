using Hangfire;
using Microsoft.EntityFrameworkCore;
using Moq;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Modules.Catalog.Services;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Shared.Contracts.Interfaces;

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
    public async Task CreatePropertyAsync_AdvanceRentTooHigh_ThrowsException()
    {
        var service = CreateService();
        var dto = new CreatePropertyDto
        {
            Title = "Test",
            Price = 500m,
            AdvanceRentMonths = 3 // Max is 2
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePropertyAsync(Guid.NewGuid(), dto,
                Enumerable.Empty<FileDto>(),
                new List<string>(), 0,
                Enumerable.Empty<FileDto>()));
    }

    [Fact]
    public async Task CreatePropertyAsync_NegativeDeposit_ThrowsException()
    {
        var service = CreateService();
        var dto = new CreatePropertyDto
        {
            Title = "Test",
            Price = 500m,
            AdvanceRentMonths = 0,
            Deposit = -100m
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePropertyAsync(Guid.NewGuid(), dto,
                Enumerable.Empty<FileDto>(),
                new List<string>(), 0,
                Enumerable.Empty<FileDto>()));
    }

    [Fact]
    public async Task CreatePropertyAsync_DepositExceedsTwoMonthsRent_ThrowsException()
    {
        var service = CreateService();
        var dto = new CreatePropertyDto
        {
            Title = "Test",
            Price = 500m,
            AdvanceRentMonths = 0,
            Deposit = 1100m // Max = 500 * 2 = 1000
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePropertyAsync(Guid.NewGuid(), dto,
                Enumerable.Empty<FileDto>(),
                new List<string>(), 0,
                Enumerable.Empty<FileDto>()));
    }

    [Fact]
    public async Task CreatePropertyAsync_ZeroPriceWithDeposit_ThrowsException()
    {
        var service = CreateService();
        var dto = new CreatePropertyDto
        {
            Title = "Test",
            Price = 0m,
            AdvanceRentMonths = 0,
            Deposit = 500m
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreatePropertyAsync(Guid.NewGuid(), dto,
                Enumerable.Empty<FileDto>(),
                new List<string>(), 0,
                Enumerable.Empty<FileDto>()));
    }
}
