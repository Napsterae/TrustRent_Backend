using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TrustRent.Modules.Leasing.Contracts.Interfaces;

namespace TrustRent.Modules.Leasing.Services;

/// <summary>
/// DocuSeal signing provider implementation (cloud).
/// Calls DocuSeal REST API (api.docuseal.eu) to manage signing submissions
/// and handle webhook events.
/// </summary>
public class DocuSealSigningProvider : ISigningProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly bool _qesEnabled;
    private readonly ILogger<DocuSealSigningProvider> _logger;

    /// <summary>
    /// Cache of submitter info (id, email, embed_url) per submission,
    /// populated during InitiateSigningAsync and used by GetEmbeddedUrlAsync.
    /// </summary>
    private readonly ConcurrentDictionary<string, List<SubmitterEmbedInfo>> _submittersCache = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public DocuSealSigningProvider(
        string baseUrl,
        string apiKey,
        bool qesEnabled,
        HttpClient httpClient,
        ILogger<DocuSealSigningProvider> logger)
    {
        _apiKey = apiKey;
        _qesEnabled = qesEnabled;
        _httpClient = httpClient;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _httpClient.DefaultRequestHeaders.Add("X-Auth-Token", apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public string Name => "DocuSeal";

    /// <summary>
    /// Creates a DocuSeal submission with all signers in preserved (sequential) order.
    /// POST /api/v1/submissions
    /// </summary>
    public async Task<InitiateSigningResult> InitiateSigningAsync(
        byte[] contractPdf,
        string documentName,
        List<SignerInfo> signers)
    {
        try
        {
            var pdfBase64 = Convert.ToBase64String(contractPdf);

            var submitters = signers.Select(s => new
            {
                email = s.Email,
                name = s.Name,
                order = s.SequenceOrder,
                qes = _qesEnabled ? true : (bool?)null
            }).ToList();

            var payload = new
            {
                send_email = false,
                order = "preserved",
                submitters,
                documents = new[]
                {
                    new { name = documentName, file = pdfBase64 }
                }
            };

            var json = JsonSerializer.Serialize(payload, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation(
                "Creating DocuSeal submission for document '{DocumentName}' with {SignerCount} signer(s). QES={QesEnabled}",
                documentName, signers.Count, _qesEnabled);

            var response = await _httpClient.PostAsync("api/v1/submissions", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "DocuSeal API error creating submission: {StatusCode} - {Body}",
                    (int)response.StatusCode, errorBody);
                throw new HttpRequestException(
                    $"DocuSeal submission creation failed ({(int)response.StatusCode}): {errorBody}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            using var submissionDoc = JsonDocument.Parse(responseBody);
            var root = submissionDoc.RootElement;

            // Extract the submission id (returns as integer)
            var submissionId = root.GetProperty("id").GetInt32().ToString();

            // Extract expiration date
            var expiresAt = DateTime.UtcNow.AddDays(180); // sensible default
            if (root.TryGetProperty("expires_at", out var expiresProp) &&
                expiresProp.ValueKind == JsonValueKind.String)
            {
                expiresAt = expiresProp.GetDateTime();
            }

            // Cache submitters for later embed_url lookups
            var submitterList = new List<SubmitterEmbedInfo>();
            if (root.TryGetProperty("submitters", out var submittersArray))
            {
                foreach (var submitter in submittersArray.EnumerateArray())
                {
                    var info = new SubmitterEmbedInfo
                    {
                        Id = submitter.GetProperty("id").GetString()!,
                        Email = submitter.GetProperty("email").GetString()!,
                        EmbedUrl = submitter.TryGetProperty("embed_url", out var embedUrl)
                            ? embedUrl.GetString()
                            : null
                    };
                    submitterList.Add(info);
                }
                _submittersCache[submissionId] = submitterList;
            }

            _logger.LogInformation(
                "DocuSeal submission {SubmissionId} created with {SubmitterCount} submitter(s). Expires {ExpiresAt}.",
                submissionId, submitterList.Count, expiresAt);

            return new InitiateSigningResult(submissionId, expiresAt);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create DocuSeal submission for document '{DocumentName}'.", documentName);
            throw;
        }
    }

    /// <summary>
    /// Gets the embedded signing URL for a specific signer by email.
    /// First checks the in-memory cache (populated during InitiateSigningAsync);
    /// if not found, fetches the submission from the API.
    /// </summary>
    public async Task<EmbeddedUrlResult> GetEmbeddedUrlAsync(
        string externalRequestId,
        string signerEmail)
    {
        // Try cache first
        if (_submittersCache.TryGetValue(externalRequestId, out var submitters))
        {
            var match = submitters.FirstOrDefault(s =>
                string.Equals(s.Email, signerEmail, StringComparison.OrdinalIgnoreCase));

            if (match?.EmbedUrl != null)
            {
                return new EmbeddedUrlResult(match.EmbedUrl, DateTime.UtcNow.AddDays(30));
            }
        }

        // Fallback: fetch submission from API
        return await GetEmbeddedUrlFromApiAsync(externalRequestId, signerEmail);
    }

    private async Task<EmbeddedUrlResult> GetEmbeddedUrlFromApiAsync(
        string externalRequestId, string signerEmail)
    {
        try
        {
            _logger.LogInformation(
                "Fetching submission {Id} from DocuSeal to resolve embed URL for {Email}.",
                externalRequestId, signerEmail);

            var response = await _httpClient.GetAsync($"api/v1/submissions/{externalRequestId}");

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "DocuSeal API error fetching submission {Id}: {StatusCode} - {Body}",
                    externalRequestId, (int)response.StatusCode, errorBody);
                throw new HttpRequestException(
                    $"DocuSeal GET submission failed ({(int)response.StatusCode}): {errorBody}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("submitters", out var submittersArray))
            {
                foreach (var submitter in submittersArray.EnumerateArray())
                {
                    var email = submitter.GetProperty("email").GetString();
                    if (string.Equals(email, signerEmail, StringComparison.OrdinalIgnoreCase))
                    {
                        var embedUrl = submitter.TryGetProperty("embed_url", out var url)
                            ? url.GetString()
                            : null;

                        if (!string.IsNullOrEmpty(embedUrl))
                        {
                            _logger.LogInformation(
                                "Resolved embed URL for signer {Email} in submission {Id}.",
                                signerEmail, externalRequestId);
                            return new EmbeddedUrlResult(embedUrl, DateTime.UtcNow.AddDays(30));
                        }

                        throw new InvalidOperationException(
                            $"Submitter '{signerEmail}' found in submission {externalRequestId} but embed_url is empty.");
                    }
                }
            }

            throw new KeyNotFoundException(
                $"Submitter with email '{signerEmail}' not found in submission {externalRequestId}.");
        }
        catch (KeyNotFoundException)
        {
            throw;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get embed URL for submission {Id}, email {Email}.",
                externalRequestId, signerEmail);
            throw;
        }
    }

    /// <summary>
    /// Downloads the signed PDF from DocuSeal.
    /// GET /api/v1/submissions/{id}/download
    /// </summary>
    public async Task<byte[]> DownloadSignedDocumentAsync(string externalRequestId)
    {
        try
        {
            _logger.LogInformation("Downloading signed document for submission {Id}.", externalRequestId);

            var response = await _httpClient.GetAsync($"api/v1/submissions/{externalRequestId}/download");

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "DocuSeal API error downloading submission {Id}: {StatusCode} - {Body}",
                    externalRequestId, (int)response.StatusCode, errorBody);
                throw new HttpRequestException(
                    $"DocuSeal download failed ({(int)response.StatusCode}): {errorBody}");
            }

            var pdfBytes = await response.Content.ReadAsByteArrayAsync();

            _logger.LogInformation(
                "Downloaded signed document for submission {Id}: {Length} bytes.",
                externalRequestId, pdfBytes.Length);

            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download signed document for submission {Id}.", externalRequestId);
            throw;
        }
    }

    /// <summary>
    /// Gets the current signing status from DocuSeal.
    /// GET /api/v1/submissions/{id}
    /// </summary>
    public async Task<ProviderSigningStatus> GetStatusAsync(string externalRequestId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/v1/submissions/{externalRequestId}");

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "DocuSeal API error fetching submission status {Id}: {StatusCode} - {Body}",
                    externalRequestId, (int)response.StatusCode, errorBody);
                throw new HttpRequestException(
                    $"DocuSeal GET submission status failed ({(int)response.StatusCode}): {errorBody}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var status = root.GetProperty("status").GetString();

            return status switch
            {
                "completed" => ProviderSigningStatus.Completed,
                "partially_signed" => ProviderSigningStatus.PartiallySigned,
                "expired" => ProviderSigningStatus.Expired,
                "rejected" => ProviderSigningStatus.Rejected,
                _ => ProviderSigningStatus.Pending
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get status for submission {Id}.", externalRequestId);
            throw;
        }
    }

    /// <summary>
    /// Verifies a DocuSeal webhook HMAC-SHA256 signature.
    /// DocuSeal sends the signature in the "DocuSeal-Signature" header
    /// as "sha256=hexdigest" — the hex digest is the HMAC-SHA256 of
    /// the raw request body keyed with the webhook secret.
    /// </summary>
    public bool VerifyWebhookSignature(string requestBody, string signatureHeader, string secret)
    {
        if (string.IsNullOrEmpty(signatureHeader) || string.IsNullOrEmpty(secret))
        {
            _logger.LogWarning("DocuSeal webhook verification skipped: missing signature header or secret.");
            return false;
        }

        try
        {
            // Extract hex digest — DocuSeal may prefix with "sha256="
            var providedDigest = signatureHeader.Trim();
            if (providedDigest.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            {
                providedDigest = providedDigest["sha256=".Length..];
            }

            var secretBytes = Encoding.UTF8.GetBytes(secret);
            var bodyBytes = Encoding.UTF8.GetBytes(requestBody);

            byte[] computedHash;
            using (var hmac = new HMACSHA256(secretBytes))
            {
                computedHash = hmac.ComputeHash(bodyBytes);
            }

            var computedDigest = Convert.ToHexString(computedHash).ToLowerInvariant();

            var isValid = string.Equals(computedDigest, providedDigest, StringComparison.OrdinalIgnoreCase);

            if (!isValid)
            {
                _logger.LogWarning(
                    "DocuSeal webhook HMAC verification failed. Computed={Computed}, Provided={Provided}",
                    computedDigest, providedDigest);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DocuSeal webhook signature verification threw an exception.");
            return false;
        }
    }

    /// <summary>
    /// Parses a DocuSeal webhook JSON payload into a normalized WebhookEvent.
    /// DocuSeal event types: form.completed (all signers done), form.viewed, form.started.
    /// </summary>
    public WebhookEvent ParseWebhookEvent(string requestBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(requestBody);
            var root = doc.RootElement;

            var eventType = root.GetProperty("event_type").GetString();
            var submissionId = root.GetProperty("submission_id").GetInt32().ToString();

            // Extract the last submitter's email as the signer identifier
            string? signerEmail = null;
            DateTime? completedAt = null;

            if (root.TryGetProperty("submitters", out var submittersArray) &&
                submittersArray.ValueKind == JsonValueKind.Array &&
                submittersArray.GetArrayLength() > 0)
            {
                foreach (var submitter in submittersArray.EnumerateArray())
                {
                    signerEmail = submitter.GetProperty("email").GetString();

                    if (submitter.TryGetProperty("completed_at", out var caProp) &&
                        caProp.ValueKind == JsonValueKind.String)
                    {
                        completedAt = caProp.GetDateTime();
                    }
                }
            }

            // Fallback: use top-level completed_at
            if (completedAt == null &&
                root.TryGetProperty("completed_at", out var globalCa) &&
                globalCa.ValueKind == JsonValueKind.String)
            {
                completedAt = globalCa.GetDateTime();
            }

            var mappedType = eventType switch
            {
                "form.completed" => WebhookEventType.AllCompleted,
                "form.viewed" => WebhookEventType.SignerCompleted,
                "form.started" => WebhookEventType.SignerCompleted,
                _ => WebhookEventType.SignerCompleted
            };

            _logger.LogInformation(
                "Parsed DocuSeal webhook: event_type={EventType}, submission_id={SubmissionId}, signer={Email}",
                eventType, submissionId, signerEmail ?? "unknown");

            return new WebhookEvent(
                submissionId,
                signerEmail ?? string.Empty,
                mappedType,
                completedAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse DocuSeal webhook event from body.");
            throw;
        }
    }

    /// <summary>
    /// Internal record to cache submitter info (id, email, embed_url)
    /// per submission for use by GetEmbeddedUrlAsync.
    /// </summary>
    private sealed class SubmitterEmbedInfo
    {
        public string Id { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string? EmbedUrl { get; init; }
    }
}
