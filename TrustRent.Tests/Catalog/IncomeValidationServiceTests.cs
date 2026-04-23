using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Modules.Catalog.Models.ReferenceData;
using TrustRent.Modules.Catalog.Services;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Identity.Models;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;
using TrustRent.Shared.Models.DocumentExtraction;

namespace TrustRent.Tests.Catalog;

public class IncomeValidationServiceTests
{
    private readonly Mock<IGeminiDocumentService> _geminiMock = new();
    private readonly Mock<INotificationService> _notificationMock = new();
    private readonly Mock<IUserService> _userServiceMock = new();
    private readonly Mock<ILogger<IncomeValidationService>> _loggerMock = new();

    private (IncomeValidationService Service, CatalogDbContext Context) CreateService()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new CatalogDbContext(options);
        var service = new IncomeValidationService(
            context, _geminiMock.Object, _notificationMock.Object, _userServiceMock.Object, _loggerMock.Object);
        return (service, context);
    }

    private static Property CreateProperty(Guid landlordId) => new()
    {
        Id = Guid.NewGuid(),
        LandlordId = landlordId,
        Title = "X",
        Description = "X",
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
        Street = "Rua",
        PostalCode = "1000-001",
        IsPublic = true
    };

    private static Application CreateApplication(Property property, Guid tenantId, ApplicationStatus status) => new()
    {
        Id = Guid.NewGuid(),
        PropertyId = property.Id,
        Property = property,
        TenantId = tenantId,
        Status = status,
        Message = "ola",
        DurationMonths = 12
    };

    private static SalaryRange[] DefaultRanges() => new[]
    {
        new SalaryRange { Id = Guid.NewGuid(), Code = "LT_1000", Label = "Até 1000€", MinAmount = null, MaxAmount = 1000m, DisplayOrder = 1, IsActive = true },
        new SalaryRange { Id = Guid.NewGuid(), Code = "R_1000_2000", Label = "1000€ - 2000€", MinAmount = 1000m, MaxAmount = 2000m, DisplayOrder = 2, IsActive = true },
        new SalaryRange { Id = Guid.NewGuid(), Code = "R_2000_3000", Label = "2000€ - 3000€", MinAmount = 2000m, MaxAmount = 3000m, DisplayOrder = 3, IsActive = true },
        new SalaryRange { Id = Guid.NewGuid(), Code = "GT_3000", Label = "Acima de 3000€", MinAmount = 3000m, MaxAmount = null, DisplayOrder = 4, IsActive = true }
    };

    [Fact]
    public void RequiredPayslipCount_IsThree()
    {
        var (service, ctx) = CreateService();
        Assert.Equal(3, service.RequiredPayslipCount);
        ctx.Dispose();
    }

    // --- RequestValidationAsync ---

    [Fact]
    public async Task RequestValidationAsync_WrongLandlord_Throws()
    {
        var (service, ctx) = CreateService();
        var property = CreateProperty(Guid.NewGuid());
        var app = CreateApplication(property, Guid.NewGuid(), ApplicationStatus.InterestConfirmed);
        ctx.Properties.Add(property);
        ctx.Applications.Add(app);
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.RequestValidationAsync(app.Id, Guid.NewGuid()));
        ctx.Dispose();
    }

    [Fact]
    public async Task RequestValidationAsync_WrongStatus_Throws()
    {
        var (service, ctx) = CreateService();
        var landlord = Guid.NewGuid();
        var property = CreateProperty(landlord);
        var app = CreateApplication(property, Guid.NewGuid(), ApplicationStatus.Pending);
        ctx.Properties.Add(property);
        ctx.Applications.Add(app);
        await ctx.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RequestValidationAsync(app.Id, landlord));
        ctx.Dispose();
    }

    [Fact]
    public async Task RequestValidationAsync_Valid_UpdatesStatusAndNotifies()
    {
        var (service, ctx) = CreateService();
        var landlord = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var property = CreateProperty(landlord);
        var app = CreateApplication(property, tenant, ApplicationStatus.InterestConfirmed);
        ctx.Properties.Add(property);
        ctx.Applications.Add(app);
        await ctx.SaveChangesAsync();

        await service.RequestValidationAsync(app.Id, landlord);

        var updated = await ctx.Applications.FindAsync(app.Id);
        Assert.Equal(ApplicationStatus.IncomeValidationRequested, updated!.Status);
        Assert.True(updated.IsIncomeValidationRequested);
        Assert.NotNull(updated.IncomeValidationRequestedAt);
        _notificationMock.Verify(n => n.SendNotificationAsync(
            tenant, "application", It.IsAny<string>(), app.Id), Times.Once);
        ctx.Dispose();
    }

    // --- ValidatePayslipsAsync ---

    [Fact]
    public async Task ValidatePayslipsAsync_WrongFileCount_Throws()
    {
        var (service, ctx) = CreateService();
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ValidatePayslipsAsync(Guid.NewGuid(), Guid.NewGuid(),
                new List<(Stream, string)> { (new MemoryStream(), "a.pdf") }));
        ctx.Dispose();
    }

    [Fact]
    public async Task ValidatePayslipsAsync_HappyPath_StoresRangeAndRestoresStatus()
    {
        var (service, ctx) = CreateService();
        var landlord = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var property = CreateProperty(landlord);
        var app = CreateApplication(property, tenant, ApplicationStatus.IncomeValidationRequested);
        var ranges = DefaultRanges();
        ctx.Properties.Add(property);
        ctx.Applications.Add(app);
        ctx.SalaryRanges.AddRange(ranges);
        await ctx.SaveChangesAsync();

        _userServiceMock.Setup(u => u.GetProfileAsync(tenant))
            .ReturnsAsync(new User { Id = tenant, Name = "João Silva", Email = "j@x.pt", Nif = "123456789" });

        var now = DateTime.UtcNow;
        var months = new[] { now.AddMonths(-1), now.AddMonths(-2), now.AddMonths(-3) }
            .Select(d => d.ToString("MM/yyyy")).ToArray();

        // Returns three valid payslips averaging 1500€
        var amounts = new[] { 1400m, 1500m, 1600m };
        var callCount = 0;
        _geminiMock.Setup(g => g.ExtractDocumentAsync<ReciboVencimentoResponse>(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(() =>
            {
                var i = callCount++;
                return new ReciboVencimentoResponse
                {
                    IsAuthentic = true,
                    ImageQuality = "good",
                    AllFieldsExtracted = true,
                    EmployeeName = "Joao Silva",
                    EmployeeNif = "123456789",
                    NetSalary = amounts[i],
                    GrossSalary = amounts[i] + 200m,
                    ReferenceMonth = months[i]
                };
            });

        var files = new List<(Stream, string)>
        {
            (new MemoryStream(new byte[] { 1 }), "r1.pdf"),
            (new MemoryStream(new byte[] { 1 }), "r2.pdf"),
            (new MemoryStream(new byte[] { 1 }), "r3.pdf"),
        };

        var result = await service.ValidatePayslipsAsync(app.Id, tenant, files);

        Assert.Equal("R_1000_2000", result.RangeCode);

        var updated = await ctx.Applications.FindAsync(app.Id);
        Assert.Equal(ApplicationStatus.InterestConfirmed, updated!.Status);
        Assert.NotNull(updated.IncomeRangeId);
        Assert.NotNull(updated.IncomeValidatedAt);

        _notificationMock.Verify(n => n.SendNotificationAsync(
            landlord, "application", It.IsAny<string>(), app.Id), Times.Once);
        ctx.Dispose();
    }

    [Fact]
    public async Task ValidatePayslipsAsync_NifMismatch_Throws()
    {
        var (service, ctx) = CreateService();
        var tenant = Guid.NewGuid();
        var property = CreateProperty(Guid.NewGuid());
        var app = CreateApplication(property, tenant, ApplicationStatus.IncomeValidationRequested);
        ctx.Properties.Add(property);
        ctx.Applications.Add(app);
        ctx.SalaryRanges.AddRange(DefaultRanges());
        await ctx.SaveChangesAsync();

        _userServiceMock.Setup(u => u.GetProfileAsync(tenant))
            .ReturnsAsync(new User { Id = tenant, Name = "João Silva", Email = "j@x.pt", Nif = "123456789" });

        _geminiMock.Setup(g => g.ExtractDocumentAsync<ReciboVencimentoResponse>(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new ReciboVencimentoResponse
            {
                IsAuthentic = true,
                ImageQuality = "good",
                AllFieldsExtracted = true,
                EmployeeName = "Joao Silva",
                EmployeeNif = "999999999",
                NetSalary = 1500m,
                ReferenceMonth = DateTime.UtcNow.AddMonths(-1).ToString("MM/yyyy")
            });

        var files = new List<(Stream, string)>
        {
            (new MemoryStream(new byte[] { 1 }), "r1.pdf"),
            (new MemoryStream(new byte[] { 1 }), "r2.pdf"),
            (new MemoryStream(new byte[] { 1 }), "r3.pdf"),
        };

        await Assert.ThrowsAsync<Exception>(
            () => service.ValidatePayslipsAsync(app.Id, tenant, files));
        ctx.Dispose();
    }

    [Fact]
    public async Task ValidatePayslipsAsync_NonEuroCurrency_Throws()
    {
        var (service, ctx) = CreateService();
        var tenant = Guid.NewGuid();
        var property = CreateProperty(Guid.NewGuid());
        var app = CreateApplication(property, tenant, ApplicationStatus.IncomeValidationRequested);
        ctx.Properties.Add(property);
        ctx.Applications.Add(app);
        ctx.SalaryRanges.AddRange(DefaultRanges());
        await ctx.SaveChangesAsync();

        _userServiceMock.Setup(u => u.GetProfileAsync(tenant))
            .ReturnsAsync(new User { Id = tenant, Name = "João Silva", Email = "j@x.pt", Nif = "123456789" });

        _geminiMock.Setup(g => g.ExtractDocumentAsync<ReciboVencimentoResponse>(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new ReciboVencimentoResponse
            {
                IsAuthentic = true,
                ImageQuality = "good",
                AllFieldsExtracted = true,
                EmployeeName = "João Silva",
                EmployeeNif = "123456789",
                NetSalary = 1500m,
                Currency = "GBP",
                ReferenceMonth = DateTime.UtcNow.AddMonths(-1).ToString("MM/yyyy")
            });

        var files = new List<(Stream, string)>
        {
            (new MemoryStream(new byte[] { 1 }), "r1.pdf"),
            (new MemoryStream(new byte[] { 1 }), "r2.pdf"),
            (new MemoryStream(new byte[] { 1 }), "r3.pdf"),
        };

        var ex = await Assert.ThrowsAsync<Exception>(
            () => service.ValidatePayslipsAsync(app.Id, tenant, files));
        Assert.Contains("Moeda", ex.Message);
        ctx.Dispose();
    }

    [Fact]
    public async Task ValidatePayslipsAsync_VoluntarySubmission_KeepsStatus()
    {
        var (service, ctx) = CreateService();
        var landlord = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var property = CreateProperty(landlord);
        var app = CreateApplication(property, tenant, ApplicationStatus.InterestConfirmed);
        var ranges = DefaultRanges();
        ctx.Properties.Add(property);
        ctx.Applications.Add(app);
        ctx.SalaryRanges.AddRange(ranges);
        await ctx.SaveChangesAsync();

        _userServiceMock.Setup(u => u.GetProfileAsync(tenant))
            .ReturnsAsync(new User { Id = tenant, Name = "João Silva", Email = "j@x.pt", Nif = "123456789" });

        var months = new[] { -1, -2, -3 }
            .Select(d => DateTime.UtcNow.AddMonths(d).ToString("MM/yyyy")).ToArray();
        var callCount = 0;
        _geminiMock.Setup(g => g.ExtractDocumentAsync<ReciboVencimentoResponse>(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(() => new ReciboVencimentoResponse
            {
                IsAuthentic = true,
                ImageQuality = "good",
                AllFieldsExtracted = true,
                EmployeeName = "Joao Silva",
                EmployeeNif = "123456789",
                NetSalary = 1500m,
                ReferenceMonth = months[callCount++]
            });

        var files = new List<(Stream, string)>
        {
            (new MemoryStream(new byte[] { 1 }), "r1.pdf"),
            (new MemoryStream(new byte[] { 1 }), "r2.pdf"),
            (new MemoryStream(new byte[] { 1 }), "r3.pdf"),
        };

        await service.ValidatePayslipsAsync(app.Id, tenant, files);

        var updated = await ctx.Applications.FindAsync(app.Id);
        // Status mantém-se porque foi envio voluntário (não foi pedido pelo landlord)
        Assert.Equal(ApplicationStatus.InterestConfirmed, updated!.Status);
        Assert.NotNull(updated.IncomeRangeId);
        ctx.Dispose();
    }

    [Fact]
    public async Task ValidatePayslipsAsync_RejectedStatus_Throws()
    {
        var (service, ctx) = CreateService();
        var tenant = Guid.NewGuid();
        var property = CreateProperty(Guid.NewGuid());
        var app = CreateApplication(property, tenant, ApplicationStatus.Rejected);
        ctx.Properties.Add(property);
        ctx.Applications.Add(app);
        await ctx.SaveChangesAsync();

        var files = new List<(Stream, string)>
        {
            (new MemoryStream(new byte[] { 1 }), "r1.pdf"),
            (new MemoryStream(new byte[] { 1 }), "r2.pdf"),
            (new MemoryStream(new byte[] { 1 }), "r3.pdf"),
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ValidatePayslipsAsync(app.Id, tenant, files));
        ctx.Dispose();
    }

    [Fact]
    public async Task RequestValidationAsync_RevalidationTooEarly_Throws()
    {
        var (service, ctx) = CreateService();
        var landlord = Guid.NewGuid();
        var property = CreateProperty(landlord);
        var range = DefaultRanges()[1];
        var app = CreateApplication(property, Guid.NewGuid(), ApplicationStatus.InterestConfirmed);
        app.IncomeRangeId = range.Id;
        app.IncomeValidatedAt = DateTime.UtcNow.AddDays(-10); // só passaram 10 dias
        ctx.Properties.Add(property);
        ctx.SalaryRanges.Add(range);
        ctx.Applications.Add(app);
        await ctx.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RequestValidationAsync(app.Id, landlord));
        Assert.Contains("recente", ex.Message);
        ctx.Dispose();
    }

    [Fact]
    public async Task RequestValidationAsync_RevalidationAfterCooldown_Succeeds()
    {
        var (service, ctx) = CreateService();
        var landlord = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var property = CreateProperty(landlord);
        var range = DefaultRanges()[1];
        var app = CreateApplication(property, tenant, ApplicationStatus.InterestConfirmed);
        app.IncomeRangeId = range.Id;
        app.IncomeValidatedAt = DateTime.UtcNow.AddDays(-45); // passaram 45 dias
        ctx.Properties.Add(property);
        ctx.SalaryRanges.Add(range);
        ctx.Applications.Add(app);
        await ctx.SaveChangesAsync();

        await service.RequestValidationAsync(app.Id, landlord);

        var updated = await ctx.Applications.FindAsync(app.Id);
        Assert.Equal(ApplicationStatus.IncomeValidationRequested, updated!.Status);
        var history = await ctx.ApplicationHistories.FirstAsync(h => h.ApplicationId == app.Id);
        Assert.Contains("Re-validação", history.Action);
        ctx.Dispose();
    }

    [Fact]
    public void ParseReferenceMonth_VariousFormats_ReturnsFirstDayOfMonth()
    {
        var result1 = IncomeValidationService.ParseReferenceMonth("10/2025");
        Assert.Equal(new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc), result1);

        var result2 = IncomeValidationService.ParseReferenceMonth("2025-10");
        Assert.Equal(new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc), result2);

        var result3 = IncomeValidationService.ParseReferenceMonth("Outubro/2025");
        Assert.Equal(new DateTime(2025, 10, 1, 0, 0, 0, DateTimeKind.Utc), result3);

        Assert.Null(IncomeValidationService.ParseReferenceMonth("xpto"));
    }
}
