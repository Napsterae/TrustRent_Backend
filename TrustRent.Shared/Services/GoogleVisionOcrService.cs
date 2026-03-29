using Google.Apis.Auth.OAuth2;
using Google.Cloud.Vision.V1;
using Google.Protobuf;
using Microsoft.Extensions.Configuration;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Shared.Services;

public class GoogleVisionOcrService : IOcrService
{
    private readonly ImageAnnotatorClient _client;

    public GoogleVisionOcrService(IConfiguration config)
    {
        var credentialsPath = config["GoogleCloud:CredentialsPath"];

        if (!string.IsNullOrEmpty(credentialsPath) && File.Exists(credentialsPath))
        {
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", credentialsPath);
        }

        _client = ImageAnnotatorClient.Create();

        /*
        if (!string.IsNullOrEmpty(credentialsPath) && File.Exists(credentialsPath))
        {
            // Lê o ficheiro JSON de credenciais
            var credential = GoogleCredential.FromFile(credentialsPath);
            var builder = new ImageAnnotatorClientBuilder
            {
                Credential = credential
            };
            _client = builder.Build();
        }
        else
        {
            // Se não houver ficheiro, tenta usar a variável de ambiente GOOGLE_APPLICATION_CREDENTIALS
            // (Isto é útil para quando publicares o projeto na Cloud/Azure/AWS)
            _client = ImageAnnotatorClient.Create();
        }*/
    }

    public async Task<string> ExtractTextAsync(Stream fileStream, string fileName)
    {
        if (fileStream == null || fileStream.Length == 0) return string.Empty;
        fileStream.Position = 0;

        var extension = Path.GetExtension(fileName).ToLower();
        var byteString = await ByteString.FromStreamAsync(fileStream);

        // SE FOR PDF: Usamos o método nativo de ficheiros da Google
        if (extension == ".pdf")
        {
            var request = new AnnotateFileRequest
            {
                InputConfig = new InputConfig { Content = byteString, MimeType = "application/pdf" },
                Features = { new Feature { Type = Feature.Types.Type.DocumentTextDetection } },
                Pages = { 1 } // Lê apenas a 1ª página para poupar custos
            };

            var batchRequest = new BatchAnnotateFilesRequest { Requests = { request } };
            var response = await _client.BatchAnnotateFilesAsync(batchRequest);

            var fileResponse = response.Responses.FirstOrDefault();
            return fileResponse?.Responses.FirstOrDefault()?.FullTextAnnotation?.Text ?? string.Empty;
        }

        // SE FOR IMAGEM (JPG/PNG): Usamos o método normal
        var image = Image.FromBytes(byteString.ToByteArray());
        var imgResponse = await _client.DetectDocumentTextAsync(image);
        return imgResponse?.Text ?? string.Empty;
    }
}