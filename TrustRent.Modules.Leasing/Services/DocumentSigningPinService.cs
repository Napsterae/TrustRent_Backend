using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TrustRent.Modules.Identity.Contracts.Database;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Identity.Models;
using TrustRent.Shared.Communications;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Modules.Leasing.Services;

public class DocumentSigningPinService
{
    private const int CodeDigits = 6;
    private const int CodeTtlMinutes = 10;
    private const int MaxAttempts = 5;
    private const int ResendCooldownSeconds = 60;
    private const string DocumentSigningPurpose = "document_signing";

    private readonly IdentityDbContext _identityDb;
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly ITelegramMessagingPlatformService? _telegramMessagingPlatformService;
    private readonly IWhatsAppCodeService? _whatsAppCodeService;
    private readonly ICommunicationContentService _communicationContentService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;
    private readonly ILogger<DocumentSigningPinService> _logger;

    public DocumentSigningPinService(
        IdentityDbContext identityDb,
        IUserRepository userRepository,
        IUserService userService,
        ICommunicationContentService communicationContentService,
        IEmailService emailService,
        IConfiguration config,
        ILogger<DocumentSigningPinService> logger,
        ITelegramMessagingPlatformService? telegramMessagingPlatformService = null,
        IWhatsAppCodeService? whatsAppCodeService = null)
    {
        _identityDb = identityDb;
        _userRepository = userRepository;
        _userService = userService;
        _communicationContentService = communicationContentService;
        _emailService = emailService;
        _config = config;
        _logger = logger;
        _telegramMessagingPlatformService = telegramMessagingPlatformService;
        _whatsAppCodeService = whatsAppCodeService;
    }

    /// <summary>
    /// Generate a 6-digit PIN, store in WhatsAppOneTimeCodes with Purpose="document_signing",
    /// and send via the user's preferred channel (Telegram, WhatsApp, or email fallback).
    /// </summary>
    public async Task<PinSentResult> RequestPinAsync(Guid userId, Guid leaseId)
    {
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("Utilizador não encontrado.");

        var now = DateTime.UtcNow;

        // Check cooldown: any active code for this user + purpose
        var latestActiveCode = await _identityDb.WhatsAppOneTimeCodes
            .Where(x => x.UserId == userId
                        && x.Purpose == DocumentSigningPurpose
                        && x.VerifiedAt == null
                        && x.InvalidatedAt == null
                        && x.ExpiresAt > now)
            .OrderByDescending(x => x.RequestedAt)
            .FirstOrDefaultAsync();

        if (latestActiveCode != null && latestActiveCode.RequestedAt > now.AddSeconds(-ResendCooldownSeconds))
            throw new InvalidOperationException("Ainda enviámos um código recentemente. Aguarda um minuto e tenta novamente.");

        // Invalidate any existing pending codes
        var pendingCodes = await _identityDb.WhatsAppOneTimeCodes
            .Where(x => x.UserId == userId
                        && x.Purpose == DocumentSigningPurpose
                        && x.VerifiedAt == null
                        && x.InvalidatedAt == null
                        && x.ExpiresAt > now)
            .ToListAsync();

        foreach (var pending in pendingCodes)
        {
            pending.InvalidatedAt = now;
        }

        // Generate 6-digit code
        var code = RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, CodeDigits)).ToString($"D{CodeDigits}");
        var phoneNumber = user.PhoneNumber ?? string.Empty;

        var oneTimeCode = new WhatsAppOneTimeCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PhoneNumber = phoneNumber,
            Purpose = DocumentSigningPurpose,
            CodeHash = HashCode(userId.ToString(), DocumentSigningPurpose, code),
            RequestedAt = now,
            ExpiresAt = now.AddMinutes(CodeTtlMinutes)
        };

        _identityDb.WhatsAppOneTimeCodes.Add(oneTimeCode);
        await _identityDb.SaveChangesAsync();

        // Determine channel and send
        try
        {
            if (user.WhatsAppNotificationsEnabled && user.IsPhoneNumberVerified)
            {
                var platform = PhoneContactPlatforms.Normalize(user.PhoneContactPlatform, PhoneContactPlatforms.Telegram);

                if (platform == PhoneContactPlatforms.Telegram && !string.IsNullOrWhiteSpace(user.TelegramChatId))
                {
                    if (_telegramMessagingPlatformService == null)
                        throw new InvalidOperationException("O serviço de envio por Telegram não está disponível.");

                    await _telegramMessagingPlatformService.SendLoginCodeAsync(user, code);
                    return new PinSentResult("telegram");
                }

                if (platform == PhoneContactPlatforms.WhatsApp)
                {
                    if (_whatsAppCodeService == null)
                        throw new InvalidOperationException("O serviço de envio por WhatsApp não está disponível.");

                    await _whatsAppCodeService.SendLoginCodeAsync(phoneNumber, null, null);
                    return new PinSentResult("whatsapp");
                }
            }

            // Fallback: send via email
            var renderedTemplate = await _communicationContentService.RenderEmailTemplateAsync(
                CommunicationEmailTemplateKeys.DocumentSigningCode,
                new Dictionary<string, string?>
                {
                    ["SigningCode"] = code,
                    ["SigningCodeExpiresMinutes"] = CodeTtlMinutes.ToString()
                });

            await _emailService.SendEmailAsync(
                user.Email,
                renderedTemplate.Subject,
                renderedTemplate.BodyHtml);

            return new PinSentResult("email");
        }
        catch (Exception ex)
        {
            oneTimeCode.InvalidatedAt = DateTime.UtcNow;
            await _identityDb.SaveChangesAsync();
            _logger.LogError(ex, "Falha ao enviar código de assinatura para o utilizador {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Verify a PIN code for document signing. Constant-time comparison, max 5 attempts, 10-min TTL.
    /// </summary>
    public async Task<bool> VerifyPinAsync(Guid userId, Guid leaseId, string code)
    {
        var sanitizedCode = new string((code ?? string.Empty).Where(ch => !char.IsWhiteSpace(ch)).ToArray());
        var now = DateTime.UtcNow;

        if (sanitizedCode.Length != CodeDigits || sanitizedCode.Any(ch => !char.IsDigit(ch)))
            return false;

        var storedCode = await _identityDb.WhatsAppOneTimeCodes
            .Where(x => x.UserId == userId
                        && x.Purpose == DocumentSigningPurpose
                        && x.VerifiedAt == null
                        && x.InvalidatedAt == null)
            .OrderByDescending(x => x.RequestedAt)
            .FirstOrDefaultAsync();

        if (storedCode == null || storedCode.ExpiresAt <= now)
            return false;

        storedCode.AttemptCount++;

        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(storedCode.CodeHash),
                Convert.FromHexString(HashCode(userId.ToString(), DocumentSigningPurpose, sanitizedCode))))
        {
            if (storedCode.AttemptCount >= MaxAttempts)
                storedCode.InvalidatedAt = now;

            await _identityDb.SaveChangesAsync();
            return false;
        }

        storedCode.VerifiedAt = now;
        await _identityDb.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Request PIN for a guest guarantor (token-based, unauthenticated).
    /// </summary>
    public Task<PinSentResult> RequestPinForGuestAsync(string guestToken)
    {
        // TODO Phase 2/3: implement guest guarantor PIN flow
        throw new NotImplementedException("Guest PIN flow not yet implemented.");
    }

    /// <summary>
    /// Verify PIN for a guest guarantor (token-based, unauthenticated).
    /// </summary>
    public Task<bool> VerifyPinForGuestAsync(string guestToken, string code)
    {
        // TODO Phase 2/3: implement guest guarantor PIN verification
        throw new NotImplementedException("Guest PIN verification not yet implemented.");
    }

    private string HashCode(string identifier, string purpose, string code)
    {
        var pepper = _config["AuthCodeSettings:HashPepper"]
                     ?? _config["JwtSettings:SecretKey"]
                     ?? throw new InvalidOperationException("Falta AuthCodeSettings:HashPepper ou JwtSettings:SecretKey para calcular o hash do código.");

        var payload = Encoding.UTF8.GetBytes($"{identifier}:{purpose}:{code}:{pepper}");
        return Convert.ToHexString(SHA256.HashData(payload));
    }
}

public record PinSentResult(string SentVia); // "telegram" | "whatsapp" | "email"
