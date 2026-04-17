using Microsoft.EntityFrameworkCore;
using Moq;
using TrustRent.Api.Services;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;

namespace TrustRent.Tests.Api;

public class CatalogLeaseActivationServiceTests
{
    private LeasingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LeasingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LeasingDbContext(options);
    }

    private Lease CreateAwaitingPaymentLease() => new()
    {
        Id = Guid.NewGuid(),
        PropertyId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        LandlordId = Guid.NewGuid(),
        ApplicationId = Guid.NewGuid(),
        MonthlyRent = 800m,
        DurationMonths = 12,
        StartDate = DateTime.UtcNow.AddDays(30),
        EndDate = DateTime.UtcNow.AddDays(30).AddMonths(12),
        Status = LeaseStatus.AwaitingPayment
    };

    [Fact]
    public async Task ActivateLeaseAfterPaymentAsync_AwaitingPayment_ActivatesLease()
    {
        using var context = CreateContext();
        var lease = CreateAwaitingPaymentLease();
        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        var catalogAccessMock = new Mock<ICatalogAccessService>();
        var notificationMock = new Mock<INotificationService>();

        var sut = new CatalogLeaseActivationService(context, catalogAccessMock.Object, notificationMock.Object);
        await sut.ActivateLeaseAfterPaymentAsync(lease.Id);

        var updated = await context.Leases.Include(l => l.History).FirstAsync(l => l.Id == lease.Id);
        Assert.Equal(LeaseStatus.Active, updated.Status);
        Assert.NotNull(updated.ContractSignedAt);
        Assert.Single(updated.History);
    }

    [Fact]
    public async Task ActivateLeaseAfterPaymentAsync_UpdatesCatalogAndNotifies()
    {
        using var context = CreateContext();
        var lease = CreateAwaitingPaymentLease();
        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        var catalogAccessMock = new Mock<ICatalogAccessService>();
        var notificationMock = new Mock<INotificationService>();

        var sut = new CatalogLeaseActivationService(context, catalogAccessMock.Object, notificationMock.Object);
        await sut.ActivateLeaseAfterPaymentAsync(lease.Id);

        catalogAccessMock.Verify(c => c.UpdateApplicationStatusAsync(
            lease.ApplicationId, (int)ApplicationStatus.LeaseActive, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        catalogAccessMock.Verify(c => c.SetPropertyTenantAsync(lease.PropertyId, lease.TenantId), Times.Once);
        catalogAccessMock.Verify(c => c.RejectOtherApplicationsAsync(lease.PropertyId, lease.ApplicationId, notificationMock.Object), Times.Once);
        notificationMock.Verify(n => n.SendNotificationAsync(lease.TenantId, It.IsAny<string>(), It.IsAny<string>(), lease.Id), Times.Once);
        notificationMock.Verify(n => n.SendNotificationAsync(lease.LandlordId, It.IsAny<string>(), It.IsAny<string>(), lease.Id), Times.Once);
    }

    [Fact]
    public async Task ActivateLeaseAfterPaymentAsync_NotAwaitingPayment_DoesNotActivate()
    {
        using var context = CreateContext();
        var lease = CreateAwaitingPaymentLease();
        lease.Status = LeaseStatus.Active; // Already active
        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        var catalogAccessMock = new Mock<ICatalogAccessService>();
        var notificationMock = new Mock<INotificationService>();

        var sut = new CatalogLeaseActivationService(context, catalogAccessMock.Object, notificationMock.Object);
        await sut.ActivateLeaseAfterPaymentAsync(lease.Id);

        catalogAccessMock.Verify(c => c.UpdateApplicationStatusAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ActivateLeaseAfterPaymentAsync_NonExistentLease_ThrowsException()
    {
        using var context = CreateContext();
        var catalogAccessMock = new Mock<ICatalogAccessService>();
        var notificationMock = new Mock<INotificationService>();

        var sut = new CatalogLeaseActivationService(context, catalogAccessMock.Object, notificationMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ActivateLeaseAfterPaymentAsync(Guid.NewGuid()));
    }
}
