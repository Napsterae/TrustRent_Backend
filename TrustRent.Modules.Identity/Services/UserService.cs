using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Identity.Models;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models.DocumentExtraction;
using TrustRent.Shared.Services;

namespace TrustRent.Modules.Identity.Services;

public class UserService : IUserService
{
    private sealed record PhoneRule(string DialCode, Regex MobileRegex, string Example);

    private static readonly IReadOnlyDictionary<string, PhoneRule> PhoneRules = new Dictionary<string, PhoneRule>
    {
        ["PT"] = new("351", new Regex("^9\\d{8}$", RegexOptions.Compiled), "912345678"),
        ["ES"] = new("34", new Regex("^[67]\\d{8}$", RegexOptions.Compiled), "612345678"),
        ["FR"] = new("33", new Regex("^[67]\\d{8}$", RegexOptions.Compiled), "612345678"),
        ["DE"] = new("49", new Regex("^1\\d{9,11}$", RegexOptions.Compiled), "15123456789"),
        ["IT"] = new("39", new Regex("^3\\d{8,9}$", RegexOptions.Compiled), "3123456789"),
        ["GB"] = new("44", new Regex("^7\\d{9}$", RegexOptions.Compiled), "7400123456"),
        ["IE"] = new("353", new Regex("^8[35679]\\d{7}$", RegexOptions.Compiled), "831234567"),
        ["NL"] = new("31", new Regex("^6\\d{8}$", RegexOptions.Compiled), "612345678"),
        ["BE"] = new("32", new Regex("^4\\d{8}$", RegexOptions.Compiled), "470123456"),
        ["LU"] = new("352", new Regex("^(621|628|661|671|691)\\d{6}$", RegexOptions.Compiled), "621123456"),
        ["CH"] = new("41", new Regex("^7[5-9]\\d{7}$", RegexOptions.Compiled), "791234567"),
        ["US"] = new("1", new Regex("^[2-9]\\d{9}$", RegexOptions.Compiled), "2025550123"),
        ["CA"] = new("1", new Regex("^[2-9]\\d{9}$", RegexOptions.Compiled), "4165550123"),
        ["BR"] = new("55", new Regex("^\\d{2}9\\d{8}$", RegexOptions.Compiled), "11912345678"),
        ["AO"] = new("244", new Regex("^9[1-5]\\d{7}$", RegexOptions.Compiled), "923456789"),
        ["MZ"] = new("258", new Regex("^8[2-7]\\d{7}$", RegexOptions.Compiled), "841234567"),
        ["CV"] = new("238", new Regex("^[59]\\d{6}$", RegexOptions.Compiled), "9912345"),
        ["ST"] = new("239", new Regex("^9\\d{6}$", RegexOptions.Compiled), "9812345"),
        ["GW"] = new("245", new Regex("^[5-7]\\d{6}$", RegexOptions.Compiled), "6512345"),
        ["TL"] = new("670", new Regex("^7\\d{7}$", RegexOptions.Compiled), "77234567")
    };

    private readonly IUnitOfWork _uow;
    private readonly IImageService _imageService;
    private readonly IGeminiDocumentService _geminiService;
    private readonly IUserContactAccessService _userContactAccessService;

    public UserService(
        IUnitOfWork uow,
        IImageService imageService,
        IGeminiDocumentService geminiService,
        IUserContactAccessService userContactAccessService)
    {
        _uow = uow;
        _imageService = imageService;
        _geminiService = geminiService;
        _userContactAccessService = userContactAccessService;
    }

    public async Task<User?> GetProfileAsync(Guid userId)
        => await _uow.Users.GetByIdAsync(userId);

    public async Task<UserProfileDto?> GetProfileDtoAsync(Guid userId)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null) return null;

        return new UserProfileDto(
            user.Id,
            user.Name,
            user.Email,
            user.Nif,
            user.CitizenCardNumber,
            user.Address,
            user.PostalCode,
            user.PhoneCountryCode,
            user.PhoneNumber,
            user.ProfilePictureUrl,
            user.IsIdentityVerified,
            user.IdentityExpiryDate,
            user.IsNoDebtVerified,
            user.NoDebtExpiryDate,
            user.TrustScore
        );
    }

    public async Task<PublicUserProfileDto?> GetPublicProfileAsync(Guid userId, Guid viewerUserId)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        if (user == null) return null;

        var canViewDirectContact = viewerUserId == userId
            || await _userContactAccessService.CanViewDirectContactAsync(viewerUserId, userId);

        return new PublicUserProfileDto(
            user.Id,
            user.Name,
            canViewDirectContact ? user.Email : null,
            user.ProfilePictureUrl,
            user.TrustScore,
            user.IsIdentityVerified,
            user.IsNoDebtVerified,
            canViewDirectContact ? user.PhoneCountryCode : null,
            canViewDirectContact ? user.PhoneNumber : null
        );
    }

    public async Task UpdateProfileAsync(Guid userId, UpdateProfileDto request)
    {
        var user = await _uow.Users.GetByIdAsync(userId) ?? throw new Exception("Utilizador não encontrado.");
        var normalizedName = request.Name.Trim();
        var normalizedEmail = request.Email.Trim();
        var normalizedNif = NormalizeOptionalValue(request.Nif);
        var normalizedCitizenCardNumber = NormalizeOptionalDigits(request.CitizenCardNumber);
        var normalizedAddress = NormalizeOptionalValue(request.Address);
        var normalizedPostalCode = NormalizeOptionalValue(request.PostalCode);
        var (normalizedPhoneCountryCode, normalizedPhoneNumber) = NormalizePhone(request.PhoneCountryCode, request.PhoneNumber);

        if (user.IsIdentityVerified)
        {
            if (normalizedName != user.Name || normalizedNif != user.Nif || normalizedCitizenCardNumber != user.CitizenCardNumber)
            {
                throw new Exception("Não podes alterar o Nome, NIF ou Cartão de Cidadão após a validação de identidade.");
            }
        }

        if (!string.IsNullOrWhiteSpace(normalizedNif))
        {
            if (normalizedNif.Length != 9 || !normalizedNif.All(char.IsDigit))
                throw new Exception("O NIF deve conter exatamente 9 números.");

            if (!await _uow.Users.IsNifUniqueAsync(normalizedNif, userId))
                throw new Exception("Este NIF já está registado noutra conta.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedCitizenCardNumber))
        {
            if (normalizedCitizenCardNumber.Length != 8 || !normalizedCitizenCardNumber.All(char.IsDigit))
                throw new Exception("O Número do Cartão de Cidadão deve conter exatamente os 8 números principais.");

            if (!await _uow.Users.IsCcUniqueAsync(normalizedCitizenCardNumber, userId))
                throw new Exception("Este Cartão de Cidadão já está registado noutra conta.");
        }

        user.Name = normalizedName;
        user.Email = normalizedEmail;
        user.Nif = normalizedNif;
        user.CitizenCardNumber = normalizedCitizenCardNumber;
        user.Address = normalizedAddress;
        user.PostalCode = normalizedPostalCode;
        user.PhoneCountryCode = normalizedPhoneCountryCode;
        user.PhoneNumber = normalizedPhoneNumber;

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

        string? extractedName = null;
        string? extractedNif = null;
        string? extractedCc = null;

        // 1. VALIDAÇÃO DO CARTÃO DE CIDADÃO (FRENTE E VERSO)
        if (ccFrontStream != null && ccFrontStream.Length > 0 && ccBackStream != null && ccBackStream.Length > 0)
        {
            var files = new List<(Stream Stream, string FileName)>
            {
                (ccFrontStream, ccFrontFileName ?? "frente.jpg"),
                (ccBackStream, ccBackFileName ?? "verso.jpg")
            };

            var cc = await _geminiService.ExtractDocumentAsync<CartaoCidadaoResponse>(files, DocumentPrompts.CartaoCidadao);

            ValidateDocumentResponse(cc);

            // Normalizar e limpar campos extraídos
            extractedName = $"{cc.FirstNames?.Trim()} {cc.LastNames?.Trim()}".Trim();
            
            // Fallback para FullName se os específicos falharem
            if (string.IsNullOrWhiteSpace(extractedName))
                extractedName = cc.FullName?.Trim();

            extractedNif = cc.Nif?.Replace(" ", "").Replace("-", "").Trim();
            extractedCc = cc.CitizenCardNumber?.Replace(" ", "").Replace("-", "").Trim();

            // Validações básicas de formato para os campos extraídos
            if (string.IsNullOrWhiteSpace(extractedName) || string.IsNullOrWhiteSpace(extractedNif) || string.IsNullOrWhiteSpace(extractedCc))
            {
                throw new Exception("Não foi possível extrair todos os campos obrigatórios do Cartão de Cidadão (Nome, NIF ou Número CC).");
            }

            // Verificar unicidade se mudarem
            if (extractedNif != user.Nif && !await _uow.Users.IsNifUniqueAsync(extractedNif, userId))
                throw new Exception("O NIF extraído do documento já está registado noutra conta.");

            if (extractedCc != user.CitizenCardNumber && !await _uow.Users.IsCcUniqueAsync(extractedCc, userId))
                throw new Exception("O Cartão de Cidadão extraído do documento já está registado noutra conta.");

            // SUBSTITUIÇÃO DOS CAMPOS
            user.Name = extractedName;
            user.Nif = extractedNif;
            user.CitizenCardNumber = extractedCc;
            
            user.IsIdentityVerified = true;
            user.IdentityExpiryDate = ParseDate(cc.ExpiryDate);
            user.TrustScore += 20;
        }

        // 2. VALIDAÇÃO DA CERTIDÃO DE NÃO DÍVIDA
        if (noDebtStream != null && noDebtStream.Length > 0 && !string.IsNullOrEmpty(noDebtFileName))
        {
            var cert = await _geminiService.ExtractDocumentAsync<CertidaoNaoDividaResponse>(
                noDebtStream, noDebtFileName, DocumentPrompts.CertidaoNaoDivida);

            ValidateDocumentResponse(cert);

            bool hasNif = !string.IsNullOrEmpty(user.Nif)
                && !string.IsNullOrEmpty(cert.Nif)
                && cert.Nif.Contains(user.Nif);

            if (!hasNif || !cert.IsTaxRegularized)
            {
                throw new Exception("Validação AT Falhou: Certidão inválida, não pertence ao utilizador ou deteta dívidas.");
            }

            user.IsNoDebtVerified = true;
            user.NoDebtExpiryDate = ParseDate(cert.ExpiryDate);
            user.TrustScore += 15;
        }

        await _uow.SaveChangesAsync();
        return new VerificationResultDto(
            user.IsIdentityVerified, 
            user.IdentityExpiryDate, 
            user.IsNoDebtVerified, 
            user.NoDebtExpiryDate, 
            user.TrustScore,
            extractedName,
            extractedNif,
            extractedCc
        );
    }

    /// <summary>
    /// DEV-ONLY: Marca o Cartão de Cidadão como validado sem chamar a IA, reutilizando os
    /// dados pessoais já presentes no perfil (preenchidos no registo). O endpoint que
    /// expõe este método tem de ser bloqueado fora de ambientes de desenvolvimento.
    /// </summary>
    public async Task<VerificationResultDto> SimulateVerifyCitizenCardAsync(Guid userId)
    {
        var user = await _uow.Users.GetByIdAsync(userId) ?? throw new Exception("Utilizador não encontrado.");

        if (string.IsNullOrWhiteSpace(user.Nif) || string.IsNullOrWhiteSpace(user.CitizenCardNumber))
        {
            throw new Exception("Para simular a validação preenche primeiro o NIF e o número do Cartão de Cidadão no perfil.");
        }

        if (!user.IsIdentityVerified)
        {
            user.TrustScore += 20;
        }

        user.IsIdentityVerified = true;
        user.IdentityExpiryDate = DateTime.UtcNow.AddYears(5);

        await _uow.SaveChangesAsync();

        return new VerificationResultDto(
            user.IsIdentityVerified,
            user.IdentityExpiryDate,
            user.IsNoDebtVerified,
            user.NoDebtExpiryDate,
            user.TrustScore,
            user.Name,
            user.Nif,
            user.CitizenCardNumber
        );
    }

    /// <summary>
    /// DEV-ONLY: Marca a Certidão de Não Dívida como validada sem chamar a IA. Exige NIF
    /// preenchido (igual ao fluxo real). O endpoint é bloqueado fora de Development.
    /// </summary>
    public async Task<VerificationResultDto> SimulateVerifyNoDebtAsync(Guid userId)
    {
        var user = await _uow.Users.GetByIdAsync(userId) ?? throw new Exception("Utilizador não encontrado.");

        if (string.IsNullOrWhiteSpace(user.Nif))
            throw new Exception("Para simular a validação preenche primeiro o NIF no perfil.");

        if (!user.IsNoDebtVerified)
            user.TrustScore += 15;

        user.IsNoDebtVerified = true;
        // As certidões de não dívida (AT/SS) costumam ter validade de 3 meses.
        user.NoDebtExpiryDate = DateTime.UtcNow.AddMonths(3);

        await _uow.SaveChangesAsync();

        return new VerificationResultDto(
            user.IsIdentityVerified,
            user.IdentityExpiryDate,
            user.IsNoDebtVerified,
            user.NoDebtExpiryDate,
            user.TrustScore,
            user.Name,
            user.Nif,
            user.CitizenCardNumber
        );
    }

    private static void ValidateDocumentResponse(GeminiDocumentResponse response)
    {
        if (!response.IsAuthentic)
        {
            throw new Exception(
                "O documento enviado não passou na verificação de autenticidade. " +
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
            throw new Exception(qualityMessage);

        if (!response.AllFieldsExtracted)
        {
            throw new Exception(
                "Não foi possível extrair toda a informação necessária do documento. " +
                "Verifica que o documento está completo, legível e bem iluminado. " +
                "Se estás a usar uma foto, tenta enviar o ficheiro PDF original."
            );
        }
    }

    private static bool MatchNames(string? extractedName, string userName)
    {
        if (string.IsNullOrWhiteSpace(extractedName)) return false;

        var normalizedExtracted = RemoveDiacritics(extractedName).ToUpperInvariant();
        var normalizedUser = RemoveDiacritics(userName).ToUpperInvariant();

        var userParts = normalizedUser.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (userParts.Length == 0) return false;

        bool hasFirstName = normalizedExtracted.Contains(userParts.First());
        bool hasLastName = userParts.Length <= 1 || normalizedExtracted.Contains(userParts.Last());

        return hasFirstName && hasLastName;
    }

    private static DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;

        string[] formats = ["dd/MM/yyyy", "dd-MM-yyyy", "dd.MM.yyyy", "yyyy-MM-dd", "yyyy/MM/dd"];

        if (DateTime.TryParseExact(dateStr, formats, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
        {
            return date;
        }

        return null;
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim();
    }

    private static string? NormalizeOptionalDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }

    private static (string? PhoneCountryCode, string? PhoneNumber) NormalizePhone(string? phoneCountryCode, string? phoneNumber)
    {
        var normalizedPhoneCountryCode = NormalizeOptionalValue(phoneCountryCode)?.ToUpperInvariant();
        var normalizedPhoneNumber = NormalizeOptionalInternationalPhone(phoneNumber);

        if (normalizedPhoneCountryCode == null && normalizedPhoneNumber == null)
            return (null, null);

        if (normalizedPhoneCountryCode == null || normalizedPhoneNumber == null)
            throw new Exception("Seleciona o país e indica o número de telemóvel completo.");

        if (normalizedPhoneCountryCode.Length != 2 || !normalizedPhoneCountryCode.All(char.IsLetter))
            throw new Exception("O país selecionado para o telemóvel é inválido.");

        if (!PhoneRules.TryGetValue(normalizedPhoneCountryCode, out var phoneRule))
            throw new Exception("O país selecionado para o telemóvel ainda não é suportado.");

        if (!IsValidInternationalPhone(normalizedPhoneNumber))
            throw new Exception("O número de telemóvel deve estar no formato internacional válido.");

        var phoneDigits = normalizedPhoneNumber[1..];
        if (!phoneDigits.StartsWith(phoneRule.DialCode, StringComparison.Ordinal))
            throw new Exception("O indicativo selecionado não corresponde ao número de telemóvel indicado.");

        var localNumber = phoneDigits[phoneRule.DialCode.Length..];
        if (!phoneRule.MobileRegex.IsMatch(localNumber))
            throw new Exception($"O número de telemóvel não é válido para {GetCountryName(normalizedPhoneCountryCode)}. Exemplo: {phoneRule.Example}");

        return (normalizedPhoneCountryCode, normalizedPhoneNumber);
    }

    private static string? NormalizeOptionalInternationalPhone(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber)) return null;

        var buffer = new StringBuilder();
        foreach (var character in phoneNumber.Trim())
        {
            if (char.IsDigit(character))
            {
                buffer.Append(character);
                continue;
            }

            if (character == '+' && buffer.Length == 0)
            {
                buffer.Append(character);
            }
        }

        return buffer.Length == 0 ? null : buffer.ToString();
    }

    private static bool IsValidInternationalPhone(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber) || phoneNumber[0] != '+')
            return false;

        var digits = phoneNumber[1..];
        return digits.Length >= 7 && digits.Length <= 15 && digits.All(char.IsDigit);
    }

    private static string GetCountryName(string countryCode)
    {
        return countryCode switch
        {
            "PT" => "Portugal",
            "ES" => "Espanha",
            "FR" => "França",
            "DE" => "Alemanha",
            "IT" => "Itália",
            "GB" => "Reino Unido",
            "IE" => "Irlanda",
            "NL" => "Países Baixos",
            "BE" => "Bélgica",
            "LU" => "Luxemburgo",
            "CH" => "Suíça",
            "US" => "Estados Unidos",
            "CA" => "Canadá",
            "BR" => "Brasil",
            "AO" => "Angola",
            "MZ" => "Moçambique",
            "CV" => "Cabo Verde",
            "ST" => "São Tomé e Príncipe",
            "GW" => "Guiné-Bissau",
            "TL" => "Timor-Leste",
            _ => countryCode
        };
    }

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

    public async Task UpdateTrustScoreAsync(Guid userId, int newScore)
    {
        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new Exception("Utilizador não encontrado.");

        user.TrustScore = Math.Clamp(newScore, 0, 100);
        await _uow.SaveChangesAsync();
    }
}