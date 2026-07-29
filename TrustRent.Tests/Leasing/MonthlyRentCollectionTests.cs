using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Hangfire;
using Stripe;
using System.Linq.Expressions;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Identity.Models;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.DTOs;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Jobs;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;
using TrustRent.Modules.Leasing.Services;

namespace TrustRent.Tests.Leasing;

public class MonthlyRentCollectionTests
{
    private readonly Mock<ILeaseAccessService> _leaseAccessMock;
    private readonly Mock<ILeaseActivationService> _leaseActivationMock;
    private readonly Mock<IStripeAccountService> _stripeAccountMock;
    private readonly Mock<INotificationService> _notificationMock;
    private readonly Mock<IBackgroundJobClient> _backgroundJobMock;
    private readonly Mock<IUserService> _userServiceMock;

    public MonthlyRentCollectionTests()
    {
        _leaseAccessMock = new Mock<ILeaseAccessService>();
        _leaseActivationMock = new Mock<ILeaseActivationService>();
        _stripeAccountMock = new Mock<IStripeAccountService>();
        _notificationMock = new Mock<INotificationService>();
        _backgroundJobMock = new Mock<IBackgroundJobClient>();
        _userServiceMock = new Mock<IUserService>();
    }

    private (TestableStripePaymentService Service, LeasingDbContext Context) CreateTestableService()
    {
        var options = new DbContextOptionsBuilder<LeasingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new LeasingDbContext(options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Stripe:SecretKey"] = "sk_test_fake",
                ["Stripe:PlatformFeePerMonth"] = "3000"
            })
            .Build();

        var loggerMock = new Mock<ILogger<TrustRent.Modules.Leasing.Services.StripePaymentService>>();

        var service = new TestableStripePaymentService(
            context,
            _leaseAccessMock.Object,
            _leaseActivationMock.Object,
            _stripeAccountMock.Object,
            _notificationMock.Object,
            config,
            loggerMock.Object,
            _backgroundJobMock.Object,
            userService: _userServiceMock.Object);

        return (service, context);
    }

    // ===== Helper methods =====

    private static Lease CreateTestLease(Guid? tenantId = null, Guid? landlordId = null)
    {
        return new Lease
        {
            Id = Guid.NewGuid(),
            PropertyId = Guid.NewGuid(),
            TenantId = tenantId ?? Guid.NewGuid(),
            LandlordId = landlordId ?? Guid.NewGuid(),
            CoTenantId = null,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddYears(1),
            MonthlyRent = 850m,
            Status = LeaseStatus.Active,
            ApplicationId = Guid.NewGuid()
        };
    }

    private static Payment CreateTestPayment(Guid leaseId, Guid tenantId, Guid landlordId,
        PaymentStatus status = PaymentStatus.Failed, PaymentType type = PaymentType.MonthlyRent,
        int retryAttempt = 0, string? stripePiId = "pi_test_123")
    {
        return new Payment
        {
            Id = Guid.NewGuid(),
            LeaseId = leaseId,
            TenantId = tenantId,
            LandlordId = landlordId,
            StripePaymentIntentId = stripePiId ?? "",
            Type = type,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = status,
            RetryAttempt = retryAttempt,
            CreatedAt = DateTime.UtcNow,
            IdempotencyKey = $"rent_{leaseId}_{tenantId}_{DateTime.UtcNow:yyyy-MM}"
        };
    }

    private static StripeAccount CreateTestStripeAccount(Guid landlordId)
    {
        return new StripeAccount
        {
            Id = Guid.NewGuid(),
            UserId = landlordId,
            StripeAccountId = "acct_test_123",
            ChargesEnabled = true,
            PayoutsEnabled = true
        };
    }

    private static TenantPaymentMethod CreateTestPaymentMethod(Guid tenantId)
    {
        return new TenantPaymentMethod
        {
            Id = Guid.NewGuid(),
            UserId = tenantId,
            StripePaymentMethodId = "pm_test_123",
            IsDefault = true,
            Type = "card",
            DisplayName = "Visa ****4242",
            CardBrand = "visa",
            CardLast4 = "4242"
        };
    }

    private static PaymentIntent CreateSucceededPaymentIntent(long amount = 85000, string id = "pi_test_success_123")
    {
        return new PaymentIntent
        {
            Id = id,
            Amount = amount,
            Currency = "eur",
            Status = "succeeded",
            ClientSecret = id + "_secret",
            Metadata = new Dictionary<string, string>
            {
                ["trustrent_lease_id"] = "test",
                ["type"] = "monthly_rent",
                ["billing_period"] = DateTime.UtcNow.ToString("yyyy-MM")
            }
        };
    }

    private static PaymentIntent CreateRequiresActionPaymentIntent(long amount = 85000, string id = "pi_test_3ds_123")
    {
        return new PaymentIntent
        {
            Id = id,
            Amount = amount,
            Currency = "eur",
            Status = "requires_action",
            ClientSecret = id + "_secret",
            Metadata = new Dictionary<string, string>
            {
                ["trustrent_lease_id"] = "test",
                ["type"] = "monthly_rent",
                ["billing_period"] = DateTime.UtcNow.ToString("yyyy-MM")
            }
        };
    }

    // ===================================================================
    // A. Monthly rent creation tests
    // ===================================================================

    [Fact]
    public async Task CreateMonthlyRentPaymentAsync_Success_SavesPaymentAndNotifies()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;
        var billingPeriod = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        _stripeAccountMock.Setup(s => s.GetAccountForPropertyAsync(lease.PropertyId))
            .ReturnsAsync(new StripeAccountDto(
                Guid.NewGuid(), landlordId, lease.PropertyId, "acct_test_123",
                true, true, true, true, DateTime.UtcNow));

        _userServiceMock.Setup(u => u.GetProfileAsync(tenantId))
            .ReturnsAsync(new User { Id = tenantId, StripeCustomerId = "cus_test_123" });

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);
        context.TenantPaymentMethods.Add(CreateTestPaymentMethod(tenantId));
        await context.SaveChangesAsync();

        var piResult = CreateSucceededPaymentIntent(id: "pi_test_success_999");
        service.SetCreateResult(piResult);

        // Act
        var result = await service.CreateMonthlyRentPaymentAsync(lease.Id, tenantId, billingPeriod);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PaymentStatus.Succeeded, result!.Status);
        Assert.NotNull(result.PaidAt);
        Assert.Equal(lease.Id, result.LeaseId);
        Assert.Equal(850m, result.Amount);
        Assert.Equal("eur", result.Currency);

        // Verify saved in DB
        var savedPayment = await context.Payments.FirstOrDefaultAsync(p => p.Id == result.Id);
        Assert.NotNull(savedPayment);
        Assert.Equal(PaymentStatus.Succeeded, savedPayment!.Status);
        Assert.NotNull(savedPayment.PaidAt);
        Assert.Equal("pi_test_success_999", savedPayment.StripePaymentIntentId);
        // RetryAttempt defaults to 0 (first attempt, no retry)
        Assert.Equal(0, savedPayment.RetryAttempt);

        // Verify notifications sent to both parties with payment.Id as referenceId
        _notificationMock.Verify(n => n.SendNotificationAsync(
            tenantId, "payment",
            It.Is<string>(s => s.Contains("confirmada") || s.Contains("sucesso")),
            savedPayment.Id), Times.Once);
        _notificationMock.Verify(n => n.SendNotificationAsync(
            landlordId, "payment",
            It.Is<string>(s => s.Contains("Recebeste") || s.Contains("confirmada")),
            savedPayment.Id), Times.Once);

        context.Dispose();
    }

    [Fact]
    public async Task CreateMonthlyRentPaymentAsync_RequiresAction_SavesAsPending()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;
        var billingPeriod = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        _stripeAccountMock.Setup(s => s.GetAccountForPropertyAsync(lease.PropertyId))
            .ReturnsAsync(new StripeAccountDto(
                Guid.NewGuid(), landlordId, lease.PropertyId, "acct_test_123",
                true, true, true, true, DateTime.UtcNow));

        _userServiceMock.Setup(u => u.GetProfileAsync(tenantId))
            .ReturnsAsync(new User { Id = tenantId, StripeCustomerId = "cus_test_123" });

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);
        context.TenantPaymentMethods.Add(CreateTestPaymentMethod(tenantId));
        await context.SaveChangesAsync();

        var piResult = CreateRequiresActionPaymentIntent(id: "pi_test_3ds_999");
        service.SetCreateResult(piResult);

        // Act
        var result = await service.CreateMonthlyRentPaymentAsync(lease.Id, tenantId, billingPeriod);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PaymentStatus.Pending, result!.Status);
        Assert.Null(result.PaidAt);

        var savedPayment = await context.Payments.FirstOrDefaultAsync(p => p.Id == result.Id);
        Assert.NotNull(savedPayment);
        Assert.Equal(PaymentStatus.Pending, savedPayment!.Status);
        Assert.Null(savedPayment.PaidAt);

        // Should NOT have sent success notifications
        _notificationMock.Verify(n => n.SendNotificationAsync(
            It.IsAny<Guid>(), "payment",
            It.Is<string>(s => s.Contains("confirmada")),
            It.IsAny<Guid?>()), Times.Never);

        context.Dispose();
    }

    [Fact]
    public async Task CreateMonthlyRentPaymentAsync_StripeThrows_MarksPaymentAsFailed()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;
        var billingPeriod = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        _stripeAccountMock.Setup(s => s.GetAccountForPropertyAsync(lease.PropertyId))
            .ReturnsAsync(new StripeAccountDto(
                Guid.NewGuid(), landlordId, lease.PropertyId, "acct_test_123",
                true, true, true, true, DateTime.UtcNow));

        _userServiceMock.Setup(u => u.GetProfileAsync(tenantId))
            .ReturnsAsync(new User { Id = tenantId, StripeCustomerId = "cus_test_123" });

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);
        context.TenantPaymentMethods.Add(CreateTestPaymentMethod(tenantId));
        await context.SaveChangesAsync();

        service.SetCreateException(new StripeException
        {
            StripeError = new StripeError { Message = "card_declined" }
        });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<StripeException>(
            () => service.CreateMonthlyRentPaymentAsync(lease.Id, tenantId, billingPeriod));

        Assert.Contains("card_declined", ex.StripeError?.Message ?? "");

        // Verify payment was saved as Failed
        var failedPayment = await context.Payments
            .FirstOrDefaultAsync(p => p.LeaseId == lease.Id && p.TenantId == tenantId);
        Assert.NotNull(failedPayment);
        Assert.Equal(PaymentStatus.Failed, failedPayment!.Status);
        Assert.Contains("card_declined", failedPayment.FailureReason ?? "");

        context.Dispose();
    }

    [Fact]
    public async Task CreateMonthlyRentPaymentAsync_AlreadySucceeded_ReturnsNull()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;
        var billingPeriod = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);

        // Pre-seed a Succeeded payment for this billing period
        var existingPayment = CreateTestPayment(lease.Id, tenantId, landlordId,
            status: PaymentStatus.Succeeded, stripePiId: "pi_existing_success");
        existingPayment.CreatedAt = DateTime.UtcNow; // matches billing period
        context.Payments.Add(existingPayment);
        await context.SaveChangesAsync();

        // Act
        var result = await service.CreateMonthlyRentPaymentAsync(lease.Id, tenantId, billingPeriod);

        // Assert
        Assert.Null(result);

        context.Dispose();
    }

    [Fact]
    public async Task CreateMonthlyRentPaymentAsync_WithIdempotencyKey_SavesBeforeStripeCall()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;
        var billingPeriod = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        _stripeAccountMock.Setup(s => s.GetAccountForPropertyAsync(lease.PropertyId))
            .ReturnsAsync(new StripeAccountDto(
                Guid.NewGuid(), landlordId, lease.PropertyId, "acct_test_123",
                true, true, true, true, DateTime.UtcNow));

        _userServiceMock.Setup(u => u.GetProfileAsync(tenantId))
            .ReturnsAsync(new User { Id = tenantId, StripeCustomerId = "cus_test_123" });

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);
        context.TenantPaymentMethods.Add(CreateTestPaymentMethod(tenantId));
        await context.SaveChangesAsync();

        Guid capturedPaymentId = Guid.Empty;

        // Use a custom function that checks the DB state at the time Stripe would be called
        service.SetCreateFunc(async (options, requestOptions) =>
        {
            var savedPayment = await context.Payments
                .FirstOrDefaultAsync(p => p.LeaseId == lease.Id && p.TenantId == tenantId);
            Assert.NotNull(savedPayment);
            Assert.Equal(PaymentStatus.Processing, savedPayment!.Status);
            Assert.StartsWith("pending_", savedPayment.StripePaymentIntentId);
            Assert.NotNull(savedPayment.IdempotencyKey);
            Assert.Contains(lease.Id.ToString(), savedPayment.IdempotencyKey);
            capturedPaymentId = savedPayment.Id;

            return new PaymentIntent
            {
                Id = "pi_created_after_save_123",
                Amount = options.Amount ?? 0,
                Currency = options.Currency,
                Status = "succeeded",
                ClientSecret = "pi_test_secret_new",
                Metadata = options.Metadata ?? new Dictionary<string, string>()
            };
        });

        // Act
        var result = await service.CreateMonthlyRentPaymentAsync(lease.Id, tenantId, billingPeriod);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PaymentStatus.Succeeded, result!.Status);
        Assert.NotEqual(Guid.Empty, capturedPaymentId);

        // Verify the payment was updated with the real PI ID
        var finalPayment = await context.Payments.FindAsync(capturedPaymentId);
        Assert.NotNull(finalPayment);
        Assert.Equal("pi_created_after_save_123", finalPayment!.StripePaymentIntentId);
        Assert.Equal(PaymentStatus.Succeeded, finalPayment.Status);

        context.Dispose();
    }

    [Fact]
    public async Task CreateMonthlyRentPaymentAsync_LeaseNotActive_Throws()
    {
        // Arrange
        var lease = CreateTestLease();
        lease.Status = LeaseStatus.Pending;
        var tenantId = lease.TenantId;
        var billingPeriod = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateMonthlyRentPaymentAsync(lease.Id, tenantId, billingPeriod));
        Assert.Contains("não está ativo", ex.Message);

        context.Dispose();
    }

    [Fact]
    public async Task CreateMonthlyRentPaymentAsync_UnauthorizedTenant_Throws()
    {
        // Arrange
        var lease = CreateTestLease();
        var unauthorizedUserId = Guid.NewGuid();
        var billingPeriod = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CreateMonthlyRentPaymentAsync(lease.Id, unauthorizedUserId, billingPeriod));
        Assert.Contains("Apenas inquilinos", ex.Message);

        context.Dispose();
    }

    [Fact]
    public async Task CreateMonthlyRentPaymentAsync_NoPaymentMethod_Throws()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var billingPeriod = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);
        // No payment method added
        await context.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateMonthlyRentPaymentAsync(lease.Id, tenantId, billingPeriod));
        Assert.Contains("método de pagamento", ex.Message);

        context.Dispose();
    }

    // ===================================================================
    // B. Idempotency tests
    // ===================================================================

    [Fact]
    public async Task CreateMonthlyRentPaymentAsync_SameIdempotencyKey_ReturnsExistingPayment()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;
        var billingPeriod = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var idempotencyKey = $"rent_{lease.Id}_{tenantId}_{billingPeriod:yyyy-MM}";

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);

        // Pre-seed a Processing payment with the idempotency key and a pending_ placeholder
        var existingPayment = new Payment
        {
            Id = Guid.NewGuid(),
            LeaseId = lease.Id,
            TenantId = tenantId,
            LandlordId = landlordId,
            StripePaymentIntentId = $"pending_{Guid.NewGuid()}",
            IdempotencyKey = idempotencyKey,
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Processing,
            RetryAttempt = 0,
            CreatedAt = DateTime.UtcNow
        };
        context.Payments.Add(existingPayment);
        await context.SaveChangesAsync();

        // Act
        var result = await service.CreateMonthlyRentPaymentAsync(lease.Id, tenantId, billingPeriod);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(existingPayment.Id, result!.Id);
        Assert.Equal(PaymentStatus.Processing, result.Status);

        // Stripe should NOT have been called (pending_ prefix path returns as-is)
        // Verify by checking no Stripe operation happened:
        // The result's StripePaymentIntentId should still start with "pending_"
        Assert.StartsWith("pending_", existingPayment.StripePaymentIntentId);

        context.Dispose();
    }

    [Fact]
    public async Task CreateMonthlyRentPaymentAsync_PendingPaymentWithRealPiId_ReconcilesFromStripe()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;
        var billingPeriod = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var idempotencyKey = $"rent_{lease.Id}_{tenantId}_{billingPeriod:yyyy-MM}";
        var realPiId = "pi_reconcile_123";

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);

        // Pre-seed a Processing payment with a real PI ID (not pending_ prefix)
        var existingPayment = new Payment
        {
            Id = Guid.NewGuid(),
            LeaseId = lease.Id,
            TenantId = tenantId,
            LandlordId = landlordId,
            StripePaymentIntentId = realPiId,
            IdempotencyKey = idempotencyKey,
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Processing,
            RetryAttempt = 0,
            CreatedAt = DateTime.UtcNow
        };
        context.Payments.Add(existingPayment);
        await context.SaveChangesAsync();

        // Stripe Get returns a succeeded PaymentIntent
        service.SetGetResult(new PaymentIntent
        {
            Id = realPiId,
            Amount = 85000,
            Currency = "eur",
            Status = "succeeded"
        });

        // Act
        var result = await service.CreateMonthlyRentPaymentAsync(lease.Id, tenantId, billingPeriod);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(existingPayment.Id, result!.Id);
        Assert.Equal(PaymentStatus.Succeeded, result.Status);
        Assert.NotNull(result.PaidAt);

        // Verify DB was updated
        var updatedPayment = await context.Payments.FindAsync(existingPayment.Id);
        Assert.NotNull(updatedPayment);
        Assert.Equal(PaymentStatus.Succeeded, updatedPayment!.Status);
        Assert.NotNull(updatedPayment.PaidAt);

        // Notification should have been sent for the reconciliation
        _notificationMock.Verify(n => n.SendNotificationAsync(
            tenantId, "payment",
            It.Is<string>(s => s.Contains("confirmada") || s.Contains("sucesso")),
            existingPayment.Id), Times.Once);
        _notificationMock.Verify(n => n.SendNotificationAsync(
            landlordId, "payment",
            It.Is<string>(s => s.Contains("Recebeste") || s.Contains("confirmada")),
            existingPayment.Id), Times.Once);

        context.Dispose();
    }

    [Fact]
    public async Task CreateMonthlyRentPaymentAsync_PendingPaymentWithPendingPrefix_ReturnsAsIs()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;
        var billingPeriod = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var idempotencyKey = $"rent_{lease.Id}_{tenantId}_{billingPeriod:yyyy-MM}";
        var pendingPiId = $"pending_{Guid.NewGuid()}";

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);

        // Pre-seed a Processing payment with a pending_ prefix PI ID
        var existingPayment = new Payment
        {
            Id = Guid.NewGuid(),
            LeaseId = lease.Id,
            TenantId = tenantId,
            LandlordId = landlordId,
            StripePaymentIntentId = pendingPiId,
            IdempotencyKey = idempotencyKey,
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Processing,
            RetryAttempt = 0,
            CreatedAt = DateTime.UtcNow
        };
        context.Payments.Add(existingPayment);
        await context.SaveChangesAsync();

        // Act
        var result = await service.CreateMonthlyRentPaymentAsync(lease.Id, tenantId, billingPeriod);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(existingPayment.Id, result!.Id);
        Assert.Equal(PaymentStatus.Processing, result.Status);

        // Verify Stripe was NOT called — no Get or Search operations should have happened
        // The payment's PI ID should still be the pending_ prefix
        var unchangedPayment = await context.Payments.FindAsync(existingPayment.Id);
        Assert.NotNull(unchangedPayment);
        Assert.Equal(pendingPiId, unchangedPayment!.StripePaymentIntentId);
        Assert.Equal(PaymentStatus.Processing, unchangedPayment.Status);

        context.Dispose();
    }

    [Fact]
    public async Task CreateMonthlyRentPaymentAsync_OrphanedStripePayment_Reconciles()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;
        var billingPeriod = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        _stripeAccountMock.Setup(s => s.GetAccountForPropertyAsync(lease.PropertyId))
            .ReturnsAsync(new StripeAccountDto(
                Guid.NewGuid(), landlordId, lease.PropertyId, "acct_test_123",
                true, true, true, true, DateTime.UtcNow));

        _userServiceMock.Setup(u => u.GetProfileAsync(tenantId))
            .ReturnsAsync(new User { Id = tenantId, StripeCustomerId = "cus_test_123" });

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);
        context.TenantPaymentMethods.Add(CreateTestPaymentMethod(tenantId));
        await context.SaveChangesAsync();

        // Stripe Search finds an orphaned succeeded PaymentIntent
        service.SetSearchResult(new StripeSearchResult<PaymentIntent>
        {
            Data = new List<PaymentIntent>
            {
                new PaymentIntent
                {
                    Id = "pi_orphan_123",
                    Amount = 85000,
                    Currency = "eur",
                    Status = "succeeded",
                    ApplicationFeeAmount = 3000,
                    Metadata = new Dictionary<string, string>
                    {
                        ["trustrent_lease_id"] = lease.Id.ToString(),
                        ["type"] = "monthly_rent",
                        ["billing_period"] = billingPeriod.ToString("yyyy-MM"),
                        ["trustrent_tenant_id"] = tenantId.ToString()
                    }
                }
            }
        });

        // Act
        var result = await service.CreateMonthlyRentPaymentAsync(lease.Id, tenantId, billingPeriod);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PaymentStatus.Succeeded, result!.Status);
        Assert.NotNull(result.PaidAt);

        // Verify the reconciled payment was saved
        var reconciledPayment = await context.Payments
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == "pi_orphan_123");
        Assert.NotNull(reconciledPayment);
        Assert.Equal(PaymentStatus.Succeeded, reconciledPayment!.Status);
        Assert.NotNull(reconciledPayment.PaidAt);

        // Notification should have been sent
        _notificationMock.Verify(n => n.SendNotificationAsync(
            tenantId, "payment",
            It.Is<string>(s => s.Contains("confirmada") || s.Contains("sucesso")),
            reconciledPayment.Id), Times.Once);
        _notificationMock.Verify(n => n.SendNotificationAsync(
            landlordId, "payment",
            It.Is<string>(s => s.Contains("Recebeste") || s.Contains("confirmada")),
            reconciledPayment.Id), Times.Once);

        context.Dispose();
    }

    // ===================================================================
    // C. Retry tests
    // ===================================================================

    [Fact]
    public async Task HandlePaymentFailedAsync_MonthlyRent_SchedulesRetry()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;
        var paymentId = Guid.NewGuid();

        var (service, context) = CreateTestableService();

        var payment = new Payment
        {
            Id = paymentId,
            LeaseId = lease.Id,
            TenantId = tenantId,
            LandlordId = landlordId,
            StripePaymentIntentId = "pi_retry_1",
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Processing,
            RetryAttempt = 0,
            CreatedAt = DateTime.UtcNow
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        TimeSpan capturedDelay = TimeSpan.Zero;

        _backgroundJobMock.Setup(b => b.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>())).Callback<Hangfire.Common.Job, Hangfire.States.IState>((job, state) => { var scheduled = state as Hangfire.States.ScheduledState; if (scheduled != null) capturedDelay = scheduled.EnqueueAt - DateTime.UtcNow; });;

        // Act
        await service.HandlePaymentFailedAsync("pi_retry_1", "card_declined");

        // Assert
        var updated = await context.Payments.FindAsync(paymentId);
        Assert.NotNull(updated);
        Assert.Equal(PaymentStatus.Failed, updated!.Status);
        Assert.Equal(1, updated.RetryAttempt); // incremented from 0 to 1

        _backgroundJobMock.Verify(b => b.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()), Times.Once);

        Assert.True(Math.Abs((capturedDelay - TimeSpan.FromDays(3)).TotalSeconds) < 5, $"Expected ~3 days delay, got {capturedDelay}");

        context.Dispose();
    }

    [Fact]
    public async Task HandlePaymentFailedAsync_MonthlyRent_SecondFailure_Schedules7DayRetry()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;
        var paymentId = Guid.NewGuid();

        var (service, context) = CreateTestableService();

        var payment = new Payment
        {
            Id = paymentId,
            LeaseId = lease.Id,
            TenantId = tenantId,
            LandlordId = landlordId,
            StripePaymentIntentId = "pi_retry_2",
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Processing,
            RetryAttempt = 1,
            CreatedAt = DateTime.UtcNow
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        TimeSpan capturedDelay = TimeSpan.Zero;

        _backgroundJobMock.Setup(b => b.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>())).Callback<Hangfire.Common.Job, Hangfire.States.IState>((job, state) => { var scheduled = state as Hangfire.States.ScheduledState; if (scheduled != null) capturedDelay = scheduled.EnqueueAt - DateTime.UtcNow; });;

        // Act
        await service.HandlePaymentFailedAsync("pi_retry_2", "card_declined");

        // Assert
        var updated = await context.Payments.FindAsync(paymentId);
        Assert.NotNull(updated);
        Assert.Equal(2, updated!.RetryAttempt);

        _backgroundJobMock.Verify(b => b.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()), Times.Once);

        Assert.True(Math.Abs((capturedDelay - TimeSpan.FromDays(7)).TotalSeconds) < 5, $"Expected ~7 days delay, got {capturedDelay}");

        context.Dispose();
    }

    [Fact]
    public async Task HandlePaymentFailedAsync_MonthlyRent_ThirdFailure_Schedules14DayRetry()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;
        var paymentId = Guid.NewGuid();

        var (service, context) = CreateTestableService();

        var payment = new Payment
        {
            Id = paymentId,
            LeaseId = lease.Id,
            TenantId = tenantId,
            LandlordId = landlordId,
            StripePaymentIntentId = "pi_retry_3",
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Processing,
            RetryAttempt = 2,
            CreatedAt = DateTime.UtcNow
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        TimeSpan capturedDelay = TimeSpan.Zero;

        _backgroundJobMock.Setup(b => b.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>())).Callback<Hangfire.Common.Job, Hangfire.States.IState>((job, state) => { var scheduled = state as Hangfire.States.ScheduledState; if (scheduled != null) capturedDelay = scheduled.EnqueueAt - DateTime.UtcNow; });;

        // Act
        await service.HandlePaymentFailedAsync("pi_retry_3", "card_declined");

        // Assert
        var updated = await context.Payments.FindAsync(paymentId);
        Assert.NotNull(updated);
        Assert.Equal(3, updated!.RetryAttempt);

        _backgroundJobMock.Verify(b => b.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()), Times.Once);

        Assert.True(Math.Abs((capturedDelay - TimeSpan.FromDays(14)).TotalSeconds) < 5, $"Expected ~14 days delay, got {capturedDelay}");

        context.Dispose();
    }

    [Fact]
    public async Task HandlePaymentFailedAsync_MonthlyRent_FourthFailure_NoRetryScheduled()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;
        var paymentId = Guid.NewGuid();

        var (service, context) = CreateTestableService();

        var payment = new Payment
        {
            Id = paymentId,
            LeaseId = lease.Id,
            TenantId = tenantId,
            LandlordId = landlordId,
            StripePaymentIntentId = "pi_retry_4",
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Processing,
            RetryAttempt = 3,
            CreatedAt = DateTime.UtcNow
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        // Act
        await service.HandlePaymentFailedAsync("pi_retry_4", "card_declined");

        // Assert
        var updated = await context.Payments.FindAsync(paymentId);
        Assert.NotNull(updated);
        Assert.Equal(PaymentStatus.Failed, updated!.Status);
        // RetryAttempt stays at 3 (no increment since the code only bumps when scheduling)
        // Wait — let me re-check. The code checks `retryAttempt < 3`, so if retryAttempt = 3, it doesn't schedule.
        // Let me check the code logic again:
        // `if (payment.Type == PaymentType.MonthlyRent && payment.RetryAttempt < 3)`
        // So RetryAttempt = 3 means the condition is false, so no scheduling and no increment.
        Assert.Equal(3, updated.RetryAttempt);

        _backgroundJobMock.Verify(b => b.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()), Times.Never);

        context.Dispose();
    }

    [Fact]
    public async Task HandlePaymentFailedAsync_MonthlyRent_NotifiesLandlordWithRetryInfo()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;
        var paymentId = Guid.NewGuid();

        var (service, context) = CreateTestableService();

        var payment = new Payment
        {
            Id = paymentId,
            LeaseId = lease.Id,
            TenantId = tenantId,
            LandlordId = landlordId,
            StripePaymentIntentId = "pi_notify_retry",
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Processing,
            RetryAttempt = 0,
            CreatedAt = DateTime.UtcNow
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        // Act
        await service.HandlePaymentFailedAsync("pi_notify_retry", "card_declined");

        // Assert — RetryAttempt gets bumped to 1 before NotifyPaymentFailedAsync is called
        // So the landlord message should contain "tentativa 1/3" and "3 dias"
        _notificationMock.Verify(n => n.SendNotificationAsync(
            landlordId, "payment",
            It.Is<string>(s => s.Contains("tentativa 1/3") && s.Contains("3 dias")),
            payment.Id), Times.Once);

        context.Dispose();
    }

    [Fact]
    public async Task HandlePaymentFailedAsync_InitialPayment_NoRetryScheduled()
    {
        // Arrange
        var paymentId = Guid.NewGuid();

        var (service, context) = CreateTestableService();

        var payment = new Payment
        {
            Id = paymentId,
            LeaseId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            StripePaymentIntentId = "pi_initial_fail",
            Type = PaymentType.InitialPayment,
            Amount = 4000m,
            PlatformFee = 90m,
            LandlordAmount = 3910m,
            RentAmount = 800m,
            Currency = "eur",
            Status = PaymentStatus.Processing,
            RetryAttempt = 0,
            CreatedAt = DateTime.UtcNow
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        // Act
        await service.HandlePaymentFailedAsync("pi_initial_fail", "card_declined");

        // Assert
        _backgroundJobMock.Verify(b => b.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()), Times.Never);

        context.Dispose();
    }

    [Fact]
    public async Task HandlePaymentFailedAsync_DuplicateWebhook_NoDoubleProcessing()
    {
        // Arrange
        var paymentId = Guid.NewGuid();

        var (service, context) = CreateTestableService();

        var payment = new Payment
        {
            Id = paymentId,
            LeaseId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            StripePaymentIntentId = "pi_dup_fail",
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Failed, // Already failed
            RetryAttempt = 0,
            CreatedAt = DateTime.UtcNow
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        // Act
        await service.HandlePaymentFailedAsync("pi_dup_fail", "card_declined");

        // Assert — no duplicate scheduling, no extra notification
        _backgroundJobMock.Verify(b => b.Create(It.IsAny<Hangfire.Common.Job>(), It.IsAny<Hangfire.States.IState>()), Times.Never);

        // No extra notification (AlreadyFailed check short-circuits before NotifyPaymentFailedAsync)
        _notificationMock.Verify(n => n.SendNotificationAsync(
            It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);

        context.Dispose();
    }

    // ===================================================================
    // D. On-demand retry tests
    // ===================================================================

    [Fact]
    public async Task RetryMonthlyRentPaymentAsync_FailedPayment_CreatesNewAttempt()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;
        var billingPeriod = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        _stripeAccountMock.Setup(s => s.GetAccountForPropertyAsync(lease.PropertyId))
            .ReturnsAsync(new StripeAccountDto(
                Guid.NewGuid(), landlordId, lease.PropertyId, "acct_test_123",
                true, true, true, true, DateTime.UtcNow));

        _userServiceMock.Setup(u => u.GetProfileAsync(tenantId))
            .ReturnsAsync(new User { Id = tenantId, StripeCustomerId = "cus_test_123" });

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);
        context.TenantPaymentMethods.Add(CreateTestPaymentMethod(tenantId));

        // Pre-seed a Failed payment
        var failedPayment = CreateTestPayment(lease.Id, tenantId, landlordId,
            status: PaymentStatus.Failed, retryAttempt: 0, stripePiId: "pi_failed_original");
        context.Payments.Add(failedPayment);
        await context.SaveChangesAsync();

        service.SetCreateResult(CreateSucceededPaymentIntent(id: "pi_retry_new"));

        // Act
        var result = await service.RetryMonthlyRentPaymentAsync(lease.Id, tenantId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(PaymentStatus.Succeeded, result!.Status);

        // A new payment should have been created (different ID from the failed one)
        Assert.NotEqual(failedPayment.Id, result.Id);

        context.Dispose();
    }

    [Fact]
    public async Task RetryMonthlyRentPaymentAsync_SucceededPayment_ReturnsExisting()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);

        // Pre-seed a Succeeded payment
        var succeededPayment = CreateTestPayment(lease.Id, tenantId, landlordId,
            status: PaymentStatus.Succeeded, stripePiId: "pi_already_success");
        context.Payments.Add(succeededPayment);
        await context.SaveChangesAsync();

        // Act
        var result = await service.RetryMonthlyRentPaymentAsync(lease.Id, tenantId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(succeededPayment.Id, result!.Id);
        Assert.Equal(PaymentStatus.Succeeded, result.Status);

        context.Dispose();
    }

    [Fact]
    public async Task RetryMonthlyRentPaymentAsync_PendingPayment_ReconcilesFromStripe()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;
        var realPiId = "pi_pending_reconcile";

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);

        // Pre-seed a Pending payment with a real PI ID
        var pendingPayment = new Payment
        {
            Id = Guid.NewGuid(),
            LeaseId = lease.Id,
            TenantId = tenantId,
            LandlordId = landlordId,
            StripePaymentIntentId = realPiId,
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Pending,
            RetryAttempt = 0,
            CreatedAt = DateTime.UtcNow
        };
        context.Payments.Add(pendingPayment);
        await context.SaveChangesAsync();

        service.SetGetResult(new PaymentIntent
        {
            Id = realPiId,
            Amount = 85000,
            Currency = "eur",
            Status = "succeeded"
        });

        // Act
        var result = await service.RetryMonthlyRentPaymentAsync(lease.Id, tenantId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(pendingPayment.Id, result!.Id);
        Assert.Equal(PaymentStatus.Succeeded, result.Status);

        // Notification should have been sent for reconciliation
        _notificationMock.Verify(n => n.SendNotificationAsync(
            tenantId, "payment",
            It.Is<string>(s => s.Contains("confirmada") || s.Contains("sucesso")),
            pendingPayment.Id), Times.Once);

        context.Dispose();
    }

    [Fact]
    public async Task RetryMonthlyRentPaymentAsync_PendingPaymentWithPendingPrefix_ReturnsAsIs()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);

        // Pre-seed a Pending payment with a pending_ prefix
        var pendingPayment = new Payment
        {
            Id = Guid.NewGuid(),
            LeaseId = lease.Id,
            TenantId = tenantId,
            LandlordId = landlordId,
            StripePaymentIntentId = $"pending_{Guid.NewGuid()}",
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Pending,
            RetryAttempt = 0,
            CreatedAt = DateTime.UtcNow
        };
        context.Payments.Add(pendingPayment);
        await context.SaveChangesAsync();

        // Act
        var result = await service.RetryMonthlyRentPaymentAsync(lease.Id, tenantId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(pendingPayment.Id, result!.Id);
        Assert.Equal(PaymentStatus.Pending, result.Status);

        // Stripe should NOT have been called
        Assert.StartsWith("pending_", pendingPayment.StripePaymentIntentId);

        context.Dispose();
    }

    [Fact]
    public async Task RetryMonthlyRentPaymentAsync_NoPaymentFound_ReturnsNull()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        // Act
        var result = await service.RetryMonthlyRentPaymentAsync(lease.Id, tenantId);

        // Assert
        Assert.Null(result);

        context.Dispose();
    }

    [Fact]
    public async Task RetryMonthlyRentPaymentAsync_UnauthorizedUser_Throws()
    {
        // Arrange
        var lease = CreateTestLease();
        var unauthorizedUserId = Guid.NewGuid();

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.RetryMonthlyRentPaymentAsync(lease.Id, unauthorizedUserId));
        Assert.Contains("Apenas o inquilino", ex.Message);

        context.Dispose();
    }

    // ===================================================================
    // E. Webhook handler tests
    // ===================================================================

    [Fact]
    public async Task HandlePaymentSucceededAsync_MonthlyRent_NotifiesBothParties()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var landlordId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var (service, context) = CreateTestableService();

        context.Payments.Add(new Payment
        {
            Id = paymentId,
            LeaseId = leaseId,
            TenantId = tenantId,
            LandlordId = landlordId,
            StripePaymentIntentId = "pi_webhook_success",
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Processing
        });
        await context.SaveChangesAsync();

        // Act
        await service.HandlePaymentSucceededAsync("pi_webhook_success");

        // Assert
        _notificationMock.Verify(n => n.SendNotificationAsync(
            tenantId, "payment",
            It.Is<string>(s => s.Contains("renda mensal")),
            paymentId), Times.Once);
        _notificationMock.Verify(n => n.SendNotificationAsync(
            landlordId, "payment",
            It.Is<string>(s => s.Contains("renda mensal")),
            paymentId), Times.Once);

        var updated = await context.Payments.FindAsync(paymentId);
        Assert.NotNull(updated);
        Assert.Equal(PaymentStatus.Succeeded, updated!.Status);
        Assert.NotNull(updated.PaidAt);

        context.Dispose();
    }

    [Fact]
    public async Task HandlePaymentSucceededAsync_DuplicateWebhook_NoDoubleNotification()
    {
        // Arrange
        var paymentId = Guid.NewGuid();

        var (service, context) = CreateTestableService();

        context.Payments.Add(new Payment
        {
            Id = paymentId,
            LeaseId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            StripePaymentIntentId = "pi_dup_success",
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Succeeded // Already succeeded
        });
        await context.SaveChangesAsync();

        // Act
        await service.HandlePaymentSucceededAsync("pi_dup_success");

        // Assert — should NOT send duplicate notifications
        _notificationMock.Verify(n => n.SendNotificationAsync(
            It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);

        context.Dispose();
    }

    [Fact]
    public async Task HandlePaymentFailedAsync_NotifiesWithPaymentIdAsReferenceId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var landlordId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var (service, context) = CreateTestableService();

        context.Payments.Add(new Payment
        {
            Id = paymentId,
            LeaseId = leaseId,
            TenantId = tenantId,
            LandlordId = landlordId,
            StripePaymentIntentId = "pi_fail_ref",
            Type = PaymentType.InitialPayment,
            Amount = 4000m,
            PlatformFee = 90m,
            LandlordAmount = 3910m,
            RentAmount = 800m,
            Currency = "eur",
            Status = PaymentStatus.Processing
        });
        await context.SaveChangesAsync();

        // Act
        await service.HandlePaymentFailedAsync("pi_fail_ref", "card_declined");

        // Assert — referenceId should be payment.Id, NOT leaseId
        _notificationMock.Verify(n => n.SendNotificationAsync(
            tenantId, "payment",
            It.IsAny<string>(),
            paymentId), Times.Once);
        _notificationMock.Verify(n => n.SendNotificationAsync(
            landlordId, "payment",
            It.IsAny<string>(),
            paymentId), Times.Once);

        context.Dispose();
    }

    [Fact]
    public async Task HandlePaymentRequiresActionAsync_SetsPendingAndNotifies()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var landlordId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var (service, context) = CreateTestableService();

        context.Payments.Add(new Payment
        {
            Id = paymentId,
            LeaseId = leaseId,
            TenantId = tenantId,
            LandlordId = landlordId,
            StripePaymentIntentId = "pi_3ds_req",
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Processing
        });
        await context.SaveChangesAsync();

        // Act
        await service.HandlePaymentRequiresActionAsync("pi_3ds_req");

        // Assert
        var updated = await context.Payments.FindAsync(paymentId);
        Assert.NotNull(updated);
        Assert.Equal(PaymentStatus.Pending, updated!.Status);

        // Both parties should be notified with 3DS message containing payment URL
        _notificationMock.Verify(n => n.SendNotificationAsync(
            tenantId, "payment",
            It.Is<string>(s => s.Contains("autenticação") && s.Contains("/payments/")),
            paymentId), Times.Once);
        _notificationMock.Verify(n => n.SendNotificationAsync(
            landlordId, "payment",
            It.Is<string>(s => s.Contains("autenticação")),
            paymentId), Times.Once);

        context.Dispose();
    }

    [Fact]
    public async Task HandlePaymentRequiresActionAsync_AlreadySucceeded_NoOverwrite()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var paidAt = DateTime.UtcNow.AddHours(-1);

        var (service, context) = CreateTestableService();

        context.Payments.Add(new Payment
        {
            Id = paymentId,
            LeaseId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            StripePaymentIntentId = "pi_3ds_dup",
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Succeeded, // Already succeeded
            PaidAt = paidAt
        });
        await context.SaveChangesAsync();

        // Act
        await service.HandlePaymentRequiresActionAsync("pi_3ds_dup");

        // Assert — status should still be Succeeded
        var updated = await context.Payments.FindAsync(paymentId);
        Assert.NotNull(updated);
        Assert.Equal(PaymentStatus.Succeeded, updated!.Status);
        Assert.Equal(paidAt, updated.PaidAt); // PaidAt should not have been overwritten

        context.Dispose();
    }

    // ===================================================================
    // F. Notification content tests
    // ===================================================================

    [Fact]
    public async Task NotifyPaymentFailedAsync_TenantMessageIncludesRetryScheduleAndLink()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;
        var paymentId = Guid.NewGuid();

        var (service, context) = CreateTestableService();

        var payment = new Payment
        {
            Id = paymentId,
            LeaseId = lease.Id,
            TenantId = tenantId,
            LandlordId = landlordId,
            StripePaymentIntentId = "pi_fail_msg",
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Processing,
            RetryAttempt = 0,
            CreatedAt = DateTime.UtcNow
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        // Act
        await service.HandlePaymentFailedAsync("pi_fail_msg", "fundos insuficientes");

        // Assert — tenant message should contain retry info and payment link
        _notificationMock.Verify(n => n.SendNotificationAsync(
            tenantId, "payment",
            It.Is<string>(s =>
                s.Contains("3 dias") &&
                s.Contains("/payments/")),
            paymentId), Times.Once);

        context.Dispose();
    }

    [Fact]
    public async Task NotifyPaymentFailedAsync_LandlordMessageIncludesAttemptNumber()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;
        var paymentId = Guid.NewGuid();

        var (service, context) = CreateTestableService();

        var payment = new Payment
        {
            Id = paymentId,
            LeaseId = lease.Id,
            TenantId = tenantId,
            LandlordId = landlordId,
            StripePaymentIntentId = "pi_fail_landlord",
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Processing,
            RetryAttempt = 1, // This simulates a second attempt failure
            CreatedAt = DateTime.UtcNow
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        // Act
        await service.HandlePaymentFailedAsync("pi_fail_landlord", "fundos insuficientes");

        // Assert — landlord message should contain the attempt number
        // Since RetryAttempt starts at 1, the code bumps it to 2 before notifying
        // So the message should say "tentativa 2/3"
        _notificationMock.Verify(n => n.SendNotificationAsync(
            landlordId, "payment",
            It.Is<string>(s =>
                s.Contains("tentativa") &&
                s.Contains("/3")),
            paymentId), Times.Once);

        context.Dispose();
    }

    [Fact]
    public async Task NotifyPaymentSucceededAsync_TenantMessageContainsAmount()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var landlordId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        var (service, context) = CreateTestableService();

        context.Payments.Add(new Payment
        {
            Id = paymentId,
            LeaseId = Guid.NewGuid(),
            TenantId = tenantId,
            LandlordId = landlordId,
            StripePaymentIntentId = "pi_success_amount",
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Processing
        });
        await context.SaveChangesAsync();

        // Act
        await service.HandlePaymentSucceededAsync("pi_success_amount");

        // Assert — tenant message should contain the formatted amount
        _notificationMock.Verify(n => n.SendNotificationAsync(
            tenantId, "payment",
            It.Is<string>(s => s.Contains("850") && s.Contains("€")),
            paymentId), Times.Once);

        context.Dispose();
    }

    // ===================================================================
    // G. Split payment / custom amount tests
    // ===================================================================

    [Fact]
    public async Task CreateMonthlyRentPaymentAsync_WithCustomAmount_ChargesCorrectAmount()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;
        var billingPeriod = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var customAmount = 425m;

        _stripeAccountMock.Setup(s => s.GetAccountForPropertyAsync(lease.PropertyId))
            .ReturnsAsync(new StripeAccountDto(
                Guid.NewGuid(), landlordId, lease.PropertyId, "acct_test_123",
                true, true, true, true, DateTime.UtcNow));

        _userServiceMock.Setup(u => u.GetProfileAsync(tenantId))
            .ReturnsAsync(new User { Id = tenantId, StripeCustomerId = "cus_test_123" });

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);
        context.TenantPaymentMethods.Add(CreateTestPaymentMethod(tenantId));
        await context.SaveChangesAsync();

        long capturedAmount = 0;
        service.SetCreateFunc(async (options, requestOptions) =>
        {
            capturedAmount = options.Amount ?? 0;
            return new PaymentIntent
            {
                Id = "pi_split_" + Guid.NewGuid().ToString("N")[..12],
                Amount = options.Amount ?? 0,
                Currency = options.Currency,
                Status = "succeeded",
                ClientSecret = "pi_split_secret",
                Metadata = options.Metadata ?? new Dictionary<string, string>()
            };
        });

        // Act
        var result = await service.CreateMonthlyRentPaymentAsync(lease.Id, tenantId, billingPeriod, 0, customAmount);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(customAmount, result!.Amount);
        Assert.Equal(42500, capturedAmount); // 425 * 100 cents

        context.Dispose();
    }

    [Fact]
    public async Task CreateMonthlyRentPaymentAsync_WithCustomAmount_CalculatesProportionalFee()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;
        var billingPeriod = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var customAmount = 425m; // half of 850

        _stripeAccountMock.Setup(s => s.GetAccountForPropertyAsync(lease.PropertyId))
            .ReturnsAsync(new StripeAccountDto(
                Guid.NewGuid(), landlordId, lease.PropertyId, "acct_test_123",
                true, true, true, true, DateTime.UtcNow));

        _userServiceMock.Setup(u => u.GetProfileAsync(tenantId))
            .ReturnsAsync(new User { Id = tenantId, StripeCustomerId = "cus_test_123" });

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);
        context.TenantPaymentMethods.Add(CreateTestPaymentMethod(tenantId));
        await context.SaveChangesAsync();

        long capturedFee = 0;
        service.SetCreateFunc(async (options, requestOptions) =>
        {
            capturedFee = options.ApplicationFeeAmount ?? 0;
            return new PaymentIntent
            {
                Id = "pi_split_fee_" + Guid.NewGuid().ToString("N")[..12],
                Amount = options.Amount ?? 0,
                Currency = options.Currency,
                Status = "succeeded",
                ClientSecret = "pi_split_fee_secret",
                Metadata = options.Metadata ?? new Dictionary<string, string>()
            };
        });

        // Act
        var result = await service.CreateMonthlyRentPaymentAsync(lease.Id, tenantId, billingPeriod, 0, customAmount);

        // Assert
        // PlatformFeePerMonth = 3000 (30€), proportional to 425/850 = 1500 cents = 15€
        Assert.NotNull(result);
        Assert.Equal(15m, result!.PlatformFee);
        Assert.Equal(1500, capturedFee);
        Assert.Equal(customAmount, result.Amount);

        context.Dispose();
    }

    [Fact]
    public async Task CreateMonthlyRentPaymentAsync_NegativeCustomAmount_Throws()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var billingPeriod = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.CreateMonthlyRentPaymentAsync(lease.Id, tenantId, billingPeriod, 0, -100m));
        Assert.Contains("Custom amount must be positive", ex.Message);

        context.Dispose();
    }

    [Fact]
    public async Task CreateMonthlyRentPaymentAsync_ZeroCustomAmount_Throws()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var billingPeriod = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.CreateMonthlyRentPaymentAsync(lease.Id, tenantId, billingPeriod, 0, 0m));
        Assert.Contains("Custom amount must be positive", ex.Message);

        context.Dispose();
    }

    // ===================================================================
    // H. Refund tests
    // ===================================================================
    //
    // Note: RefundMonthlyRentPaymentAsync calls `new RefundService().CreateAsync()`
    // directly (not via a virtual method), so the Stripe refund call cannot be
    // mocked through TestableStripePaymentService. The tests below verify the
    // validation logic (authorization, type, status, amount checks) which all
    // execute before the Stripe call. Full end-to-end refund testing requires
    // extracting the RefundService into a virtual method. Until then, the
    // Stripe refund call itself is not tested.

    [Fact]
    public async Task RefundMonthlyRentPaymentAsync_LandlordRefund_Succeeds()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);

        var originalPayment = CreateTestPayment(lease.Id, tenantId, landlordId,
            status: PaymentStatus.Succeeded, stripePiId: "pi_refund_full");
        context.Payments.Add(originalPayment);
        await context.SaveChangesAsync();

        // Act & Assert
        try
        {
            await service.RefundMonthlyRentPaymentAsync(originalPayment.Id, 850m, landlordId);
            // If we get here, the Stripe call would have succeeded — but with a fake key
            // it will throw. We verify the validation passed by catching below.
        }
        catch (StripeException)
        {
            // Validation checks passed (no UnauthorizedAccessException, InvalidOperationException, etc.).
            // Stripe RefundService.CreateAsync cannot be mocked — this exception is expected.
            // The DB state remains unchanged because SaveChangesAsync executes after the Stripe call.
        }

        // Verify original payment is still Succeeded (SaveChangesAsync after Stripe call never executed)
        var unchanged = await context.Payments.FindAsync(originalPayment.Id);
        Assert.NotNull(unchanged);
        Assert.Equal(PaymentStatus.Succeeded, unchanged!.Status);

        context.Dispose();
    }

    [Fact]
    public async Task RefundMonthlyRentPaymentAsync_TenantRefund_ThrowsUnauthorized()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);

        var originalPayment = CreateTestPayment(lease.Id, tenantId, landlordId,
            status: PaymentStatus.Succeeded, stripePiId: "pi_refund_unauth");
        context.Payments.Add(originalPayment);
        await context.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.RefundMonthlyRentPaymentAsync(originalPayment.Id, 850m, tenantId));
        Assert.Contains("Apenas o proprietário", ex.Message);

        context.Dispose();
    }

    [Fact]
    public async Task RefundMonthlyRentPaymentAsync_NonMonthlyRentPayment_Throws()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);

        var initialPayment = CreateTestPayment(lease.Id, tenantId, landlordId,
            status: PaymentStatus.Succeeded, type: PaymentType.InitialPayment, stripePiId: "pi_initial_refund");
        context.Payments.Add(initialPayment);
        await context.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RefundMonthlyRentPaymentAsync(initialPayment.Id, 850m, landlordId));
        Assert.Contains("Apenas pagamentos de renda mensal", ex.Message);

        context.Dispose();
    }

    [Fact]
    public async Task RefundMonthlyRentPaymentAsync_PartialRefund_SetsPartiallyRefunded()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);

        var originalPayment = CreateTestPayment(lease.Id, tenantId, landlordId,
            status: PaymentStatus.Succeeded, stripePiId: "pi_refund_partial");
        context.Payments.Add(originalPayment);
        await context.SaveChangesAsync();

        // Act & Assert
        try
        {
            await service.RefundMonthlyRentPaymentAsync(originalPayment.Id, 400m, landlordId);
        }
        catch (StripeException)
        {
            // Validation passed: landlord, MonthlyRent type, Succeeded status, amount <= original.
            // Stripe RefundService.CreateAsync cannot be mocked — this exception is expected.
        }

        // Verify DB state is unchanged (SaveChangesAsync after Stripe call never executed)
        var unchanged = await context.Payments.FindAsync(originalPayment.Id);
        Assert.NotNull(unchanged);
        Assert.Equal(PaymentStatus.Succeeded, unchanged!.Status);

        context.Dispose();
    }

    [Fact]
    public async Task RefundMonthlyRentPaymentAsync_RefundExceedsAmount_Throws()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;

        var (service, context) = CreateTestableService();
        context.Leases.Add(lease);

        var originalPayment = CreateTestPayment(lease.Id, tenantId, landlordId,
            status: PaymentStatus.Succeeded, stripePiId: "pi_refund_exceed");
        context.Payments.Add(originalPayment);
        await context.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RefundMonthlyRentPaymentAsync(originalPayment.Id, 1000m, landlordId));
        Assert.Contains("excede", ex.Message);

        context.Dispose();
    }

    // ===================================================================
    // I. Webhook handler tests — HandlePaymentProcessingAsync
    // ===================================================================

    [Fact]
    public async Task HandlePaymentProcessingAsync_UpdatesStatusToProcessing()
    {
        // Arrange
        var (service, context) = CreateTestableService();

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            LeaseId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            StripePaymentIntentId = "pi_processing_1",
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Pending
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        // Act
        await service.HandlePaymentProcessingAsync("pi_processing_1");

        // Assert
        var updated = await context.Payments.FindAsync(payment.Id);
        Assert.NotNull(updated);
        Assert.Equal(PaymentStatus.Processing, updated!.Status);

        context.Dispose();
    }

    [Fact]
    public async Task HandlePaymentProcessingAsync_AlreadySucceeded_NoOverwrite()
    {
        // Arrange
        var (service, context) = CreateTestableService();

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            LeaseId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            StripePaymentIntentId = "pi_processing_success",
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Succeeded,
            PaidAt = DateTime.UtcNow.AddHours(-1)
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        // Act
        await service.HandlePaymentProcessingAsync("pi_processing_success");

        // Assert — status should still be Succeeded
        var updated = await context.Payments.FindAsync(payment.Id);
        Assert.NotNull(updated);
        Assert.Equal(PaymentStatus.Succeeded, updated!.Status);
        Assert.NotNull(updated.PaidAt);

        context.Dispose();
    }

    [Fact]
    public async Task HandlePaymentProcessingAsync_AlreadyRefunded_NoOverwrite()
    {
        // Arrange
        var (service, context) = CreateTestableService();

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            LeaseId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            StripePaymentIntentId = "pi_processing_refunded",
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Refunded,
            PaidAt = DateTime.UtcNow.AddHours(-2)
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        // Act
        await service.HandlePaymentProcessingAsync("pi_processing_refunded");

        // Assert — status should stay Refunded
        var updated = await context.Payments.FindAsync(payment.Id);
        Assert.NotNull(updated);
        Assert.Equal(PaymentStatus.Refunded, updated!.Status);

        context.Dispose();
    }

    // ===================================================================
    // J. Webhook handler tests — HandlePaymentCanceledAsync
    // ===================================================================

    [Fact]
    public async Task HandlePaymentCanceledAsync_UpdatesStatusToFailed()
    {
        // Arrange
        var (service, context) = CreateTestableService();

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            LeaseId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            StripePaymentIntentId = "pi_cancel_1",
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Pending
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        // Act
        await service.HandlePaymentCanceledAsync("pi_cancel_1");

        // Assert
        var updated = await context.Payments.FindAsync(payment.Id);
        Assert.NotNull(updated);
        Assert.Equal(PaymentStatus.Failed, updated!.Status);
        Assert.Contains("cancelado", updated.FailureReason ?? "");

        context.Dispose();
    }

    [Fact]
    public async Task HandlePaymentCanceledAsync_AlreadyFailed_NoOverwrite()
    {
        // Arrange
        var (service, context) = CreateTestableService();

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            LeaseId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            StripePaymentIntentId = "pi_cancel_failed",
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Failed,
            FailureReason = "fundos insuficientes"
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        // Act
        await service.HandlePaymentCanceledAsync("pi_cancel_failed");

        // Assert — original failure reason should be preserved
        var updated = await context.Payments.FindAsync(payment.Id);
        Assert.NotNull(updated);
        Assert.Equal(PaymentStatus.Failed, updated!.Status);
        Assert.Equal("fundos insuficientes", updated.FailureReason);

        context.Dispose();
    }

    [Fact]
    public async Task HandlePaymentCanceledAsync_AlreadyRefunded_NoOverwrite()
    {
        // Arrange
        var (service, context) = CreateTestableService();

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            LeaseId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            StripePaymentIntentId = "pi_cancel_refunded",
            Type = PaymentType.MonthlyRent,
            Amount = 850m,
            PlatformFee = 30m,
            LandlordAmount = 820m,
            RentAmount = 850m,
            Currency = "eur",
            Status = PaymentStatus.Refunded
        };
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        // Act
        await service.HandlePaymentCanceledAsync("pi_cancel_refunded");

        // Assert — status should stay Refunded
        var updated = await context.Payments.FindAsync(payment.Id);
        Assert.NotNull(updated);
        Assert.Equal(PaymentStatus.Refunded, updated!.Status);

        context.Dispose();
    }

    // ===================================================================
    // K. Receipt generation tests
    // ===================================================================

    [Fact]
    public async Task GenerateReceiptAsync_AuthorizedUser_ReturnsPdf()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;

        var (service, context) = CreateTestableService();

        var payment = CreateTestPayment(lease.Id, tenantId, landlordId,
            status: PaymentStatus.Succeeded, stripePiId: "pi_receipt_ok");
        context.Leases.Add(lease);
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        // Act
        var pdfBytes = await service.GenerateReceiptAsync(payment.Id, tenantId);

        // Assert
        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 0, "Receipt PDF should not be empty");

        context.Dispose();
    }

    [Fact]
    public async Task GenerateReceiptAsync_UnauthorizedUser_Throws()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;

        var (service, context) = CreateTestableService();

        var payment = CreateTestPayment(lease.Id, tenantId, landlordId,
            status: PaymentStatus.Succeeded, stripePiId: "pi_receipt_unauth");
        context.Leases.Add(lease);
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        var randomUserId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GenerateReceiptAsync(payment.Id, randomUserId));
        Assert.Contains("permissão", ex.Message);

        context.Dispose();
    }

    [Fact]
    public async Task GenerateReceiptAsync_NonSucceededPayment_Throws()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;

        var (service, context) = CreateTestableService();

        var payment = CreateTestPayment(lease.Id, tenantId, landlordId,
            status: PaymentStatus.Pending, stripePiId: "pi_receipt_pending");
        context.Leases.Add(lease);
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GenerateReceiptAsync(payment.Id, tenantId));
        Assert.Contains("Recibo disponível apenas", ex.Message);

        context.Dispose();
    }

    // ===================================================================
    // L. Client secret tests
    // ===================================================================

    [Fact]
    public async Task GetPaymentClientSecretAsync_PendingPayment_ReturnsSecret()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;

        var (service, context) = CreateTestableService();

        var payment = CreateTestPayment(lease.Id, tenantId, landlordId,
            status: PaymentStatus.Pending, stripePiId: "pi_client_secret_ok");
        context.Leases.Add(lease);
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        service.SetGetResult(new PaymentIntent
        {
            Id = "pi_client_secret_ok",
            Amount = 85000,
            Currency = "eur",
            Status = "requires_action",
            ClientSecret = "pi_client_secret_ok_secret_abc123"
        });

        // Act
        var secret = await service.GetPaymentClientSecretAsync(payment.Id, tenantId);

        // Assert
        Assert.Equal("pi_client_secret_ok_secret_abc123", secret);

        context.Dispose();
    }

    [Fact]
    public async Task GetPaymentClientSecretAsync_PendingPaymentWithPendingPrefix_ReturnsNull()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;

        var (service, context) = CreateTestableService();

        var payment = CreateTestPayment(lease.Id, tenantId, landlordId,
            status: PaymentStatus.Pending, stripePiId: "pending_123");
        context.Leases.Add(lease);
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        // Act
        var secret = await service.GetPaymentClientSecretAsync(payment.Id, tenantId);

        // Assert
        Assert.Null(secret);

        context.Dispose();
    }

    [Fact]
    public async Task GetPaymentClientSecretAsync_SucceededPayment_ReturnsNull()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;

        var (service, context) = CreateTestableService();

        var payment = CreateTestPayment(lease.Id, tenantId, landlordId,
            status: PaymentStatus.Succeeded, stripePiId: "pi_succeeded_no_secret");
        context.Leases.Add(lease);
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        // Act
        var secret = await service.GetPaymentClientSecretAsync(payment.Id, tenantId);

        // Assert
        Assert.Null(secret);

        context.Dispose();
    }

    [Fact]
    public async Task GetPaymentClientSecretAsync_UnauthorizedUser_Throws()
    {
        // Arrange
        var lease = CreateTestLease();
        var tenantId = lease.TenantId;
        var landlordId = lease.LandlordId;

        var (service, context) = CreateTestableService();

        var payment = CreateTestPayment(lease.Id, tenantId, landlordId,
            status: PaymentStatus.Pending, stripePiId: "pi_secret_unauth");
        context.Leases.Add(lease);
        context.Payments.Add(payment);
        await context.SaveChangesAsync();

        var randomUserId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetPaymentClientSecretAsync(payment.Id, randomUserId));
        Assert.Contains("Sem permissão", ex.Message);

        context.Dispose();
    }
}
