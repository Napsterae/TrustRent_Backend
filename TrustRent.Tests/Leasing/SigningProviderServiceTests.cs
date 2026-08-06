using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TrustRent.Api.Services;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Modules.Identity.Contracts.Database;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Identity.Models;
using TrustRent.Modules.Identity.Repositories;
using TrustRent.Modules.Identity.Services;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Modules.Leasing.Services;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;

namespace TrustRent.Tests.Leasing;

/// <summary>
/// Exercises SigningProviderService.InitiateEmbeddedSigningAsync against the REAL
/// UserService/UserRepository/IdentityDbContext stack (no mocked IUserService) to prove
/// the signer-profile resolution never runs concurrent DbContext operations.
///
/// Regression: the old code ran Task.WhenAll over GetProfileDtoAsync, and each call
/// awaited the SAME scoped IdentityDbContext -> EF Core throws "A second operation was
/// started on this context instance before a previous operation completed". The InMemory
/// provider does not enforce EF Core's concurrency guard, so a plain real-stack test would
/// pass even with the bug; the NonConcurrentUserService observer below makes the overlap
/// deterministic (Task.Yield window + in-flight counter) so the test is red on the buggy
/// code and green on the fix.
/// </summary>
public class SigningProviderServiceTests
{
    [Fact]
    public async Task InitiateEmbeddedSigningAsync_MultipleSigners_RealUserService_DoesNotStartConcurrentContextOperations()
    {
        var landlordId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
        var pdfPath = Path.Combine(Path.GetTempPath(), $"signing-provider-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(pdfPath, pdfBytes);

        try
        {
            // Real Identity stack: the InMemory IdentityDbContext is shared by the
            // UserRepository + UnitOfWork + UserService, exactly like a request scope.
            var identityOptions = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            await using var identityDb = new IdentityDbContext(identityOptions);

            identityDb.Users.AddRange(
                new User { Id = landlordId, Name = "Senhorio", Email = "landlord@trustrent.local" },
                new User { Id = tenantId, Name = "Inquilino", Email = "tenant@trustrent.local" });
            await identityDb.SaveChangesAsync();

            var realUserService = new UserService(
                new UnitOfWork(identityDb),
                Mock.Of<IImageService>(),
                Mock.Of<IGeminiDocumentService>(),
                Mock.Of<IUserContactAccessService>());

            // Observer: forwards every call to the REAL UserService but proves that at no
            // point are two GetProfileDtoAsync calls in flight at the same time.
            var observedUserService = new NonConcurrentUserService(realUserService);

            // Real Leasing stack: a lease with 2 signers and a generated contract file.
            var leasingOptions = new DbContextOptionsBuilder<LeasingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            await using var leasingDb = new LeasingDbContext(leasingOptions);

            var lease = new Lease
            {
                Id = leaseId,
                PropertyId = Guid.NewGuid(),
                TenantId = tenantId,
                LandlordId = landlordId,
                ApplicationId = Guid.NewGuid(),
                Status = LeaseStatus.PendingLandlordSignature,
                ContractFilePath = pdfPath,
                ContractType = "Official",
                RequiredSignaturesCount = 2
            };
            lease.Signatures.AddRange(new[]
            {
                new LeaseSignature { Id = Guid.NewGuid(), LeaseId = leaseId, UserId = landlordId, Role = LeaseSignatoryRole.Landlord, SequenceOrder = 1 },
                new LeaseSignature { Id = Guid.NewGuid(), LeaseId = leaseId, UserId = tenantId, Role = LeaseSignatoryRole.Tenant, SequenceOrder = 2 }
            });
            leasingDb.Leases.Add(lease);
            await leasingDb.SaveChangesAsync();

            // Test-double provider: no external calls, but returns recipient ids per signer email.
            var provider = new Mock<ISigningProvider>();
            provider.Setup(p => p.Name).Returns("TestProvider");
            provider
                .Setup(p => p.InitiateSigningAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<List<SignerInfo>>()))
                .ReturnsAsync((byte[] _, string _, List<SignerInfo> signers) =>
                    new InitiateSigningResult(
                        "test-request-1",
                        DateTime.UtcNow.AddDays(1),
                        signers.ToDictionary(s => s.Email, s => $"rec-{s.SequenceOrder}")));
            provider
                .Setup(p => p.GetEmbeddedUrlAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new EmbeddedUrlResult("about:blank", DateTime.UtcNow.AddDays(1)));

            var contractGen = new Mock<IContractGenerationService>();
            contractGen.Setup(c => c.GetContractBytesAsync(pdfPath)).ReturnsAsync(pdfBytes);

            var signing = new SigningProviderService(
                provider.Object,
                leasingDb,
                Mock.Of<ICatalogAccessService>(),
                contractGen.Object,
                Mock.Of<ILeaseService>(),
                observedUserService,
                new ConfigurationBuilder().AddInMemoryCollection().Build(),
                NullLogger<SigningProviderService>.Instance);

            // Act — the call that used to fire two GetProfileDtoAsync calls concurrently.
            var result = await signing.InitiateEmbeddedSigningAsync(leaseId, landlordId);

            // Assert — profile fetches were strictly serial (the EF Core concurrent-operation bug).
            Assert.True(
                observedUserService.MaxOverlap <= 1,
                $"GetProfileDtoAsync calls overlapped {observedUserService.MaxOverlap} deep — " +
                "InitiateEmbeddedSigningAsync must resolve signer profiles sequentially.");

            // And the flow itself completed cleanly with real profile data + recipient ids.
            Assert.Equal("TestProvider", result.Provider);
            Assert.Equal("about:blank", result.EmbeddedUrl);

            var stored = await leasingDb.Leases.AsNoTracking().Include(l => l.Signatures).SingleAsync(l => l.Id == leaseId);
            Assert.Equal("TestProvider", stored.SignatureProvider);
            Assert.Equal("test-request-1", stored.ExternalSigningRequestId);
            Assert.All(stored.Signatures, s => Assert.False(string.IsNullOrEmpty(s.ExternalSignerId)));
        }
        finally
        {
            if (File.Exists(pdfPath)) File.Delete(pdfPath);
        }
    }

    /// <summary>
    /// Regression: completing a second signer on the same document used to write the SAME
    /// SignatureRef ("embedded_{provider}_{externalRequestId}") for every signer, which
    /// violated the unique index IX_LeaseSignatures_SignatureRef on the real Postgres DB
    /// ("duplicate key value violates unique constraint"). Asserts the refs stay distinct.
    /// </summary>
    [Fact]
    public async Task HandleSignerCompletedAsync_TwoSigners_ProducesDistinctSignatureRefs()
    {
        var landlordId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };

        var leasingOptions = new DbContextOptionsBuilder<LeasingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var leasingDb = new LeasingDbContext(leasingOptions);

        // Lease already initiated with the (fake) provider: request id + per-signer recipient ids.
        var lease = new Lease
        {
            Id = leaseId,
            PropertyId = Guid.NewGuid(),
            TenantId = tenantId,
            LandlordId = landlordId,
            ApplicationId = Guid.NewGuid(),
            Status = LeaseStatus.PendingLandlordSignature,
            ContractType = "Official",
            RequiredSignaturesCount = 2,
            SignatureProvider = "TestProvider",
            ExternalSigningRequestId = "test-request-1"
        };
        lease.Signatures.AddRange(new[]
        {
            new LeaseSignature { Id = Guid.NewGuid(), LeaseId = leaseId, UserId = landlordId, Role = LeaseSignatoryRole.Landlord, SequenceOrder = 1, ExternalSignerId = "rec-1" },
            new LeaseSignature { Id = Guid.NewGuid(), LeaseId = leaseId, UserId = tenantId, Role = LeaseSignatoryRole.Tenant, SequenceOrder = 2, ExternalSignerId = "rec-2" }
        });
        leasingDb.Leases.Add(lease);
        await leasingDb.SaveChangesAsync();

        var provider = new Mock<ISigningProvider>();
        provider.Setup(p => p.Name).Returns("TestProvider");
        provider.Setup(p => p.DownloadSignedDocumentAsync("test-request-1")).ReturnsAsync(pdfBytes);

        var signing = new SigningProviderService(
            provider.Object,
            leasingDb,
            Mock.Of<ICatalogAccessService>(),
            Mock.Of<IContractGenerationService>(),
            Mock.Of<ILeaseService>(),
            Mock.Of<IUserService>(),
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            NullLogger<SigningProviderService>.Instance);

        // Two signers complete in order — on the old code both wrote the SAME SignatureRef.
        await signing.HandleSignerCompletedAsync("test-request-1", "landlord@trustrent.local", "rec-1");
        await signing.HandleSignerCompletedAsync("test-request-1", "tenant@trustrent.local", "rec-2");

        var stored = await leasingDb.Leases.AsNoTracking().Include(l => l.Signatures).SingleAsync(l => l.Id == leaseId);
        var landlordSig = stored.Signatures.Single(s => s.UserId == landlordId);
        var tenantSig = stored.Signatures.Single(s => s.UserId == tenantId);

        Assert.False(string.IsNullOrEmpty(landlordSig.SignatureRef));
        Assert.False(string.IsNullOrEmpty(tenantSig.SignatureRef));
        Assert.StartsWith("embedded_TestProvider_", landlordSig.SignatureRef);
        Assert.StartsWith("embedded_TestProvider_", tenantSig.SignatureRef);
        // The unique-index invariant: refs must differ per signer.
        Assert.NotEqual(landlordSig.SignatureRef, tenantSig.SignatureRef);

        Assert.Equal(LeaseStatus.AwaitingPayment, stored.Status);
        Assert.All(stored.Signatures, s => Assert.True(s.Signed));
    }

    /// <summary>
    /// Regression: the embedded/webhook completion path transitioned the LEASE to AwaitingPayment
    /// but never synced the APPLICATION, so the frontend's InitialPaymentPanel (which only renders
    /// when application.status == "AwaitingPayment") was unreachable through the embedded flow.
    /// Mirrors the legacy upload path's application sync (LeaseService.ActivateLeaseAsync) and must
    /// stay idempotent for duplicate webhook events.
    /// </summary>
    [Fact]
    public async Task HandleSignerCompletedAsync_AllSigned_UpdatesApplicationToAwaitingPayment_AndIsIdempotent()
    {
        var landlordId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };

        // Real Catalog stack: application starts at ContractPendingSignature.
        var catalogOptions = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var catalogDb = new CatalogDbContext(catalogOptions);
        catalogDb.Applications.Add(new Application
        {
            Id = applicationId,
            PropertyId = Guid.NewGuid(),
            TenantId = tenantId,
            DurationMonths = 12,
            Status = ApplicationStatus.ContractPendingSignature
        });
        await catalogDb.SaveChangesAsync();
        var catalogAccess = new CatalogAccessService(catalogDb);

        // Real Leasing stack: initiated 2-signer lease linked to the application.
        var leasingOptions = new DbContextOptionsBuilder<LeasingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var leasingDb = new LeasingDbContext(leasingOptions);

        var lease = new Lease
        {
            Id = leaseId,
            PropertyId = Guid.NewGuid(),
            TenantId = tenantId,
            LandlordId = landlordId,
            ApplicationId = applicationId,
            Status = LeaseStatus.PendingLandlordSignature,
            ContractType = "Official",
            RequiredSignaturesCount = 2,
            SignatureProvider = "TestProvider",
            ExternalSigningRequestId = "test-request-1"
        };
        lease.Signatures.AddRange(new[]
        {
            new LeaseSignature { Id = Guid.NewGuid(), LeaseId = leaseId, UserId = landlordId, Role = LeaseSignatoryRole.Landlord, SequenceOrder = 1, ExternalSignerId = "rec-1" },
            new LeaseSignature { Id = Guid.NewGuid(), LeaseId = leaseId, UserId = tenantId, Role = LeaseSignatoryRole.Tenant, SequenceOrder = 2, ExternalSignerId = "rec-2" }
        });
        leasingDb.Leases.Add(lease);
        await leasingDb.SaveChangesAsync();

        var provider = new Mock<ISigningProvider>();
        provider.Setup(p => p.Name).Returns("TestProvider");
        provider.Setup(p => p.DownloadSignedDocumentAsync("test-request-1")).ReturnsAsync(pdfBytes);

        var signing = new SigningProviderService(
            provider.Object,
            leasingDb,
            catalogAccess,
            Mock.Of<IContractGenerationService>(),
            Mock.Of<ILeaseService>(),
            Mock.Of<IUserService>(),
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            NullLogger<SigningProviderService>.Instance);

        // First signer -> still pending; application unchanged.
        await signing.HandleSignerCompletedAsync("test-request-1", "landlord@trustrent.local", "rec-1");
        // Second (last) signer -> lease AND application both AwaitingPayment.
        await signing.HandleSignerCompletedAsync("test-request-1", "tenant@trustrent.local", "rec-2");

        var storedLease = await leasingDb.Leases.AsNoTracking().SingleAsync(l => l.Id == leaseId);
        var storedApp = await catalogDb.Applications.AsNoTracking().Include(a => a.History).SingleAsync(a => a.Id == applicationId);

        Assert.Equal(LeaseStatus.AwaitingPayment, storedLease.Status);
        Assert.Equal(ApplicationStatus.AwaitingPayment, storedApp.Status);
        // The same audit entry the legacy path writes (UpdateApplicationStatusAsync history).
        Assert.Contains(storedApp.History, h => h.Action == "Aguarda Pagamento");

        // Duplicate DOCUMENT_SIGNED for the last signer: must be a no-op (no throw, no re-transition).
        await signing.HandleSignerCompletedAsync("test-request-1", "tenant@trustrent.local", "rec-2");

        var afterDuplicateLease = await leasingDb.Leases.AsNoTracking().SingleAsync(l => l.Id == leaseId);
        var afterDuplicateApp = await catalogDb.Applications.AsNoTracking().Include(a => a.History).SingleAsync(a => a.Id == applicationId);
        Assert.Equal(LeaseStatus.AwaitingPayment, afterDuplicateLease.Status);
        Assert.Equal(ApplicationStatus.AwaitingPayment, afterDuplicateApp.Status);
        // No duplicate audit row: the application status sync ran exactly once.
        Assert.Single(afterDuplicateApp.History.Where(h => h.Action == "Aguarda Pagamento"));
    }

    /// <summary>
    /// Forwards to the real UserService while detecting overlapping GetProfileDtoAsync calls
    /// (the exact condition that used to hit the shared scoped IdentityDbContext concurrently).
    /// </summary>
    private sealed class NonConcurrentUserService : IUserService
    {
        private readonly IUserService _inner;
        private int _inFlight;
        public int MaxOverlap { get; private set; }

        public NonConcurrentUserService(IUserService inner) => _inner = inner;

        public async Task<UserProfileDto?> GetProfileDtoAsync(Guid userId)
        {
            var inFlight = Interlocked.Increment(ref _inFlight);
            try
            {
                MaxOverlap = Math.Max(MaxOverlap, inFlight);
                // Yield so a concurrent caller (Task.WhenAll over the same service) is
                // deterministically observed even though the InMemory provider completes
                // queries synchronously.
                await Task.Yield();
                return await _inner.GetProfileDtoAsync(userId);
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }

        public Task<User?> GetProfileAsync(Guid userId) => _inner.GetProfileAsync(userId);
        public Task<PublicUserProfileDto?> GetPublicProfileAsync(Guid userId, Guid viewerUserId) => _inner.GetPublicProfileAsync(userId, viewerUserId);
        public Task UpdateProfileAsync(Guid userId, UpdateProfileDto request) => _inner.UpdateProfileAsync(userId, request);
        public Task<PhoneVerificationRequestResult> RequestPhoneNumberVerificationAsync(Guid userId, RequestPhoneVerificationDto? request, string? sourceIp, string? userAgent, CancellationToken ct = default) => _inner.RequestPhoneNumberVerificationAsync(userId, request, sourceIp, userAgent, ct);
        public Task<PhoneVerificationStatusDto> GetPhoneVerificationStatusAsync(Guid userId, CancellationToken ct = default) => _inner.GetPhoneVerificationStatusAsync(userId, ct);
        public Task VerifyPhoneNumberAsync(Guid userId, string code, CancellationToken ct = default) => _inner.VerifyPhoneNumberAsync(userId, code, ct);
        public Task UpdateNotificationPreferencesAsync(Guid userId, UpdateNotificationPreferencesDto request) => _inner.UpdateNotificationPreferencesAsync(userId, request);
        public Task UpdatePasswordAsync(Guid userId, string currentPassword, string newPassword) => _inner.UpdatePasswordAsync(userId, currentPassword, newPassword);
        public Task<string> UpdateAvatarAsync(Guid userId, Stream fileStream, string fileName) => _inner.UpdateAvatarAsync(userId, fileStream, fileName);
        public Task<VerificationResultDto> VerifyDocumentsAsync(Guid userId, Stream? ccFrontStream, string? ccFrontFileName, Stream? ccBackStream, string? ccBackFileName, Stream? noDebtStream, string? noDebtFileName, Stream? addressProofStream, string? addressProofFileName) => _inner.VerifyDocumentsAsync(userId, ccFrontStream, ccFrontFileName, ccBackStream, ccBackFileName, noDebtStream, noDebtFileName, addressProofStream, addressProofFileName);
        public Task<VerificationResultDto> SimulateVerifyCitizenCardAsync(Guid userId) => _inner.SimulateVerifyCitizenCardAsync(userId);
        public Task<VerificationResultDto> SimulateVerifyNoDebtAsync(Guid userId) => _inner.SimulateVerifyNoDebtAsync(userId);
        public Task UpdateTrustScoreAsync(Guid userId, int newScore) => _inner.UpdateTrustScoreAsync(userId, newScore);
    }
}
