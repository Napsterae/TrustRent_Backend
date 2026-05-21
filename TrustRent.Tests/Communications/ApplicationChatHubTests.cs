using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TrustRent.Modules.Communications.Contracts.Database;
using TrustRent.Modules.Communications.Hubs;
using TrustRent.Modules.Communications.Models;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Identity.Models;
using TrustRent.Shared.Communications;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Tests.Communications;

public class ApplicationChatHubTests
{
    [Fact]
    public async Task SendMessage_WhenEmailFails_PersistsAndBroadcastsMessage()
    {
        var applicationId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        await using var context = CreateContext();
        var statusValidatorMock = new Mock<IApplicationStatusValidator>();
        var notificationServiceMock = new Mock<INotificationService>();
        var userServiceMock = new Mock<IUserService>();
        var communicationContentServiceMock = new Mock<ICommunicationContentService>();
        var emailServiceMock = new Mock<IEmailService>();
        var loggerMock = new Mock<ILogger<ApplicationChatHub>>();

        statusValidatorMock
            .Setup(service => service.GetApplicationChatContextAsync(applicationId))
            .ReturnsAsync(new ApplicationChatContext
            {
                ApplicationId = applicationId,
                TenantId = senderId,
                LandlordId = recipientId,
                PropertyTitle = "Apartamento T2"
            });
        statusValidatorMock
            .Setup(service => service.IsApplicationChatLockedAsync(applicationId))
            .ReturnsAsync(false);

        notificationServiceMock
            .Setup(service => service.SendNotificationAsync(recipientId, "application", It.IsAny<string>(), applicationId))
            .Returns(Task.CompletedTask);

        userServiceMock
            .Setup(service => service.GetProfileAsync(senderId))
            .ReturnsAsync(new User { Id = senderId, Name = "Miguel", Email = "miguel@example.com" });
        userServiceMock
            .Setup(service => service.GetProfileAsync(recipientId))
            .ReturnsAsync(new User { Id = recipientId, Name = "Joana", Email = "joana@example.com" });

        communicationContentServiceMock
            .Setup(service => service.RenderEmailTemplateAsync(
                CommunicationEmailTemplateKeys.ApplicationNewMessage,
                It.IsAny<IReadOnlyDictionary<string, string?>>(),
                "pt-PT",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RenderedEmailTemplateContent(
                CommunicationEmailTemplateKeys.ApplicationNewMessage,
                "Application New Message",
                "v1",
                "Nova mensagem",
                "<p>Mensagem</p>",
                "Mensagem",
                false,
                new Dictionary<string, string>()));

        emailServiceMock
            .Setup(service => service.SendEmailAsync("joana@example.com", "Nova mensagem", "<p>Mensagem</p>"))
            .ThrowsAsync(new InvalidOperationException("SES down"));

        var clientProxyMock = new Mock<IClientProxy>();
        clientProxyMock
            .Setup(proxy => proxy.SendCoreAsync(
                "ReceiveMessage",
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var clientsMock = new Mock<IHubCallerClients>();
        clientsMock
            .Setup(clients => clients.Group(applicationId.ToString()))
            .Returns(clientProxyMock.Object);

        var contextMock = new Mock<HubCallerContext>();
        contextMock
            .SetupGet(ctx => ctx.User)
            .Returns(new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, senderId.ToString())
            ],
            "TestAuth")));

        var hub = new ApplicationChatHub(
            context,
            statusValidatorMock.Object,
            notificationServiceMock.Object,
            userServiceMock.Object,
            communicationContentServiceMock.Object,
            emailServiceMock.Object,
            configuration: null,
            logger: loggerMock.Object)
        {
            Context = contextMock.Object,
            Clients = clientsMock.Object
        };

        await hub.SendMessage(applicationId, senderId, "Ola, ainda esta disponivel?");

        var messages = await context.Messages.ToListAsync();
        Assert.Single(messages);
        Assert.Equal(applicationId, messages[0].ContextId);
        Assert.Equal(senderId, messages[0].SenderId);
        Assert.Equal("Ola, ainda esta disponivel?", messages[0].Content);

        clientProxyMock.Verify(proxy => proxy.SendCoreAsync(
            "ReceiveMessage",
            It.Is<object[]>(args => args.Length == 1
                && args[0] != null
                && args[0].GetType() == typeof(Message)
                && ((Message)args[0]).Content == "Ola, ainda esta disponivel?"),
            It.IsAny<CancellationToken>()), Times.Once);
        notificationServiceMock.Verify(service => service.SendNotificationAsync(recipientId, "application", It.IsAny<string>(), applicationId), Times.Once);
        emailServiceMock.Verify(service => service.SendEmailAsync("joana@example.com", "Nova mensagem", "<p>Mensagem</p>"), Times.Once);
    }

    private static CommunicationsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CommunicationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CommunicationsDbContext(options);
    }
}