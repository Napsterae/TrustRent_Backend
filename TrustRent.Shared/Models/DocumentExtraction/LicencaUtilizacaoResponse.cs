namespace TrustRent.Shared.Models.DocumentExtraction;

public class LicencaUtilizacaoResponse : GeminiDocumentResponse
{
    public string? LicenseNumber { get; set; }
    public string? LicenseDate { get; set; }
    public string? LicenseIssuer { get; set; }
}