namespace TrustRent.Shared.Contracts.Interfaces;

public interface IOcrService
{
    // Recebe o stream do ficheiro e devolve todo o texto extraído da imagem
    Task<string> ExtractTextAsync(Stream fileStream, string fileName);
}