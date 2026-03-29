using System.Text.RegularExpressions;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Modules.Catalog.Services;

public class DocumentExtractionService : IDocumentExtractionService
{
    private readonly IOcrService _ocrService;

    public DocumentExtractionService(IOcrService ocrService)
    {
        _ocrService = ocrService;
    }

    public async Task<DocumentExtractionResultDto> ExtractDataAsync(Stream fileStream, string fileName, string docType)
    {
        var extractedText = await _ocrService.ExtractTextAsync(fileStream, fileName);
        var normalizedText = extractedText.ToUpperInvariant().Replace("\n", " ");

        var result = new DocumentExtractionResultDto();

        if (docType == "caderneta")
        {
            var matchArtigo = Regex.Match(normalizedText, @"ARTIGO MATRICIAL[:\s]*(\d+)");
            var matchFracao = Regex.Match(normalizedText, @"FRAÇÃO[:\s]*([A-Z0-9]+)");

            result = result with
            {
                MatrixArticle = matchArtigo.Success ? matchArtigo.Groups[1].Value : null,
                PropertyFraction = matchFracao.Success ? matchFracao.Groups[1].Value : null
            };
        }
        else if (docType == "certificado")
        {
            var matchClass = Regex.Match(normalizedText, @"CLASSE ENERG[ÉE]TICA[:\s]*([A-F]\+?)");
            var matchNumber = Regex.Match(normalizedText, @"CERTIFICADO N[ºO][:.\s]*([\d\w-]+)");

            result = result with
            {
                EnergyClass = matchClass.Success ? matchClass.Groups[1].Value : null,
                EnergyCertNumber = matchNumber.Success ? matchNumber.Groups[1].Value : null
            };
        }
        else if (docType == "modelo2")
        {
            var matchReg = Regex.Match(normalizedText, @"N[ºO] DE REGISTO[:\s]*(\d+)");

            result = result with
            {
                AtRegistrationNumber = matchReg.Success ? matchReg.Groups[1].Value : null
            };
        }

        return result;
    }
}