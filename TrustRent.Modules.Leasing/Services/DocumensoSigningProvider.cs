using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TrustRent.Modules.Leasing.Contracts.Interfaces;

namespace TrustRent.Modules.Leasing.Services;

/// <summary>
/// Documenso signing provider implementation (self-hosted).
/// Calls Documenso REST API v2 over HTTP.
/// </summary>
public class DocumensoSigningProvider : ISigningProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DocumensoSigningProvider> _logger;

    /// <summary>
    /// Cache: ExternalRequestId (documentId) -> (email -> recipientId).
    /// Static to survive across all instances; entries never evicted because
    /// signing documents are finite and short-lived by nature.
    /// </summary>
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _recipientCache = new();

    public DocumensoSigningProvider(string baseUrl, string apiKey, ILogger<DocumensoSigningProvider> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/")
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public string Name => "Documenso";

    public async Task<InitiateSigningResult> InitiateSigningAsync(
        byte[] contractPdf,
        string documentName,
        List<SignerInfo> signers)
    {
        var base64Pdf = Convert.ToBase64String(contractPdf);

        var payload = new
        {
            name = documentName,
            file = base64Pdf,
            recipients = signers.Select(s => new
            {
                email = s.Email,
                name = s.Name,
                order = s.SequenceOrder
            }).ToList(),
            meta = new
            {
                signingProvider = "Documenso"
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation(
            "Documenso: creating document '{DocumentName}' with {SignerCount} signer(s).",
            documentName, signers.Count);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync("api/v2/documents", content);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Documenso HTTP request failed during InitiateSigningAsync.");
            throw;
        }

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Documenso POST /api/v2/documents failed: {StatusCode} — {Body}",
                (int)response.StatusCode, responseBody);
            throw new HttpRequestException(
                $"Documenso document creation failed ({(int)response.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        var documentId = root.GetProperty("id").GetString()!;
        var createdAt = root.TryGetProperty("createdAt", out var ca)
            ? ca.GetDateTime()
            : DateTime.UtcNow;

        // Default expiration: 30 days from creation
        var expiresAt = createdAt.AddDays(30);

        // Extract recipient IDs from the response
        var recipientMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("recipients", out var recipients))
        {
            foreach (var r in recipients.EnumerateArray())
            {
                var email = r.GetProperty("email").GetString()!;
                var recipientId = r.GetProperty("id").GetString()!;
                recipientMap[email] = recipientId;
            }
        }

        // Cache the mapping so GetEmbeddedUrlAsync can look up recipientId by email
        _recipientCache[documentId] = recipientMap;

        _logger.LogInformation(
            "Documenso: document created with id={DocumentId}, {RecipientCount} recipient(s).",
            documentId, recipientMap.Count);

        return new InitiateSigningResult(
            documentId,
            expiresAt,
            recipientMap);
    }

    public async Task<EmbeddedUrlResult> GetEmbeddedUrlAsync(
        string externalRequestId,
        string signerEmail)
    {
        // Look up recipient ID from the cache populated during InitiateSigningAsync
        if (!_recipientCache.TryGetValue(externalRequestId, out var recipientMap)
            || !recipientMap.TryGetValue(signerEmail, out var recipientId))
        {
            _logger.LogWarning(
                "Documenso: recipient ID not found in cache for document {DocumentId}, email {Email}. " +
                "Falling back to fetching recipients from API.",
                externalRequestId, signerEmail);

            // Fallback: fetch the document's recipients from the API
            recipientId = await FetchRecipientIdAsync(externalRequestId, signerEmail);
            if (recipientId == null)
            {
                throw new InvalidOperationException(
                    $"Documenso: signer with email '{signerEmail}' not found on document '{externalRequestId}'.");
            }
        }

        _logger.LogInformation(
            "Documenso: requesting embedded-sign-url for document {DocumentId}, recipient {RecipientId}.",
            externalRequestId, recipientId);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(
                $"api/v2/documents/{externalRequestId}/recipients/{recipientId}/embedded-sign-url",
                new StringContent("{}", Encoding.UTF8, "application/json"));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Documenso HTTP request failed during GetEmbeddedUrlAsync.");
            throw;
        }

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Documenso POST /api/v2/documents/{Id}/recipients/{RecipientId}/embedded-sign-url failed: {StatusCode} — {Body}",
                externalRequestId, recipientId, (int)response.StatusCode, responseBody);
            throw new HttpRequestException(
                $"Documenso embedded URL request failed ({(int)response.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        var embeddedUrl = root.GetProperty("embeddedUrl").GetString()!;
        var expiresAt = root.TryGetProperty("expiresAt", out var ea)
            ? ea.GetDateTime()
            : DateTime.UtcNow.AddHours(1);

        return new EmbeddedUrlResult(embeddedUrl, expiresAt);
    }

    public async Task<byte[]> DownloadSignedDocumentAsync(string externalRequestId)
    {
        _logger.LogInformation(
            "Documenso: downloading signed document {DocumentId}.",
            externalRequestId);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync($"api/v2/documents/{externalRequestId}/signed-document");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Documenso HTTP request failed during DownloadSignedDocumentAsync.");
            throw;
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Documenso GET /api/v2/documents/{Id}/signed-document failed: {StatusCode} — {Body}",
                externalRequestId, (int)response.StatusCode, errorBody);
            throw new HttpRequestException(
                $"Documenso signed document download failed ({(int)response.StatusCode}).");
        }

        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<ProviderSigningStatus> GetStatusAsync(string externalRequestId)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync($"api/v2/documents/{externalRequestId}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Documenso HTTP request failed during GetStatusAsync.");
            throw;
        }

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Documenso GET /api/v2/documents/{Id} failed: {StatusCode} — {Body}",
                externalRequestId, (int)response.StatusCode, responseBody);
            throw new HttpRequestException(
                $"Documenso status request failed ({(int)response.StatusCode}).");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        var statusStr = root.GetProperty("status").GetString()!;

        var status = statusStr.ToUpperInvariant() switch
        {
            "PENDING" => ProviderSigningStatus.Pending,
            "PARTIALLY_SIGNED" => ProviderSigningStatus.PartiallySigned,
            "COMPLETED" => ProviderSigningStatus.Completed,
            "REJECTED" => ProviderSigningStatus.Rejected,
            "EXPIRED" => ProviderSigningStatus.Expired,
            _ => ProviderSigningStatus.Pending
        };

        _logger.LogInformation(
            "Documenso: document {DocumentId} status = {Status}.",
            externalRequestId, statusStr);

        return status;
    }

    public bool VerifyWebhookSignature(string requestBody, string signatureHeader, string secret)
    {
        if (string.IsNullOrEmpty(signatureHeader))
            return false;

        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var bodyBytes = Encoding.UTF8.GetBytes(requestBody);

        using var hmac = new HMACSHA256(secretBytes);
        var computedHash = hmac.ComputeHash(bodyBytes);
        var computedSignature = Convert.ToBase64String(computedHash);

        // Constant-time comparison to prevent timing attacks
        var result = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature),
            Encoding.UTF8.GetBytes(signatureHeader));

        if (!result)
        {
            _logger.LogWarning(
                "Documenso webhook HMAC verification failed. Expected signature: {Expected}",
                computedSignature);
        }

        return result;
    }

    public WebhookEvent ParseWebhookEvent(string requestBody)
    {
        using var doc = JsonDocument.Parse(requestBody);
        var root = doc.RootElement;

        var eventTypeStr = root.GetProperty("type").GetString() ?? string.Empty;
        var documentId = root.TryGetProperty("documentId", out var di)
            ? di.GetString() ?? string.Empty
            : string.Empty;
        var recipientEmail = root.TryGetProperty("recipientEmail", out var re)
            ? re.GetString() ?? string.Empty
            : string.Empty;
        var recipientId = root.TryGetProperty("recipientId", out var ri)
            ? ri.GetString()
            : null;
        var triggeredAt = root.TryGetProperty("triggeredAt", out var ta)
            ? ta.GetDateTime()
            : (DateTime?)null;

        var eventType = eventTypeStr switch
        {
            "DOCUMENT_SIGNED" => WebhookEventType.SignerCompleted,
            "DOCUMENT_COMPLETED" => WebhookEventType.AllCompleted,
            "DOCUMENT_REJECTED" => WebhookEventType.Rejected,
            "DOCUMENT_EXPIRED" => WebhookEventType.Expired,
            _ => WebhookEventType.SignerCompleted
        };

        _logger.LogInformation(
            "Documenso webhook parsed: type={EventType}, documentId={DocumentId}, recipient={RecipientEmail}.",
            eventTypeStr, documentId, recipientEmail);

        return new WebhookEvent(
            documentId,
            recipientEmail,
            eventType,
            triggeredAt,
            recipientId);
    }

    /// <summary>
    /// Fallback: fetches the list of recipients for a document and returns
    /// the recipient ID matching the given email. Populates the cache as a side effect.
    /// </summary>
    private async Task<string?> FetchRecipientIdAsync(string externalRequestId, string email)
    {
        // Documenso may expose GET /api/v2/documents/{id}/recipients
        // If not available, we try the document detail which includes recipients.
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync($"api/v2/documents/{externalRequestId}");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Documenso HTTP request failed during FetchRecipientIdAsync.");
            return null;
        }

        if (!response.IsSuccessStatusCode)
            return null;

        var responseBody = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        if (!root.TryGetProperty("recipients", out var recipients))
            return null;

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? foundId = null;

        foreach (var r in recipients.EnumerateArray())
        {
            var rEmail = r.GetProperty("email").GetString()!;
            var rId = r.GetProperty("id").GetString()!;
            map[rEmail] = rId;

            if (string.Equals(rEmail, email, StringComparison.OrdinalIgnoreCase))
            {
                foundId = rId;
            }
        }

        // Update cache with full map
        _recipientCache[externalRequestId] = map;

        return foundId;
    }
}
