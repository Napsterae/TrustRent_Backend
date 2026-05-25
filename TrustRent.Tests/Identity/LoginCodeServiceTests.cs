using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TrustRent.Modules.Identity.Contracts.Database;
using TrustRent.Modules.Identity.Models;
using TrustRent.Modules.Identity.Services;
using TrustRent.Shared.Communications;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Tests.Identity;

public class LoginCodeServiceTests
{
    [Fact]
    public async Task VerifyLoginCodeAsync_AllowsWhitespaceInsideCode()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new IdentityDbContext(options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = "TrustRent_Super_Secret_Key_Para_O_JWT_2026_Tem_De_Ser_Longa"
            })
            .Build();

        var sentCode = string.Empty;
        var communicationContentService = new Mock<ICommunicationContentService>();
        communicationContentService
            .Setup(service => service.RenderEmailTemplateAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string?>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyDictionary<string, string?>, string, CancellationToken>((_, variables, _, _) =>
                sentCode = variables["LoginCode"] ?? string.Empty)
            .ReturnsAsync(new RenderedEmailTemplateContent(
                CommunicationEmailTemplateKeys.AuthLoginCode,
                "Código de login",
                "1.0.2",
                "subject",
                "body",
                "body",
                false,
                new Dictionary<string, string>()));

        var emailService = new Mock<IEmailService>();
        emailService
            .Setup(service => service.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<EmailSendOptions>()))
            .Returns(Task.CompletedTask);

        var sut = new LoginCodeService(
            db,
            communicationContentService.Object,
            emailService.Object,
            NullLogger<LoginCodeService>.Instance,
            config);

        await sut.SendLoginCodeAsync("test@example.com", null, null);

        var loginCode = await db.EmailLoginCodes.SingleAsync();
        Assert.NotNull(loginCode);
        Assert.False(string.IsNullOrWhiteSpace(sentCode));

        var verifiedEmail = await sut.VerifyLoginCodeAsync("test@example.com", $"{sentCode[..3]} {sentCode[3..]}");

        Assert.Equal("test@example.com", verifiedEmail);
        Assert.NotNull(loginCode.VerifiedAt);
    }
}