using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TrustRent.Api.Endpoints;
using TrustRent.Modules.Admin.Contracts.Interfaces;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Modules.Identity.Contracts.Database;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Modules.Leasing.Services;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;

namespace TrustRent.Tests.Leasing;

/// <summary>
/// End-to-end (HTTP-level) tests for the Development-only simulate-signature-complete endpoint
/// running against the real MockSigningProvider + SigningProviderService stack.
/// </summary>
public class SimulateSignatureCompleteEndpointTests
{
    [Fact]
    public async Task SimulateSignatureComplete_DrivesFullSigningFlowToAwaitingPayment()
    {
        var landlordId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
        var pdfPath = Path.Combine(Path.GetTempPath(), $"mock-e2e-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(pdfPath, pdfBytes);

        var lease = CreateTestLease(leaseId, landlordId, tenantId, pdfPath);

        try
        {
            await using var harness = await SigningHarness.CreateAsync(landlordId, lease, pdfBytes);

            // 1. First call: next unsigned signer = Landlord.
            var r1 = await harness.Client.PostAsync($"/api/leases/{leaseId}/simulate-signature-complete", null);
            Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
            var p1 = await r1.Content.ReadFromJsonAsync<SimulateCompleteResponse>();
            Assert.Equal("Landlord", p1!.CompletedSigner);
            Assert.Equal("PendingTenantSignature", p1.LeaseStatus);
            Assert.Equal(1, p1.SignedCount);
            Assert.False(p1.AllSigned);

            // 2. Second call with ?signer=Tenant → all signed → AwaitingPayment.
            var r2 = await harness.Client.PostAsync($"/api/leases/{leaseId}/simulate-signature-complete?signer=Tenant", null);
            Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
            var p2 = await r2.Content.ReadFromJsonAsync<SimulateCompleteResponse>();
            Assert.Equal("Tenant", p2!.CompletedSigner);
            Assert.Equal("AwaitingPayment", p2.LeaseStatus);
            Assert.Equal(2, p2.SignedCount);
            Assert.True(p2.AllSigned);

            // 3. Persisted state: every signature signed, lease AwaitingPayment,
            //    SignatureProvider="Mock" (so the frontend renders EmbeddedSignature).
            var stored = await harness.ReadLeaseAsync(leaseId);
            Assert.All(stored.Signatures, s => Assert.True(s.Signed));
            Assert.Equal(LeaseStatus.AwaitingPayment, stored.Status);
            Assert.NotNull(stored.ContractSignedAt);
            Assert.Equal("Mock", stored.SignatureProvider);
        }
        finally
        {
            if (File.Exists(pdfPath)) File.Delete(pdfPath);
        }
    }

    [Fact]
    public async Task SimulateSignatureComplete_OutOfOrderSigner_ReturnsBadRequest()
    {
        var landlordId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };
        var pdfPath = Path.Combine(Path.GetTempPath(), $"mock-e2e-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(pdfPath, pdfBytes);

        var lease = CreateTestLease(leaseId, landlordId, tenantId, pdfPath);

        try
        {
            await using var harness = await SigningHarness.CreateAsync(landlordId, lease, pdfBytes);

            // Tenant is not the next unsigned signer (Landlord is) → rejected.
            var response = await harness.Client.PostAsync(
                $"/api/leases/{leaseId}/simulate-signature-complete?signer=Tenant", null);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            if (File.Exists(pdfPath)) File.Delete(pdfPath);
        }
    }

    private static Lease CreateTestLease(Guid leaseId, Guid landlordId, Guid tenantId, string pdfPath)
    {
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
        return lease;
    }

    private sealed record SimulateCompleteResponse(
        Guid LeaseId,
        string CompletedSigner,
        Guid CompletedSignerUserId,
        string LeaseStatus,
        int SignedCount,
        int RequiredSignaturesCount,
        bool AllSigned);

    private sealed class SigningHarness : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private SigningHarness(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public async Task<Lease> ReadLeaseAsync(Guid leaseId)
        {
            await using var scope = _app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<LeasingDbContext>();
            return await db.Leases.AsNoTracking().Include(l => l.Signatures).SingleAsync(l => l.Id == leaseId);
        }

        public static async Task<SigningHarness> CreateAsync(Guid userId, Lease lease, byte[] pdfBytes)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                EnvironmentName = Environments.Development,
            });

            builder.WebHost.UseTestServer();
            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            builder.Services.AddAuthorization();

            var dbName = $"signing-endpoints-{Guid.NewGuid()}";
            builder.Services.AddDbContext<LeasingDbContext>(options =>
                options.UseInMemoryDatabase(dbName));
            builder.Services.AddDbContext<IdentityDbContext>(options =>
                options.UseInMemoryDatabase($"identity-signing-{Guid.NewGuid()}"));
            builder.Services.AddSingleton(Mock.Of<IStagingAccessService>());
            builder.Services.AddSingleton(Mock.Of<IUserService>());
            builder.Services.AddSingleton(Mock.Of<ILeaseService>());
            builder.Services.AddSingleton(Mock.Of<IUserRepository>());
            builder.Services.AddSingleton(Mock.Of<ICommunicationContentService>());
            builder.Services.AddSingleton(Mock.Of<IEmailService>());
            builder.Services.AddSingleton(Mock.Of<INotificationService>());
            builder.Services.AddSingleton(Mock.Of<IGuarantorService>());
            builder.Services.AddSingleton(Mock.Of<IGeminiDocumentService>());
            builder.Services.AddScoped<DocumentSigningPinService>();

            // Real Mock provider + real orchestration, wired exactly like Program.cs does for the mock.
            builder.Services.AddSingleton<ISigningProvider>(new MockSigningProvider());
            var contractGen = new Mock<IContractGenerationService>();
            contractGen.Setup(c => c.GetContractBytesAsync(lease.ContractFilePath!)).ReturnsAsync(pdfBytes);
            builder.Services.AddSingleton(contractGen.Object);
            builder.Services.AddScoped<ISigningProviderService, SigningProviderService>();
            builder.Configuration["ElectronicSignature:Provider"] = "Mock";

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapLeaseEndpoints();

            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LeasingDbContext>();
                db.Leases.Add(lease);
                await db.SaveChangesAsync();
            }

            await app.StartAsync();

            var client = app.GetTestClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());

            return new SigningHarness(app, client);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";
        public const string UserIdHeader = "X-Test-UserId";

        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserIdHeader, out var userIdValues)
                || !Guid.TryParse(userIdValues.ToString(), out var userId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) };
            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
