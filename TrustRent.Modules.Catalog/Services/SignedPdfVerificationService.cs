using System.Security.Cryptography;
using iText.Kernel.Pdf;
using iText.Signatures;
using Microsoft.Extensions.Logging;
using TrustRent.Modules.Catalog.Contracts.Interfaces;

namespace TrustRent.Modules.Catalog.Services;

/// <summary>
/// Verifica assinaturas digitais PAdES em PDFs assinados com a Chave Móvel Digital
/// (app Autenticação.Gov). Usa iText7 para inspecionar o dicionário de assinaturas.
///
/// Mecanismo de integridade de conteúdo:
///   signUtil.GetRevision(sigName) devolve os bytes do documento tal como estavam
///   no momento em que essa assinatura foi aplicada (excluindo o valor da assinatura).
///   Fazendo SHA-256 desses bytes e comparando com o hash do documento original gerado
///   pela plataforma, garantimos que o utilizador assinou o contrato correto e não
///   um PDF arbitrário gerado fora da plataforma.
/// </summary>
public class SignedPdfVerificationService : ISignedPdfVerificationService
{
    private readonly ILogger<SignedPdfVerificationService> _logger;

    public SignedPdfVerificationService(ILogger<SignedPdfVerificationService> logger)
    {
        _logger = logger;
    }

    public async Task<PdfSignatureVerificationResult> VerifySignaturesAsync(
        byte[] pdfBytes, int expectedMinSignatureCount)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var ms = new MemoryStream(pdfBytes);
                using var reader = new PdfReader(ms);
                using var doc = new PdfDocument(reader);

                var signUtil = new SignatureUtil(doc);
                var sigNames = signUtil.GetSignatureNames();

                _logger.LogInformation("[PDF Verify] Encontradas {Count} assinatura(s) no documento.", sigNames.Count);

                if (sigNames.Count < expectedMinSignatureCount)
                {
                    return new PdfSignatureVerificationResult(
                        false, sigNames.Count, [], [],
                        $"Esperadas pelo menos {expectedMinSignatureCount} assinatura(s), mas o documento contém apenas {sigNames.Count}. " +
                        "Certifica-te de que assinaste o documento com a app Autenticação.Gov antes de fazer o upload.");
                }

                var infos = new List<PdfSignatureInfo>();
                var preSignatureHashes = new List<string>();

                foreach (var name in sigNames)
                {
                    // ---- Calcular hash do documento antes desta assinatura ----
                    // GetRevision devolve os bytes do estado do documento no momento da assinatura
                    // i.e., o conteúdo que foi efetivamente assinado (ByteRange excluído).
                    string revisionHash;
                    try
                    {
                        using var revStream = signUtil.ExtractRevision(name);
                        using var revMs = new MemoryStream();
                        revStream.CopyTo(revMs);
                        var revBytes = revMs.ToArray();
                        revisionHash = Convert.ToBase64String(SHA256.HashData(revBytes));
                        _logger.LogInformation("[PDF Verify] Revision hash para '{Name}': {Hash}", name, revisionHash[..16] + "...");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[PDF Verify] Não foi possível extrair revisão para '{Name}'.", name);
                        revisionHash = string.Empty;
                    }

                    preSignatureHashes.Add(revisionHash);

                    // ---- Verificar assinatura criptográfica ----
                    PdfPKCS7? pkcs7 = null;
                    try
                    {
                        pkcs7 = signUtil.ReadSignatureData(name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[PDF Verify] Não foi possível ler a assinatura '{Name}'.", name);
                        infos.Add(new PdfSignatureInfo(name, "Desconhecido", DateTime.MinValue, false, false));
                        continue;
                    }

                    var cert = pkcs7.GetSigningCertificate();
                    var subject = cert?.GetSubjectDN()?.ToString() ?? "Desconhecido";
                    var signingTime = pkcs7.GetSignDate();
                    bool isIntact;

                    try
                    {
                        isIntact = pkcs7.VerifySignatureIntegrityAndAuthenticity();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[PDF Verify] Falha ao verificar integridade da assinatura '{Name}'.", name);
                        isIntact = false;
                    }

                    _logger.LogInformation("[PDF Verify] Assinatura '{Name}': Sujeito={Subject}, Data={Date}, Íntegra={Intact}",
                        name, subject, signingTime, isIntact);

                    infos.Add(new PdfSignatureInfo(subject, subject, signingTime, isIntact, true));
                }

                var allValid = infos.Count >= expectedMinSignatureCount && infos.All(s => s.IsIntact);

                if (!allValid)
                {
                    var invalidSigs = infos.Where(s => !s.IsIntact).Select(s => s.CertificateSubject);
                    return new PdfSignatureVerificationResult(
                        false, infos.Count, infos, preSignatureHashes,
                        $"Uma ou mais assinaturas estão inválidas ou corrompidas: {string.Join(", ", invalidSigs)}");
                }

                return new PdfSignatureVerificationResult(true, infos.Count, infos, preSignatureHashes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PDF Verify] Erro ao verificar assinaturas do PDF.");
                return new PdfSignatureVerificationResult(
                    false, 0, [], [],
                    "Não foi possível processar o documento PDF. Certifica-te de que o ficheiro é um PDF válido e assinado digitalmente.");
            }
        });
    }
}
