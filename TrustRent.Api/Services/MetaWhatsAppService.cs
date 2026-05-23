using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Api.Services;

public sealed class MetaWhatsAppService : IWhatsAppService
{
    private const string DefaultApiVersion = "v22.0";
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly AdminDbContext _adminDb;
    private readonly ILogger<MetaWhatsAppService> _logger;

    public MetaWhatsAppService(HttpClient httpClient, IConfiguration config, AdminDbContext adminDb, ILogger<MetaWhatsAppService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _adminDb = adminDb;
        _logger = logger;
    }

    public Task SendLoginCodeAsync(string phoneNumber, string code, CancellationToken cancellationToken = default)
        => SendTemplateMessageAsync(phoneNumber, GetRequiredTemplateName("WhatsAppSettings:Templates:LoginCode"), [code], cancellationToken);

    public Task SendPhoneVerificationCodeAsync(string phoneNumber, string code, CancellationToken cancellationToken = default)
        => SendTemplateMessageAsync(phoneNumber, GetRequiredTemplateName("WhatsAppSettings:Templates:PhoneVerification"), [code], cancellationToken);

    public Task SendNotificationAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
        => SendTemplateMessageAsync(phoneNumber, GetRequiredTemplateName("WhatsAppSettings:Templates:Notification"), [message], cancellationToken);

    private async Task SendTemplateMessageAsync(string phoneNumber, string templateName, IReadOnlyList<string> bodyParameters, CancellationToken cancellationToken)
    {
        var mode = (_config["WhatsAppSettings:Mode"] ?? "Mock").Trim();
        if (string.Equals(mode, "Disabled", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("O canal WhatsApp está desativado.");

        var senderNumber = await _adminDb.PlatformSettings
            .Where(setting => setting.Key == "whatsapp.sender_number")
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.Equals(mode, "Mock", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "[WHATSAPP MOCK] Mensagem template {Template} enviada para {PhoneNumber} a partir de {SenderNumber}. Parâmetros: {Parameters}",
                templateName,
                phoneNumber,
                senderNumber ?? "não configurado",
                string.Join(", ", bodyParameters));
            return;
        }

        var accessToken = _config["WhatsAppSettings:AccessToken"];
        var phoneNumberId = _config["WhatsAppSettings:PhoneNumberId"];
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(phoneNumberId))
            throw new InvalidOperationException("WhatsAppSettings:AccessToken e WhatsAppSettings:PhoneNumberId são obrigatórios para envio real.");

        var apiVersion = _config["WhatsAppSettings:ApiVersion"] ?? DefaultApiVersion;
        var languageCode = _config["WhatsAppSettings:LanguageCode"] ?? "pt_PT";
        IReadOnlyList<WhatsAppTemplateComponent>? components = null;

        if (bodyParameters.Count > 0)
        {
            components =
            [
                new WhatsAppTemplateComponent(
                    "body",
                    bodyParameters.Select(value => new WhatsAppTemplateParameter("text", value)).ToArray())
            ];
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/{apiVersion}/{phoneNumberId}/messages")
        {
            Content = JsonContent.Create(new WhatsAppTemplateRequest(
                "whatsapp",
                NormalizeRecipient(phoneNumber),
                "template",
                new WhatsAppTemplatePayload(
                    templateName,
                    new WhatsAppTemplateLanguage(languageCode),
                    components)))
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return;

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError(
            "Falha ao enviar mensagem WhatsApp para {PhoneNumber}. Status {StatusCode}. Body {Body}",
            phoneNumber,
            (int)response.StatusCode,
            responseBody);

        throw new InvalidOperationException(GetUserFacingError(response.StatusCode));
    }

    private string GetRequiredTemplateName(string configKey)
    {
        var templateName = _config[configKey];
        if (!string.IsNullOrWhiteSpace(templateName))
            return templateName;

        throw new InvalidOperationException($"{configKey} é obrigatório para envio de mensagens WhatsApp.");
    }

    private static string NormalizeRecipient(string phoneNumber)
        => new string(phoneNumber.Where(char.IsDigit).ToArray());

    private static string GetUserFacingError(HttpStatusCode statusCode)
        => statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                => "O serviço de WhatsApp rejeitou as credenciais configuradas.",
            HttpStatusCode.TooManyRequests
                => "O serviço de WhatsApp atingiu o limite de envio. Tenta novamente mais tarde.",
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity
                => "O serviço de WhatsApp rejeitou a mensagem. Verifica os templates e o número configurados.",
            _ => "Não foi possível enviar a mensagem por WhatsApp neste momento."
        };

    private sealed record WhatsAppTemplateRequest(
        [property: JsonPropertyName("messaging_product")] string MessagingProduct,
        [property: JsonPropertyName("to")] string To,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("template")] WhatsAppTemplatePayload Template);

    private sealed record WhatsAppTemplatePayload(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("language")] WhatsAppTemplateLanguage Language,
        [property: JsonPropertyName("components")] IReadOnlyList<WhatsAppTemplateComponent>? Components);

    private sealed record WhatsAppTemplateLanguage(
        [property: JsonPropertyName("code")] string Code);

    private sealed record WhatsAppTemplateComponent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("parameters")] IReadOnlyList<WhatsAppTemplateParameter> Parameters);

    private sealed record WhatsAppTemplateParameter(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string Text);
}