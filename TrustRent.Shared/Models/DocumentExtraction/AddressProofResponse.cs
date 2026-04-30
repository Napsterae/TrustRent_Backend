namespace TrustRent.Shared.Models.DocumentExtraction;

public class AddressProofResponse : GeminiDocumentResponse
{
    public string? HolderName { get; set; }
    public string? Nif { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public string? DocumentType { get; set; }
    public string? IssuerName { get; set; }
    public string? IssueDate { get; set; }
}