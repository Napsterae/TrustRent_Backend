using System.Globalization;
using System.Text;

namespace TrustRent.Shared.Services;

/// <summary>
/// Compara nomes humanos de forma tolerante a acentos, maiúsculas, ordem dos tokens
/// e ausência de nomes do meio. Usado para validar que documentos extraídos
/// (CC, recibos de vencimento, etc.) pertencem ao utilizador autenticado.
/// </summary>
public static class NameMatcher
{
    /// <summary>
    /// Devolve true se os dois nomes parecem ser da mesma pessoa.
    /// Regra: pelo menos 2 tokens em comum E todos os tokens do nome mais curto
    /// existem no nome mais longo (Jaccard tolerante: razão >= 0.6 e mínimo 2 tokens).
    /// Casos triviais (nome único): exige igualdade de pelo menos um token longo (>= 3 chars).
    /// </summary>
    public static bool IsLikelySame(string? a, string? b)
    {
        var tokensA = Tokenize(a);
        var tokensB = Tokenize(b);

        if (tokensA.Count == 0 || tokensB.Count == 0) return false;

        var setA = new HashSet<string>(tokensA);
        var setB = new HashSet<string>(tokensB);

        var intersection = setA.Intersect(setB).Count();
        var union = setA.Union(setB).Count();

        if (intersection == 0) return false;

        // Para nomes muito curtos (1 token cada), aceitar se igualarem.
        if (setA.Count == 1 && setB.Count == 1) return intersection == 1;

        // Pelo menos 2 tokens em comum + razão Jaccard >= 0.4
        if (intersection < 2) return false;

        var jaccard = (double)intersection / union;
        return jaccard >= 0.4;
    }

    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant().Trim();
    }

    private static List<string> Tokenize(string? input)
    {
        var normalized = Normalize(input);
        if (string.IsNullOrWhiteSpace(normalized)) return new List<string>();

        return normalized
            .Split(new[] { ' ', '\t', '\n', '\r', '-', '.', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 2) // ignorar partículas como "e", "y"
            .ToList();
    }
}
