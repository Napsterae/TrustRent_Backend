using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using TrustRent.Modules.Communications.Contracts.Database;
using TrustRent.Modules.Communications.Hubs;
using TrustRent.Modules.Communications.Models;
using TrustRent.Modules.Communications.Services;

namespace TrustRent.Tests.Communications;

public class NotificationServiceTests
{
    private CommunicationsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CommunicationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CommunicationsDbContext(options);
    }

    private (NotificationService Service, Mock<IHubContext<NotificationHub>> HubMock) CreateService(CommunicationsDbContext context)
    {
        var hubMock = new Mock<IHubContext<NotificationHub>>();
        var clientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();

        hubMock.Setup(h => h.Clients).Returns(clientsMock.Object);
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);
        clientProxyMock.Setup(p => p.SendCoreAsync(
            It.IsAny<string>(),
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new NotificationService(context, hubMock.Object);
        return (service, hubMock);
    }

    [Fact]
    public async Task SendNotificationAsync_PersistsToDatabase()
    {
        using var context = CreateContext();
        var (service, _) = CreateService(context);
        var userId = Guid.NewGuid();

        await service.SendNotificationAsync(userId, "application", "Nova candidatura recebida");

        var notifications = await context.Notifications.ToListAsync();
        Assert.Single(notifications);
        Assert.Equal(userId, notifications[0].UserId);
        Assert.Equal("application", notifications[0].Type);
        Assert.Equal("Nova candidatura recebida", notifications[0].Message);
        Assert.False(notifications[0].IsRead);
    }

    [Fact]
    public async Task SendNotificationAsync_WithReferenceId_PersistsCorrectly()
    {
        using var context = CreateContext();
        var (service, _) = CreateService(context);
        var userId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();

        await service.SendNotificationAsync(userId, "ticket", "Novo ticket", referenceId);

        var notification = await context.Notifications.FirstAsync();
        Assert.Equal(referenceId, notification.ReferenceId);
    }

    [Fact]
    public async Task SendNotificationAsync_SendsViaSignalR()
    {
        using var context = CreateContext();
        var (service, hubMock) = CreateService(context);
        var userId = Guid.NewGuid();

        await service.SendNotificationAsync(userId, "payment", "Pagamento confirmado");

        hubMock.Verify(h => h.Clients.Group($"user_{userId}"), Times.Once);
    }

    [Fact]
    public async Task NotifyTicketCreatedAsync_SendsCorrectNotification()
    {
        using var context = CreateContext();
        var (service, _) = CreateService(context);
        var landlordId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        await service.NotifyTicketCreatedAsync(landlordId, ticketId, "Apartamento T2 Lisboa");

        var notification = await context.Notifications.FirstAsync();
        Assert.Equal(landlordId, notification.UserId);
        Assert.Equal("ticket", notification.Type);
        Assert.Contains("Apartamento T2 Lisboa", notification.Message);
        Assert.Equal(ticketId, notification.ReferenceId);
    }

    [Fact]
    public async Task SendNotificationAsync_MultipleNotifications_AllPersisted()
    {
        using var context = CreateContext();
        var (service, _) = CreateService(context);
        var userId = Guid.NewGuid();

        await service.SendNotificationAsync(userId, "type1", "Message 1");
        await service.SendNotificationAsync(userId, "type2", "Message 2");
        await service.SendNotificationAsync(userId, "type3", "Message 3");

        Assert.Equal(3, await context.Notifications.CountAsync());
    }
}
