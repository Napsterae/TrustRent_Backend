namespace TrustRent.Shared.Contracts.Interfaces;

public interface IGeminiDocumentService
{
    Task<T> ExtractDocumentAsync<T>(Stream fileStream, string fileName, string prompt)
        where T : class;

    Task<T> ExtractDocumentAsync<T>(IReadOnlyList<(Stream Stream, string FileName)> files, string prompt)
        where T : class;
}