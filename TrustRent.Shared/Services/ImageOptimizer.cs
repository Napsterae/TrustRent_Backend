using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace TrustRent.Shared.Services;

public static class ImageOptimizer
{
    // Recebe o stream original e devolve um novo stream otimizado em WebP
    public static async Task<Stream> OptimizeAsync(Stream inputStream, int maxWidth = 1920)
    {
        var outputStream = new MemoryStream();

        // Carrega a imagem para a memória usando o ImageSharp
        using var image = await Image.LoadAsync(inputStream);

        // Se a imagem for mais larga que o nosso limite (ex: 1920px), redimensionamos!
        if (image.Width > maxWidth)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(maxWidth, 0), // O 0 faz com que a altura seja calculada automaticamente para manter a proporção
                Mode = ResizeMode.Max
            }));
        }

        // Guarda a imagem no outputStream usando o formato WebP (alta qualidade, baixo peso)
        var encoder = new WebpEncoder { Quality = 80 }; // 80% de qualidade é o "sweet spot" para a web
        await image.SaveAsWebpAsync(outputStream, encoder);

        // MUITO IMPORTANTE: Repor a "agulha" do stream no início para que a AWS/Cloudinary consigam ler o ficheiro otimizado desde o princípio
        outputStream.Position = 0;

        return outputStream;
    }
}