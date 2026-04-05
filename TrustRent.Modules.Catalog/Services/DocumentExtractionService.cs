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
            var matchFreguesia = Regex.Match(normalizedText, @"FREGUESIA[/\s]*(CONCELHO)[:\s]*(.+?)(?=\s{2,}|$)");
            if (!matchFreguesia.Success)
                matchFreguesia = Regex.Match(normalizedText, @"(?:FREGUESIA|CONCELHO)[:\s]*(.+?)(?=\s{2,}|ARTIGO|FRAÇÃO|$)");

            result = result with
            {
                MatrixArticle = matchArtigo.Success ? matchArtigo.Groups[1].Value : null,
                PropertyFraction = matchFracao.Success ? matchFracao.Groups[1].Value : null,
                ParishConcelho = matchFreguesia.Success ? matchFreguesia.Groups[matchFreguesia.Groups.Count - 1].Value.Trim() : null
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
        else if (docType == "certidao")
        {
            var matchDescricao = Regex.Match(normalizedText, @"(?:DESCRI[ÇC][ÃA]O|N[ºO]\s*DE\s*DESCRI[ÇC][ÃA]O)[:\s]*([\d/\-]+)");
            var matchConservatoria = Regex.Match(normalizedText, @"CONSERVAT[ÓO]RIA[:\s]*(?:DE\s*)?(.+?)(?=\s{2,}|N[ºO]|$)");

            result = result with
            {
                PermanentCertNumber = matchDescricao.Success ? matchDescricao.Groups[1].Value.Trim() : null,
                PermanentCertOffice = matchConservatoria.Success ? matchConservatoria.Groups[1].Value.Trim() : null
            };
        }
        else if (docType == "licenca")
        {
            var matchLicenca = Regex.Match(normalizedText, @"(?:ALVAR[ÁA]|LICEN[ÇC]A)\s*N[ºO][:\s]*([\d/\-]+)");
            var matchData = Regex.Match(normalizedText, @"(?:DATA\s*(?:DE\s*)?EMISS[ÃA]O|EMITID[OA]\s*(?:EM|A))[:\s]*(\d{2}[/\-]\d{2}[/\-]\d{4})");
            var matchCamara = Regex.Match(normalizedText, @"C[ÂA]MARA\s*MUNICIPAL[:\s]*(?:DE\s*)?(.+?)(?=\s{2,}|ALVAR|LICEN|$)");

            result = result with
            {
                LicenseNumber = matchLicenca.Success ? matchLicenca.Groups[1].Value.Trim() : null,
                LicenseDate = matchData.Success ? matchData.Groups[1].Value.Trim() : null,
                LicenseIssuer = matchCamara.Success ? matchCamara.Groups[1].Value.Trim() : null
            };
        }

        return result;
    }
}