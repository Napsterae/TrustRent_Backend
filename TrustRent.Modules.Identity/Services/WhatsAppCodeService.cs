using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TrustRent.Modules.Identity.Contracts.Database;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Shared.Security;
using TrustRent.Modules.Identity.Models;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Modules.Identity.Services;

public class WhatsAppCodeService : IWhatsAppCodeService
{
    private const int CodeDigits = 6;
    private const int CodeTtlMinutes = 10;
    private const int MaxAttempts = 5;
    private const int ResendCooldownSeconds = 60;
    private const string LoginPurpose = "login";
    private const string PhoneVerificationPurpose = "phone_verification";

    private readonly IdentityDbContext _db;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IConfiguration _config;
    private readonly ILogger<WhatsAppCodeService> _logger;

    public WhatsAppCodeService(
        IdentityDbContext db,
        IWhatsAppService whatsAppService,
        IConfiguration config,
        ILogger<WhatsAppCodeService> logger)
    {
        _db = db;
        _whatsAppService = whatsAppService;
        _config = config;
        _logger = logger;
    }

    public async Task<WhatsAppCodeDispatchResult> SendLoginCodeAsync(string phoneNumber, string? sourceIp, string? userAgent, CancellationToken ct = default)
    {
        var normalizedPhoneNumber = NormalizePhoneNumber(phoneNumber);
        var now = DateTime.UtcNow;

        var phoneBlindIndex = EncryptionHelperV2.ComputeBlindIndex(EncryptionHelperV2.NormalizePhone(normalizedPhoneNumber));
        var canReceiveLoginCode = await _db.Users.AnyAsync(
            user => user.PhoneNumberBlindIndex == phoneBlindIndex && user.IsPhoneNumberVerified,
            ct);

        if (!canReceiveLoginCode)
            return new WhatsAppCodeDispatchResult(MaskPhoneNumber(normalizedPhoneNumber), now.AddMinutes(CodeTtlMinutes));

        return await SendCodeAsync(null, normalizedPhoneNumber, LoginPurpose, sourceIp, userAgent, ct);
    }

    public async Task<string> VerifyLoginCodeAsync(string phoneNumber, string code, CancellationToken ct = default)
        => await VerifyCodeAsync(null, NormalizePhoneNumber(phoneNumber), code, LoginPurpose, ct);

    public async Task<WhatsAppCodeDispatchResult> SendPhoneVerificationCodeAsync(Guid userId, string phoneNumber, string? sourceIp, string? userAgent, CancellationToken ct = default)
        => await SendCodeAsync(userId, NormalizePhoneNumber(phoneNumber), PhoneVerificationPurpose, sourceIp, userAgent, ct);

    public async Task VerifyPhoneVerificationCodeAsync(Guid userId, string phoneNumber, string code, CancellationToken ct = default)
        => await VerifyCodeAsync(userId, NormalizePhoneNumber(phoneNumber), code, PhoneVerificationPurpose, ct);

    private async Task<WhatsAppCodeDispatchResult> SendCodeAsync(Guid? userId, string phoneNumber, string purpose, string? sourceIp, string? userAgent, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var phoneBlindIndex = EncryptionHelperV2.ComputeBlindIndex(EncryptionHelperV2.NormalizePhone(phoneNumber));

        var latestActiveCode = await _db.WhatsAppOneTimeCodes
            .Where(x => x.PhoneNumberBlindIndex == phoneBlindIndex
                        && x.Purpose == purpose
                        && x.UserId == userId
                        && x.VerifiedAt == null
                        && x.InvalidatedAt == null
                        && x.ExpiresAt > now)
            .OrderByDescending(x => x.RequestedAt)
            .FirstOrDefaultAsync(ct);

        if (latestActiveCode != null && latestActiveCode.RequestedAt > now.AddSeconds(-ResendCooldownSeconds))
            throw new InvalidOperationException("Ainda enviámos um código recentemente. Aguarda um minuto e tenta novamente.");

        foreach (var pendingCode in await _db.WhatsAppOneTimeCodes
                     .Where(x => x.PhoneNumberBlindIndex == phoneBlindIndex
                                 && x.Purpose == purpose
                                 && x.UserId == userId
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
            UserId = userId,
            PhoneNumber = phoneNumber,
            PhoneNumberBlindIndex = phoneBlindIndex,
            Purpose = purpose,
            CodeHash = HashCode(phoneNumber, purpose, code),
            RequestedAt = now,
            ExpiresAt = now.AddMinutes(CodeTtlMinutes),
            RequestedFromIp = IpHashHelper.Hash(sourceIp),
            RequestedUserAgent = UserAgentHelper.Simplify(userAgent)
        };

        _db.WhatsAppOneTimeCodes.Add(oneTimeCode);
        await _db.SaveChangesAsync(ct);

        try
        {
            if (purpose == LoginPurpose)
                await _whatsAppService.SendLoginCodeAsync(phoneNumber, code, ct);
            else
                await _whatsAppService.SendPhoneVerificationCodeAsync(phoneNumber, code, ct);
        }
        catch (Exception ex)
        {
            oneTimeCode.InvalidatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogError(ex, "Falha ao enviar código WhatsApp para {PhoneNumber} com propósito {Purpose}", phoneNumber, purpose);
            throw;
        }

        return new WhatsAppCodeDispatchResult(MaskPhoneNumber(phoneNumber), oneTimeCode.ExpiresAt);
    }

    private async Task<string> VerifyCodeAsync(Guid? userId, string phoneNumber, string code, string purpose, CancellationToken ct)
    {
        var sanitizedCode = new string((code ?? string.Empty).Where(ch => !char.IsWhiteSpace(ch)).ToArray());
        var now = DateTime.UtcNow;

        if (sanitizedCode.Length != CodeDigits || sanitizedCode.Any(ch => !char.IsDigit(ch)))
            throw new UnauthorizedAccessException("Código inválido ou expirado.");

        var phoneBlindIndex = EncryptionHelperV2.ComputeBlindIndex(EncryptionHelperV2.NormalizePhone(phoneNumber));
        var loginCode = await _db.WhatsAppOneTimeCodes
            .Where(x => x.PhoneNumberBlindIndex == phoneBlindIndex
                        && x.Purpose == purpose
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
                Convert.FromHexString(HashCode(phoneNumber, purpose, sanitizedCode))))
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