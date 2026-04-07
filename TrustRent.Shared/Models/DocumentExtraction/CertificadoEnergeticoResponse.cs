namespace TrustRent.Shared.Models.DocumentExtraction;

public class CertificadoEnergeticoResponse : GeminiDocumentResponse
{
    public string? EnergyClass { get; set; }
    public string? EnergyCertNumber { get; set; }
}