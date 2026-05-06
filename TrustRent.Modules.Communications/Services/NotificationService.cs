using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrustRent.Modules.Communications.Contracts.Database;
using TrustRent.Modules.Communications.Hubs;
using TrustRent.Modules.Communications.Models;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Modules.Communications.Services;

public class NotificationService : INotificationService
{
    private readonly CommunicationsDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IExpoPushService _expoPushService;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        CommunicationsDbContext context,
        IHubContext<NotificationHub> hubContext,
        IExpoPushService expoPushService,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _expoPushService = expoPushService;
        _logger = logger;
    }

    public async Task SendNotificationAsync(Guid userId, string type, string message, Guid? referenceId = null)
    {
        // 1. Persistir na Base de Dados
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Message = message,
            ReferenceId = referenceId,
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        // 2. Emitir via SignalR em Tempo Real para o grupo do utilizador
        // Usamos o grupo "user_{userId}" que definimos no Hub
        await _hubContext.Clients.Group($"user_{userId}").SendAsync("ReceiveNotification", new
        {
            notification.Id,
            notification.Type,
            notification.Message,
            notification.ReferenceId,
            notification.IsRead,
            notification.CreatedAt
        });

        var pushDevices = await _context.PushDevices
            .Where(device => device.UserId == userId && device.IsActive)
            .ToListAsync();

        if (pushDevices.Count == 0)
            return;

        try
        {
            var invalidTokens = await _expoPushService.SendNotificationAsync(
                pushDevices.Select(device => device.ExpoPushToken),
                "TrustRent",
                notification.Message,
                new
                {
                    notificationId = notification.Id,
                    notification.Type,
                    notification.ReferenceId,
                    notification.CreatedAt
                });

            if (invalidTokens.Count == 0)
                return;

            foreach (var device in pushDevices.Where(device => invalidTokens.Contains(device.ExpoPushToken)))
            {
                device.IsActive = false;
                device.LastSeenAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao enviar push background para o utilizador {UserId}", userId);
        }
    }

    // Métodos de conveniência para o futuro módulo de tickets ou outros eventos
    public async Task NotifyTicketCreatedAsync(Guid landlordId, Guid ticketId, string propertyTitle)
    {
        await SendNotificationAsync(landlordId, "ticket", $"Tens um novo ticket de manutenção para o imóvel '{propertyTitle}'.", ticketId);
    }
}
