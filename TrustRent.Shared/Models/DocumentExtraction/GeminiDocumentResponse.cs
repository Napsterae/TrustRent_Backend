namespace TrustRent.Shared.Models.DocumentExtraction;

public class GeminiDocumentResponse
{
    public bool IsAuthentic { get; set; }
    public string? FraudReason { get; set; }
    public string ImageQuality { get; set; } = "good";
    public bool AllFieldsExtracted { get; set; }
}