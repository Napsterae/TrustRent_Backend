using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Shared.Services;

public class CloudinaryImageService : IImageService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryImageService(IConfiguration config)
    {
        var account = new Account(
            config["CloudinarySettings:CloudName"],
            config["CloudinarySettings:ApiKey"],
            config["CloudinarySettings:ApiSecret"]
        );

        _cloudinary = new Cloudinary(account);
        _cloudinary.Api.Secure = true; // Garante que gera links HTTPS
    }

    public async Task<string> UploadImageAsync(Stream fileStream, string fileName, string folder)
    {
        if (fileStream == null || fileStream.Length == 0)
            throw new ArgumentException("O ficheiro está vazio.");

        using var optimizedStream = await ImageOptimizer.OptimizeAsync(fileStream);

        var uniqueFileName = $"{Guid.NewGuid()}.webp";

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(uniqueFileName, optimizedStream),
            Folder = $"TrustRent/{folder}",
            UseFilename = true,
            UniqueFilename = true,
            Overwrite = false
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams);

        if (uploadResult.Error != null)
        {
            throw new Exception($"Erro no upload para a Cloud: {uploadResult.Error.Message}");
        }

        // Devolve o URL final, limpo e público!
        return uploadResult.SecureUrl.ToString();
    }

    public async Task DeleteImageAsync(string imageUrl)
    {
        try
        {
            // O URL do Cloudinary costuma ser: https://res.cloudinary.com/xyz/image/upload/v123/TrustRent/properties/id/foto.webp
            // Precisamos extrair apenas "TrustRent/properties/id/foto"
            var startIndex = imageUrl.IndexOf("TrustRent/");
            if (startIndex != -1)
            {
                var publicIdWithExt = imageUrl.Substring(startIndex);

                // Remover a extensão (.webp)
                var lastDotIndex = publicIdWithExt.LastIndexOf('.');
                var publicId = lastDotIndex != -1 ? publicIdWithExt.Substring(0, lastDotIndex) : publicIdWithExt;

                var deletionParams = new DeletionParams(publicId);
                await _cloudinary.DestroyAsync(deletionParams);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao apagar imagem do Cloudinary: {ex.Message}");
        }
    }
}