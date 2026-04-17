using Microsoft.EntityFrameworkCore;
using TrustRent.Api.Services;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Shared.Models;

namespace TrustRent.Tests.Api;

public class CatalogLeaseAccessServiceTests
{
    private LeasingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LeasingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LeasingDbContext(options);
    }

    private Lease CreateTestLease(Guid? applicationId = null) => new()
    {
        Id = Guid.NewGuid(),
        PropertyId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        LandlordId = Guid.NewGuid(),
        ApplicationId = applicationId ?? Guid.NewGuid(),
        MonthlyRent = 800m,
        Deposit = 1600m,
        AdvanceRentMonths = 1,
        DurationMonths = 12,
        StartDate = DateTime.UtcNow.AddDays(30),
        EndDate = DateTime.UtcNow.AddDays(30).AddMonths(12),
        Status = LeaseStatus.AwaitingPayment
    };

    [Fact]
    public async Task GetLeaseAccessContextAsync_ExistingLease_ReturnsContext()
    {
        using var context = CreateContext();
        var lease = CreateTestLease();
        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        var sut = new CatalogLeaseAccessService(context);
        var result = await sut.GetLeaseAccessContextAsync(lease.Id);

        Assert.NotNull(result);
        Assert.Equal(lease.Id, result!.LeaseId);
        Assert.Equal(lease.MonthlyRent, result.MonthlyRent);
        Assert.Equal(lease.Deposit, result.Deposit);
        Assert.Equal(lease.AdvanceRentMonths, result.AdvanceRentMonths);
        Assert.Equal(lease.TenantId, result.TenantId);
        Assert.Equal(lease.LandlordId, result.LandlordId);
    }

    [Fact]
    public async Task GetLeaseAccessContextAsync_NonExistent_ReturnsNull()
    {
        using var context = CreateContext();
        var sut = new CatalogLeaseAccessService(context);

        var result = await sut.GetLeaseAccessContextAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLeaseAccessContextAsync_ReturnsStatusAsString()
    {
        using var context = CreateContext();
        var lease = CreateTestLease();
        lease.Status = LeaseStatus.Active;
        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        var sut = new CatalogLeaseAccessService(context);
        var result = await sut.GetLeaseAccessContextAsync(lease.Id);

        Assert.Equal("Active", result!.LeaseStatus);
    }
}
