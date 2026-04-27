using System.Globalization;
using System.Text;

namespace TrustRent.Shared.Security;

public static class EmailHelper
{
    /// <summary>
    /// Normalizes an email address by:
    /// 1. Trimming whitespace
    /// 2. Removing diacritics (e.g., "joão" → "joao", "ç" → "c")
    /// 3. Converting to lowercase
    /// 4. Validating format
    /// </summary>
    /// <param name="email">The raw email input</param>
    /// <returns>The normalized email</returns>
    /// <exception cref="ArgumentException">If the email format is invalid</exception>
    public static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null or empty.", nameof(email));

        var trimmed = email.Trim();

        // Validate basic email format before normalization
        ValidateEmailFormat(trimmed);

        var withoutDiacritics = RemoveDiacritics(trimmed);
        var normalized = withoutDiacritics.ToLowerInvariant();

        // Final validation after normalization
        if (!IsValidEmail(normalized))
            throw new ArgumentException($"Invalid email format after normalization: {normalized}", nameof(email));

        return normalized;
    }

    /// <summary>
    /// Tolerant variant of <see cref="NormalizeEmail"/> that returns false instead of throwing
    /// when the input is missing or malformed. Use on read paths (e.g. login lookups) where
    /// invalid input should be treated as "not found" rather than a server error.
    /// </summary>
    public static bool TryNormalizeEmail(string? email, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(email)) return false;
        try
        {
            normalized = NormalizeEmail(email);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Removes diacritical marks from a string (e.g., "ã" → "a", "ç" → "c", "ó" → "o").
    /// Handles Portuguese and other Western European characters.
    /// </summary>
    /// <param name="text">The input text</param>
    /// <returns>The text without diacritical marks</returns>
    public static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder(capacity: normalizedString.Length);

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static void ValidateEmailFormat(string email)
    {
        if (string.IsNullOrEmpty(email))
            throw new ArgumentException("Email cannot be null or empty.", nameof(email));

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
            throw new ArgumentException("Email must contain a '@' character.", nameof(email));

        var localPart = email[..atIndex];
        var domainPart = email[(atIndex + 1)..];

        if (string.IsNullOrEmpty(localPart) || string.IsNullOrEmpty(domainPart))
            throw new ArgumentException("Email local part or domain cannot be empty.", nameof(email));

        if (localPart.Length > 64)
            throw new ArgumentException("Email local part exceeds maximum length.", nameof(email));

        if (!IsValidEmail(email))
            throw new ArgumentException("Invalid email format.", nameof(email));
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
