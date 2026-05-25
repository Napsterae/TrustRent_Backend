using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TrustRent.Modules.Identity.Contracts.Database;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Identity.Models;
using TrustRent.Modules.Identity.Repositories;
using TrustRent.Modules.Identity.Services;

namespace TrustRent.Tests.Identity;

public class PhoneLoginCodeServiceTests
{
    [Fact]
    public async Task SendLoginCodeAsync_TelegramUser_SendsTelegramCodeAndStoresPendingCode()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new IdentityDbContext(options);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Teste",
            Email = "teste@example.com",
            PasswordHash = string.Empty,
            PhoneNumber = "+351912345678",
            PhoneCountryCode = "PT",
            IsPhoneNumberVerified = true,
            PhoneContactPlatform = PhoneContactPlatforms.Telegram,
            TelegramChatId = "123456"
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = "TrustRent_Super_Secret_Key_Para_O_JWT_2026_Tem_De_Ser_Longa"
            })
            .Build();

        var telegramService = new Mock<ITelegramMessagingPlatformService>();
        telegramService
            .Setup(service => service.SendLoginCodeAsync(user, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new PhoneLoginCodeService(
            db,
            new UserRepository(db),
            config,
            NullLogger<PhoneLoginCodeService>.Instance,
            telegramService.Object);

        var result = await sut.SendLoginCodeAsync(user.PhoneNumber, null, null);

        Assert.Equal(PhoneContactPlatforms.Telegram, result.Platform);
        Assert.Equal("+351*******78", result.MaskedPhoneNumber);
        Assert.Single(db.WhatsAppOneTimeCodes);
        Assert.Equal("telegram_login", db.WhatsAppOneTimeCodes.Single().Purpose);
        telegramService.Verify(service => service.SendLoginCodeAsync(user, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyLoginCodeAsync_AllowsWhitespaceInsideTelegramCode()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new IdentityDbContext(options);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Teste",
            Email = "teste@example.com",
            PasswordHash = string.Empty,
            PhoneNumber = "+351912345678",
            PhoneCountryCode = "PT",
            IsPhoneNumberVerified = true,
            PhoneContactPlatform = PhoneContactPlatforms.Telegram,
            TelegramChatId = "123456"
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = "TrustRent_Super_Secret_Key_Para_O_JWT_2026_Tem_De_Ser_Longa"
            })
            .Build();

        string sentCode = string.Empty;
        var telegramService = new Mock<ITelegramMessagingPlatformService>();
        telegramService
            .Setup(service => service.SendLoginCodeAsync(user, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<User, string, CancellationToken>((_, code, _) => sentCode = code)
            .Returns(Task.CompletedTask);

        var sut = new PhoneLoginCodeService(
            db,
            new UserRepository(db),
            config,
            NullLogger<PhoneLoginCodeService>.Instance,
            telegramService.Object);

        await sut.SendLoginCodeAsync(user.PhoneNumber, null, null);

        var verifiedPhone = await sut.VerifyLoginCodeAsync(user.PhoneNumber, $"{sentCode[..3]} {sentCode[3..]}");

        Assert.Equal(user.PhoneNumber, verifiedPhone);
        Assert.NotNull(db.WhatsAppOneTimeCodes.Single().VerifiedAt);
    }
}