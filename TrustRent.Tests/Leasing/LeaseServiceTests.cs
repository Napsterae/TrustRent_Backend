using Hangfire;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Moq;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Contracts.DTOs;
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
    private readonly Mock<ICommunicationContentService> _communicationContentMock;
    private readonly Mock<IEmailService> _emailMock;
    private readonly Mock<IBackgroundJobClient> _bgJobsMock;

    public LeaseServiceTests()
    {
        _catalogAccessMock = new Mock<ICatalogAccessService>();
        _notificationMock = new Mock<INotificationService>();
        _contractGenMock = new Mock<IContractGenerationService>();
        _digitalSigMock = new Mock<IDigitalSignatureService>();
        _pdfVerifyMock = new Mock<ISignedPdfVerificationService>();
        _userServiceMock = new Mock<IUserService>();
        _communicationContentMock = new Mock<ICommunicationContentService>();
        _emailMock = new Mock<IEmailService>();
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
            _communicationContentMock.Object,
            _emailMock.Object,
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
    public async Task GetSignatureStatusAsync_LeaseWithSignatureRows_ReturnsStatusAndSynthesizesTopLevelSummary()
    {
        var (service, context) = CreateService();
        var tenantId = Guid.NewGuid();
        var landlordId = Guid.NewGuid();
        var landlordSignedAt = DateTime.UtcNow.AddHours(-1);
        var lease = CreateTestLease(tenantId: tenantId, landlordId: landlordId, status: LeaseStatus.AwaitingSignatures);
        lease.RequiredSignaturesCount = 0;
        lease.Signatures.Add(new LeaseSignature
        {
            Id = Guid.NewGuid(),
            LeaseId = lease.Id,
            UserId = landlordId,
            Role = LeaseSignatoryRole.Landlord,
            SequenceOrder = 1,
            Signed = true,
            SignedAt = landlordSignedAt,
            SignatureVerified = true,
            SignatureCertSubject = "CMD - Senhorio"
        });
        lease.Signatures.Add(new LeaseSignature
        {
            Id = Guid.NewGuid(),
            LeaseId = lease.Id,
            UserId = tenantId,
            Role = LeaseSignatoryRole.Tenant,
            SequenceOrder = 2,
            Signed = false,
        });
        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        var result = await service.GetSignatureStatusAsync(lease.Id, tenantId);

        Assert.NotNull(result);
        Assert.True(result!.LandlordSigned);
        Assert.True(result.LandlordSignatureVerified);
        Assert.Equal(landlordSignedAt, result.LandlordSignedAt);
        Assert.Equal("CMD - Senhorio", result.LandlordSignatureCertSubject);
        Assert.False(result.TenantSigned);
        Assert.Equal("AwaitingSignatures", result.LeaseStatus);
        Assert.Equal(2, result.RequiredSignaturesCount);
        Assert.Equal(1, result.SignedCount);
        Assert.Equal(2, result.Signatories.Count);
        Assert.Equal(new[] { "Landlord", "Tenant" }, result.Signatories.Select(s => s.Role).ToArray());
        Assert.True(result.Signatories[0].Signed);
        Assert.False(result.Signatories[1].Signed);

        context.Dispose();
    }

    [Fact]
    public async Task GetSignatureStatusAsync_LegacyLeaseWithoutSignatureRows_UsesAcceptedTermsForExtraPartiesAndSynthesizesRequiredCount()
    {
        var (service, context) = CreateService();
        var tenantId = Guid.NewGuid();
        var landlordId = Guid.NewGuid();
        var coTenantId = Guid.NewGuid();

        var lease = CreateTestLease(tenantId: tenantId, landlordId: landlordId, status: LeaseStatus.AwaitingSignatures);
        lease.CoTenantId = coTenantId;
        lease.RequiredSignaturesCount = 0;
        lease.TermAcceptances.Add(new LeaseTermAcceptance
        {
            Id = Guid.NewGuid(),
            LeaseId = lease.Id,
            UserId = coTenantId,
            Role = LeaseSignatoryRole.CoTenant,
            AcceptedAt = DateTime.UtcNow.AddMinutes(-10),
        });

        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        var result = await service.GetSignatureStatusAsync(lease.Id, tenantId);

        Assert.NotNull(result);
        Assert.Equal(3, result!.RequiredSignaturesCount);
        Assert.Equal(1, result.SignedCount);
        Assert.Equal(new[] { "Landlord", "Tenant", "CoTenant" }, result.Signatories.Select(s => s.Role).ToArray());
        Assert.True(result.Signatories[2].Signed);
        Assert.NotNull(result.Signatories[2].AcceptedTermsAt);

        context.Dispose();
    }

    [Fact]
    public async Task GetSignatureStatusAsync_LeaseWithSignatureRows_DoesNotPromoteAcceptedTermsToSigned()
    {
        var (service, context) = CreateService();
        var tenantId = Guid.NewGuid();
        var landlordId = Guid.NewGuid();

        var lease = CreateTestLease(tenantId: tenantId, landlordId: landlordId, status: LeaseStatus.AwaitingSignatures);
        lease.RequiredSignaturesCount = 2;
        lease.Signatures.Add(new LeaseSignature
        {
            Id = Guid.NewGuid(),
            LeaseId = lease.Id,
            UserId = landlordId,
            Role = LeaseSignatoryRole.Landlord,
            SequenceOrder = 1,
            Signed = true,
            SignedAt = DateTime.UtcNow.AddHours(-1),
            SignatureVerified = true,
        });
        lease.Signatures.Add(new LeaseSignature
        {
            Id = Guid.NewGuid(),
            LeaseId = lease.Id,
            UserId = tenantId,
            Role = LeaseSignatoryRole.Tenant,
            SequenceOrder = 2,
            Signed = false,
            SignatureVerified = false,
        });
        lease.TermAcceptances.Add(new LeaseTermAcceptance
        {
            Id = Guid.NewGuid(),
            LeaseId = lease.Id,
            UserId = tenantId,
            Role = LeaseSignatoryRole.Tenant,
            AcceptedAt = DateTime.UtcNow.AddMinutes(-5),
        });
        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        var result = await service.GetSignatureStatusAsync(lease.Id, tenantId);

        Assert.NotNull(result);
        Assert.False(result!.TenantSigned);
        Assert.Equal(1, result.SignedCount);

        var tenantSignatory = result.Signatories.Single(s => s.Role == "Tenant");
        Assert.False(tenantSignatory.Signed);
        Assert.Null(tenantSignatory.SignedAt);
        Assert.False(tenantSignatory.SignatureVerified);
        Assert.NotNull(tenantSignatory.AcceptedTermsAt);

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

    [Fact]
    public async Task ConfirmLeaseStartDateAsync_InformalLease_NotifiesCurrentSignerWhenAcceptanceFlowStarts()
    {
        var (service, context) = CreateService();
        var landlordId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var lease = CreateTestLease(tenantId: tenantId, landlordId: landlordId, status: LeaseStatus.Pending);
        lease.ContractType = "Informal";
        lease.Signatures.Add(new LeaseSignature
        {
            Id = Guid.NewGuid(),
            LeaseId = lease.Id,
            UserId = landlordId,
            Role = LeaseSignatoryRole.Landlord,
            SequenceOrder = 1,
        });
        lease.Signatures.Add(new LeaseSignature
        {
            Id = Guid.NewGuid(),
            LeaseId = lease.Id,
            UserId = tenantId,
            Role = LeaseSignatoryRole.Tenant,
            SequenceOrder = 2,
        });
        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        _catalogAccessMock.Setup(c => c.GetApplicationContextAsync(lease.ApplicationId)).ReturnsAsync(new ApplicationContext
        {
            Id = lease.ApplicationId,
            PropertyId = lease.PropertyId,
            TenantId = tenantId,
            LandlordId = landlordId,
            DurationMonths = lease.DurationMonths,
        });
        _catalogAccessMock.Setup(c => c.UpdateApplicationStatusAsync(
            lease.ApplicationId,
            (int)ApplicationStatus.ContractPendingSignature,
            landlordId,
            It.IsAny<string>(),
            It.IsAny<string>())).Returns(Task.CompletedTask);

        var result = await service.ConfirmLeaseStartDateAsync(lease.Id, landlordId, new ConfirmLeaseStartDateDto
        {
            StartDate = DateTime.UtcNow.AddDays(15)
        });

        Assert.Equal(LeaseStatus.AwaitingSignatures.ToString(), result.Status);
        _notificationMock.Verify(n => n.SendNotificationAsync(
            landlordId,
            "lease",
            It.Is<string>(message => message.Contains("É a tua vez de aceitar")),
            lease.Id), Times.Once);
        _notificationMock.Verify(n => n.SendNotificationAsync(
            landlordId,
            "lease",
            It.IsAny<string>(),
            lease.Id), Times.Once);
        _notificationMock.Verify(n => n.SendNotificationAsync(
            tenantId,
            "lease",
            It.IsAny<string>(),
            lease.Id), Times.Once);

        context.Dispose();
    }

    [Fact]
    public async Task ConfirmSignatureAsync_NotifiesOnlyNextPendingSigner()
    {
        var (service, context) = CreateService();
        var landlordId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var coTenantId = Guid.NewGuid();
        var guarantorId = Guid.NewGuid();

        var lease = CreateTestLease(tenantId: tenantId, landlordId: landlordId, status: LeaseStatus.AwaitingSignatures);
        lease.ContractType = "Official";
        lease.CoTenantId = coTenantId;
        lease.GuarantorUserId = guarantorId;
        lease.LandlordSigned = true;
        lease.LandlordSignedAt = DateTime.UtcNow.AddMinutes(-30);
        lease.Signatures.Add(new LeaseSignature
        {
            Id = Guid.NewGuid(),
            LeaseId = lease.Id,
            UserId = landlordId,
            Role = LeaseSignatoryRole.Landlord,
            SequenceOrder = 1,
            Signed = true,
            SignedAt = DateTime.UtcNow.AddMinutes(-30),
            SignatureVerified = true,
        });
        lease.Signatures.Add(new LeaseSignature
        {
            Id = Guid.NewGuid(),
            LeaseId = lease.Id,
            UserId = tenantId,
            Role = LeaseSignatoryRole.Tenant,
            SequenceOrder = 2,
        });
        lease.Signatures.Add(new LeaseSignature
        {
            Id = Guid.NewGuid(),
            LeaseId = lease.Id,
            UserId = coTenantId,
            Role = LeaseSignatoryRole.CoTenant,
            SequenceOrder = 3,
        });
        lease.Signatures.Add(new LeaseSignature
        {
            Id = Guid.NewGuid(),
            LeaseId = lease.Id,
            UserId = guarantorId,
            Role = LeaseSignatoryRole.Guarantor,
            SequenceOrder = 4,
        });
        context.Leases.Add(lease);
        await context.SaveChangesAsync();

        _digitalSigMock.Setup(d => d.VerifyCmdSignatureAsync("tx-1", "123456"))
            .ReturnsAsync(new CmdSignatureConfirmResult(true, "sig-ref"));

        var result = await service.ConfirmSignatureAsync(lease.Id, tenantId, new ConfirmLeaseSignatureDto
        {
            TransactionId = "tx-1",
            OtpCode = "123456"
        });

        Assert.Equal(LeaseStatus.AwaitingSignatures.ToString(), result.Status);
        _notificationMock.Verify(n => n.SendNotificationAsync(
            coTenantId,
            "lease",
            It.Is<string>(message => message.Contains("É a tua vez de assinar")),
            lease.Id), Times.Once);
        _notificationMock.Verify(n => n.SendNotificationAsync(
            guarantorId,
            "lease",
            It.IsAny<string>(),
            lease.Id), Times.Never);

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

    [Fact]
    public async Task ActivateLeaseAsync_NotifiesCoTenantAndGuarantorWhenAwaitingPaymentStarts()
    {
        var (service, context) = CreateService();
        var landlordId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var coTenantId = Guid.NewGuid();
        var guarantorId = Guid.NewGuid();

        var lease = CreateTestLease(tenantId: tenantId, landlordId: landlordId, status: LeaseStatus.PendingGuarantorSignature);
        lease.CoTenantId = coTenantId;
        lease.GuarantorUserId = guarantorId;
        context.Leases.Add(lease);

        _catalogAccessMock.Setup(c => c.UpdateApplicationStatusAsync(
            lease.ApplicationId,
            (int)ApplicationStatus.AwaitingPayment,
            Guid.Empty,
            It.IsAny<string>(),
            It.IsAny<string>())).Returns(Task.CompletedTask);

        var method = typeof(LeaseService).GetMethod("ActivateLeaseAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = method!.Invoke(service, new object[] { lease }) as Task;
        Assert.NotNull(task);
        await task!;

        Assert.Equal(LeaseStatus.AwaitingPayment, lease.Status);
        _notificationMock.Verify(n => n.SendNotificationAsync(tenantId, "payment", It.IsAny<string>(), lease.Id), Times.Once);
        _notificationMock.Verify(n => n.SendNotificationAsync(landlordId, "payment", It.IsAny<string>(), lease.Id), Times.Once);
        _notificationMock.Verify(n => n.SendNotificationAsync(coTenantId, "payment", It.Is<string>(s => s.Contains("pagamento inicial")), lease.Id), Times.Once);
        _notificationMock.Verify(n => n.SendNotificationAsync(guarantorId, "payment", It.Is<string>(s => s.Contains("pagamento inicial")), lease.Id), Times.Once);

        context.Dispose();
    }
}
