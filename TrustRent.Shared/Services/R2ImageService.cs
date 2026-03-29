using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Shared.Services;

public class R2ImageService : IImageService
{
    private readonly AmazonS3Client _s3Client;
    private readonly string _bucketName;
    private readonly string _publicDomain;

    public R2ImageService(IConfiguration config)
    {
        var accessKey = config["CloudflareR2Settings:AccessKey"];
        var secretKey = config["CloudflareR2Settings:SecretKey"];
        var serviceUrl = config["CloudflareR2Settings:ServiceUrl"];

        _bucketName = config["CloudflareR2Settings:BucketName"]!;
        _publicDomain = config["CloudflareR2Settings:PublicDomain"]!;

        // Configuração específica para a R2 (compatibilidade S3)
        var s3Config = new AmazonS3Config
        {
            ServiceURL = serviceUrl,
        };

        _s3Client = new AmazonS3Client(accessKey, secretKey, s3Config);
    }

    public async Task<string> UploadImageAsync(Stream fileStream, string fileName, string folder)
    {
        if (fileStream == null || fileStream.Length == 0)
            throw new ArgumentException("O ficheiro está vazio.");

        // Otimizamos a imagem para max 1920px e formato WebP
        using var optimizedStream = await ImageOptimizer.OptimizeAsync(fileStream);

        // Como vamos guardar em WebP, forçamos a extensão e o nome
        var uniqueFileName = $"{Guid.NewGuid()}.webp";
        var objectKey = $"{folder}/{uniqueFileName}";

        // Caminho final no Bucket (ex: "profiles/1234-5678.jpg")
        var putRequest = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            InputStream = optimizedStream, // Enviamos o stream já otimizado e muito mais leve!
            ContentType = "image/webp",    // Atualizamos o tipo de ficheiro
            DisablePayloadSigning = true
        };

        var response = await _s3Client.PutObjectAsync(putRequest);

        if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
        {
            throw new Exception("Falha ao fazer upload da imagem para a Cloudflare R2.");
        }

        // Devolve o URL público que o React vai ler (ex: https://pub-dummy.r2.dev/profiles/1234-5678.jpg)
        return $"{_publicDomain}/{objectKey}";
    }

    public async Task DeleteImageAsync(string imageUrl)
    {
        try
        {
            // Extrair a "Key" (caminho interno) a partir do URL público
            // Ex: "https://pub-dummy.r2.dev/properties/123/foto.webp" -> "properties/123/foto.webp"
            var objectKey = imageUrl.Replace($"{_publicDomain}/", "");

            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            };

            await _s3Client.DeleteObjectAsync(deleteRequest);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao apagar imagem da Cloudflare R2: {ex.Message}");
        }
    }

    // Helper para descobrir o Content-Type
    private string GetContentType(string extension)
    {
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };
    }
}