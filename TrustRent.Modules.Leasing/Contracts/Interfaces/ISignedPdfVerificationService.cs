namespace TrustRent.Modules.Leasing.Contracts.Interfaces;

public interface ISignedPdfVerificationService
{
    Task<PdfSignatureVerificationResult> VerifySignaturesAsync(byte[] pdfBytes, int expectedMinSignatureCount);
}

public record PdfSignatureVerificationResult(
    bool IsValid,
    int SignatureCount,
    List<PdfSignatureInfo> Signatures,
    List<string> PreSignatureDocumentHashes,
    string? ErrorMessage = null);

public record PdfSignatureInfo(
    string SignerName,
    string CertificateSubject,
    DateTime SigningTime,
    bool IsIntact,
    bool IsCertChainValid);
