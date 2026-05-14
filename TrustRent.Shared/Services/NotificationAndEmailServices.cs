using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Shared.Services;

public class EmailService : IEmailService
{
    private const int DefaultSendTimeoutSeconds = 15;
    private static int _missingConfigLogged;
    private readonly IConfiguration _config;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, IEmailTemplateService emailTemplateService, ILogger<EmailService> logger)
    {
        _config = config;
        _emailTemplateService = emailTemplateService;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
        => await SendEmailAsync(to, subject, body, new EmailSendOptions());

    public async Task SendEmailAsync(string to, string subject, string body, EmailSendOptions options)
    {
        if (!TryGetSmtpSettings(out var settings))
        {
            if (Interlocked.Exchange(ref _missingConfigLogged, 1) == 0)
                _logger.LogWarning("EmailService sem configuração SMTP. Define EmailSettings:* para ativar envio real via Amazon SES.");

            _logger.LogInformation("Email suprimido para {Recipient} com assunto {Subject} porque o SMTP não está configurado.", to, subject);
            return;
        }

        var fromAddress = options.FromAddress ?? settings.DefaultFromAddress;
        if (string.IsNullOrWhiteSpace(fromAddress))
            throw new InvalidOperationException("EmailSettings:FromAddress é obrigatório para envio SMTP.");

        var fromName = options.FromName ?? settings.DefaultFromName;
        var htmlBody = _emailTemplateService.RenderTransactionalEmail(subject, body);

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, fromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };

        message.To.Add(new MailAddress(to));

        var replyTo = options.ReplyToAddress ?? settings.DefaultReplyToAddress;
        if (!string.IsNullOrWhiteSpace(replyTo))
            message.ReplyToList.Add(new MailAddress(replyTo));

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            EnableSsl = settings.UseStartTls,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(settings.Username, settings.Password)
        };

        var sendTimeoutSeconds = GetSendTimeoutSeconds();
        try
        {
            await client.SendMailAsync(message).WaitAsync(TimeSpan.FromSeconds(sendTimeoutSeconds));
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex,
                "Timeout SMTP ao enviar email para {Recipient} com assunto {Subject} apos {TimeoutSeconds}s.",
                to,
                subject,
                sendTimeoutSeconds);

            throw new InvalidOperationException("O serviço de email não respondeu a tempo. Tenta novamente.", ex);
        }

        _logger.LogInformation("Email enviado para {Recipient} com assunto {Subject}", to, subject);
    }

    private bool TryGetSmtpSettings(out SmtpSettings settings)
    {
        var host = _config["EmailSettings:Host"];
        var username = _config["EmailSettings:Username"];
        var password = _config["EmailSettings:Password"];
        var defaultFromAddress = _config["EmailSettings:FromAddress"];
        var port = int.TryParse(_config["EmailSettings:Port"], out var parsedPort) ? parsedPort : 587;
        var useStartTls = !bool.TryParse(_config["EmailSettings:UseStartTls"], out var parsedTls) || parsedTls;

        settings = new SmtpSettings(
            host ?? string.Empty,
            port,
            username ?? string.Empty,
            password ?? string.Empty,
            defaultFromAddress ?? string.Empty,
            _config["EmailSettings:FromName"] ?? "Wekaza",
            _config["EmailSettings:ReplyToAddress"],
            useStartTls);

        return !string.IsNullOrWhiteSpace(settings.Host)
               && !string.IsNullOrWhiteSpace(settings.Username)
               && !string.IsNullOrWhiteSpace(settings.Password)
               && !string.IsNullOrWhiteSpace(settings.DefaultFromAddress);
    }

    private int GetSendTimeoutSeconds()
    {
        var configured = int.TryParse(_config["EmailSettings:SendTimeoutSeconds"], out var parsed)
            ? parsed
            : DefaultSendTimeoutSeconds;

        return Math.Clamp(configured, 5, 120);
    }

    private sealed record SmtpSettings(
        string Host,
        int Port,
        string Username,
        string Password,
        string DefaultFromAddress,
        string DefaultFromName,
        string? DefaultReplyToAddress,
        bool UseStartTls);
}
