using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Modules.Leasing.Services;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Contracts.Models;

namespace TrustRent.Tests.Leasing;

public class StripePaymentServiceTests
{
    private readonly Mock<ILeaseAccessService> _leaseAccessMock;
    private readonly Mock<ILeaseActivationService> _leaseActivationMock;
    private readonly Mock<IStripeAccountService> _stripeAccountMock;

    public StripePaymentServiceTests()
    {
        _leaseAccessMock = new Mock<ILeaseAccessService>();
        _leaseActivationMock = new Mock<ILeaseActivationService>();
        _stripeAccountMock = new Mock<IStripeAccountService>();
    }

    private (StripePaymentService Service, LeasingDbContext Context) CreateService(int platformFee = 3000)
    {
        var options = new DbContextOptionsBuilder<LeasingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new LeasingDbContext(options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Stripe:SecretKey"] = "sk_test_fake",
                ["Stripe:PlatformFeePerMonth"] = platformFee.ToString()
            })
            .Build();

        var loggerMock = new Mock<ILogger<StripePaymentService>>();

        var service = new StripePaymentService(
            context,
            _leaseAccessMock.Object,
            _leaseActivationMock.Object,
            _stripeAccountMock.Object,
            config,
            loggerMock.Object);

        return (service, context);
    }

    // --- GetInitialPaymentBreakdownAsync ---

    [Fact]
    public async Task GetInitialPaymentBreakdownAsync_ValidLease_ReturnsCorrectBreakdown()
    {
        var leaseId = Guid.NewGuid();
        _leaseAccessMock.Setup(l => l.GetLeaseAccessContextAsync(leaseId)).ReturnsAsync(new LeaseAccessContext
        {
            LeaseId = leaseId,
            MonthlyRent = 800m,
            AdvanceRentMonths = 2,
            Deposit = 1600m,
            TenantId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            PropertyId = Guid.NewGuid(),
            LeaseStatus = "AwaitingPayment"
        });

        var (service, context) = CreateService(platformFee: 3000); // 30€ per month

        var result = await service.GetInitialPaymentBreakdownAsync(leaseId);

        Assert.Equal(800m, result.MonthlyRent);
        Assert.Equal(1600m, result.AdvanceRent); // 800 * 2
        Assert.Equal(2, result.AdvanceRentMonths);
        Assert.Equal(1600m, result.Deposit);
        Assert.Equal(4000m, result.Total); // 800 + 1600 + 1600
        Assert.Equal(90m, result.PlatformFee); // 30 * (1 + 2) = 90
        Assert.Equal(3910m, result.LandlordReceives); // 4000 - 90

        context.Dispose();
    }

    [Fact]
    public async Task GetInitialPaymentBreakdownAsync_NoDeposit_CalculatesCorrectly()
    {
        var leaseId = Guid.NewGuid();
        _leaseAccessMock.Setup(l => l.GetLeaseAccessContextAsync(leaseId)).ReturnsAsync(new LeaseAccessContext
        {
            LeaseId = leaseId,
            MonthlyRent = 500m,
            AdvanceRentMonths = 0,
            Deposit = null,
            TenantId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            PropertyId = Guid.NewGuid(),
            LeaseStatus = "AwaitingPayment"
        });

        var (service, context) = CreateService(platformFee: 3000);

        var result = await service.GetInitialPaymentBreakdownAsync(leaseId);

        Assert.Equal(500m, result.MonthlyRent);
        Assert.Equal(0m, result.AdvanceRent);
        Assert.Equal(0m, result.Deposit);
        Assert.Equal(500m, result.Total);
        Assert.Equal(30m, result.PlatformFee); // 30 * 1 = 30
        Assert.Equal(470m, result.LandlordReceives);

        context.Dispose();
    }

    [Fact]
    public async Task GetInitialPaymentBreakdownAsync_LeaseNotFound_ThrowsException()
    {
        _leaseAccessMock.Setup(l => l.GetLeaseAccessContextAsync(It.IsAny<Guid>())).ReturnsAsync((LeaseAccessContext?)null);

        var (service, context) = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GetInitialPaymentBreakdownAsync(Guid.NewGuid()));

        context.Dispose();
    }

    // --- GetPaymentByIdAsync ---

    [Fact]
    public async Task GetPaymentByIdAsync_ExistingPayment_CorrectUser_ReturnsPayment()
    {
        var (service, context) = CreateService();
        var tenantId = Guid.NewGuid();
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            LeaseId = Guid.NewGuid(),
            TenantId = tenantId,
            LandlordId = Guid.NewGuid(),
            Type = PaymentType.InitialPayment,
            Amount = 1000m,
            PlatformFee = 30m,
            LandlordAmount = 970m,
            RentAmount = 500m,
            DepositAmount = 500m,
            Currency = "eur",
            Status = PaymentStatus.Succeeded
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        var result = await service.GetPaymentByIdAsync(payment.Id, tenantId);

        Assert.NotNull(result);
        Assert.Equal(payment.Id, result!.Id);
        Assert.Equal(1000m, result.Amount);

        context.Dispose();
    }

    [Fact]
    public async Task GetPaymentByIdAsync_WrongUser_ThrowsUnauthorized()
    {
        var (service, context) = CreateService();
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            LeaseId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            Type = PaymentType.InitialPayment,
            Amount = 1000m,
            PlatformFee = 30m,
            LandlordAmount = 970m,
            RentAmount = 500m,
            Currency = "eur",
            Status = PaymentStatus.Succeeded
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetPaymentByIdAsync(payment.Id, Guid.NewGuid()));

        context.Dispose();
    }

    // --- GetPaymentsByLeaseAsync ---

    [Fact]
    public async Task GetPaymentsByLeaseAsync_MultiplePayments_ReturnsAll()
    {
        var leaseId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var landlordId = Guid.NewGuid();

        _leaseAccessMock.Setup(l => l.GetLeaseAccessContextAsync(leaseId)).ReturnsAsync(new LeaseAccessContext
        {
            LeaseId = leaseId,
            TenantId = tenantId,
            LandlordId = landlordId,
            PropertyId = Guid.NewGuid(),
            MonthlyRent = 800m,
            AdvanceRentMonths = 0,
            LeaseStatus = "Active"
        });

        var (service, context) = CreateService();

        context.Payments.AddRange(
            new Payment
            {
                Id = Guid.NewGuid(), LeaseId = leaseId, TenantId = tenantId, LandlordId = landlordId,
                Type = PaymentType.InitialPayment, Amount = 2000m, PlatformFee = 60m, LandlordAmount = 1940m,
                RentAmount = 800m, DepositAmount = 1200m, Currency = "eur", Status = PaymentStatus.Succeeded
            },
            new Payment
            {
                Id = Guid.NewGuid(), LeaseId = leaseId, TenantId = tenantId, LandlordId = landlordId,
                Type = PaymentType.MonthlyRent, Amount = 800m, PlatformFee = 30m, LandlordAmount = 770m,
                RentAmount = 800m, Currency = "eur", Status = PaymentStatus.Succeeded
            }
        );
        await context.SaveChangesAsync();

        var result = await service.GetPaymentsByLeaseAsync(leaseId, tenantId);

        Assert.Equal(2, result.Count());

        context.Dispose();
    }

    // --- HandlePaymentSucceededAsync ---

    [Fact]
    public async Task HandlePaymentSucceededAsync_ExistingPayment_UpdatesStatus()
    {
        var (service, context) = CreateService();
        var leaseId = Guid.NewGuid();
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            LeaseId = leaseId,
            TenantId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            StripePaymentIntentId = "pi_test_123",
            Type = PaymentType.InitialPayment,
            Amount = 1000m,
            PlatformFee = 30m,
            LandlordAmount = 970m,
            RentAmount = 1000m,
            Currency = "eur",
            Status = PaymentStatus.Processing
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        await service.HandlePaymentSucceededAsync("pi_test_123");

        var updated = await context.Payments.FindAsync(payment.Id);
        Assert.Equal(PaymentStatus.Succeeded, updated!.Status);
        Assert.NotNull(updated.PaidAt);

        _leaseActivationMock.Verify(l => l.ActivateLeaseAfterPaymentAsync(leaseId), Times.Once);

        context.Dispose();
    }

    [Fact]
    public async Task HandlePaymentFailedAsync_ExistingPayment_UpdatesStatusAndReason()
    {
        var (service, context) = CreateService();
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            LeaseId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            StripePaymentIntentId = "pi_fail_456",
            Type = PaymentType.InitialPayment,
            Amount = 1000m,
            PlatformFee = 30m,
            LandlordAmount = 970m,
            RentAmount = 1000m,
            Currency = "eur",
            Status = PaymentStatus.Processing
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        await service.HandlePaymentFailedAsync("pi_fail_456", "Card declined");

        var updated = await context.Payments.FindAsync(payment.Id);
        Assert.Equal(PaymentStatus.Failed, updated!.Status);
        Assert.Equal("Card declined", updated.FailureReason);

        context.Dispose();
    }
}
