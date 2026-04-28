namespace TrustRent.Shared.Models.DocumentExtraction;

/// <summary>
/// Recibo verde / fatura-recibo eletrónica emitida no Portal das Finanças por
/// trabalhador independente. Usado em conjunto com a declaração de atividade
/// para estimar a média de rendimento mensal.
/// </summary>
public class ReciboVerdeResponse : GeminiDocumentResponse
{
    public string? IssuerName { get; set; }       // prestador (deve ser o utilizador)
    public string? IssuerNif { get; set; }
    public string? AcquirerName { get; set; }     // adquirente (cliente / entidade pagadora)
    public string? AcquirerNif { get; set; }
    public string? IssueDate { get; set; }        // "DD/MM/AAAA"
    public string? ReferenceMonth { get; set; }   // "MM/AAAA" — mês a que se refere
    public decimal? BaseAmount { get; set; }      // valor base / honorários (€)
    public decimal? TotalAmount { get; set; }     // valor total (com IVA, se aplicável)
    public string? Currency { get; set; }
}
