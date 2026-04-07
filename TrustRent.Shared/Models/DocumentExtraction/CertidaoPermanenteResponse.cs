namespace TrustRent.Shared.Models.DocumentExtraction;

public class CertidaoPermanenteResponse : GeminiDocumentResponse
{
    public string? PermanentCertNumber { get; set; }
    public string? PermanentCertOffice { get; set; }
}