namespace TrustRent.Shared.Models.DocumentExtraction;

public class ReciboVencimentoResponse : GeminiDocumentResponse
{
    public string? EmployeeName { get; set; }
    public string? EmployeeNif { get; set; }
    public string? EmployerName { get; set; }
    public string? EmployerNif { get; set; }
    public string? ReferenceMonth { get; set; } // "MM/AAAA"
    public string? IssueDate { get; set; }      // "DD/MM/AAAA"
    public decimal? GrossSalary { get; set; }
    public decimal? NetSalary { get; set; }
    public string? Currency { get; set; }
}
