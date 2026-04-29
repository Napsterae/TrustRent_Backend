using Hangfire;
using Microsoft.EntityFrameworkCore;
using Moq;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.DTOs;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Modules.Leasing.Services;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;

namespace TrustRent.Tests.Leasing;

public class MultiSignatureFlowTests
{
    private static (LeaseService Service, LeasingDbContext Db, Mock<ICatalogAccessService> Catalog) Build()
    {
        var options = new DbContextOptionsBuilder<LeasingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new LeasingDbContext(options);

        var catalog = new Mock<ICatalogAccessService>();
        var service = new LeaseService(
            db, catalog.Object,
            Mock.Of<INotificationService>(),
            Mock.Of<IContractGenerationService>(),
            Mock.Of<IDigitalSignatureService>(),
            Mock.Of<ISignedPdfVerificationService>(),
            Mock.Of<IUserService>(),
            Mock.Of<IEmailService>(),
            Mock.Of<IBackgroundJobClient>());
        return (service, db, catalog);
    }

    private static ApplicationContext BuildAppContext(Guid appId, Guid tenantId, Guid landlordId, Guid? coTenant = null, Guid? guarantor = null)
        => new()
        {
            Id = appId,
            PropertyId = Guid.NewGuid(),
            TenantId = tenantId,
            LandlordId = landlordId,
            DurationMonths = 36,
            Status = (int)ApplicationStatus.Accepted,
            Price = 800m,
            Deposit = 1600m,
            AdvanceRentMonths = 0,
            LeaseRegime = "PermanentHousing",
            HasOfficialContract = false,
            CoTenantUserId = coTenant,
            GuarantorUserId = guarantor,
            GuarantorRecordId = guarantor.HasValue ? Guid.NewGuid() : null
        };

    [Fact]
    public async Task InitiateLease_TenantOnly_Creates2Signatures()
    {
        var (svc, db, catalog) = Build();
        var appId = Guid.NewGuid(); var tenant = Guid.NewGuid(); var landlord = Guid.NewGuid();
        catalog.Setup(c => c.GetApplicationContextAsync(appId)).ReturnsAsync(BuildAppContext(appId, tenant, landlord));

        var dto = new InitiateLeaseProcedureDto { ProposedStartDate = DateTime.UtcNow.AddDays(30) };
        var result = await svc.InitiateLeaseProcedureAsync(appId, landlord, dto);

        var lease = await db.Leases.Include(l => l.Signatures).FirstAsync(l => l.Id == result.Id);
        Assert.Equal(2, lease.RequiredSignaturesCount);
        Assert.Equal(2, lease.Signatures.Count);
        Assert.Contains(lease.Signatures, s => s.Role == LeaseSignatoryRole.Landlord);
        Assert.Contains(lease.Signatures, s => s.Role == LeaseSignatoryRole.Tenant);
    }

    [Fact]
    public async Task InitiateLease_WithCoTenant_Creates3Signatures()
    {
        var (svc, db, catalog) = Build();
        var appId = Guid.NewGuid(); var tenant = Guid.NewGuid(); var landlord = Guid.NewGuid(); var co = Guid.NewGuid();
        catalog.Setup(c => c.GetApplicationContextAsync(appId)).ReturnsAsync(BuildAppContext(appId, tenant, landlord, coTenant: co));

        var dto = new InitiateLeaseProcedureDto { ProposedStartDate = DateTime.UtcNow.AddDays(30) };
        var result = await svc.InitiateLeaseProcedureAsync(appId, landlord, dto);

        var lease = await db.Leases.Include(l => l.Signatures).FirstAsync(l => l.Id == result.Id);
        Assert.Equal(3, lease.RequiredSignaturesCount);
        Assert.Contains(lease.Signatures, s => s.Role == LeaseSignatoryRole.CoTenant && s.UserId == co);
    }

    [Fact]
    public async Task InitiateLease_WithCoTenantAndGuarantor_Creates4Signatures_OrderedByRole()
    {
        var (svc, db, catalog) = Build();
        var appId = Guid.NewGuid(); var tenant = Guid.NewGuid(); var landlord = Guid.NewGuid();
        var co = Guid.NewGuid(); var guar = Guid.NewGuid();
        catalog.Setup(c => c.GetApplicationContextAsync(appId)).ReturnsAsync(BuildAppContext(appId, tenant, landlord, co, guar));

        var dto = new InitiateLeaseProcedureDto { ProposedStartDate = DateTime.UtcNow.AddDays(30) };
        var result = await svc.InitiateLeaseProcedureAsync(appId, landlord, dto);

        var lease = await db.Leases.Include(l => l.Signatures).FirstAsync(l => l.Id == result.Id);
        Assert.Equal(4, lease.RequiredSignaturesCount);
        var ordered = lease.Signatures.OrderBy(s => s.SequenceOrder).Select(s => s.Role).ToList();
        Assert.Equal(new[] { LeaseSignatoryRole.Landlord, LeaseSignatoryRole.Tenant, LeaseSignatoryRole.CoTenant, LeaseSignatoryRole.Guarantor }, ordered);
        Assert.All(lease.Signatures, s => Assert.False(s.Signed));
        Assert.Equal(co, lease.CoTenantId);
        Assert.Equal(guar, lease.GuarantorUserId);
    }

    [Fact]
    public async Task InitiateLease_NotApplicationParty_Throws()
    {
        var (svc, _, catalog) = Build();
        var appId = Guid.NewGuid();
        catalog.Setup(c => c.GetApplicationContextAsync(appId))
            .ReturnsAsync(BuildAppContext(appId, Guid.NewGuid(), Guid.NewGuid()));

        var dto = new InitiateLeaseProcedureDto { ProposedStartDate = DateTime.UtcNow.AddDays(30) };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.InitiateLeaseProcedureAsync(appId, Guid.NewGuid(), dto));
    }
}
