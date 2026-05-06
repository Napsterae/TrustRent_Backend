using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace TrustRent.Modules.Communications.Services;

public class ExpoPushService : IExpoPushService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<ExpoPushService> _logger;

    public ExpoPushService(HttpClient httpClient, ILogger<ExpoPushService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<string>> SendNotificationAsync(
        IEnumerable<string> expoPushTokens,
        string title,
        string body,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        var tokens = expoPushTokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => token.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (tokens.Length == 0)
            return Array.Empty<string>();

        var messages = tokens.Select(token => new ExpoPushMessage(
            token,
            title,
            body,
            "default",
            "default",
            data)).ToArray();

        using var content = new StringContent(JsonSerializer.Serialize(messages, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync("push/send", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Expo push request failed with status code {StatusCode}", response.StatusCode);
            return Array.Empty<string>();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<ExpoPushResponse>(stream, JsonOptions, cancellationToken);
        if (payload?.Data == null || payload.Data.Count == 0)
            return Array.Empty<string>();

        var invalidTokens = new List<string>();
        for (var index = 0; index < Math.Min(tokens.Length, payload.Data.Count); index++)
        {
            var ticket = payload.Data[index];
            if (!string.Equals(ticket.Status, "error", StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(ticket.Details?.Error, "DeviceNotRegistered", StringComparison.OrdinalIgnoreCase))
                invalidTokens.Add(tokens[index]);
        }

        return invalidTokens;
    }

    private sealed record ExpoPushMessage(
        string To,
        string Title,
        string Body,
        string Sound,
        string ChannelId,
        object? Data);

    private sealed record ExpoPushResponse(List<ExpoPushTicket>? Data);

    private sealed record ExpoPushTicket(string Status, string? Message, ExpoPushTicketDetails? Details);

    private sealed record ExpoPushTicketDetails(string? Error);
}