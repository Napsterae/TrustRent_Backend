namespace TrustRent.Shared.Models.DocumentExtraction;

public class TaxRegistrationValidationResponse : GeminiDocumentResponse
{
    public string? AtRegistrationNumber { get; set; }
    public string? LandlordName { get; set; }
    public string? LandlordNif { get; set; }
}
