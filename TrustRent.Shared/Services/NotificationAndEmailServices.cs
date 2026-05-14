using Amazon;
using Amazon.Runtime;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Shared.Services;

public class EmailService : IEmailService
{
    private const int DefaultSendTimeoutSeconds = 15;
    private const string DefaultSesRegion = "eu-north-1";
    private static int _missingSmtpConfigLogged;
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
        var useSmtp = ShouldUseSmtp();
        var provider = useSmtp ? "SMTP" : "SES API";
        var fromAddress = options.FromAddress ?? _config["EmailSettings:FromAddress"];

        if (string.IsNullOrWhiteSpace(fromAddress))
            throw new InvalidOperationException("EmailSettings:FromAddress é obrigatório para envio de email.");

        var fromName = options.FromName ?? _config["EmailSettings:FromName"] ?? "Wekaza";
        var replyTo = options.ReplyToAddress ?? _config["EmailSettings:ReplyToAddress"];
        var htmlBody = _emailTemplateService.RenderTransactionalEmail(subject, body);
        var textBody = BuildPlainTextBody(body);

        var emailMessage = new EmailMessage(
            to,
            subject,
            htmlBody,
            textBody,
            fromAddress,
            fromName,
            replyTo);

        if (useSmtp)
        {
            await SendViaSmtpAsync(emailMessage);
        }
        else
        {
            await SendViaSesApiAsync(emailMessage);
        }

        _logger.LogInformation(
            "Email enviado para {Recipient} com assunto {Subject} via {Provider}",
            to,
            subject,
            provider);
    }

    private async Task SendViaSmtpAsync(EmailMessage emailMessage)
    {
        if (!TryGetSmtpSettings(out var settings))
        {
            if (Interlocked.Exchange(ref _missingSmtpConfigLogged, 1) == 0)
            {
                _logger.LogWarning(
                    "EmailService sem configuração SMTP. Define EmailSettings:* e USE_SMTP=true para ativar o envio SMTP.");
            }

            _logger.LogInformation(
                "Email suprimido para {Recipient} com assunto {Subject} porque o SMTP não está configurado.",
                emailMessage.To,
                emailMessage.Subject);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(emailMessage.FromAddress, emailMessage.FromName),
            Subject = emailMessage.Subject,
            Body = emailMessage.TextBody,
            IsBodyHtml = false,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };

        message.To.Add(new MailAddress(emailMessage.To));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(emailMessage.TextBody, Encoding.UTF8, MediaTypeNames.Text.Plain));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(emailMessage.HtmlBody, Encoding.UTF8, MediaTypeNames.Text.Html));

        var replyTo = emailMessage.ReplyToAddress ?? settings.DefaultReplyToAddress;
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
            _logger.LogError(
                ex,
                "Timeout SMTP ao enviar email para {Recipient} com assunto {Subject} apos {TimeoutSeconds}s.",
                emailMessage.To,
                emailMessage.Subject,
                sendTimeoutSeconds);

            throw new InvalidOperationException("O serviço de email não respondeu a tempo. Tenta novamente.", ex);
        }
    }

    private async Task SendViaSesApiAsync(EmailMessage emailMessage)
    {
        using var client = CreateSesClient();

        var request = new SendEmailRequest
        {
            FromEmailAddress = new MailAddress(emailMessage.FromAddress, emailMessage.FromName).ToString(),
            Destination = new Destination
            {
                ToAddresses = new List<string> { emailMessage.To }
            },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Amazon.SimpleEmailV2.Model.Content
                    {
                        Data = emailMessage.Subject,
                        Charset = Encoding.UTF8.WebName
                    },
                    Body = new Body
                    {
                        Text = new Amazon.SimpleEmailV2.Model.Content
                        {
                            Data = emailMessage.TextBody,
                            Charset = Encoding.UTF8.WebName
                        },
                        Html = new Amazon.SimpleEmailV2.Model.Content
                        {
                            Data = emailMessage.HtmlBody,
                            Charset = Encoding.UTF8.WebName
                        }
                    }
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(emailMessage.ReplyToAddress))
            request.ReplyToAddresses = new List<string> { emailMessage.ReplyToAddress };

        var sendTimeoutSeconds = GetSendTimeoutSeconds();
        try
        {
            await client.SendEmailAsync(request).WaitAsync(TimeSpan.FromSeconds(sendTimeoutSeconds));
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(
                ex,
                "Timeout SES API ao enviar email para {Recipient} com assunto {Subject} apos {TimeoutSeconds}s.",
                emailMessage.To,
                emailMessage.Subject,
                sendTimeoutSeconds);

            throw new InvalidOperationException("O serviço de email não respondeu a tempo. Tenta novamente.", ex);
        }
        catch (AmazonSimpleEmailServiceV2Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha SES API ao enviar email para {Recipient} com assunto {Subject}. Codigo AWS: {ErrorCode}.",
                emailMessage.To,
                emailMessage.Subject,
                ex.ErrorCode);

            throw new InvalidOperationException("O serviço de email não está configurado corretamente.", ex);
        }
        catch (AmazonClientException ex)
        {
            _logger.LogError(
                ex,
                "Falha de cliente AWS ao enviar email para {Recipient} com assunto {Subject}.",
                emailMessage.To,
                emailMessage.Subject);

            throw new InvalidOperationException("Não foi possível contactar o serviço de email.", ex);
        }
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

    private AmazonSimpleEmailServiceV2Client CreateSesClient()
    {
        var regionName = GetFirstNonEmpty(
                             "AWS_REGION",
                             "AWS:Region",
                             "AWS_DEFAULT_REGION",
                             "EmailSettings:AwsRegion")
                         ?? DefaultSesRegion;

        var regionEndpoint = RegionEndpoint.GetBySystemName(regionName);

        var accessKeyId = GetFirstNonEmpty(
            "AWS_ACCESS_KEY_ID",
            "AWS:AccessKey",
            "EmailSettings:AwsAccessKeyId");
        var secretAccessKey = GetFirstNonEmpty(
            "AWS_SECRET_ACCESS_KEY",
            "AWS:SecretKey",
            "EmailSettings:AwsSecretAccessKey");
        var sessionToken = GetFirstNonEmpty(
            "AWS_SESSION_TOKEN",
            "EmailSettings:AwsSessionToken");

        if (!string.IsNullOrWhiteSpace(accessKeyId) && !string.IsNullOrWhiteSpace(secretAccessKey))
        {
            AWSCredentials credentials = string.IsNullOrWhiteSpace(sessionToken)
                ? new BasicAWSCredentials(accessKeyId, secretAccessKey)
                : new SessionAWSCredentials(accessKeyId, secretAccessKey, sessionToken);

            return new AmazonSimpleEmailServiceV2Client(credentials, regionEndpoint);
        }

        return new AmazonSimpleEmailServiceV2Client(regionEndpoint);
    }

    private bool ShouldUseSmtp()
        => GetBooleanSetting(defaultValue: false, "USE_SMTP", "USE_MTP", "EmailSettings:UseSmtp");

    private int GetSendTimeoutSeconds()
    {
        var configured = int.TryParse(_config["EmailSettings:SendTimeoutSeconds"], out var parsed)
            ? parsed
            : DefaultSendTimeoutSeconds;

        return Math.Clamp(configured, 5, 120);
    }

    private string? GetFirstNonEmpty(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = _config[key];
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private bool GetBooleanSetting(bool defaultValue, params string[] keys)
    {
        var value = GetFirstNonEmpty(keys);
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        if (bool.TryParse(value, out var parsed))
            return parsed;

        return value switch
        {
            "1" => true,
            "0" => false,
            _ => defaultValue
        };
    }

    private static string BuildPlainTextBody(string body)
    {
        var source = string.IsNullOrWhiteSpace(body) ? "Sem conteúdo." : body;
        var withLineBreaks = Regex.Replace(source, @"<\s*br\s*/?>", "\n", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        withLineBreaks = Regex.Replace(withLineBreaks, @"<\s*/p\s*>", "\n\n", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        withLineBreaks = Regex.Replace(withLineBreaks, @"<\s*/div\s*>", "\n", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var noTags = Regex.Replace(withLineBreaks, @"<[^>]+>", " ", RegexOptions.CultureInvariant);
        var decoded = WebUtility.HtmlDecode(noTags)
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');

        var normalized = Regex.Replace(decoded, @"[ \t]+", " ", RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @" ?\n ?", "\n", RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n", RegexOptions.CultureInvariant).Trim();

        return string.IsNullOrWhiteSpace(normalized)
            ? "Sem conteúdo."
            : normalized;
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

    private sealed record EmailMessage(
        string To,
        string Subject,
        string HtmlBody,
        string TextBody,
        string FromAddress,
        string FromName,
        string? ReplyToAddress);
}
