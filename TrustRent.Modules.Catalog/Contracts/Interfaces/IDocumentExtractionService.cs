using TrustRent.Modules.Catalog.Contracts.DTOs;

namespace TrustRent.Modules.Catalog.Contracts.Interfaces;

public interface IDocumentExtractionService
{
    Task<DocumentExtractionResultDto> ExtractDataAsync(Stream fileStream, string fileName, string docType);
}