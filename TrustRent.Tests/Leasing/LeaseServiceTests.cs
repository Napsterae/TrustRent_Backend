using Hangfire;
using Microsoft.EntityFrameworkCore;
using Moq;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Modules.Leasing.Services;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;

namespace TrustRent.Tests.Leasing;

public class LeaseServiceTests
{
    private readonly Mock<ICatalogAccessService> _catalogAccessMock;
    private readonly Mock<INotificationService> _notificationMock;
    private readonly Mock<IContractGenerationService> _contractGenMock;
    private readonly Mock<IDigitalSignatureService> _digitalSigMock;
    private readonly Mock<ISignedPdfVerificationService> _pdfVerifyMock;
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<IBackgroundJobClient> _bgJobsMock;

    public LeaseServiceTests()
    {
        _catalogAccessMock = new Mock<ICatalogAccessService>();
        _notificationMock = new Mock<INotificationService>();
        _contractGenMock = new Mock<IContractGenerationService>();
        _digitalSigMock = new Mock<IDigitalSignatureService>();
        _pdfVerifyMock = new Mock<ISignedPdfVerificationService>();
        _userServiceMock = new Mock<IUserService>();
        _bgJobsMock = new Mock<IBackgroundJobClient>();
    }

    private (LeaseService Service, LeasingDbContext Context) CreateService()
    {
        var options = new DbContextOptionsBuilder<LeasingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new LeasingDbContext(options);

        var service = new LeaseService(
            context,
            _catalogAccessMock.Object,
            _notificationMock.Object,
            _contractGenMock.Object,
            _digitalSigMock.Object,
            _pdfVerifyMock.Object,
            _userServiceMock.Object,
            _bgJobsMock.Object);

        return (service, context);
    }

    private Lease CreateTestLease(Guid? tenantId = null, Guid? landlordId = null, LeaseStatus status = LeaseStatus.Pending)
    {
        return new Lease
        {
            Id = Guid.NewGuid(),
            PropertyId = Guid.NewGuid(),
            TenantId = tenantId ?? Guid.NewGuid(),
            LandlordId = landlordId ?? Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            StartDate = DateTime.UtcNow.AddDays(30),
            EndDate = DateTime.UtcNow.AddDays(30).AddMonths(12),
            DurationMonths = 12,
            MonthlyRent = 800m,
            Deposit = 1600m,
            AdvanceRentMonths = 0,
            ContractType = "Informal",
            Status = status
        };
    }

    // --- GetLeaseByIdAsync ---

    [Fact]
    public async Task GetLeaseByIdAsync_ExistingLease_AuthorizedUser_ReturnsLease()
    {
        var (service, context) = CreateService();
        var tenantId = Guid.NewGuid();
        var lease = CreateTestLease(tenantId: tenantId);
        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        var result = await service.GetLeaseByIdAsync(lease.Id, tenantId);

        Assert.NotNull(result);
        Assert.Equal(lease.Id, result!.Id);

        context.Dispose();
    }

    [Fact]
    public async Task GetLeaseByIdAsync_NonExistent_ReturnsNull()
    {
        var (service, context) = CreateService();

        var result = await service.GetLeaseByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);

        context.Dispose();
    }

    [Fact]
    public async Task GetLeaseByIdAsync_UnauthorizedUser_ThrowsUnauthorized()
    {
        var (service, context) = CreateService();
        var lease = CreateTestLease();
        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetLeaseByIdAsync(lease.Id, Guid.NewGuid()));

        context.Dispose();
    }

    // --- GetLeaseByApplicationIdAsync ---

    [Fact]
    public async Task GetLeaseByApplicationIdAsync_Existing_ReturnsLease()
    {
        var (service, context) = CreateService();
        var tenantId = Guid.NewGuid();
        var lease = CreateTestLease(tenantId: tenantId);
        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        var result = await service.GetLeaseByApplicationIdAsync(lease.ApplicationId, tenantId);

        Assert.NotNull(result);
        Assert.Equal(lease.ApplicationId, result!.ApplicationId);

        context.Dispose();
    }

    [Fact]
    public async Task GetLeaseByApplicationIdAsync_NonExistent_ReturnsNull()
    {
        var (service, context) = CreateService();

        var result = await service.GetLeaseByApplicationIdAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);

        context.Dispose();
    }

    // --- GetSignatureStatusAsync ---

    [Fact]
    public async Task GetSignatureStatusAsync_ExistingLease_ReturnsStatus()
    {
        var (service, context) = CreateService();
        var tenantId = Guid.NewGuid();
        var lease = CreateTestLease(tenantId: tenantId, status: LeaseStatus.AwaitingSignatures);
        lease.LandlordSigned = true;
        lease.LandlordSignedAt = DateTime.UtcNow.AddHours(-1);
        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        var result = await service.GetSignatureStatusAsync(lease.Id, tenantId);

        Assert.NotNull(result);
        Assert.True(result!.LandlordSigned);
        Assert.False(result.TenantSigned);
        Assert.Equal("AwaitingSignatures", result.LeaseStatus);

        context.Dispose();
    }

    [Fact]
    public async Task GetSignatureStatusAsync_UnauthorizedUser_ThrowsUnauthorized()
    {
        var (service, context) = CreateService();
        var lease = CreateTestLease();
        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetSignatureStatusAsync(lease.Id, Guid.NewGuid()));

        context.Dispose();
    }

    // --- GetLeasesForTenantAsync ---

    [Fact]
    public async Task GetLeasesForTenantAsync_MultipleLeases_ReturnsOrderedByDate()
    {
        var (service, context) = CreateService();
        var tenantId = Guid.NewGuid();

        var lease1 = CreateTestLease(tenantId: tenantId);
        lease1.CreatedAt = DateTime.UtcNow.AddDays(-10);
        var lease2 = CreateTestLease(tenantId: tenantId);
        lease2.CreatedAt = DateTime.UtcNow;

        context.Leases.AddRange(lease1, lease2);
        await context.SaveChangesAsync();

        var result = (await service.GetLeasesForTenantAsync(tenantId)).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(lease2.Id, result[0].Id); // Most recent first

        context.Dispose();
    }

    [Fact]
    public async Task GetLeasesForTenantAsync_NoLeases_ReturnsEmpty()
    {
        var (service, context) = CreateService();

        var result = await service.GetLeasesForTenantAsync(Guid.NewGuid());

        Assert.Empty(result);

        context.Dispose();
    }

    // --- CancelLeaseAsync ---

    [Fact]
    public async Task CancelLeaseAsync_PendingLease_CancelsAndNotifies()
    {
        var (service, context) = CreateService();
        var tenantId = Guid.NewGuid();
        var landlordId = Guid.NewGuid();
        var lease = CreateTestLease(tenantId: tenantId, landlordId: landlordId, status: LeaseStatus.Pending);
        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        _catalogAccessMock.Setup(c => c.UpdateApplicationStatusAsync(
            lease.ApplicationId, It.IsAny<int>(), tenantId, It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var dto = new TrustRent.Modules.Leasing.Contracts.DTOs.CancelLeaseDto { Reason = "Mudança de planos" };
        var result = await service.CancelLeaseAsync(lease.Id, tenantId, dto);

        Assert.Equal(LeaseStatus.Cancelled.ToString(), result.Status);
        _notificationMock.Verify(n => n.SendNotificationAsync(landlordId, "lease", It.IsAny<string>(), lease.Id), Times.Once);
        _catalogAccessMock.Verify(c => c.UpdateApplicationStatusAsync(
            lease.ApplicationId, It.IsAny<int>(), tenantId, It.IsAny<string>(), It.IsAny<string>()), Times.Once);

        context.Dispose();
    }

    [Fact]
    public async Task CancelLeaseAsync_NonExistentLease_ThrowsKeyNotFound()
    {
        var (service, context) = CreateService();

        var dto = new TrustRent.Modules.Leasing.Contracts.DTOs.CancelLeaseDto { Reason = "Reason" };

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.CancelLeaseAsync(Guid.NewGuid(), Guid.NewGuid(), dto));

        context.Dispose();
    }
}
