namespace TrustRent.Shared.Models.DocumentExtraction;

/// <summary>
/// Declaração de início de atividade (Portal das Finanças) — comprova que um trabalhador
/// independente está coletado e ativo perante a AT.
/// </summary>
public class DeclaracaoInicioAtividadeResponse : GeminiDocumentResponse
{
    public string? TaxpayerName { get; set; }
    public string? TaxpayerNif { get; set; }
    public List<string>? CaeCodes { get; set; }       // ex: ["62010", "70220"]
    public string? CaePrincipalDescription { get; set; }
    public string? ActivityStartDate { get; set; }    // "DD/MM/AAAA"
    public string? ActivityStatus { get; set; }       // "Activa", "Cessada", "Suspensa"
    public string? IssueDate { get; set; }            // "DD/MM/AAAA"
}
