using Microsoft.EntityFrameworkCore;
using TrustRent.Api.Services;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Shared.Models;

namespace TrustRent.Tests.Api;

public class LeasingAccessServiceTests
{
    private LeasingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LeasingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LeasingDbContext(options);
    }

    private Lease CreateTestLease(Guid? applicationId = null, Guid? propertyId = null) => new()
    {
        Id = Guid.NewGuid(),
        PropertyId = propertyId ?? Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        LandlordId = Guid.NewGuid(),
        ApplicationId = applicationId ?? Guid.NewGuid(),
        MonthlyRent = 800m,
        DurationMonths = 12,
        StartDate = DateTime.UtcNow.AddDays(30),
        EndDate = DateTime.UtcNow.AddDays(30).AddMonths(12),
        Status = LeaseStatus.Active
    };

    [Fact]
    public async Task GetLeaseByApplicationIdAsync_ExistingLease_ReturnsDto()
    {
        using var context = CreateContext();
        var appId = Guid.NewGuid();
        var lease = CreateTestLease(applicationId: appId);
        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        var sut = new LeasingAccessService(context);
        var result = await sut.GetLeaseByApplicationIdAsync(appId);

        Assert.NotNull(result);
        Assert.Equal(lease.Id, result!.Id);
        Assert.Equal(appId, result.ApplicationId);
    }

    [Fact]
    public async Task GetLeaseByApplicationIdAsync_NonExistent_ReturnsNull()
    {
        using var context = CreateContext();
        var sut = new LeasingAccessService(context);

        var result = await sut.GetLeaseByApplicationIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLeasesByApplicationIdsAsync_MultipleLeases_ReturnsDictionary()
    {
        using var context = CreateContext();
        var appId1 = Guid.NewGuid();
        var appId2 = Guid.NewGuid();
        var appId3 = Guid.NewGuid(); // No lease for this

        context.Leases.Add(CreateTestLease(applicationId: appId1));
        context.Leases.Add(CreateTestLease(applicationId: appId2));
        await context.SaveChangesAsync();

        var sut = new LeasingAccessService(context);
        var result = await sut.GetLeasesByApplicationIdsAsync(new[] { appId1, appId2, appId3 });

        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey(appId1));
        Assert.True(result.ContainsKey(appId2));
        Assert.False(result.ContainsKey(appId3));
    }

    [Fact]
    public async Task GetLeasesByPropertyIdAsync_MultipleLeases_ReturnsOrderedByCreatedAt()
    {
        using var context = CreateContext();
        var propertyId = Guid.NewGuid();

        var lease1 = CreateTestLease(propertyId: propertyId);
        lease1.CreatedAt = DateTime.UtcNow.AddDays(-10);
        var lease2 = CreateTestLease(propertyId: propertyId);
        lease2.CreatedAt = DateTime.UtcNow;

        context.Leases.AddRange(lease1, lease2);
        await context.SaveChangesAsync();

        var sut = new LeasingAccessService(context);
        var result = await sut.GetLeasesByPropertyIdAsync(propertyId);

        Assert.Equal(2, result.Count);
        Assert.Equal(lease2.Id, result[0].Id); // Most recent first
    }

    [Fact]
    public async Task GetLeasesByPropertyIdAsync_NoLeases_ReturnsEmptyList()
    {
        using var context = CreateContext();
        var sut = new LeasingAccessService(context);

        var result = await sut.GetLeasesByPropertyIdAsync(Guid.NewGuid());

        Assert.Empty(result);
    }
}
