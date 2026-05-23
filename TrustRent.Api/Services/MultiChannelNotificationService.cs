using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Identity.Models;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Api.Services;

public sealed class MultiChannelNotificationService : INotificationService
{
    private readonly TrustRent.Modules.Communications.Services.NotificationService _innerNotificationService;
    private readonly IUserRepository _userRepository;
    private readonly IWhatsAppService _whatsAppService;
    private readonly ITelegramMessagingPlatformService? _telegramMessagingPlatformService;
    private readonly ILogger<MultiChannelNotificationService> _logger;

    public MultiChannelNotificationService(
        TrustRent.Modules.Communications.Services.NotificationService innerNotificationService,
        IUserRepository userRepository,
        IWhatsAppService whatsAppService,
        ITelegramMessagingPlatformService? telegramMessagingPlatformService,
        ILogger<MultiChannelNotificationService> logger)
    {
        _innerNotificationService = innerNotificationService;
        _userRepository = userRepository;
        _whatsAppService = whatsAppService;
        _telegramMessagingPlatformService = telegramMessagingPlatformService;
        _logger = logger;
    }

    public async Task SendNotificationAsync(Guid userId, string type, string message, Guid? referenceId = null)
    {
        await _innerNotificationService.SendNotificationAsync(userId, type, message, referenceId);

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null
            || !user.WhatsAppNotificationsEnabled
            || !user.IsPhoneNumberVerified
            || string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            return;
        }

        try
        {
            var platform = PhoneContactPlatforms.Normalize(user.PhoneContactPlatform, PhoneContactPlatforms.Telegram);
            if (platform == PhoneContactPlatforms.Telegram)
            {
                if (_telegramMessagingPlatformService == null)
                    return;

                await _telegramMessagingPlatformService.SendNotificationAsync(user, message);
            }
            else
            {
                await _whatsAppService.SendNotificationAsync(user.PhoneNumber, message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao enviar notificação na plataforma de contacto do utilizador {UserId}", userId);
        }
    }
}