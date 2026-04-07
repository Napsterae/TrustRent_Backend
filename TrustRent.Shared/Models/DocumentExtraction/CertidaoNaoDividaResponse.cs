namespace TrustRent.Shared.Models.DocumentExtraction;

public class CertidaoNaoDividaResponse : GeminiDocumentResponse
{
    public string? Nif { get; set; }
    public bool IsTaxRegularized { get; set; }
    public string? ExpiryDate { get; set; }
}