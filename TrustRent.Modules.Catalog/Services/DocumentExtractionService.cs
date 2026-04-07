using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models.DocumentExtraction;
using TrustRent.Shared.Services;

namespace TrustRent.Modules.Catalog.Services;

public class DocumentExtractionService : IDocumentExtractionService
{
    private readonly IGeminiDocumentService _geminiService;

    public DocumentExtractionService(IGeminiDocumentService geminiService)
    {
        _geminiService = geminiService;
    }

    public async Task<DocumentExtractionResultDto> ExtractDataAsync(
        Stream fileStream, string fileName, string docType)
    {
        var prompt = DocumentPrompts.GetPromptForDocType(docType);

        return docType switch
        {
            "caderneta" => await ExtractCadernetaAsync(fileStream, fileName, prompt),
            "certificado" => await ExtractCertificadoAsync(fileStream, fileName, prompt),
            "modelo2" => await ExtractRegistoAtAsync(fileStream, fileName, prompt),
            "certidao" => await ExtractCertidaoAsync(fileStream, fileName, prompt),
            "licenca" => await ExtractLicencaAsync(fileStream, fileName, prompt),
            _ => throw new Exception("Tipo de documento não suportado.")
        };
    }

    private async Task<DocumentExtractionResultDto> ExtractCadernetaAsync(
        Stream fileStream, string fileName, string prompt)
    {
        var response = await _geminiService.ExtractDocumentAsync<CadernetaPredialResponse>(fileStream, fileName, prompt);
        ValidateResponse(response);
        return new DocumentExtractionResultDto(
            MatrixArticle: response.MatrixArticle,
            PropertyFraction: response.PropertyFraction,
            ParishConcelho: response.ParishConcelho
        );
    }

    private async Task<DocumentExtractionResultDto> ExtractCertificadoAsync(
        Stream fileStream, string fileName, string prompt)
    {
        var response = await _geminiService.ExtractDocumentAsync<CertificadoEnergeticoResponse>(fileStream, fileName, prompt);
        ValidateResponse(response);
        return new DocumentExtractionResultDto(
            EnergyClass: response.EnergyClass,
            EnergyCertNumber: response.EnergyCertNumber
        );
    }

    private async Task<DocumentExtractionResultDto> ExtractRegistoAtAsync(
        Stream fileStream, string fileName, string prompt)
    {
        var response = await _geminiService.ExtractDocumentAsync<RegistoAtResponse>(fileStream, fileName, prompt);
        ValidateResponse(response);
        return new DocumentExtractionResultDto(
            AtRegistrationNumber: response.AtRegistrationNumber
        );
    }

    private async Task<DocumentExtractionResultDto> ExtractCertidaoAsync(
        Stream fileStream, string fileName, string prompt)
    {
        var response = await _geminiService.ExtractDocumentAsync<CertidaoPermanenteResponse>(fileStream, fileName, prompt);
        ValidateResponse(response);
        return new DocumentExtractionResultDto(
            PermanentCertNumber: response.PermanentCertNumber,
            PermanentCertOffice: response.PermanentCertOffice
        );
    }

    private async Task<DocumentExtractionResultDto> ExtractLicencaAsync(
        Stream fileStream, string fileName, string prompt)
    {
        var response = await _geminiService.ExtractDocumentAsync<LicencaUtilizacaoResponse>(fileStream, fileName, prompt);
        ValidateResponse(response);
        return new DocumentExtractionResultDto(
            LicenseNumber: response.LicenseNumber,
            LicenseDate: response.LicenseDate,
            LicenseIssuer: response.LicenseIssuer
        );
    }

    private static void ValidateResponse(GeminiDocumentResponse response)
    {
        if (!response.IsAuthentic)
        {
            throw new Exception(
                "O documento enviado não passou na verificação de autenticidade. " +
                "Por favor, envia o documento original sem alterações."
            );
        }

        var qualityMessage = response.ImageQuality switch
        {
            "blurry" => "A imagem está desfocada. Tira uma nova foto com melhor foco e iluminação.",
            "dark" => "A imagem está muito escura. Tira uma nova foto com melhor iluminação.",
            "cropped" => "O documento parece estar cortado. Certifica-te que todo o documento está visível na foto.",
            "unreadable" => "Não foi possível ler o documento. Tenta com uma foto mais nítida ou com o PDF original.",
            _ => null
        };

        if (qualityMessage != null)
            throw new Exception(qualityMessage);

        if (!response.AllFieldsExtracted)
        {
            throw new Exception(
                "Não foi possível extrair toda a informação necessária do documento. " +
                "Verifica que o documento está completo, legível e bem iluminado. " +
                "Se estás a usar uma foto, tenta enviar o ficheiro PDF original."
            );
        }
    }
}