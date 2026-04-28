using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Contracts.DTOs;
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
        new SalaryRange { Id = Guid.NewGuid(), Code = "LT_1000", Label = "Ate 1000", MinAmount = null, MaxAmount = 1000m, DisplayOrder = 1, IsActive = true },
        new SalaryRange { Id = Guid.NewGuid(), Code = "R_1000_2000", Label = "1000-2000", MinAmount = 1000m, MaxAmount = 2000m, DisplayOrder = 2, IsActive = true },
        new SalaryRange { Id = Guid.NewGuid(), Code = "R_2000_3000", Label = "2000-3000", MinAmount = 2000m, MaxAmount = 3000m, DisplayOrder = 3, IsActive = true },
        new SalaryRange { Id = Guid.NewGuid(), Code = "GT_3000", Label = "Acima 3000", MinAmount = 3000m, MaxAmount = null, DisplayOrder = 4, IsActive = true }
    };

    private static (Stream Stream, string FileName) Fake(string name = "f.pdf")
        => (new MemoryStream(new byte[] { 1 }), name);

    [Fact]
    public void Limits_AreThree()
    {
        var (service, ctx) = CreateService();
        Assert.Equal(3, service.MaxPayslipCount);
        Assert.Equal(3, service.PayslipsToSkipDeclaration);
        ctx.Dispose();
    }

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

    [Fact]
    public async Task ValidateAsync_Employee_3Payslips_Happy()
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
            .ReturnsAsync(new User { Id = tenant, Name = "Joao Silva", Email = "j@x.pt", Nif = "123456789" });

        var now = DateTime.UtcNow;
        var months = new[] { now.AddMonths(-1), now.AddMonths(-2), now.AddMonths(-3) }
            .Select(d => d.ToString("MM/yyyy")).ToArray();
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
                    EmployerName = "Acme Lda",
                    EmployerNif = "500111222",
                    NetSalary = amounts[i],
                    GrossSalary = amounts[i] + 200m,
                    ReferenceMonth = months[i]
                };
            });

        var submission = new IncomeValidationSubmissionDto(
            EmploymentType.Employee,
            new List<(Stream, string)> { Fake("r1.pdf"), Fake("r2.pdf"), Fake("r3.pdf") },
            null, null);

        var result = await service.ValidateAsync(app.Id, tenant, submission);

        Assert.Equal("R_1000_2000", result.RangeCode);
        Assert.Equal("Payslips", result.Method);
        Assert.Equal("Employee", result.EmploymentType);
        Assert.Equal(3, result.PayslipsProvidedCount);
        Assert.Equal("Acme Lda", result.EmployerName);
        Assert.Equal("500111222", result.EmployerNif);

        var updated = await ctx.Applications.FindAsync(app.Id);
        Assert.Equal(ApplicationStatus.InterestConfirmed, updated!.Status);
        Assert.NotNull(updated.IncomeRangeId);
        Assert.Equal("Acme Lda", updated.EmployerName);
        ctx.Dispose();
    }

    [Fact]
    public async Task ValidateAsync_Employee_2Payslips_RequiresDeclaration()
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
            .ReturnsAsync(new User { Id = tenant, Name = "Joao Silva", Email = "j@x.pt", Nif = "123456789" });

        var now = DateTime.UtcNow;
        var months = new[] { now.AddMonths(-1), now.AddMonths(-2) }
            .Select(d => d.ToString("MM/yyyy")).ToArray();
        var idx = 0;
        _geminiMock.Setup(g => g.ExtractDocumentAsync<ReciboVencimentoResponse>(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(() => new ReciboVencimentoResponse
            {
                IsAuthentic = true,
                ImageQuality = "good",
                AllFieldsExtracted = true,
                EmployeeName = "Joao Silva",
                EmployeeNif = "123456789",
                EmployerName = "Acme Lda",
                EmployerNif = "500111222",
                NetSalary = 1500m,
                ReferenceMonth = months[idx++]
            });

        var submission = new IncomeValidationSubmissionDto(
            EmploymentType.Employee,
            new List<(Stream, string)> { Fake("r1.pdf"), Fake("r2.pdf") },
            null, null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ValidateAsync(app.Id, tenant, submission));
        ctx.Dispose();
    }

    [Fact]
    public async Task ValidateAsync_Employee_1Payslip_WithDeclaration_Happy()
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
            .ReturnsAsync(new User { Id = tenant, Name = "Joao Silva", Email = "j@x.pt", Nif = "123456789" });

        _geminiMock.Setup(g => g.ExtractDocumentAsync<ReciboVencimentoResponse>(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new ReciboVencimentoResponse
            {
                IsAuthentic = true,
                ImageQuality = "good",
                AllFieldsExtracted = true,
                EmployeeName = "Joao Silva",
                EmployeeNif = "123456789",
                EmployerName = "Acme Lda",
                EmployerNif = "500111222",
                NetSalary = 1500m,
                ReferenceMonth = DateTime.UtcNow.AddMonths(-1).ToString("MM/yyyy")
            });

        _geminiMock.Setup(g => g.ExtractDocumentAsync<DeclaracaoEntidadeEmpregadoraResponse>(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DeclaracaoEntidadeEmpregadoraResponse
            {
                IsAuthentic = true,
                ImageQuality = "good",
                AllFieldsExtracted = true,
                EmployeeName = "Joao Silva",
                EmployeeNif = "123456789",
                EmployerName = "Acme Lda",
                EmployerNif = "500111222",
                Position = "Programador",
                ContractType = "Sem termo",
                EmploymentStartDate = DateTime.UtcNow.AddDays(-40).ToString("dd/MM/yyyy"),
                IssueDate = DateTime.UtcNow.AddDays(-5).ToString("dd/MM/yyyy"),
                HasSignatureAndStamp = true
            });

        var submission = new IncomeValidationSubmissionDto(
            EmploymentType.Employee,
            new List<(Stream, string)> { Fake("r1.pdf") },
            Fake("decl.pdf"),
            null);

        var result = await service.ValidateAsync(app.Id, tenant, submission);

        Assert.Equal("PayslipsWithEmployerDeclaration", result.Method);
        Assert.Equal(1, result.PayslipsProvidedCount);
        Assert.Equal("Acme Lda", result.EmployerName);

        var updated = await ctx.Applications.FindAsync(app.Id);
        Assert.NotNull(updated!.EmploymentStartDate);
        ctx.Dispose();
    }

    [Fact]
    public async Task ValidateAsync_NifMismatch_Throws()
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
            .ReturnsAsync(new User { Id = tenant, Name = "Joao Silva", Email = "j@x.pt", Nif = "123456789" });

        _geminiMock.Setup(g => g.ExtractDocumentAsync<ReciboVencimentoResponse>(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new ReciboVencimentoResponse
            {
                IsAuthentic = true, ImageQuality = "good", AllFieldsExtracted = true,
                EmployeeName = "Joao Silva", EmployeeNif = "999999999",
                NetSalary = 1500m,
                ReferenceMonth = DateTime.UtcNow.AddMonths(-1).ToString("MM/yyyy")
            });

        var submission = new IncomeValidationSubmissionDto(
            EmploymentType.Employee,
            new List<(Stream, string)> { Fake("r1.pdf"), Fake("r2.pdf"), Fake("r3.pdf") },
            null, null);

        await Assert.ThrowsAsync<Exception>(
            () => service.ValidateAsync(app.Id, tenant, submission));
        ctx.Dispose();
    }

    [Fact]
    public async Task ValidateAsync_SelfEmployed_Happy()
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
            .ReturnsAsync(new User { Id = tenant, Name = "Maria Costa", Email = "m@x.pt", Nif = "234567890" });

        _geminiMock.Setup(g => g.ExtractDocumentAsync<DeclaracaoInicioAtividadeResponse>(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new DeclaracaoInicioAtividadeResponse
            {
                IsAuthentic = true, ImageQuality = "good", AllFieldsExtracted = true,
                TaxpayerName = "Maria Costa",
                TaxpayerNif = "234567890",
                CaeCodes = new() { "62010" },
                CaePrincipalDescription = "Programacao informatica",
                ActivityStatus = "Activa",
                ActivityStartDate = "01/06/2024",
                IssueDate = DateTime.UtcNow.ToString("dd/MM/yyyy")
            });

        var now = DateTime.UtcNow;
        var months = new[] { now.AddMonths(-1), now.AddMonths(-2), now.AddMonths(-3) }
            .Select(d => d.ToString("MM/yyyy")).ToArray();
        var idx = 0;
        _geminiMock.Setup(g => g.ExtractDocumentAsync<ReciboVerdeResponse>(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(() => new ReciboVerdeResponse
            {
                IsAuthentic = true, ImageQuality = "good", AllFieldsExtracted = true,
                IssuerName = "Maria Costa",
                IssuerNif = "234567890",
                AcquirerName = "Cliente X",
                BaseAmount = 1800m,
                TotalAmount = 1800m,
                ReferenceMonth = months[idx++]
            });

        var submission = new IncomeValidationSubmissionDto(
            EmploymentType.SelfEmployed,
            new List<(Stream, string)> { Fake("rv1.pdf"), Fake("rv2.pdf"), Fake("rv3.pdf") },
            null,
            Fake("atividade.pdf"));

        var result = await service.ValidateAsync(app.Id, tenant, submission);

        Assert.Equal("ActivityWithGreenReceipts", result.Method);
        Assert.Equal("SelfEmployed", result.EmploymentType);
        Assert.Equal("R_1000_2000", result.RangeCode);
        Assert.Equal("Programacao informatica", result.EmployerName);
        Assert.Null(result.EmployerNif);

        var updated = await ctx.Applications.FindAsync(app.Id);
        Assert.NotNull(updated!.EmploymentStartDate);
        ctx.Dispose();
    }

    [Fact]
    public async Task ValidateAsync_SelfEmployed_NoActivityDeclaration_Throws()
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
            .ReturnsAsync(new User { Id = tenant, Name = "Maria", Email = "m@x.pt", Nif = "234567890" });

        var submission = new IncomeValidationSubmissionDto(
            EmploymentType.SelfEmployed,
            new List<(Stream, string)> { Fake("rv1.pdf") },
            null,
            null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ValidateAsync(app.Id, tenant, submission));
        ctx.Dispose();
    }

    [Fact]
    public async Task ValidateAsync_RejectedStatus_Throws()
    {
        var (service, ctx) = CreateService();
        var tenant = Guid.NewGuid();
        var property = CreateProperty(Guid.NewGuid());
        var app = CreateApplication(property, tenant, ApplicationStatus.Rejected);
        ctx.Properties.Add(property);
        ctx.Applications.Add(app);
        await ctx.SaveChangesAsync();

        var submission = new IncomeValidationSubmissionDto(
            EmploymentType.Employee,
            new List<(Stream, string)> { Fake("r1.pdf"), Fake("r2.pdf"), Fake("r3.pdf") },
            null, null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ValidateAsync(app.Id, tenant, submission));
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
        app.IncomeValidatedAt = DateTime.UtcNow.AddDays(-10);
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
        app.IncomeValidatedAt = DateTime.UtcNow.AddDays(-45);
        ctx.Properties.Add(property);
        ctx.SalaryRanges.Add(range);
        ctx.Applications.Add(app);
        await ctx.SaveChangesAsync();

        await service.RequestValidationAsync(app.Id, landlord);

        var updated = await ctx.Applications.FindAsync(app.Id);
        Assert.Equal(ApplicationStatus.IncomeValidationRequested, updated!.Status);
        var history = await ctx.ApplicationHistories.FirstAsync(h => h.ApplicationId == app.Id);
        Assert.Contains("Re-valida", history.Action, StringComparison.OrdinalIgnoreCase);
        ctx.Dispose();
    }
}
