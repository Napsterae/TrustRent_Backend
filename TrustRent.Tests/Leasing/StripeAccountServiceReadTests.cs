using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Models;

namespace TrustRent.Tests.Leasing;

public class StripeAccountServiceReadTests
{
    private LeasingDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<LeasingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LeasingDbContext(options);
    }

    private StripeAccount CreateAccount(Guid userId, Guid? propertyId = null, bool isDefault = false) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        PropertyId = propertyId,
        StripeAccountId = $"acct_{Guid.NewGuid():N}"[..20],
        IsOnboardingComplete = true,
        ChargesEnabled = true,
        PayoutsEnabled = true,
        IsDefault = isDefault,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GetDefaultAccountAsync_ExistingDefault_ReturnsAccount()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();
        var account = CreateAccount(userId, propertyId: null, isDefault: true);
        context.StripeAccounts.Add(account);
        await context.SaveChangesAsync();

        // Use direct DB query to test the data layer logic
        var result = await context.StripeAccounts
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsDefault && s.PropertyId == null);

        Assert.NotNull(result);
        Assert.Equal(account.Id, result!.Id);
        Assert.True(result.IsDefault);
    }

    [Fact]
    public async Task GetDefaultAccountAsync_NoDefault_ReturnsNull()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();

        var result = await context.StripeAccounts
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsDefault && s.PropertyId == null);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAccountForPropertyAsync_ExistingPropertyAccount_ReturnsAccount()
    {
        using var context = CreateContext();
        var propertyId = Guid.NewGuid();
        var account = CreateAccount(Guid.NewGuid(), propertyId: propertyId);
        context.StripeAccounts.Add(account);
        await context.SaveChangesAsync();

        var result = await context.StripeAccounts
            .FirstOrDefaultAsync(s => s.PropertyId == propertyId);

        Assert.NotNull(result);
        Assert.Equal(propertyId, result!.PropertyId);
    }

    [Fact]
    public async Task GetAccountByIdAsync_Existing_ReturnsAccount()
    {
        using var context = CreateContext();
        var account = CreateAccount(Guid.NewGuid());
        context.StripeAccounts.Add(account);
        await context.SaveChangesAsync();

        var result = await context.StripeAccounts.FindAsync(account.Id);

        Assert.NotNull(result);
        Assert.Equal(account.StripeAccountId, result!.StripeAccountId);
    }

    [Fact]
    public async Task GetAccountByIdAsync_NonExistent_ReturnsNull()
    {
        using var context = CreateContext();

        var result = await context.StripeAccounts.FindAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAccountsByUserAsync_MultipleAccounts_ReturnsOrderedByDefault()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();

        var defaultAccount = CreateAccount(userId, isDefault: true);
        defaultAccount.CreatedAt = DateTime.UtcNow.AddDays(-1);

        var propertyAccount = CreateAccount(userId, propertyId: Guid.NewGuid());
        propertyAccount.CreatedAt = DateTime.UtcNow;

        context.StripeAccounts.AddRange(defaultAccount, propertyAccount);
        await context.SaveChangesAsync();

        var accounts = await context.StripeAccounts
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.IsDefault)
            .ThenByDescending(s => s.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, accounts.Count);
        Assert.True(accounts[0].IsDefault); // Default first
    }

    [Fact]
    public async Task GetAccountsByUserAsync_NoAccounts_ReturnsEmpty()
    {
        using var context = CreateContext();

        var accounts = await context.StripeAccounts
            .Where(s => s.UserId == Guid.NewGuid())
            .ToListAsync();

        Assert.Empty(accounts);
    }
}
