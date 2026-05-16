using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Communications.Contracts.Database;
using TrustRent.Modules.Identity.Contracts.Database;
using TrustRent.Shared.Communications;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Api.Services;

public sealed class LegalDocumentNotificationJob : ILegalDocumentNotificationJob
{
    private readonly CommunicationsDbContext _communicationsDb;
    private readonly IdentityDbContext _identityDb;
    private readonly AdminDbContext _adminDb;
    private readonly ICommunicationContentService _communicationContentService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LegalDocumentNotificationJob> _logger;

    public LegalDocumentNotificationJob(
        CommunicationsDbContext communicationsDb,
        IdentityDbContext identityDb,
        AdminDbContext adminDb,
        ICommunicationContentService communicationContentService,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<LegalDocumentNotificationJob> logger)
    {
        _communicationsDb = communicationsDb;
        _identityDb = identityDb;
        _adminDb = adminDb;
        _communicationContentService = communicationContentService;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendPublishedVersionNotificationAsync(Guid legalDocumentVersionId)
    {
        var document = await _communicationsDb.LegalDocumentVersions.FirstOrDefaultAsync(entry => entry.Id == legalDocumentVersionId);
        if (document is null)
            return;

        if (document.NotificationSentAt.HasValue)
            return;

        var documentUrl = await BuildDocumentUrlAsync(document.DocumentType, document.Version);
        var rendered = await _communicationContentService.RenderEmailTemplateAsync(
            CommunicationEmailTemplateKeys.LegalDocumentUpdated,
            new Dictionary<string, string?>
            {
                ["DocumentTitle"] = document.Title,
                ["DocumentTypeLabel"] = GetDocumentTypeLabel(document.DocumentType),
                ["DocumentVersion"] = document.Version,
                ["DocumentUrl"] = documentUrl
            });

        var recipients = await _identityDb.Users
            .AsNoTracking()
            .Where(user => !string.IsNullOrWhiteSpace(user.Email))
            .Select(user => user.Email!)
            .Distinct()
            .ToListAsync();

        foreach (var recipient in recipients)
        {
            try
            {
                await _emailService.SendEmailAsync(recipient, rendered.Subject, rendered.BodyHtml);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao enviar notificação de documento legal {DocumentId} para {Recipient}", legalDocumentVersionId, recipient);
            }
        }

        document.NotificationSentAt = DateTime.UtcNow;
        await _communicationsDb.SaveChangesAsync();
    }

    private async Task<string> BuildDocumentUrlAsync(string documentType, string version)
    {
        var settings = await _adminDb.PlatformSettings
            .AsNoTracking()
            .Where(setting => setting.Key == "branding.frontend_base_url")
            .ToDictionaryAsync(setting => setting.Key, setting => setting.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        var frontendBaseUrl = settings.TryGetValue("branding.frontend_base_url", out var configuredBaseUrl) && !string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? configuredBaseUrl
            : (_configuration["Frontend:BaseUrl"] ?? _configuration["App:FrontendBaseUrl"] ?? "http://localhost:5173");

        var slug = documentType switch
        {
            "privacy_policy" => "politica-de-privacidade",
            "terms_of_use" => "termos-de-utilizacao",
            _ => "documentos-legais"
        };

        return $"{frontendBaseUrl.TrimEnd('/')}/{slug}?version={Uri.EscapeDataString(version)}";
    }

    private static string GetDocumentTypeLabel(string documentType)
        => documentType switch
        {
            "privacy_policy" => "Política de privacidade",
            "terms_of_use" => "Termos de utilização",
            _ => "Documento legal"
        };
}