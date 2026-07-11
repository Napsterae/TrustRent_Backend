using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TrustRent.Api.Services;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Models;
using TrustRent.Modules.Identity.Contracts.Database;
using TrustRent.Modules.Identity.Models;

namespace TrustRent.Tests.Api;

public class TelegramMessagingPlatformServiceTests
{
    [Fact]
    public async Task StartPhoneVerificationAsync_WebhookConflict_ThrowsHelpfulMessage()
    {
        await using var adminDb = CreateAdminDbContext();
        await using var identityDb = CreateIdentityDbContext();
        await SeedTelegramSettingsAsync(adminDb);

        var user = CreateUser();
        identityDb.Users.Add(user);
        await identityDb.SaveChangesAsync();

        using var httpClient = CreateHttpClient((_, _) => Task.FromResult(CreateJsonResponse(WebhookConflictResponseJson)));
        var sut = new TelegramMessagingPlatformService(httpClient, adminDb, identityDb, NullLogger<TelegramMessagingPlatformService>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.StartPhoneVerificationAsync(user.Id, "+351912345678"));

        Assert.True(ex.Message.Contains("webhook", StringComparison.OrdinalIgnoreCase));

        var reloadedUser = await identityDb.Users.SingleAsync(x => x.Id == user.Id);
        Assert.Null(reloadedUser.TelegramPendingVerificationToken);
        Assert.Null(reloadedUser.TelegramPendingExpectedPhoneNumber);
    }

    [Fact]
    public async Task GetPhoneVerificationStatusAsync_WebhookConflict_ReturnsDiagnosticMessage()
    {
        await using var adminDb = CreateAdminDbContext();
        await using var identityDb = CreateIdentityDbContext();
        await SeedTelegramSettingsAsync(adminDb);

        var user = CreateUser();
        user.PendingPhoneCountryCode = "PT";
        user.PendingPhoneNumber = "+351912345678";
        user.PendingPhoneContactPlatform = PhoneContactPlatforms.Telegram;
        user.TelegramChatId = "123456789";
        var tokenHash = HashToken("token123");
        user.TelegramPendingVerificationToken = tokenHash;
        user.TelegramPendingExpectedPhoneNumber = "+351912345678";
        user.TelegramPendingVerificationExpiresAt = DateTime.UtcNow.AddMinutes(10);

        identityDb.Users.Add(user);
        await identityDb.SaveChangesAsync();

        using var httpClient = CreateHttpClient((_, _) => Task.FromResult(CreateJsonResponse(WebhookConflictResponseJson)));
        var sut = new TelegramMessagingPlatformService(httpClient, adminDb, identityDb, NullLogger<TelegramMessagingPlatformService>.Instance);

        var result = await sut.GetPhoneVerificationStatusAsync(user.Id);

        Assert.True(result.IsConfigured);
        Assert.False(result.AwaitingContactShare);
        Assert.False(result.IsPhoneNumberVerified);
        Assert.Null(result.DeepLinkUrl);
        Assert.True(result.Message.Contains("webhook", StringComparison.OrdinalIgnoreCase));
    }

        [Fact]
        public async Task GetPhoneVerificationStatusAsync_NumericChatIdUpdate_LinksConversation()
        {
                await using var adminDb = CreateAdminDbContext();
                await using var identityDb = CreateIdentityDbContext();
                await SeedTelegramSettingsAsync(adminDb);

                var user = CreateUser();
                user.PendingPhoneCountryCode = "PT";
                user.PendingPhoneNumber = "+351912345678";
                user.PendingPhoneContactPlatform = PhoneContactPlatforms.Telegram;
                var tokenHash = HashToken("token123");
                user.TelegramPendingVerificationToken = tokenHash;
                user.TelegramPendingExpectedPhoneNumber = "+351912345678";
                user.TelegramPendingVerificationExpiresAt = DateTime.UtcNow.AddMinutes(10);

                identityDb.Users.Add(user);
                await identityDb.SaveChangesAsync();

                using var httpClient = CreateHttpClient((request, _) =>
                {
                        if (request.RequestUri?.AbsolutePath.EndsWith("/getUpdates", StringComparison.Ordinal) == true)
                        {
                                return Task.FromResult(CreateJsonResponse(
                                        """
                                        {
                                            "ok": true,
                                            "result": [
                                                {
                                                    "update_id": 1,
                                                    "message": {
                                                        "message_id": 10,
                                                        "text": "/start token123",
                                                        "chat": { "id": 123456789 },
                                                        "from": {
                                                            "id": 123456789,
                                                            "username": "testuser"
                                                        }
                                                    }
                                                }
                                            ]
                                        }
                                        """));
                        }

                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
                });

                var sut = new TelegramMessagingPlatformService(httpClient, adminDb, identityDb, NullLogger<TelegramMessagingPlatformService>.Instance);

                var result = await sut.GetPhoneVerificationStatusAsync(user.Id);

                var reloadedUser = await identityDb.Users.SingleAsync(x => x.Id == user.Id);
                Assert.True(result.IsConfigured);
                Assert.True(result.HasStartedConversation);
                Assert.True(result.AwaitingContactShare);
                Assert.True(result.Message.Contains("Partilha o teu contacto", StringComparison.OrdinalIgnoreCase));
                Assert.Equal("123456789", reloadedUser.TelegramChatId);
                Assert.Equal("testuser", reloadedUser.TelegramUsername);
        }

    private static AdminDbContext CreateAdminDbContext()
        => new(new DbContextOptionsBuilder<AdminDbContext>()
            .UseInMemoryDatabase($"telegram-admin-{Guid.NewGuid():N}")
            .Options);

    private static IdentityDbContext CreateIdentityDbContext()
        => new(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase($"telegram-identity-{Guid.NewGuid():N}")
            .Options);

    private static async Task SeedTelegramSettingsAsync(AdminDbContext adminDb)
    {
        adminDb.PlatformSettings.AddRange(
            new PlatformSetting { Key = "telegram.bot_token", Value = "test-token" },
            new PlatformSetting { Key = "telegram.bot_username", Value = "test_bot" });
        await adminDb.SaveChangesAsync();
    }

    private static string HashToken(string rawToken)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

    private static User CreateUser()
        => new()
        {
            Id = Guid.NewGuid(),
            Name = "Test User",
            Email = $"user-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            PhoneContactPlatform = PhoneContactPlatforms.Telegram
        };

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        => new(new RecordingHttpMessageHandler(responseFactory))
        {
            BaseAddress = new Uri("https://api.telegram.org")
        };

    private static HttpResponseMessage CreateJsonResponse(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private const string WebhookConflictResponseJson = """
        {
          "ok": false,
          "error_code": 409,
          "description": "Conflict: can't use getUpdates method while webhook is active; use deleteWebhook to delete the webhook first"
        }
        """;

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responseFactory;

        public RecordingHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _responseFactory(request, cancellationToken);
    }
}