using System.Globalization;
using System.Text;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Identity.Models;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models.DocumentExtraction;
using TrustRent.Shared.Services;

namespace TrustRent.Modules.Identity.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _uow;
    private readonly IImageService _imageService;
    private readonly IGeminiDocumentService _geminiService;

    public UserService(IUnitOfWork uow, IImageService imageService, IGeminiDocumentService geminiService)
    {
        _uow = uow;
        _imageService = imageService;
        _geminiService = geminiService;
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