using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.DTOs;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Shared;
using TrustRent.Shared.Contracts.DTOs;
using TrustRent.Shared.Models;

namespace TrustRent.Modules.Leasing.Services;

public interface ISigningProviderService
{
    /// <summary>
    /// Initiate or resume embedded signing for a lease. Returns the embedded signing URL
    /// for the current signer.
    /// </summary>
    Task<EmbeddedSigningDto> InitiateEmbeddedSigningAsync(Guid leaseId, Guid userId);

    /// <summary>
    /// Handle a webhook event from a signing provider when a signer completes.
    /// Downloads the signed PDF, updates the signature row, transitions status.
    /// </summary>
    Task HandleSignerCompletedAsync(string externalRequestId, string signerEmail, string? externalSignerId = null);

    /// <summary>
    /// Handle a generic webhook event from a signing provider.
    /// Verifies HMAC, then delegates to the appropriate handler.
    /// </summary>
    Task HandleWebhookAsync(string providerName, string requestBody, string signatureHeader);

    /// <summary>
    /// Initiate embedded signing for a guest guarantor.
    /// </summary>
    Task<EmbeddedSigningDto> InitiateEmbeddedSigningForGuestAsync(string guestToken);
}

public class SigningProviderService : ISigningProviderService
{
    private readonly ISigningProvider _provider;
    private readonly LeasingDbContext _db;
    private readonly IContractGenerationService _contractGenerationService;
    private readonly ILeaseService _leaseService;
    private readonly IUserService _userService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SigningProviderService> _logger;

    public SigningProviderService(
        ISigningProvider provider,
        LeasingDbContext db,
        IContractGenerationService contractGenerationService,
        ILeaseService leaseService,
        IUserService userService,
        IConfiguration configuration,
        ILogger<SigningProviderService> logger)
    {
        _provider = provider;
        _db = db;
        _contractGenerationService = contractGenerationService;
        _leaseService = leaseService;
        _userService = userService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<EmbeddedSigningDto> InitiateEmbeddedSigningAsync(Guid leaseId, Guid userId)
    {
        var lease = await _db.Leases
            .Include(l => l.Signatures)
            .FirstOrDefaultAsync(l => l.Id == leaseId)
            ?? throw new KeyNotFoundException("Arrendamento não encontrado.");

        // Validate: user is the expected signer for the current status
        var expectedRole = GetExpectedUploadRole(lease.Status);
        if (expectedRole == null)
            throw new InvalidOperationException("O contrato não está numa fase de assinatura.");

        var signature = lease.Signatures
            .FirstOrDefault(s => s.UserId == userId && s.Role == expectedRole.Value);
        if (signature == null)
            throw new UnauthorizedAccessException("Não és o signatário esperado para esta fase.");

        if (signature.Signed)
            throw new InvalidOperationException("Já assinaste este contrato.");

        // If no external request has been created yet, initiate one
        if (string.IsNullOrEmpty(lease.ExternalSigningRequestId))
        {
            if (string.IsNullOrEmpty(lease.ContractFilePath) || !File.Exists(lease.ContractFilePath))
                throw new InvalidOperationException("O contrato ainda não foi gerado.");

            var contractPdf = await _contractGenerationService.GetContractBytesAsync(lease.ContractFilePath);

            // Resolve actual name and email from UserService for each signer
            var orderedSignatures = lease.Signatures
                .OrderBy(s => s.SequenceOrder)
                .ToList();

            var resolvedSignerTasks = orderedSignatures
                .Select(async s =>
                {
                    var user = await _userService.GetProfileDtoAsync(s.UserId);
                    return new
                    {
                        Signature = s,
                        Email = user?.Email ?? $"{s.UserId}@trustrent.local",
                        Name = user?.Name ?? $"Signatário #{s.SequenceOrder}"
                    };
                })
                .ToList();

            var resolvedSigners = await Task.WhenAll(resolvedSignerTasks);

            var signers = resolvedSigners
                .Select(rs => new SignerInfo(
                    rs.Signature.UserId,
                    rs.Email,
                    rs.Name,
                    rs.Signature.Role,
                    rs.Signature.SequenceOrder))
                .ToList();

            var result = await _provider.InitiateSigningAsync(
                contractPdf,
                $"Contrato de Arrendamento - {leaseId}",
                signers);

            lease.SignatureProvider = _provider.Name;
            lease.ExternalSigningRequestId = result.ExternalRequestId;
            lease.UpdatedAt = DateTime.UtcNow;

            // Store ExternalSignerId (provider's recipient ID) on each LeaseSignature
            if (result.RecipientIds != null)
            {
                foreach (var rs in resolvedSigners)
                {
                    if (result.RecipientIds.TryGetValue(rs.Email, out var recipientId))
                    {
                        rs.Signature.ExternalSignerId = recipientId;
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Documenso: recipient ID not returned for email {Email} (userId {UserId}).",
                            rs.Email, rs.Signature.UserId);
                    }
                }
            }

            await _db.SaveChangesAsync();
        }

        // Get the embedded URL for the current signer
        var currentSignature = lease.Signatures
            .First(s => s.UserId == userId && s.Role == expectedRole.Value);

        // Resolve actual email from UserService
        var currentUser = await _userService.GetProfileDtoAsync(userId);
        var signerEmail = currentUser?.Email ?? $"{userId}@trustrent.local";
        var urlResult = await _provider.GetEmbeddedUrlAsync(
            lease.ExternalSigningRequestId!,
            signerEmail);

        return new EmbeddedSigningDto
        {
            EmbeddedUrl = urlResult.EmbeddedUrl,
            Provider = _provider.Name,
            ExpiresAt = urlResult.ExpiresAt
        };
    }

    public async Task HandleSignerCompletedAsync(string externalRequestId, string signerEmail, string? externalSignerId = null)
    {
        // 1. Find lease by ExternalSigningRequestId
        var lease = await _db.Leases
            .Include(l => l.Signatures)
            .FirstOrDefaultAsync(l => l.ExternalSigningRequestId == externalRequestId)
            ?? throw new KeyNotFoundException("Arrendamento não encontrado para o ExternalSigningRequestId fornecido.");

        // 2. Find LeaseSignature by ExternalSignerId (recipientId from provider)
        LeaseSignature? signature = null;

        if (!string.IsNullOrEmpty(externalSignerId))
        {
            signature = lease.Signatures
                .FirstOrDefault(s => s.ExternalSignerId == externalSignerId);
        }

        if (signature == null)
        {
            _logger.LogInformation(
                "SignerCompleted: no match by ExternalSignerId for request {ReqId}, email {Email}. " +
                "Falling back to first unsigned signer by sequence order.",
                externalRequestId, signerEmail);

            // Fallback: match by sequence order (first unsigned)
            signature = lease.Signatures
                .Where(s => !s.Signed)
                .OrderBy(s => s.SequenceOrder)
                .FirstOrDefault();
        }

        if (signature == null || signature.Signed)
        {
            _logger.LogWarning(
                "SignerCompleted event ignored for request {ExternalRequestId}, signer {Email}: already signed or not found.",
                externalRequestId, signerEmail);
            return; // idempotent
        }

        // 3. Download signed PDF from provider
        var signedPdf = await _provider.DownloadSignedDocumentAsync(externalRequestId);
        if (signedPdf == null || signedPdf.Length == 0)
            throw new InvalidOperationException("Provider returned empty signed document.");

        // 4. SaveSignedPdf (reuse existing helper pattern)
        var party = signature.Role.ToString().ToLowerInvariant();
        var signedFilePath = SaveSignedPdf(signedPdf, lease.Id, party, $"signed_{party}.pdf");

        var now = DateTime.UtcNow;

        // 5. Update LeaseSignature
        signature.Signed = true;
        signature.SignedAt = now;
        signature.SignatureRef = $"embedded_{_provider.Name}_{externalRequestId}";
        signature.SignedFilePath = signedFilePath;
        signature.SignatureVerified = true;
        signature.UpdatedAt = now;

        // 6. Update legacy Lease fields
        UpdateLegacySignatureFields(lease, signature.Role, now, signature.SignatureRef);

        // 7. Transition status
        var moreSigners = lease.Signatures.Any(s => !s.Signed);
        if (moreSigners)
        {
            var nextRole = lease.Signatures
                .Where(s => !s.Signed)
                .OrderBy(s => s.SequenceOrder)
                .First().Role;
            lease.Status = GetPendingUploadStatus(nextRole);
        }
        else
        {
            // All signed — activate
            lease.Status = LeaseStatus.AwaitingPayment;
            lease.ContractSignedAt = now;

            lease.History.Add(new LeaseHistory
            {
                LeaseId = lease.Id,
                ActorId = Guid.Empty,
                Action = "AwaitingPayment",
                Message = "Contrato assinado por todas as partes via assinatura digital integrada. Aguarda pagamento inicial do inquilino."
            });

            // TODO Phase 2/3: Send notifications to tenant/landlord
        }

        lease.UpdatedAt = now;
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Signer {Email} completed for lease {LeaseId}. Status now: {Status}",
            signerEmail, lease.Id, lease.Status);
    }

    public async Task HandleWebhookAsync(string providerName, string requestBody, string signatureHeader)
    {
        using var activity = Telemetry.Source.StartActivity("SigningWebhook");
        activity?.SetTag("document.provider", providerName);
        // Resolve webhook secret from configuration
        var secret = _configuration[$"ElectronicSignature:{providerName}:WebhookSecret"];
        if (string.IsNullOrEmpty(secret))
        {
            _logger.LogWarning(
                "Webhook secret not configured for provider {Provider}. Skipping HMAC verification.",
                providerName);
        }
        else if (!_provider.VerifyWebhookSignature(requestBody, signatureHeader, secret))
        {
            _logger.LogError(
                "Webhook HMAC verification failed for provider {Provider}. Signature header: {Header}",
                providerName, signatureHeader);
            throw new UnauthorizedAccessException("Invalid webhook signature.");
        }

        var webhookEvent = _provider.ParseWebhookEvent(requestBody);

        switch (webhookEvent.EventType)
        {
            case WebhookEventType.SignerCompleted:
                await HandleSignerCompletedAsync(
                    webhookEvent.ExternalRequestId,
                    webhookEvent.SignerEmail,
                    webhookEvent.ExternalSignerId);
                break;

            case WebhookEventType.AllCompleted:
                // All signers completed: download final document and ensure
                // all signatures are marked. Pass the ExternalSignerId if available.
                await HandleSignerCompletedAsync(
                    webhookEvent.ExternalRequestId,
                    webhookEvent.SignerEmail,
                    webhookEvent.ExternalSignerId);
                break;

            case WebhookEventType.Rejected:
                _logger.LogWarning(
                    "Signing rejected for request {ExternalRequestId}.",
                    webhookEvent.ExternalRequestId);
                break;

            case WebhookEventType.Expired:
                _logger.LogWarning(
                    "Signing expired for request {ExternalRequestId}.",
                    webhookEvent.ExternalRequestId);
                break;
        }
    }

    public Task<EmbeddedSigningDto> InitiateEmbeddedSigningForGuestAsync(string guestToken)
    {
        // TODO Phase 2/3: implement guest guarantor flow
        throw new NotImplementedException("Guest embedded signing not yet implemented.");
    }

    private static string SaveSignedPdf(byte[] pdfBytes, Guid leaseId, string party, string originalFileName)
    {
        var dir = Path.Combine("contracts", "signed", leaseId.ToString());
        Directory.CreateDirectory(dir);
        var fileName = $"{party}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
        var path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, pdfBytes);
        return path;
    }

    private static LeaseSignatoryRole? GetExpectedUploadRole(LeaseStatus status)
        => status switch
        {
            LeaseStatus.PendingLandlordSignature => LeaseSignatoryRole.Landlord,
            LeaseStatus.PendingTenantSignature => LeaseSignatoryRole.Tenant,
            LeaseStatus.PendingCoTenantSignature => LeaseSignatoryRole.CoTenant,
            LeaseStatus.PendingGuarantorSignature => LeaseSignatoryRole.Guarantor,
            _ => null
        };

    private static LeaseStatus GetPendingUploadStatus(LeaseSignatoryRole role)
        => role switch
        {
            LeaseSignatoryRole.Landlord => LeaseStatus.PendingLandlordSignature,
            LeaseSignatoryRole.Tenant => LeaseStatus.PendingTenantSignature,
            LeaseSignatoryRole.CoTenant => LeaseStatus.PendingCoTenantSignature,
            LeaseSignatoryRole.Guarantor => LeaseStatus.PendingGuarantorSignature,
            _ => LeaseStatus.PendingTenantSignature
        };

    private static void UpdateLegacySignatureFields(Lease lease, LeaseSignatoryRole role, DateTime signedAt, string signatureRef)
    {
        switch (role)
        {
            case LeaseSignatoryRole.Landlord:
                lease.LandlordSigned = true;
                lease.LandlordSignedAt = signedAt;
                lease.LandlordSignatureRef = signatureRef;
                break;
            case LeaseSignatoryRole.Tenant:
                lease.TenantSigned = true;
                lease.TenantSignedAt = signedAt;
                lease.TenantSignatureRef = signatureRef;
                break;
            // CoTenant and Guarantor don't have legacy fields
        }
    }
}

public class EmbeddedSigningDto
{
    public string EmbeddedUrl { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
