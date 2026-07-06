using TrustRent.Shared.Models;

namespace TrustRent.Modules.Leasing.Contracts.Interfaces;

public interface ISigningProvider
{
    string Name { get; }

    /// <summary>
    /// Create a signing request in the provider with all signers in sequence.
    /// </summary>
    Task<InitiateSigningResult> InitiateSigningAsync(
        byte[] contractPdf,
        string documentName,
        List<SignerInfo> signers);

    /// <summary>
    /// Get the embedded signing URL for a specific signer.
    /// </summary>
    Task<EmbeddedUrlResult> GetEmbeddedUrlAsync(
        string externalRequestId,
        string signerEmail);

    /// <summary>
    /// Download the signed document (final or partial).
    /// </summary>
    Task<byte[]> DownloadSignedDocumentAsync(string externalRequestId);

    /// <summary>
    /// Get the current signing status from the provider.
    /// </summary>
    Task<ProviderSigningStatus> GetStatusAsync(string externalRequestId);

    /// <summary>
    /// Verify webhook signature (HMAC or similar).
    /// </summary>
    bool VerifyWebhookSignature(string requestBody, string signatureHeader, string secret);

    /// <summary>
    /// Parse webhook payload into a normalized event.
    /// </summary>
    WebhookEvent ParseWebhookEvent(string requestBody);
}

public record SignerInfo(Guid UserId, string Email, string Name, LeaseSignatoryRole Role, int SequenceOrder);
public record InitiateSigningResult(
    string ExternalRequestId,
    DateTime ExpiresAt,
    IReadOnlyDictionary<string, string>? RecipientIds = null);

public record EmbeddedUrlResult(string EmbeddedUrl, DateTime ExpiresAt);

public record WebhookEvent(
    string ExternalRequestId,
    string SignerEmail,
    WebhookEventType EventType,
    DateTime? CompletedAt,
    string? ExternalSignerId = null);

public enum WebhookEventType
{
    SignerCompleted,
    AllCompleted,
    Rejected,
    Expired
}

public enum ProviderSigningStatus
{
    Pending,
    PartiallySigned,
    Completed,
    Expired,
    Rejected
}
