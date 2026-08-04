using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Modules.Leasing.Services;
using TrustRent.Shared.Models;

namespace TrustRent.Tests.Leasing;

public class MockSigningProviderTests
{
    private static readonly byte[] ContractPdf = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // "%PDF"

    private static List<SignerInfo> CreateSigners() => new()
    {
        new SignerInfo(Guid.NewGuid(), "landlord@trustrent.local", "Senhorio", LeaseSignatoryRole.Landlord, 1),
        new SignerInfo(Guid.NewGuid(), "tenant@trustrent.local", "Inquilino", LeaseSignatoryRole.Tenant, 2)
    };

    [Fact]
    public async Task InitiateSigningAsync_ReturnsMockRequestIdAndRecipientIdsForEverySigner()
    {
        var provider = new MockSigningProvider();
        var signers = CreateSigners();

        var result = await provider.InitiateSigningAsync(ContractPdf, "Contrato de Arrendamento", signers);

        Assert.StartsWith("mock-", result.ExternalRequestId);
        Assert.NotNull(result.RecipientIds);
        Assert.Equal(2, result.RecipientIds.Count);
        Assert.Contains("landlord@trustrent.local", result.RecipientIds.Keys);
        Assert.Contains("tenant@trustrent.local", result.RecipientIds.Keys);
        Assert.NotEqual(result.RecipientIds["landlord@trustrent.local"], result.RecipientIds["tenant@trustrent.local"]);

        // Each call creates a distinct request (like a new document per initiation).
        var second = await provider.InitiateSigningAsync(ContractPdf, "Contrato", signers);
        Assert.NotEqual(result.ExternalRequestId, second.ExternalRequestId);
    }

    [Fact]
    public async Task GetEmbeddedUrlAsync_ReturnsHarmlessDataUri()
    {
        var provider = new MockSigningProvider();
        var signers = CreateSigners();
        var result = await provider.InitiateSigningAsync(ContractPdf, "Contrato", signers);

        var url = await provider.GetEmbeddedUrlAsync(result.ExternalRequestId, signers[0].Email);

        Assert.StartsWith("data:text/html;base64,", url.EmbeddedUrl);
    }

    [Fact]
    public async Task DownloadSignedDocumentAsync_ReturnsTheOriginalContractBytes()
    {
        var provider = new MockSigningProvider();
        var result = await provider.InitiateSigningAsync(ContractPdf, "Contrato", CreateSigners());

        var downloaded = await provider.DownloadSignedDocumentAsync(result.ExternalRequestId);

        Assert.Equal(ContractPdf, downloaded);
    }

    [Fact]
    public async Task DownloadSignedDocumentAsync_UnknownRequest_ThrowsKeyNotFound()
    {
        var provider = new MockSigningProvider();

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            provider.DownloadSignedDocumentAsync("mock-does-not-exist"));
    }

    // ---- SigningProviderService completion path with the mock (signer order) ----

    [Fact]
    public async Task HandleSignerCompletedAsync_WithMock_TransitionsThroughSignerOrderToAwaitingPayment()
    {
        var landlordId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();

        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
        var pdfPath = Path.Combine(Path.GetTempPath(), $"mock-sign-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(pdfPath, pdfBytes);

        try
        {
            var options = new DbContextOptionsBuilder<LeasingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            await using var db = new LeasingDbContext(options);

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
            db.Leases.Add(lease);
            await db.SaveChangesAsync();

            var contractGen = new Mock<IContractGenerationService>();
            contractGen.Setup(c => c.GetContractBytesAsync(pdfPath)).ReturnsAsync(pdfBytes);
            var userService = new Mock<IUserService>(); // returns null -> fallback emails
            var leaseService = new Mock<ILeaseService>();

            var provider = new MockSigningProvider();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ElectronicSignature:Provider"] = "Mock"
            }).Build();

            var signing = new SigningProviderService(
                provider,
                db,
                contractGen.Object,
                leaseService.Object,
                userService.Object,
                configuration,
                NullLogger<SigningProviderService>.Instance);

            // 1. Initiate embedded signing (creates the mock external request + signer ids).
            var initiated = await signing.InitiateEmbeddedSigningAsync(leaseId, landlordId);
            Assert.Equal("Mock", initiated.Provider);
            Assert.StartsWith("data:text/html;base64,", initiated.EmbeddedUrl);

            var afterInit = await db.Leases.Include(l => l.Signatures).SingleAsync(l => l.Id == leaseId);
            Assert.Equal("Mock", afterInit.SignatureProvider);
            Assert.False(string.IsNullOrEmpty(afterInit.ExternalSigningRequestId));
            var landlordSig = afterInit.Signatures.Single(s => s.UserId == landlordId);
            var tenantSig = afterInit.Signatures.Single(s => s.UserId == tenantId);
            Assert.False(string.IsNullOrEmpty(landlordSig.ExternalSignerId));
            Assert.False(string.IsNullOrEmpty(tenantSig.ExternalSignerId));

            // 2. Landlord completes first -> next signer pending.
            await signing.HandleSignerCompletedAsync(
                afterInit.ExternalSigningRequestId!, $"{landlordId}@trustrent.local", landlordSig.ExternalSignerId);

            var afterLandlord = await db.Leases.Include(l => l.Signatures).SingleAsync(l => l.Id == leaseId);
            Assert.True(afterLandlord.Signatures.Single(s => s.UserId == landlordId).Signed);
            Assert.False(afterLandlord.Signatures.Single(s => s.UserId == tenantId).Signed);
            Assert.Equal(LeaseStatus.PendingTenantSignature, afterLandlord.Status);

            // 3. Tenant completes -> all signed, AwaitingPayment (the transition that precedes payment).
            await signing.HandleSignerCompletedAsync(
                afterInit.ExternalSigningRequestId!, $"{tenantId}@trustrent.local", tenantSig.ExternalSignerId);

            var afterTenant = await db.Leases.Include(l => l.Signatures).SingleAsync(l => l.Id == leaseId);
            Assert.True(afterTenant.Signatures.All(s => s.Signed));
            Assert.Equal(LeaseStatus.AwaitingPayment, afterTenant.Status);
            Assert.NotNull(afterTenant.ContractSignedAt);
        }
        finally
        {
            if (File.Exists(pdfPath)) File.Delete(pdfPath);
        }
    }
}
