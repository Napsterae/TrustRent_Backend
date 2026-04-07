using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Shared.Services;

public class GeminiDocumentService : IGeminiDocumentService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<GeminiDocumentService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GeminiDocumentService(HttpClient httpClient, IConfiguration config, ILogger<GeminiDocumentService> logger)
    {
        _httpClient = httpClient;
        _apiKey = config["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini:ApiKey não está configurada.");
        _model = config["Gemini:Model"] ?? "gemini-2.0-flash";
        _logger = logger;
    }

    public async Task<T> ExtractDocumentAsync<T>(Stream fileStream, string fileName, string prompt)
        where T : class
    {
        return await ExtractDocumentAsync<T>(
            new List<(Stream Stream, string FileName)> { (fileStream, fileName) },
            prompt);
    }

    public async Task<T> ExtractDocumentAsync<T>(IReadOnlyList<(Stream Stream, string FileName)> files, string prompt)
        where T : class
    {
        var parts = new List<object>();

        foreach (var (stream, fileName) in files)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var base64 = Convert.ToBase64String(ms.ToArray());

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var mimeType = extension switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            parts.Add(new { inline_data = new { mime_type = mimeType, data = base64 } });
        }

        parts.Add(new { text = prompt });

        var requestBody = new
        {
            contents = new[]
            {
                new { parts }
            },
            generationConfig = new
            {
                response_mime_type = "application/json",
                temperature = 0.1
            }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync(url, content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao comunicar com a API do Gemini");
            throw new Exception("Erro de ligação à API de Inteligência Artificial. Tenta novamente.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("Gemini API error {StatusCode}: {Body}", response.StatusCode, errorBody);
            
            // Tentar extrair uma mensagem de erro mais amigável do JSON do Gemini
            string userMessage = "Erro ao processar o documento com IA.";
            try 
            {
                using var errDoc = JsonDocument.Parse(errorBody);
                if (errDoc.RootElement.TryGetProperty("error", out var errorEl) && 
                    errorEl.TryGetProperty("message", out var msgEl))
                {
                    var geminiMsg = msgEl.GetString() ?? "";
                    if (geminiMsg.Contains("not found"))
                        userMessage = $"O modelo de IA '{_model}' não foi encontrado ou não é suportado.";
                    else if (geminiMsg.Contains("API key"))
                        userMessage = "Erro de autenticação com a API de IA. Verifica a ApiKey.";
                }
            } catch { /* ignore parsing errors */ }

            throw new Exception(userMessage);
        }

        var responseJson = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            _logger.LogWarning("Gemini retornou resposta sem candidatos: {Response}", responseJson);
            throw new Exception("Não foi possível processar o documento. Tenta novamente.");
        }

        var textContent = candidates[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrEmpty(textContent))
        {
            _logger.LogWarning("Gemini retornou texto vazio");
            throw new Exception("Não foi possível ler o documento. Tenta com uma foto mais nítida ou com o PDF original.");
        }

        var result = JsonSerializer.Deserialize<T>(textContent, JsonOptions);

        return result ?? throw new Exception("Erro ao interpretar a resposta da IA.");
    }
}