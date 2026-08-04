using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using TrustRent.Modules.Leasing.Contracts.Interfaces;

namespace TrustRent.Modules.Leasing.Services;

/// <summary>
/// Development-only signing provider that fakes the full embedded-signing lifecycle with
/// zero external calls:
///   - InitiateSigningAsync returns a deterministic mock request id and per-signer recipient ids.
///   - GetEmbeddedUrlAsync returns a harmless data: URI with a small local "Assinatura simulada"
///     page (no external network requests).
///   - DownloadSignedDocumentAsync returns the original contract PDF bytes (stored in memory when
///     the request was initiated) as the "signed" artifact.
/// Registered ONLY in Development (see Program.cs) and only when
/// <c>ElectronicSignature:Provider == "Mock"</c>. Never used in production.
/// </summary>
public sealed class MockSigningProvider : ISigningProvider
{
    public string Name => "Mock";

    private sealed record MockSigningRequest(
        byte[] ContractPdf,
        DateTime ExpiresAt,
        IReadOnlyDictionary<string, string> RecipientIds);

    private readonly ConcurrentDictionary<string, MockSigningRequest> _requests = new();

    public Task<InitiateSigningResult> InitiateSigningAsync(
        byte[] contractPdf,
        string documentName,
        List<SignerInfo> signers)
    {
        // Deterministic, unique external request id (mock_ prefix distinguishes it from real providers).
        var externalRequestId = $"mock-{Guid.NewGuid():N}";

        var recipientIds = signers.ToDictionary(
            s => s.Email,
            s => $"rec-{s.SequenceOrder}-{Guid.NewGuid():N}",
            StringComparer.OrdinalIgnoreCase);

        _requests[externalRequestId] = new MockSigningRequest(
            contractPdf,
            DateTime.UtcNow.AddDays(30),
            recipientIds);

        return Task.FromResult(new InitiateSigningResult(
            externalRequestId,
            DateTime.UtcNow.AddDays(30),
            recipientIds));
    }

    public Task<EmbeddedUrlResult> GetEmbeddedUrlAsync(string externalRequestId, string signerEmail)
    {
        var request = GetRequest(externalRequestId);

        // Harmless to render: a data: URI with a small local HTML page (no external calls).
        var html = $"""
            <!DOCTYPE html>
            <html lang="pt">
            <head><meta charset="utf-8" /><title>Assinatura simulada</title></head>
            <body style="font-family:system-ui,sans-serif;background:#f8fafc;color:#0f172a;display:flex;align-items:center;justify-content:center;height:100vh;margin:0">
              <div style="text-align:center;padding:24px">
                <div style="font-size:48px">&#9997;&#65039;</div>
                <h2 style="margin:8px 0 4px">Assinatura simulada</h2>
                <p style="margin:0;color:#475569">MockSigningProvider &mdash; signat&aacute;rio {WebUtility.HtmlEncode(signerEmail)}</p>
                <p style="margin:12px 0 0;font-size:12px;color:#94a3b8">Este contrato &eacute; dado como assinado pelo endpoint de simula&ccedil;&atilde;o.</p>
              </div>
            </body>
            </html>
            """;
        var dataUri = "data:text/html;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(html));

        return Task.FromResult(new EmbeddedUrlResult(dataUri, request.ExpiresAt));
    }

    public Task<byte[]> DownloadSignedDocumentAsync(string externalRequestId)
    {
        // The "signed" artifact is the lease's original contract PDF, stored when initiated.
        return Task.FromResult(GetRequest(externalRequestId).ContractPdf);
    }

    public Task<ProviderSigningStatus> GetStatusAsync(string externalRequestId)
    {
        GetRequest(externalRequestId); // throws if unknown
        return Task.FromResult(ProviderSigningStatus.Completed);
    }

    public bool VerifyWebhookSignature(string requestBody, string signatureHeader, string secret) => true;

    public WebhookEvent ParseWebhookEvent(string requestBody)
    {
        using var doc = JsonDocument.Parse(requestBody);
        var root = doc.RootElement;
        var eventTypeStr = root.TryGetProperty("type", out var t) ? t.GetString() ?? string.Empty : string.Empty;
        var documentId = root.TryGetProperty("documentId", out var d) ? d.GetString() ?? string.Empty : string.Empty;
        var email = root.TryGetProperty("recipientEmail", out var e) ? e.GetString() ?? string.Empty : string.Empty;
        var recipientId = root.TryGetProperty("recipientId", out var r) ? r.GetString() : null;

        var eventType = eventTypeStr switch
        {
            "DOCUMENT_COMPLETED" => WebhookEventType.AllCompleted,
            "DOCUMENT_REJECTED" => WebhookEventType.Rejected,
            "DOCUMENT_EXPIRED" => WebhookEventType.Expired,
            _ => WebhookEventType.SignerCompleted
        };

        return new WebhookEvent(documentId, email, eventType, DateTime.UtcNow, recipientId);
    }

    private MockSigningRequest GetRequest(string externalRequestId)
        => _requests.TryGetValue(externalRequestId, out var request)
            ? request
            : throw new KeyNotFoundException(
                $"Nenhum pedido de assinatura simulado conhecido: {externalRequestId}. " +
                "Inicie a assinatura no mesmo processo da API (o mock guarda o PDF em memória) antes de a simular.");
}
