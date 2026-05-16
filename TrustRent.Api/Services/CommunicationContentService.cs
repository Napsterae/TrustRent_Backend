using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Communications.Contracts.Database;
using TrustRent.Shared.Communications;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Api.Services;

public sealed class CommunicationContentService : ICommunicationContentService
{
    private static readonly Regex TokenRegex = new(@"\{\{\s*(?<key>[A-Za-z0-9_]+)\s*\}\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly CommunicationsDbContext _communicationsDb;
    private readonly AdminDbContext _adminDb;
    private readonly IConfiguration _configuration;

    public CommunicationContentService(
        CommunicationsDbContext communicationsDb,
        AdminDbContext adminDb,
        IConfiguration configuration)
    {
        _communicationsDb = communicationsDb;
        _adminDb = adminDb;
        _configuration = configuration;
    }

    public IReadOnlyList<CommunicationVariableDefinition> GetVariableDefinitions(string? templateKey = null, string? documentType = null)
    {
        if (!string.IsNullOrWhiteSpace(documentType))
            return CommunicationCatalog.GetVariablesForDocument(documentType);

        if (!string.IsNullOrWhiteSpace(templateKey))
            return CommunicationCatalog.GetVariablesForTemplate(templateKey);

        return CommunicationCatalog.Variables;
    }

    public IReadOnlyList<EmailTemplateSeedDefinition> GetEmailTemplateCatalog()
        => CommunicationCatalog.EmailTemplates;

    public IReadOnlyList<LegalDocumentSeedDefinition> GetLegalDocumentCatalog()
        => CommunicationCatalog.LegalDocuments;

    public async Task<RenderedEmailTemplateContent> RenderEmailTemplateAsync(
        string key,
        IReadOnlyDictionary<string, string?> variables,
        string locale = "pt-PT",
        CancellationToken cancellationToken = default)
    {
        var definition = CommunicationCatalog.GetEmailTemplateDefinition(key);

        var template = await _communicationsDb.EmailTemplates
            .AsNoTracking()
            .Where(entry => entry.Key == key && entry.Locale == locale && entry.IsActive)
            .OrderByDescending(entry => entry.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? await _communicationsDb.EmailTemplates
                .AsNoTracking()
                .Where(entry => entry.Key == key && entry.Locale == "pt-PT" && entry.IsActive)
                .OrderByDescending(entry => entry.UpdatedAt)
                .FirstOrDefaultAsync(cancellationToken);

        var supportedVariableKeys = (template is null ? definition.SupportedVariables : GetVariableDefinitions(key).Select(item => item.Key).ToArray())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var resolvedVariables = await ResolveVariablesAsync(supportedVariableKeys, variables, cancellationToken);
        var subjectTemplate = template?.Subject ?? definition.SubjectTemplate;
        var bodyHtmlTemplate = template?.BodyHtml ?? definition.BodyHtmlTemplate;
        var bodyTextTemplate = string.IsNullOrWhiteSpace(template?.BodyText)
            ? definition.BodyTextTemplate
            : template!.BodyText;

        var renderedHtml = RenderHtml(bodyHtmlTemplate, resolvedVariables);
        var renderedText = string.IsNullOrWhiteSpace(bodyTextTemplate)
            ? BuildPlainText(renderedHtml)
            : RenderText(bodyTextTemplate!, resolvedVariables);

        return new RenderedEmailTemplateContent(
            key,
            template?.Name ?? definition.DefaultName,
            template?.Version ?? definition.DefaultVersion,
            RenderText(subjectTemplate, resolvedVariables),
            renderedHtml,
            renderedText,
            template is not null,
            resolvedVariables);
    }

    public async Task<RenderedLegalDocumentContent?> GetLegalDocumentAsync(
        string documentType,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        var definition = CommunicationCatalog.GetLegalDocumentDefinition(documentType);

        var document = string.IsNullOrWhiteSpace(version)
            ? await _communicationsDb.LegalDocumentVersions
                .AsNoTracking()
                .Where(entry => entry.DocumentType == documentType && entry.IsCurrent)
                .OrderByDescending(entry => entry.PublishedAt)
                .FirstOrDefaultAsync(cancellationToken)
            : await _communicationsDb.LegalDocumentVersions
                .AsNoTracking()
                .FirstOrDefaultAsync(entry => entry.DocumentType == documentType && entry.Version == version, cancellationToken);

        if (document is null && !string.IsNullOrWhiteSpace(version) && !string.Equals(version, definition.DefaultVersion, StringComparison.OrdinalIgnoreCase))
            return null;

        var supportedVariables = definition.SupportedVariables.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var resolvedVariables = await ResolveVariablesAsync(supportedVariables, new Dictionary<string, string?>(), cancellationToken);

        if (document is null)
        {
            return new RenderedLegalDocumentContent(
                definition.DocumentType,
                RenderText(definition.Title, resolvedVariables),
                definition.DefaultVersion,
                RenderText(definition.Summary, resolvedVariables),
                RenderText(definition.ChangeSummary, resolvedVariables),
                RenderHtml(definition.BodyHtmlTemplate, resolvedVariables),
                string.IsNullOrWhiteSpace(definition.BodyTextTemplate) ? null : RenderText(definition.BodyTextTemplate, resolvedVariables),
                true,
                DateTime.UtcNow,
                false,
                resolvedVariables);
        }

        return new RenderedLegalDocumentContent(
            document.DocumentType,
            RenderText(document.Title, resolvedVariables),
            document.Version,
            RenderText(document.Summary, resolvedVariables),
            RenderText(document.ChangeSummary, resolvedVariables),
            RenderHtml(document.BodyHtml, resolvedVariables),
            string.IsNullOrWhiteSpace(document.BodyText) ? null : RenderText(document.BodyText, resolvedVariables),
            document.IsCurrent,
            document.PublishedAt,
            true,
            resolvedVariables);
    }

    private async Task<Dictionary<string, string>> ResolveVariablesAsync(
        IReadOnlyCollection<string> supportedVariableKeys,
        IReadOnlyDictionary<string, string?> overrides,
        CancellationToken cancellationToken)
    {
        var configurableVariables = CommunicationCatalog.Variables
            .Where(variable => variable.IsConfigurable && supportedVariableKeys.Contains(variable.Key, StringComparer.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(variable.SettingKey))
            .ToArray();

        var settingKeys = configurableVariables
            .Select(variable => variable.SettingKey!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var settings = settingKeys.Length == 0
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : await _adminDb.PlatformSettings
                .AsNoTracking()
                .Where(setting => settingKeys.Contains(setting.Key))
                .ToDictionaryAsync(setting => setting.Key, setting => setting.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var frontendBaseUrl = FirstNonEmpty(
                                  GetSetting(settings, "branding.frontend_base_url"),
                                  _configuration["Frontend:BaseUrl"],
                                  _configuration["App:FrontendBaseUrl"],
                                  "http://localhost:5173")
                              ?? "http://localhost:5173";

        var appName = FirstNonEmpty(
                          GetSetting(settings, "branding.app_name"),
                          _configuration["EmailSettings:FromName"],
                          "Wekaza")
                      ?? "Wekaza";

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AppName"] = appName,
            ["CompanyName"] = FirstNonEmpty(GetSetting(settings, "branding.company_name"), appName) ?? appName,
            ["CompanyAddress"] = FirstNonEmpty(GetSetting(settings, "branding.company_address"), "Lisboa, Portugal") ?? "Lisboa, Portugal",
            ["SupportEmail"] = FirstNonEmpty(
                GetSetting(settings, "branding.support_email"),
                _configuration["EmailSettings:ReplyToAddress"],
                _configuration["EmailSettings:FromAddress"],
                "suporte@wekaza.pt") ?? "suporte@wekaza.pt",
            ["SupportPhone"] = FirstNonEmpty(GetSetting(settings, "branding.support_phone"), "+351 210 000 000") ?? "+351 210 000 000",
            ["WebsiteUrl"] = FirstNonEmpty(GetSetting(settings, "branding.website_url"), frontendBaseUrl) ?? frontendBaseUrl,
            ["FrontendBaseUrl"] = frontendBaseUrl.TrimEnd('/'),
            ["PrivacyPolicyUrl"] = FirstNonEmpty(GetSetting(settings, "branding.privacy_policy_url"), $"{frontendBaseUrl.TrimEnd('/')}/politica-de-privacidade") ?? $"{frontendBaseUrl.TrimEnd('/')}/politica-de-privacidade",
            ["TermsOfUseUrl"] = FirstNonEmpty(GetSetting(settings, "branding.terms_of_use_url"), $"{frontendBaseUrl.TrimEnd('/')}/termos-de-utilizacao") ?? $"{frontendBaseUrl.TrimEnd('/')}/termos-de-utilizacao",
            ["CurrentYear"] = DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture)
        };

        foreach (var variable in CommunicationCatalog.Variables.Where(item => supportedVariableKeys.Contains(item.Key, StringComparer.OrdinalIgnoreCase)))
        {
            if (!values.ContainsKey(variable.Key) && !string.IsNullOrWhiteSpace(variable.ExampleValue))
                values[variable.Key] = variable.ExampleValue;
        }

        foreach (var pair in overrides)
        {
            if (pair.Value is null)
                continue;

            values[pair.Key] = pair.Value;
        }

        return values;
    }

    private static string? GetSetting(IReadOnlyDictionary<string, string> settings, string key)
        => settings.TryGetValue(key, out var value) ? value : null;

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string RenderText(string template, IReadOnlyDictionary<string, string> variables)
        => TokenRegex.Replace(template ?? string.Empty, match => variables.TryGetValue(match.Groups["key"].Value, out var value) ? value : string.Empty);

    private static string RenderHtml(string template, IReadOnlyDictionary<string, string> variables)
        => TokenRegex.Replace(template ?? string.Empty, match =>
        {
            if (!variables.TryGetValue(match.Groups["key"].Value, out var value))
                return string.Empty;

            return WebUtility.HtmlEncode(value ?? string.Empty)
                .Replace("\r\n", "<br />", StringComparison.Ordinal)
                .Replace("\n", "<br />", StringComparison.Ordinal);
        });

    private static string BuildPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var withBreaks = Regex.Replace(html, @"<\s*br\s*/?>", "\n", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        withBreaks = Regex.Replace(withBreaks, @"<\s*/p\s*>", "\n\n", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var withoutTags = Regex.Replace(withBreaks, @"<[^>]+>", " ", RegexOptions.CultureInvariant);
        return Regex.Replace(WebUtility.HtmlDecode(withoutTags), @"\s+", " ", RegexOptions.CultureInvariant).Trim();
    }
}