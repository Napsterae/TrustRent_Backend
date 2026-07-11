using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TrustRent.Modules.Identity.Contracts.Database;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Shared.Security;
using TrustRent.Modules.Identity.Models;

namespace TrustRent.Modules.Identity.Services;

public sealed class PhoneLoginCodeService : IPhoneLoginCodeService
{
    private const int CodeDigits = 6;
    private const int CodeTtlMinutes = 10;
    private const int MaxAttempts = 5;
    private const int ResendCooldownSeconds = 60;
    private const string TelegramLoginPurpose = "telegram_login";

    private readonly IdentityDbContext _db;
    private readonly IUserRepository _userRepository;
    private readonly ITelegramMessagingPlatformService? _telegramMessagingPlatformService;
    private readonly IWhatsAppCodeService? _whatsAppCodeService;
    private readonly IConfiguration _config;
    private readonly ILogger<PhoneLoginCodeService> _logger;

    public PhoneLoginCodeService(
        IdentityDbContext db,
        IUserRepository userRepository,
        IConfiguration config,
        ILogger<PhoneLoginCodeService> logger,
        ITelegramMessagingPlatformService? telegramMessagingPlatformService = null,
        IWhatsAppCodeService? whatsAppCodeService = null)
    {
        _db = db;
        _userRepository = userRepository;
        _config = config;
        _logger = logger;
        _telegramMessagingPlatformService = telegramMessagingPlatformService;
        _whatsAppCodeService = whatsAppCodeService;
    }

    public async Task<PhoneLoginCodeDispatchResult> SendLoginCodeAsync(string phoneNumber, string? sourceIp, string? userAgent, CancellationToken ct = default)
    {
        var normalizedPhoneNumber = NormalizePhoneNumber(phoneNumber);
        var now = DateTime.UtcNow;
        var user = await _userRepository.GetByPhoneNumberAsync(normalizedPhoneNumber);

        if (user == null || !user.IsPhoneNumberVerified)
        {
            return new PhoneLoginCodeDispatchResult(
                MaskPhoneNumber(normalizedPhoneNumber),
                PhoneContactPlatforms.Telegram,
                now.AddMinutes(CodeTtlMinutes));
        }

        var platform = PhoneContactPlatforms.Normalize(user.PhoneContactPlatform, PhoneContactPlatforms.Telegram);
        if (platform == PhoneContactPlatforms.WhatsApp)
        {
            if (_whatsAppCodeService is null)
                throw new InvalidOperationException("O serviço de envio por WhatsApp não está disponível.");

            var dispatch = await _whatsAppCodeService.SendLoginCodeAsync(normalizedPhoneNumber, sourceIp, userAgent, ct);
            return new PhoneLoginCodeDispatchResult(dispatch.MaskedPhoneNumber, PhoneContactPlatforms.WhatsApp, dispatch.ExpiresAtUtc);
        }

        if (_telegramMessagingPlatformService is null || string.IsNullOrWhiteSpace(user.TelegramChatId))
        {
            return new PhoneLoginCodeDispatchResult(
                MaskPhoneNumber(normalizedPhoneNumber),
                PhoneContactPlatforms.Telegram,
                now.AddMinutes(CodeTtlMinutes));
        }

        return await SendTelegramCodeAsync(user, normalizedPhoneNumber, sourceIp, userAgent, ct);
    }

    public async Task<string> VerifyLoginCodeAsync(string phoneNumber, string code, CancellationToken ct = default)
    {
        var normalizedPhoneNumber = NormalizePhoneNumber(phoneNumber);
        var user = await _userRepository.GetByPhoneNumberAsync(normalizedPhoneNumber);

        if (user == null || !user.IsPhoneNumberVerified)
            throw new UnauthorizedAccessException("Código inválido ou expirado.");

        var platform = PhoneContactPlatforms.Normalize(user.PhoneContactPlatform, PhoneContactPlatforms.Telegram);
        if (platform == PhoneContactPlatforms.WhatsApp)
        {
            if (_whatsAppCodeService is null)
                throw new InvalidOperationException("O serviço de envio por WhatsApp não está disponível.");

            return await _whatsAppCodeService.VerifyLoginCodeAsync(normalizedPhoneNumber, code, ct);
        }

        return await VerifyTelegramCodeAsync(user.Id, normalizedPhoneNumber, code, ct);
    }

    private async Task<PhoneLoginCodeDispatchResult> SendTelegramCodeAsync(User user, string phoneNumber, string? sourceIp, string? userAgent, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var phoneBlindIndex = EncryptionHelperV2.ComputeBlindIndex(EncryptionHelperV2.NormalizePhone(phoneNumber));

        var latestActiveCode = await _db.WhatsAppOneTimeCodes
            .Where(x => x.PhoneNumberBlindIndex == phoneBlindIndex
                        && x.Purpose == TelegramLoginPurpose
                        && x.UserId == user.Id
                        && x.VerifiedAt == null
                        && x.InvalidatedAt == null
                        && x.ExpiresAt > now)
            .OrderByDescending(x => x.RequestedAt)
            .FirstOrDefaultAsync(ct);

        if (latestActiveCode != null && latestActiveCode.RequestedAt > now.AddSeconds(-ResendCooldownSeconds))
            throw new InvalidOperationException("Ainda enviámos um código recentemente. Aguarda um minuto e tenta novamente.");

        foreach (var pendingCode in await _db.WhatsAppOneTimeCodes
                     .Where(x => x.PhoneNumberBlindIndex == phoneBlindIndex
                                 && x.Purpose == TelegramLoginPurpose
                                 && x.UserId == user.Id
                                 && x.VerifiedAt == null
                                 && x.InvalidatedAt == null
                                 && x.ExpiresAt > now)
                     .ToListAsync(ct))
        {
            pendingCode.InvalidatedAt = now;
        }

        var code = RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, CodeDigits)).ToString($"D{CodeDigits}");
        var oneTimeCode = new WhatsAppOneTimeCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PhoneNumber = phoneNumber,
            PhoneNumberBlindIndex = phoneBlindIndex,
            Purpose = TelegramLoginPurpose,
            CodeHash = HashCode(phoneNumber, TelegramLoginPurpose, code),
            RequestedAt = now,
            ExpiresAt = now.AddMinutes(CodeTtlMinutes),
            RequestedFromIp = IpHashHelper.Hash(sourceIp),
            RequestedUserAgent = UserAgentHelper.Simplify(userAgent)
        };

        _db.WhatsAppOneTimeCodes.Add(oneTimeCode);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _telegramMessagingPlatformService!.SendLoginCodeAsync(user, code, ct);
        }
        catch (Exception ex)
        {
            oneTimeCode.InvalidatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogError(ex, "Falha ao enviar código Telegram para o utilizador {UserId}", user.Id);
            throw;
        }

        return new PhoneLoginCodeDispatchResult(MaskPhoneNumber(phoneNumber), PhoneContactPlatforms.Telegram, oneTimeCode.ExpiresAt);
    }

    private async Task<string> VerifyTelegramCodeAsync(Guid userId, string phoneNumber, string code, CancellationToken ct)
    {
        var sanitizedCode = new string((code ?? string.Empty).Where(ch => !char.IsWhiteSpace(ch)).ToArray());
        var now = DateTime.UtcNow;

        if (sanitizedCode.Length != CodeDigits || sanitizedCode.Any(ch => !char.IsDigit(ch)))
            throw new UnauthorizedAccessException("Código inválido ou expirado.");

        var phoneBlindIndex = EncryptionHelperV2.ComputeBlindIndex(EncryptionHelperV2.NormalizePhone(phoneNumber));
        var loginCode = await _db.WhatsAppOneTimeCodes
            .Where(x => x.PhoneNumberBlindIndex == phoneBlindIndex
                        && x.Purpose == TelegramLoginPurpose
                        && x.UserId == userId
                        && x.VerifiedAt == null
                        && x.InvalidatedAt == null)
            .OrderByDescending(x => x.RequestedAt)
            .FirstOrDefaultAsync(ct);

        if (loginCode == null || loginCode.ExpiresAt <= now)
            throw new UnauthorizedAccessException("Código inválido ou expirado.");

        loginCode.AttemptCount++;

        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(loginCode.CodeHash),
                Convert.FromHexString(HashCode(phoneNumber, TelegramLoginPurpose, sanitizedCode))))
        {
            if (loginCode.AttemptCount >= MaxAttempts)
                loginCode.InvalidatedAt = now;

            await _db.SaveChangesAsync(ct);
            throw new UnauthorizedAccessException("Código inválido ou expirado.");
        }

        loginCode.VerifiedAt = now;
        await _db.SaveChangesAsync(ct);
        return phoneNumber;
    }

    private string HashCode(string phoneNumber, string purpose, string code)
    {
        var pepper = _config["AuthCodeSettings:HashPepper"]
                     ?? _config["JwtSettings:SecretKey"]
                     ?? throw new InvalidOperationException("Falta AuthCodeSettings:HashPepper ou JwtSettings:SecretKey para calcular o hash do código.");

        var payload = Encoding.UTF8.GetBytes($"{phoneNumber}:{purpose}:{code}:{pepper}");
        return Convert.ToHexString(SHA256.HashData(payload));
    }

    private static string NormalizePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new InvalidOperationException("Número de telemóvel obrigatório.");

        var builder = new StringBuilder();
        foreach (var character in phoneNumber.Trim())
        {
            if (builder.Length == 0 && character == '+')
            {
                builder.Append(character);
                continue;
            }

            if (char.IsDigit(character))
                builder.Append(character);
        }

        var normalizedPhoneNumber = builder.ToString();
        if (string.IsNullOrWhiteSpace(normalizedPhoneNumber)
            || normalizedPhoneNumber[0] != '+'
            || normalizedPhoneNumber.Length < 8
            || normalizedPhoneNumber.Length > 16
            || normalizedPhoneNumber[1..].Any(ch => !char.IsDigit(ch)))
        {
            throw new InvalidOperationException("Número de telemóvel inválido. Usa o formato internacional, por exemplo +351912345678.");
        }

        return normalizedPhoneNumber;
    }

    private static string MaskPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber) || phoneNumber.Length <= 6)
            return phoneNumber;

        var visiblePrefix = phoneNumber[..4];
        var visibleSuffix = phoneNumber[^2..];
        var maskedLength = Math.Max(4, phoneNumber.Length - visiblePrefix.Length - visibleSuffix.Length);
        return $"{visiblePrefix}{new string('*', maskedLength)}{visibleSuffix}";
    }
}