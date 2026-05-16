using TrustRent.Shared.Communications;

namespace TrustRent.Shared.Contracts.Interfaces;

public interface ICommunicationContentService
{
    Task<RenderedEmailTemplateContent> RenderEmailTemplateAsync(
        string key,
        IReadOnlyDictionary<string, string?> variables,
        string locale = "pt-PT",
        CancellationToken cancellationToken = default);

    Task<RenderedLegalDocumentContent?> GetLegalDocumentAsync(
        string documentType,
        string? version = null,
        CancellationToken cancellationToken = default);

    IReadOnlyList<CommunicationVariableDefinition> GetVariableDefinitions(string? templateKey = null, string? documentType = null);
    IReadOnlyList<EmailTemplateSeedDefinition> GetEmailTemplateCatalog();
    IReadOnlyList<LegalDocumentSeedDefinition> GetLegalDocumentCatalog();
}

public sealed record RenderedEmailTemplateContent(
    string Key,
    string Name,
    string Version,
    string Subject,
    string BodyHtml,
    string? BodyText,
    bool FromCustomTemplate,
    IReadOnlyDictionary<string, string> ResolvedVariables);

public sealed record RenderedLegalDocumentContent(
    string DocumentType,
    string Title,
    string Version,
    string Summary,
    string ChangeSummary,
    string BodyHtml,
    string? BodyText,
    bool IsCurrent,
    DateTime PublishedAt,
    bool FromCustomDocument,
    IReadOnlyDictionary<string, string> ResolvedVariables);