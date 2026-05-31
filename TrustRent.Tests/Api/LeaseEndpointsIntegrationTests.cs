using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
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
using TrustRent.Modules.Leasing.Contracts.DTOs;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Tests.Api;

public class LeaseEndpointsIntegrationTests
{
    [Fact]
    public async Task SignatureStatus_WhenAuthenticated_ReturnsLeaseSignaturePayload()
    {
        var userId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();

        await using var harness = await LeaseEndpointHarness.CreateAsync(userId);
        harness.LeaseService
            .Setup(service => service.GetSignatureStatusAsync(leaseId, userId))
            .ReturnsAsync(new LeaseSignatureStatusDto
            {
                LeaseId = leaseId,
                ContractType = "Official",
                LeaseStatus = "PendingTenantSignature",
                RequiredSignaturesCount = 2,
                SignedCount = 1,
                Signatories = new List<LeaseSignatoryDto>
                {
                    new()
                    {
                        UserId = Guid.NewGuid(),
                        Name = "Senhorio",
                        Role = "Landlord",
                        SequenceOrder = 1,
                        Signed = true,
                    },
                    new()
                    {
                        UserId = userId,
                        Name = "Inquilino",
                        Role = "Tenant",
                        SequenceOrder = 2,
                        Signed = false,
                    },
                },
            });

        var response = await harness.Client.GetAsync($"/api/leases/{leaseId}/signature-status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<LeaseSignatureStatusDto>();
        Assert.NotNull(payload);
        Assert.Equal(leaseId, payload.LeaseId);
        Assert.Equal("Official", payload.ContractType);
        Assert.Equal(2, payload.RequiredSignaturesCount);
        Assert.Equal("Inquilino", payload.Signatories.Last().Name);

        harness.LeaseService.Verify(service => service.GetSignatureStatusAsync(leaseId, userId), Times.Once);
    }

    [Fact]
    public async Task SignatureStatus_WhenLeaseIsMissing_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();

        await using var harness = await LeaseEndpointHarness.CreateAsync(userId);
        harness.LeaseService
            .Setup(service => service.GetSignatureStatusAsync(leaseId, userId))
            .ReturnsAsync((LeaseSignatureStatusDto?)null);

        var response = await harness.Client.GetAsync($"/api/leases/{leaseId}/signature-status");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SignatureStatus_WhenServiceRejectsUser_ReturnsForbidden()
    {
        var userId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();

        await using var harness = await LeaseEndpointHarness.CreateAsync(userId);
        harness.LeaseService
            .Setup(service => service.GetSignatureStatusAsync(leaseId, userId))
            .ThrowsAsync(new UnauthorizedAccessException("Sem permissão."));

        var response = await harness.Client.GetAsync($"/api/leases/{leaseId}/signature-status");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SignatureStatus_WhenNoAuthenticatedUser_ReturnsUnauthorized()
    {
        var leaseId = Guid.NewGuid();

        await using var harness = await LeaseEndpointHarness.CreateAsync();

        var response = await harness.Client.GetAsync($"/api/leases/{leaseId}/signature-status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        harness.LeaseService.Verify(service => service.GetSignatureStatusAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }

    private sealed class LeaseEndpointHarness : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private LeaseEndpointHarness(WebApplication app, HttpClient client, Mock<ILeaseService> leaseService)
        {
            _app = app;
            Client = client;
            LeaseService = leaseService;
        }

        public HttpClient Client { get; }

        public Mock<ILeaseService> LeaseService { get; }

        public static async Task<LeaseEndpointHarness> CreateAsync(Guid? userId = null)
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

            builder.Services.AddDbContext<LeasingDbContext>(options =>
                options.UseInMemoryDatabase($"lease-endpoints-{Guid.NewGuid()}"));
            builder.Services.AddDbContext<IdentityDbContext>(options =>
                options.UseInMemoryDatabase($"identity-endpoints-{Guid.NewGuid()}"));

            var leaseService = new Mock<ILeaseService>(MockBehavior.Strict);
            builder.Services.AddSingleton(leaseService.Object);
            builder.Services.AddSingleton(Mock.Of<IGuarantorService>());
            builder.Services.AddSingleton(Mock.Of<INotificationService>());
            builder.Services.AddSingleton(Mock.Of<IEmailService>());
            builder.Services.AddSingleton(Mock.Of<ICommunicationContentService>());
            builder.Services.AddSingleton(Mock.Of<IUserService>());
            builder.Services.AddSingleton(Mock.Of<IGeminiDocumentService>());
            builder.Services.AddSingleton(Mock.Of<IStagingAccessService>());

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapLeaseEndpoints();

            await app.StartAsync();

            var client = app.GetTestClient();
            if (userId.HasValue)
            {
                client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.Value.ToString());
            }

            return new LeaseEndpointHarness(app, client, leaseService);
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

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            };

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}