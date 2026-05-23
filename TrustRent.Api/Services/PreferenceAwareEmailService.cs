using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Services;

namespace TrustRent.Api.Services;

public sealed class PreferenceAwareEmailService : IEmailService
{
    private readonly EmailService _innerEmailService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<PreferenceAwareEmailService> _logger;

    public PreferenceAwareEmailService(
        EmailService innerEmailService,
        IUserRepository userRepository,
        ILogger<PreferenceAwareEmailService> logger)
    {
        _innerEmailService = innerEmailService;
        _userRepository = userRepository;
        _logger = logger;
    }

    public Task SendEmailAsync(string to, string subject, string body)
        => SendEmailAsync(to, subject, body, new EmailSendOptions());

    public async Task SendEmailAsync(string to, string subject, string body, EmailSendOptions options)
    {
        if (!options.BypassUserNotificationPreferences)
        {
            var user = await _userRepository.GetByEmailAsync(to);
            if (user is not null && !user.EmailNotificationsEnabled)
            {
                _logger.LogInformation(
                    "Email suprimido para o utilizador {UserId} ({Email}) porque as notificações por email estão desativadas.",
                    user.Id,
                    user.Email);
                return;
            }
        }

        await _innerEmailService.SendEmailAsync(to, subject, body, options);
    }
}