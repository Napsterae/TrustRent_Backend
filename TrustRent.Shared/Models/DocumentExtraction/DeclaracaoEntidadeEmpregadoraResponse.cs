namespace TrustRent.Shared.Models.DocumentExtraction;

/// <summary>
/// Declaração de efetividade emitida pela entidade empregadora — usada quando o trabalhador
/// ainda não tem 3 recibos de vencimento (emprego há menos de 3 meses).
/// </summary>
public class DeclaracaoEntidadeEmpregadoraResponse : GeminiDocumentResponse
{
    public string? EmployeeName { get; set; }
    public string? EmployeeNif { get; set; }
    public string? EmployerName { get; set; }
    public string? EmployerNif { get; set; }
    public string? Position { get; set; }              // cargo/função
    public string? ContractType { get; set; }          // "Sem termo", "Termo certo", "Termo incerto"
    public string? EmploymentStartDate { get; set; }   // "DD/MM/AAAA"
    public string? IssueDate { get; set; }             // "DD/MM/AAAA"
    public bool? HasSignatureAndStamp { get; set; }    // documento tem assinatura E carimbo
}
