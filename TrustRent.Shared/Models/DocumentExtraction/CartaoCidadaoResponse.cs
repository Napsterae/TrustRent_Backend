namespace TrustRent.Shared.Models.DocumentExtraction;

public class CartaoCidadaoResponse : GeminiDocumentResponse
{
    public string? FullName { get; set; }
    public string? FirstNames { get; set; }
    public string? LastNames { get; set; }
    public string? CitizenCardNumber { get; set; }
    public string? Nif { get; set; }
    public string? ExpiryDate { get; set; }
}