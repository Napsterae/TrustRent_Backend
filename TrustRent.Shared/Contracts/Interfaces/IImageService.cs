namespace TrustRent.Shared.Contracts.Interfaces;

public interface IImageService
{
    // Recebe o stream, o nome original e a pasta de destino (ex: "profiles" ou "properties")
    Task<string> UploadImageAsync(Stream fileStream, string fileName, string folder);
}