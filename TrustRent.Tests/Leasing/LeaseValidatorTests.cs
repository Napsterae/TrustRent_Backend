using TrustRent.Modules.Leasing.Models;
using TrustRent.Modules.Leasing.Services;
using TrustRent.Shared.Models;

namespace TrustRent.Tests.Leasing;

public class LeaseValidatorTests
{
    // --- ValidateInitiate ---

    [Fact]
    public void ValidateInitiate_AcceptedStatus_FutureDate_DoesNotThrow()
    {
        LeaseValidator.ValidateInitiate(
            (int)ApplicationStatus.Accepted,
            DateTime.UtcNow.AddDays(7));
    }

    [Fact]
    public void ValidateInitiate_NotAcceptedStatus_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            LeaseValidator.ValidateInitiate(
                (int)ApplicationStatus.Pending,
                DateTime.UtcNow.AddDays(7)));
    }

    [Fact]
    public void ValidateInitiate_PastDate_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            LeaseValidator.ValidateInitiate(
                (int)ApplicationStatus.Accepted,
                DateTime.UtcNow.AddDays(-1)));
    }

    [Fact]
    public void ValidateInitiate_TodayDate_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            LeaseValidator.ValidateInitiate(
                (int)ApplicationStatus.Accepted,
                DateTime.UtcNow.Date));
    }

    // --- ValidateConfirmStartDate ---

    private Lease CreateTestLease(LeaseStatus status = LeaseStatus.Pending)
    {
        var landlordId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        return new Lease
        {
            Id = Guid.NewGuid(),
            PropertyId = Guid.NewGuid(),
            TenantId = tenantId,
            LandlordId = landlordId,
            ApplicationId = Guid.NewGuid(),
            Status = status,
            MonthlyRent = 500m,
            DurationMonths = 12,
            StartDate = DateTime.UtcNow.AddDays(30),
            EndDate = DateTime.UtcNow.AddDays(30).AddMonths(12),
            ContractType = "Official"
        };
    }

    [Fact]
    public void ValidateConfirmStartDate_PendingLease_Tenant_FutureDate_DoesNotThrow()
    {
        var lease = CreateTestLease(LeaseStatus.Pending);
        LeaseValidator.ValidateConfirmStartDate(lease, lease.TenantId, DateTime.UtcNow.AddDays(30));
    }

    [Fact]
    public void ValidateConfirmStartDate_NotPending_ThrowsInvalidOperationException()
    {
        var lease = CreateTestLease(LeaseStatus.Active);
        Assert.Throws<InvalidOperationException>(() =>
            LeaseValidator.ValidateConfirmStartDate(lease, lease.TenantId, DateTime.UtcNow.AddDays(30)));
    }

    [Fact]
    public void ValidateConfirmStartDate_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        var lease = CreateTestLease(LeaseStatus.Pending);
        Assert.Throws<UnauthorizedAccessException>(() =>
            LeaseValidator.ValidateConfirmStartDate(lease, Guid.NewGuid(), DateTime.UtcNow.AddDays(30)));
    }

    [Fact]
    public void ValidateConfirmStartDate_PastDate_ThrowsArgumentException()
    {
        var lease = CreateTestLease(LeaseStatus.Pending);
        Assert.Throws<ArgumentException>(() =>
            LeaseValidator.ValidateConfirmStartDate(lease, lease.TenantId, DateTime.UtcNow.AddDays(-1)));
    }

    // --- ValidateCounterProposeStartDate ---

    [Fact]
    public void ValidateCounterProposeStartDate_PendingLease_Landlord_FutureDate_DoesNotThrow()
    {
        var lease = CreateTestLease(LeaseStatus.Pending);
        LeaseValidator.ValidateCounterProposeStartDate(lease, lease.LandlordId, DateTime.UtcNow.AddDays(45));
    }

    [Fact]
    public void ValidateCounterProposeStartDate_NotPending_ThrowsInvalidOperationException()
    {
        var lease = CreateTestLease(LeaseStatus.AwaitingSignatures);
        Assert.Throws<InvalidOperationException>(() =>
            LeaseValidator.ValidateCounterProposeStartDate(lease, lease.LandlordId, DateTime.UtcNow.AddDays(45)));
    }

    // --- ValidateRequestSignature ---

    [Fact]
    public void ValidateRequestSignature_ValidOfficialLease_DoesNotThrow()
    {
        var lease = CreateTestLease(LeaseStatus.AwaitingSignatures);
        lease.ContractType = "Official";
        LeaseValidator.ValidateRequestSignature(lease, lease.LandlordId, "+351912345678");
    }

    [Fact]
    public void ValidateRequestSignature_NotAwaitingSignatures_ThrowsInvalidOperationException()
    {
        var lease = CreateTestLease(LeaseStatus.Pending);
        lease.ContractType = "Official";
        Assert.Throws<InvalidOperationException>(() =>
            LeaseValidator.ValidateRequestSignature(lease, lease.LandlordId, "+351912345678"));
    }

    [Fact]
    public void ValidateRequestSignature_InformalContract_ThrowsInvalidOperationException()
    {
        var lease = CreateTestLease(LeaseStatus.AwaitingSignatures);
        lease.ContractType = "Informal";
        Assert.Throws<InvalidOperationException>(() =>
            LeaseValidator.ValidateRequestSignature(lease, lease.LandlordId, "+351912345678"));
    }

    [Fact]
    public void ValidateRequestSignature_InvalidPhoneNumber_ThrowsArgumentException()
    {
        var lease = CreateTestLease(LeaseStatus.AwaitingSignatures);
        lease.ContractType = "Official";
        Assert.Throws<ArgumentException>(() =>
            LeaseValidator.ValidateRequestSignature(lease, lease.LandlordId, "invalid"));
    }

    [Fact]
    public void ValidateRequestSignature_LandlordAlreadySigned_ThrowsInvalidOperationException()
    {
        var lease = CreateTestLease(LeaseStatus.AwaitingSignatures);
        lease.ContractType = "Official";
        lease.LandlordSigned = true;
        Assert.Throws<InvalidOperationException>(() =>
            LeaseValidator.ValidateRequestSignature(lease, lease.LandlordId, "+351912345678"));
    }

    [Fact]
    public void ValidateRequestSignature_TenantAlreadySigned_ThrowsInvalidOperationException()
    {
        var lease = CreateTestLease(LeaseStatus.AwaitingSignatures);
        lease.ContractType = "Official";
        lease.TenantSigned = true;
        Assert.Throws<InvalidOperationException>(() =>
            LeaseValidator.ValidateRequestSignature(lease, lease.TenantId, "+351912345678"));
    }

    [Fact]
    public void ValidateRequestSignature_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        var lease = CreateTestLease(LeaseStatus.AwaitingSignatures);
        lease.ContractType = "Official";
        Assert.Throws<UnauthorizedAccessException>(() =>
            LeaseValidator.ValidateRequestSignature(lease, Guid.NewGuid(), "+351912345678"));
    }

    // --- ValidateConfirmSignature ---

    [Fact]
    public void ValidateConfirmSignature_AwaitingSignatures_Participant_DoesNotThrow()
    {
        var lease = CreateTestLease(LeaseStatus.AwaitingSignatures);
        LeaseValidator.ValidateConfirmSignature(lease, lease.TenantId);
    }

    [Fact]
    public void ValidateConfirmSignature_NotAwaitingSignatures_ThrowsInvalidOperationException()
    {
        var lease = CreateTestLease(LeaseStatus.Pending);
        Assert.Throws<InvalidOperationException>(() =>
            LeaseValidator.ValidateConfirmSignature(lease, lease.TenantId));
    }

    // --- ValidateAcceptTerms ---

    [Fact]
    public void ValidateAcceptTerms_InformalLease_AwaitingSignatures_DoesNotThrow()
    {
        var lease = CreateTestLease(LeaseStatus.AwaitingSignatures);
        lease.ContractType = "Informal";
        LeaseValidator.ValidateAcceptTerms(lease, lease.TenantId);
    }

    [Fact]
    public void ValidateAcceptTerms_OfficialLease_ThrowsInvalidOperationException()
    {
        var lease = CreateTestLease(LeaseStatus.AwaitingSignatures);
        lease.ContractType = "Official";
        Assert.Throws<InvalidOperationException>(() =>
            LeaseValidator.ValidateAcceptTerms(lease, lease.TenantId));
    }

    [Fact]
    public void ValidateAcceptTerms_LandlordAlreadyAccepted_ThrowsInvalidOperationException()
    {
        var lease = CreateTestLease(LeaseStatus.AwaitingSignatures);
        lease.ContractType = "Informal";
        lease.LandlordSigned = true;
        Assert.Throws<InvalidOperationException>(() =>
            LeaseValidator.ValidateAcceptTerms(lease, lease.LandlordId));
    }

    // --- ValidateCancel ---

    [Fact]
    public void ValidateCancel_PendingLease_Participant_DoesNotThrow()
    {
        var lease = CreateTestLease(LeaseStatus.Pending);
        LeaseValidator.ValidateCancel(lease, lease.TenantId);
    }

    [Fact]
    public void ValidateCancel_ActiveLease_ThrowsInvalidOperationException()
    {
        var lease = CreateTestLease(LeaseStatus.Active);
        Assert.Throws<InvalidOperationException>(() =>
            LeaseValidator.ValidateCancel(lease, lease.TenantId));
    }

    [Fact]
    public void ValidateCancel_AlreadyCancelled_ThrowsInvalidOperationException()
    {
        var lease = CreateTestLease(LeaseStatus.Cancelled);
        Assert.Throws<InvalidOperationException>(() =>
            LeaseValidator.ValidateCancel(lease, lease.TenantId));
    }

    [Fact]
    public void ValidateCancel_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        var lease = CreateTestLease(LeaseStatus.Pending);
        Assert.Throws<UnauthorizedAccessException>(() =>
            LeaseValidator.ValidateCancel(lease, Guid.NewGuid()));
    }
}
