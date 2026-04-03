using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Identity.Models;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Modules.Identity.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _uow;
    private readonly IImageService _imageService;
    private readonly IOcrService _ocrService;

    public UserService(IUnitOfWork uow, IImageService imageService, IOcrService ocrService)
    {
        _uow = uow;
        _imageService = imageService;
        _ocrService = ocrService;
    }

    public async Task<User?> GetProfileAsync(Guid userId)
        => await _uow.Users.GetByIdAsync(userId);

    public async Task<PublicUserProfileDto?> GetPublicProfileAsync(Guid userId)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null) return null;

        return new PublicUserProfileDto(
            user.Id,
            user.Name,
            user.ProfilePictureUrl,
            user.TrustScore,
            user.IsIdentityVerified,
            user.IsNoDebtVerified
        );
    }

    public async Task UpdateProfileAsync(Guid userId, UpdateProfileDto request)
    {
        var user = await _uow.Users.GetByIdAsync(userId) ?? throw new Exception("Utilizador não encontrado.");

        if (user.IsIdentityVerified)
        {
            if (request.Name != user.Name || request.Nif != user.Nif || request.CitizenCardNumber != user.CitizenCardNumber)
            {
                throw new Exception("Não podes alterar o Nome, NIF ou Cartão de Cidadão após a validação de identidade.");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Nif))
        {
            if (request.Nif.Length != 9 || !request.Nif.All(char.IsDigit))
                throw new Exception("O NIF deve conter exatamente 9 números.");

            if (!await _uow.Users.IsNifUniqueAsync(request.Nif, userId))
                throw new Exception("Este NIF já está registado noutra conta.");
        }

        if (!string.IsNullOrWhiteSpace(request.CitizenCardNumber))
        {
            if (request.CitizenCardNumber.Length != 8 || !request.CitizenCardNumber.All(char.IsDigit))
                throw new Exception("O Número do Cartão de Cidadão deve conter exatamente os 8 números principais.");

            if (!await _uow.Users.IsCcUniqueAsync(request.CitizenCardNumber, userId))
                throw new Exception("Este Cartão de Cidadão já está registado noutra conta.");
        }

        user.Name = request.Name;
        user.Email = request.Email;
        user.Nif = request.Nif;
        user.CitizenCardNumber = request.CitizenCardNumber;
        user.Address = request.Address;
        user.PostalCode = request.PostalCode;

        await _uow.SaveChangesAsync();
    }

    public async Task UpdatePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await _uow.Users.GetByIdAsync(userId) ?? throw new Exception("Utilizador não encontrado.");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            throw new Exception("Password atual incorreta.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _uow.SaveChangesAsync();
    }

    public async Task<string> UpdateAvatarAsync(Guid userId, Stream fileStream, string fileName)
    {
        var user = await _uow.Users.GetByIdAsync(userId) ?? throw new Exception("Utilizador não encontrado.");

        // O ImageService trata da magia toda
        var cloudUrl = await _imageService.UploadImageAsync(fileStream, fileName, "profiles");

        user.ProfilePictureUrl = cloudUrl;
        await _uow.SaveChangesAsync();

        return user.ProfilePictureUrl;
    }

    public async Task<VerificationResultDto> VerifyDocumentsAsync(
        Guid userId, Stream? ccFrontStream, 
        string? ccFrontFileName, 
        Stream? ccBackStream, 
        string? ccBackFileName, 
        Stream? noDebtStream, 
        string? noDebtFileName)
    {
        var user = await _uow.Users.GetByIdAsync(userId) ?? throw new Exception("Utilizador não encontrado.");

        // 1. VALIDAÇÃO DO CARTÃO DE CIDADÃO (FRENTE E VERSO)
        if (ccFrontStream != null && ccFrontStream.Length > 0 && ccBackStream != null && ccBackStream.Length > 0)
        {
            // Extrai o texto da Frente
            var frontText = await _ocrService.ExtractTextAsync(ccFrontStream, ccFrontFileName);
            // Extrai o texto do Verso
            var backText = await _ocrService.ExtractTextAsync(ccBackStream, ccBackFileName);

            // Junta tudo! Assim procuramos o NIF, Nome e Validade nesta grande "sopa de letras"
            var combinedText = frontText + " \n " + backText;
            var normalizedText = RemoveDiacritics(combinedText).ToUpperInvariant().Replace(" ", "");

            bool hasNif = !string.IsNullOrEmpty(user.Nif) && Regex.IsMatch(normalizedText, $@"(?<!\d){user.Nif}(?!\d)");
            bool hasCcNum = string.IsNullOrEmpty(user.CitizenCardNumber) ||
                Regex.IsMatch(normalizedText, $@"(?<!\d){user.CitizenCardNumber}(?:\d[A-Z]|\D|$)");

            var normalizedUserName = RemoveDiacritics(user.Name).ToUpperInvariant();
            var nameParts = normalizedUserName.Split(' ');
            bool hasFirstName = normalizedText.Contains(nameParts.First());
            bool hasLastName = nameParts.Length > 1 && normalizedText.Contains(nameParts.Last());

            if (!hasNif || !hasFirstName || !hasCcNum)
            {
                throw new Exception("Validação CC Falhou: O Nome ou NIF não foram detetados ou não coincidem.");
            }

            user.IsIdentityVerified = true;
            user.IdentityExpiryDate = ExtractExpiryDate(combinedText);
            user.TrustScore += 20;
        }

        // 2. VALIDAÇÃO DA CERTIDÃO DE NÃO DÍVIDA
        if (noDebtStream != null && noDebtStream.Length > 0 && !string.IsNullOrEmpty(noDebtFileName))
        {
            var extractedText = await _ocrService.ExtractTextAsync(noDebtStream, noDebtFileName);
            var normalizedText = extractedText.ToUpperInvariant();

            // Verifica se o NIF do utilizador está no documento
            bool hasNif = !string.IsNullOrEmpty(user.Nif) && normalizedText.Contains(user.Nif);

            // Verifica a frase mágica da AT (Autoridade Tributária)
            bool isRegularized = normalizedText.Contains("SITUAÇÃO TRIBUTÁRIA REGULARIZADA") ||
                                 normalizedText.Contains("SITUACAO TRIBUTARIA REGULARIZADA");

            if (!hasNif || !isRegularized)
            {
                throw new Exception("Validação AT Falhou: Certidão inválida, não pertence ao utilizador ou deteta dívidas.");
            }

            user.IsNoDebtVerified = true;
            user.NoDebtExpiryDate = ExtractExpiryDate(extractedText); // Extrai a data!
            user.TrustScore += 15;
        }

        await _uow.SaveChangesAsync();
        return new VerificationResultDto(user.IsIdentityVerified, user.IdentityExpiryDate, user.IsNoDebtVerified, user.NoDebtExpiryDate, user.TrustScore);
    }

    // Helper Mágico para extrair datas
    private static DateTime? ExtractExpiryDate(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // 1. Procura Formato Português/Europeu: DD MM YYYY (com espaços, barras, pontos ou traços)
        // Permite até 50 caracteres de "lixo" (como o número do CC) entre a palavra "EXPIRY" e a data.
        var matchPT = Regex.Match(text, @"(?:V[ÁA]LID[OA]\s*AT[ÉE]|VALIDADE|EXPIRY\s*DATE)[\s\S]{1,50}?\b(\d{2})[\s.\-/]+(\d{2})[\s.\-/]+(\d{4})\b", RegexOptions.IgnoreCase);

        if (matchPT.Success)
        {
            var day = matchPT.Groups[1].Value;
            var month = matchPT.Groups[2].Value;
            var year = matchPT.Groups[3].Value;

            // Forçamos o formato DD/MM/YYYY limpo
            var dateStr = $"{day}/{month}/{year}";
            if (DateTime.TryParseExact(dateStr, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsedDate))
            {
                return parsedDate;
            }
        }

        // 2. Procura Formato Internacional/AT: YYYY MM DD (muito comum em PDFs das Finanças)
        var matchInt = Regex.Match(text, @"(?:V[ÁA]LID[OA]\s*AT[ÉE]|VALIDADE)[\s\S]{1,50}?\b(\d{4})[\s.\-/]+(\d{2})[\s.\-/]+(\d{2})\b", RegexOptions.IgnoreCase);

        if (matchInt.Success)
        {
            var year = matchInt.Groups[1].Value;
            var month = matchInt.Groups[2].Value;
            var day = matchInt.Groups[3].Value;

            var dateStr = $"{day}/{month}/{year}";
            if (DateTime.TryParseExact(dateStr, "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsedDate))
            {
                return parsedDate;
            }
        }

        return null;
    }

    // Helper para remover acentos (ex: "Estêvão" vira "Estevao")
    private static string RemoveDiacritics(string text)
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
}