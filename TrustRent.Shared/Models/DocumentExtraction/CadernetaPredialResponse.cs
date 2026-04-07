namespace TrustRent.Shared.Models.DocumentExtraction;

public class CadernetaPredialResponse : GeminiDocumentResponse
{
    public string? MatrixArticle { get; set; }
    public string? PropertyFraction { get; set; }
    public string? ParishConcelho { get; set; }
}