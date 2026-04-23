using TrustRent.Shared.Models.DocumentExtraction;

namespace TrustRent.Shared.Services;

/// <summary>
/// Validação partilhada das respostas do Gemini Document Service:
/// autenticidade, qualidade da imagem e completude dos campos.
/// </summary>
public static class GeminiResponseValidator
{
    public static void EnsureValid(GeminiDocumentResponse response, string? documentLabel = null)
    {
        var prefix = string.IsNullOrWhiteSpace(documentLabel) ? string.Empty : $"[{documentLabel}] ";

        if (!response.IsAuthentic)
        {
            throw new Exception(
                $"{prefix}O documento enviado não passou na verificação de autenticidade. " +
                "Por favor, envia o documento original sem alterações."
            );
        }

        var qualityMessage = response.ImageQuality switch
        {
            "blurry" => "A imagem está desfocada. Tira uma nova foto com melhor foco e iluminação.",
            "dark" => "A imagem está muito escura. Tira uma nova foto com melhor iluminação.",
            "cropped" => "O documento parece estar cortado. Certifica-te que todo o documento está visível na foto.",
            "unreadable" => "Não foi possível ler o documento. Tenta com uma foto mais nítida ou com o PDF original.",
            _ => null
        };

        if (qualityMessage != null)
            throw new Exception($"{prefix}{qualityMessage}");

        if (!response.AllFieldsExtracted)
        {
            throw new Exception(
                $"{prefix}Não foi possível extrair toda a informação necessária do documento. " +
                "Verifica que o documento está completo, legível e bem iluminado. " +
                "Se estás a usar uma foto, tenta enviar o ficheiro PDF original."
            );
        }
    }
}
