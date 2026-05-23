using Amazon;
using Amazon.Runtime;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Shared.Services;

public class EmailService : IEmailService
{
    private const int DefaultSendTimeoutSeconds = 15;
    private static int _missingSmtpConfigLogged;
    private readonly IConfiguration _config;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IResendEmailSender _resendEmailSender;
    private readonly IAmazonSesEmailSender _amazonSesEmailSender;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IConfiguration config,
        IEmailTemplateService emailTemplateService,
        IResendEmailSender resendEmailSender,
        IAmazonSesEmailSender amazonSesEmailSender,
        ILogger<EmailService> logger)
    {
        _config = config;
        _emailTemplateService = emailTemplateService;
        _resendEmailSender = resendEmailSender;
        _amazonSesEmailSender = amazonSesEmailSender;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
        => await SendEmailAsync(to, subject, body, new EmailSendOptions());

    public async Task SendEmailAsync(string to, string subject, string body, EmailSendOptions options)
    {
        var useSmtp = ShouldUseSmtp();
        var fromAddress = options.FromAddress ?? _config["EmailSettings:FromAddress"];

        if (string.IsNullOrWhiteSpace(fromAddress))
            throw new InvalidOperationException("EmailSettings:FromAddress é obrigatório para envio de email.");

        var fromName = options.FromName ?? _config["EmailSettings:FromName"] ?? "Wekaza";
        var replyTo = options.ReplyToAddress ?? _config["EmailSettings:ReplyToAddress"];
        var htmlBody = _emailTemplateService.RenderTransactionalEmail(subject, body);
        var textBody = BuildPlainTextBody(body);
        var sendTimeoutSeconds = GetSendTimeoutSeconds();

        var emailMessage = new EmailProviderMessage(
            to,
            subject,
            htmlBody,
            textBody,
            fromAddress,
            fromName,
            replyTo);

        string provider;

        if (useSmtp)
        {
            provider = "SMTP";
            await SendViaSmtpAsync(emailMessage, sendTimeoutSeconds);
        }
        else
        {
            switch (GetApiProvider())
            {
                case EmailApiProvider.Resend:
                    provider = "Resend API";
                    await _resendEmailSender.SendAsync(emailMessage, sendTimeoutSeconds);
                    break;
                case EmailApiProvider.Ses:
                    provider = "SES API";
                    await _amazonSesEmailSender.SendAsync(emailMessage, sendTimeoutSeconds);
                    break;
                default:
                    throw new InvalidOperationException("Provider de email inválido.");
            }
        }

        _logger.LogInformation(
            "Email enviado para {Recipient} com assunto {Subject} via {Provider}",
            to,
            subject,
            provider);
    }

    private async Task SendViaSmtpAsync(EmailProviderMessage emailMessage, int sendTimeoutSeconds)
    {
        if (!TryGetSmtpSettings(out var settings))
        {
            var missingSettings = GetMissingSmtpSettingKeys();
            if (Interlocked.Exchange(ref _missingSmtpConfigLogged, 1) == 0)
            {
                _logger.LogWarning(
                    "EmailService com SMTP activo mas sem configuração completa. Faltam: {MissingSettings}",
                    string.Join(", ", missingSettings));
            }

            throw new InvalidOperationException(
                $"O envio de email por SMTP está ativo, mas faltam configurações obrigatórias: {string.Join(", ", missingSettings)}.");
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

    private EmailApiProvider GetApiProvider()
    {
        var configuredProvider = GetFirstNonEmpty("EMAIL_PROVIDER", "EmailSettings:Provider");

        if (!string.IsNullOrWhiteSpace(configuredProvider))
        {
            if (TryParseApiProvider(configuredProvider, out var provider))
                return provider;

            throw new InvalidOperationException("EmailSettings:Provider inválido. Usa 'Resend' ou 'Ses'.");
        }

        return string.IsNullOrWhiteSpace(GetFirstNonEmpty("RESEND_API_KEY", "EmailSettings:Resend:ApiKey"))
            ? EmailApiProvider.Ses
            : EmailApiProvider.Resend;
    }

    private static bool TryParseApiProvider(string value, out EmailApiProvider provider)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "resend":
                provider = EmailApiProvider.Resend;
                return true;
            case "ses":
            case "amazon":
            case "amazonses":
            case "amazon-ses":
            case "aws":
            case "aws-ses":
                provider = EmailApiProvider.Ses;
                return true;
            default:
                provider = default;
                return false;
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

    private IReadOnlyList<string> GetMissingSmtpSettingKeys()
    {
        var missingKeys = new List<string>();

        if (string.IsNullOrWhiteSpace(_config["EmailSettings:Host"]))
            missingKeys.Add("EmailSettings:Host");

        if (string.IsNullOrWhiteSpace(_config["EmailSettings:Username"]))
            missingKeys.Add("EmailSettings:Username");

        if (string.IsNullOrWhiteSpace(_config["EmailSettings:Password"]))
            missingKeys.Add("EmailSettings:Password");

        if (string.IsNullOrWhiteSpace(_config["EmailSettings:FromAddress"]))
            missingKeys.Add("EmailSettings:FromAddress");

        return missingKeys;
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

    private enum EmailApiProvider
    {
        Resend,
        Ses
    }
}

public interface IResendEmailSender
{
    Task SendAsync(EmailProviderMessage emailMessage, int sendTimeoutSeconds, CancellationToken cancellationToken = default);
}

public interface IAmazonSesEmailSender
{
    Task SendAsync(EmailProviderMessage emailMessage, int sendTimeoutSeconds, CancellationToken cancellationToken = default);
}

public sealed record EmailProviderMessage(
    string To,
    string Subject,
    string HtmlBody,
    string TextBody,
    string FromAddress,
    string FromName,
    string? ReplyToAddress);

public sealed class ResendEmailSender : IResendEmailSender
{
    private const string DefaultBaseUrl = "https://api.resend.com";
    private const string UserAgent = "TrustRent.Backend/EmailService";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(HttpClient httpClient, IConfiguration config, ILogger<ResendEmailSender> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(EmailProviderMessage emailMessage, int sendTimeoutSeconds, CancellationToken cancellationToken = default)
    {
        var apiKey = GetFirstNonEmpty("RESEND_API_KEY", "EmailSettings:Resend:ApiKey");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("EmailSettings:Resend:ApiKey é obrigatório para envio via Resend.");

        var baseUrl = GetFirstNonEmpty("RESEND_BASE_URL", "EmailSettings:Resend:BaseUrl") ?? DefaultBaseUrl;
        var endpoint = new Uri(new Uri(EnsureTrailingSlash(baseUrl)), "emails");

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(
                new ResendSendEmailRequest(
                    new MailAddress(emailMessage.FromAddress, emailMessage.FromName).ToString(),
                    new[] { emailMessage.To },
                    emailMessage.Subject,
                    emailMessage.HtmlBody,
                    emailMessage.TextBody,
                    emailMessage.ReplyToAddress),
                options: SerializerOptions)
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd(UserAgent);

        HttpResponseMessage response;

        try
        {
            response = await _httpClient
                .SendAsync(request, cancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(sendTimeoutSeconds), cancellationToken);
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(
                ex,
                "Timeout Resend API ao enviar email para {Recipient} com assunto {Subject} apos {TimeoutSeconds}s.",
                emailMessage.To,
                emailMessage.Subject,
                sendTimeoutSeconds);

            throw new InvalidOperationException("O serviço de email não respondeu a tempo. Tenta novamente.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(
                ex,
                "Pedido Resend cancelado por timeout ao enviar email para {Recipient} com assunto {Subject}.",
                emailMessage.To,
                emailMessage.Subject);

            throw new InvalidOperationException("O serviço de email não respondeu a tempo. Tenta novamente.", ex);
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.IsSuccessStatusCode)
            return;

        var apiError = TryParseError(responseBody);
        var errorCode = apiError?.Name ?? "unknown_error";
        var errorMessage = apiError?.Message ?? response.ReasonPhrase ?? "Erro desconhecido.";

        _logger.LogError(
            "Falha Resend API ao enviar email para {Recipient} com assunto {Subject}. Status {StatusCode}. Codigo {ErrorCode}. Mensagem {ErrorMessage}",
            emailMessage.To,
            emailMessage.Subject,
            (int)response.StatusCode,
            errorCode,
            errorMessage);

        throw new InvalidOperationException(
            GetUserFacingErrorMessage(response.StatusCode),
            new HttpRequestException($"Resend API devolveu {(int)response.StatusCode} ({errorCode}): {errorMessage}", null, response.StatusCode));
    }

    private static string EnsureTrailingSlash(string baseUrl)
        => baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : $"{baseUrl}/";

    private static string GetUserFacingErrorMessage(HttpStatusCode statusCode)
        => statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.UnprocessableEntity
                => "O serviço Resend rejeitou o pedido. Verifica a API key, o domínio e o endereço remetente configurados.",
            HttpStatusCode.TooManyRequests
                => "O serviço de email atingiu o limite de envio. Tenta novamente mais tarde.",
            HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
                => "O serviço de email está temporariamente indisponível. Tenta novamente.",
            _ => "O serviço de email rejeitou o pedido."
        };

    private static ResendErrorResponse? TryParseError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ResendErrorResponse>(responseBody);
        }
        catch (JsonException)
        {
            return null;
        }
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

    private sealed record ResendSendEmailRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] IReadOnlyList<string> To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("reply_to")] string? ReplyTo);

    private sealed record ResendErrorResponse(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("message")] string? Message);
}

public sealed class AmazonSesEmailSender : IAmazonSesEmailSender
{
    private const string DefaultSesRegion = "eu-north-1";
    private readonly IConfiguration _config;
    private readonly ILogger<AmazonSesEmailSender> _logger;

    public AmazonSesEmailSender(IConfiguration config, ILogger<AmazonSesEmailSender> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(EmailProviderMessage emailMessage, int sendTimeoutSeconds, CancellationToken cancellationToken = default)
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

        try
        {
            await client.SendEmailAsync(request, cancellationToken).WaitAsync(TimeSpan.FromSeconds(sendTimeoutSeconds), cancellationToken);
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
}
