using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TrustRent.Modules.Identity.Contracts.Database;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Identity.Models;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Security;

namespace TrustRent.Modules.Identity.Services;

public class LoginCodeService : ILoginCodeService
{
    private const int CodeDigits = 6;
    private const int CodeTtlMinutes = 10;
    private const int MaxAttempts = 5;
    private const int ResendCooldownSeconds = 60;

    private readonly IdentityDbContext _db;
    private readonly IEmailService _emailService;
    private readonly ILogger<LoginCodeService> _logger;
    private readonly IConfiguration _config;

    public LoginCodeService(
        IdentityDbContext db,
        IEmailService emailService,
        ILogger<LoginCodeService> logger,
        IConfiguration config)
    {
        _db = db;
        _emailService = emailService;
        _logger = logger;
        _config = config;
    }

    public async Task<LoginCodeDispatchResult> SendLoginCodeAsync(string email, string? sourceIp, string? userAgent, CancellationToken ct = default)
    {
        var normalizedEmail = EmailHelper.NormalizeEmail(email);
        var now = DateTime.UtcNow;

        var latestActiveCode = await _db.EmailLoginCodes
            .Where(x => x.Email == normalizedEmail && x.VerifiedAt == null && x.InvalidatedAt == null && x.ExpiresAt > now)
            .OrderByDescending(x => x.RequestedAt)
            .FirstOrDefaultAsync(ct);

        if (latestActiveCode != null && latestActiveCode.RequestedAt > now.AddSeconds(-ResendCooldownSeconds))
            throw new InvalidOperationException("Ainda enviámos um código recentemente. Aguarda um minuto e tenta novamente.");

        foreach (var pendingCode in await _db.EmailLoginCodes
                     .Where(x => x.Email == normalizedEmail && x.VerifiedAt == null && x.InvalidatedAt == null && x.ExpiresAt > now)
                     .ToListAsync(ct))
        {
            pendingCode.InvalidatedAt = now;
        }

        var code = RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, CodeDigits)).ToString($"D{CodeDigits}");
        var loginCode = new EmailLoginCode
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            CodeHash = HashCode(normalizedEmail, code),
            RequestedAt = now,
            ExpiresAt = now.AddMinutes(CodeTtlMinutes),
            RequestedFromIp = sourceIp,
            RequestedUserAgent = userAgent
        };

        _db.EmailLoginCodes.Add(loginCode);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _emailService.SendEmailAsync(
                normalizedEmail,
                "O teu código de acesso — Wekaza",
                BuildLoginCodeBody(code, CodeTtlMinutes),
                new EmailSendOptions(
                    FromAddress: _config["EmailSettings:AuthFromAddress"],
                    FromName: _config["EmailSettings:FromName"] ?? "Wekaza"));
        }
        catch (Exception ex)
        {
            loginCode.InvalidatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogError(ex, "Falha ao enviar código de login para {Email}", normalizedEmail);
            throw;
        }

        return new LoginCodeDispatchResult(MaskEmail(normalizedEmail), loginCode.ExpiresAt);
    }

    public async Task<string> VerifyLoginCodeAsync(string email, string code, CancellationToken ct = default)
    {
        var normalizedEmail = EmailHelper.NormalizeEmail(email);
        var sanitizedCode = (code ?? string.Empty).Trim();
        var now = DateTime.UtcNow;

        if (sanitizedCode.Length != CodeDigits || sanitizedCode.Any(ch => !char.IsDigit(ch)))
            throw new UnauthorizedAccessException("Código inválido ou expirado.");

        var loginCode = await _db.EmailLoginCodes
            .Where(x => x.Email == normalizedEmail && x.VerifiedAt == null && x.InvalidatedAt == null)
            .OrderByDescending(x => x.RequestedAt)
            .FirstOrDefaultAsync(ct);

        if (loginCode == null || loginCode.ExpiresAt <= now)
            throw new UnauthorizedAccessException("Código inválido ou expirado.");

        loginCode.AttemptCount++;

        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(loginCode.CodeHash),
                Convert.FromHexString(HashCode(normalizedEmail, sanitizedCode))))
        {
            if (loginCode.AttemptCount >= MaxAttempts)
                loginCode.InvalidatedAt = now;

            await _db.SaveChangesAsync(ct);
            throw new UnauthorizedAccessException("Código inválido ou expirado.");
        }

        loginCode.VerifiedAt = now;
        await _db.SaveChangesAsync(ct);
        return normalizedEmail;
    }

    private string HashCode(string normalizedEmail, string code)
    {
        var pepper = _config["AuthCodeSettings:HashPepper"]
                     ?? _config["JwtSettings:SecretKey"]
                     ?? throw new InvalidOperationException("Falta AuthCodeSettings:HashPepper ou JwtSettings:SecretKey para calcular o hash do código.");

        var payload = Encoding.UTF8.GetBytes($"{normalizedEmail}:{code}:{pepper}");
        return Convert.ToHexString(SHA256.HashData(payload));
    }

    private static string BuildLoginCodeBody(string code, int ttlMinutes)
        => $"""
           <p style="margin:0 0 16px;font-size:16px;line-height:1.7;color:#334155">Usa o código abaixo para entrar na tua conta Wekaza.</p>
                     <div style="margin:0 0 24px;padding:20px 24px;border-radius:20px;background:linear-gradient(135deg,#7a3605 0%,#f29b4b 16%,#1e6b66 58%,#41b0a8 100%);text-align:center">
                         <div style="font-size:13px;letter-spacing:.16em;text-transform:uppercase;font-weight:700;color:#fff1df;margin-bottom:10px">Código de acesso</div>
             <div style="font-size:36px;line-height:1;font-weight:800;letter-spacing:.34em;color:#ffffff">{code}</div>
           </div>
           <p style="margin:0 0 14px;font-size:15px;line-height:1.7;color:#475569">Este código expira em <strong>{ttlMinutes} minutos</strong> e só pode ser usado uma vez.</p>
           <p style="margin:0;font-size:14px;line-height:1.6;color:#64748b">Se não pediste este acesso, podes ignorar este email.</p>
           """;

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1) return email;

        var local = email[..at];
        var domain = email[at..];
        var visible = local.Length <= 2 ? local[..1] : local[..2];
        return $"{visible}{new string('*', Math.Max(2, local.Length - visible.Length))}{domain}";
    }
}