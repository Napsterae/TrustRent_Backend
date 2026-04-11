namespace TrustRent.Modules.Catalog.Contracts.Interfaces;

/// <summary>
/// Verifica assinaturas digitais PAdES em documentos PDF assinados localmente
/// com a app Autenticação.Gov (Chave Móvel Digital).
/// </summary>
public interface ISignedPdfVerificationService
{
    /// <summary>
    /// Verifica as assinaturas digitais presentes no PDF.
    /// </summary>
    /// <param name="pdfBytes">Conteúdo do PDF assinado.</param>
    /// <param name="expectedMinSignatureCount">Número mínimo de assinaturas esperadas.</param>
    Task<PdfSignatureVerificationResult> VerifySignaturesAsync(byte[] pdfBytes, int expectedMinSignatureCount);
}

public record PdfSignatureVerificationResult(
    bool IsValid,
    int SignatureCount,
    List<PdfSignatureInfo> Signatures,
    /// <summary>
    /// SHA-256 (base64) dos bytes do documento tal como estavam quando cada assinatura foi aplicada.
    /// Índice 0 = snapshot antes da 1ª assinatura (o documento original que o senhorio assinou).
    /// Índice 1 = snapshot antes da 2ª assinatura (o PDF do senhorio que o inquilino assinou).
    /// </summary>
    List<string> PreSignatureDocumentHashes,
    string? ErrorMessage = null);

public record PdfSignatureInfo(
    string SignerName,
    string CertificateSubject,
    DateTime SigningTime,
    bool IsIntact,
    bool IsCertChainValid);

