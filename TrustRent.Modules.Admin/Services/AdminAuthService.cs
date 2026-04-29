using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Contracts.Interfaces;
using TrustRent.Modules.Admin.Models;

namespace TrustRent.Modules.Admin.Services;

public class AdminAuthService : IAdminAuthService
{
    private readonly AdminDbContext _db;
    private readonly IConfiguration _config;
    private readonly IPermissionService _permissions;

    public AdminAuthService(AdminDbContext db, IConfiguration config, IPermissionService permissions)
    {
        _db = db;
        _config = config;
        _permissions = permissions;
    }

    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private const int MaxFailedAttempts = 5;

    public async Task<AdminLoginResult> LoginAsync(string email, string password, string? mfaCode, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        var admin = await _db.AdminUsers.FirstOrDefaultAsync(x => x.Email.ToLower() == normalized && x.DeletedAt == null, ct);
        if (admin is null) throw new UnauthorizedAccessException("Credenciais inválidas.");

        if (!admin.IsActive) throw new UnauthorizedAccessException("Conta desactivada.");
        if (admin.LockedUntil.HasValue && admin.LockedUntil.Value > DateTime.UtcNow)
            throw new UnauthorizedAccessException("Conta bloqueada temporariamente.");

        if (!BCrypt.Net.BCrypt.Verify(password, admin.PasswordHash))
        {
            admin.FailedAttempts++;
            if (admin.FailedAttempts >= MaxFailedAttempts)
            {
                admin.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);
                admin.FailedAttempts = 0;
            }
            await _db.SaveChangesAsync(ct);
            throw new UnauthorizedAccessException("Credenciais inválidas.");
        }

        // MFA check
        if (admin.MfaEnabled)
        {
            if (string.IsNullOrWhiteSpace(mfaCode))
            {
                // Indicate MFA required without exposing other state.
                throw new MfaRequiredException();
            }
            if (!TotpHelper.VerifyCode(admin.MfaSecret!, mfaCode))
            {
                admin.FailedAttempts++;
                await _db.SaveChangesAsync(ct);
                throw new UnauthorizedAccessException("Código MFA inválido.");
            }
        }
        else if (admin.IsSuperAdmin)
        {
            // Plan demands MFA for super-admins; until setup, allow first-login but flag.
            // Login passes; UI should redirect to /mfa/setup based on response.
        }

        admin.FailedAttempts = 0;
        admin.LockedUntil = null;
        admin.LastLoginAt = DateTime.UtcNow;
        admin.LastLoginIp = ip;

        var perms = await _permissions.GetEffectivePermissionsAsync(admin.Id, ct);

        var jti = Guid.NewGuid().ToString("N");
        var expiryHours = int.TryParse(_config["AdminJwtSettings:ExpiryHours"], out var hh) ? hh : 8;
        var expiresAt = DateTime.UtcNow.AddHours(expiryHours);
        var csrf = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var token = GenerateJwtToken(admin, jti, expiresAt, csrf);

        var session = new AdminSession
        {
            Id = Guid.NewGuid(),
            AdminUserId = admin.Id,
            TokenId = jti,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            Ip = ip,
            UserAgent = userAgent
        };
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync(ct);

        return new AdminLoginResult(admin, token, jti, expiresAt, perms, csrf, MfaRequired: false);
    }

    public async Task LogoutAsync(string jti, CancellationToken ct = default)
    {
        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.TokenId == jti && s.RevokedAt == null, ct);
        if (session is null) return;
        session.RevokedAt = DateTime.UtcNow;
        session.RevokedReason = "logout";
        await _db.SaveChangesAsync(ct);
    }

    public Task<AdminUser?> GetByIdAsync(Guid adminUserId, CancellationToken ct = default)
        => _db.AdminUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == adminUserId && x.DeletedAt == null, ct);

    public async Task ChangePasswordAsync(Guid adminUserId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var admin = await _db.AdminUsers.FirstOrDefaultAsync(x => x.Id == adminUserId, ct)
            ?? throw new InvalidOperationException("Admin não encontrado.");
        if (!BCrypt.Net.BCrypt.Verify(currentPassword, admin.PasswordHash))
            throw new UnauthorizedAccessException("Password actual inválida.");
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 12)
            throw new ArgumentException("A nova password deve ter pelo menos 12 caracteres.");

        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        admin.PasswordChangedAt = DateTime.UtcNow;
        admin.MustChangePassword = false;
        admin.SecurityStamp = Guid.NewGuid();
        await _db.SaveChangesAsync(ct);
    }

    private string GenerateJwtToken(AdminUser admin, string jti, DateTime expiresAt, string csrfToken)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["AdminJwtSettings:SecretKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, admin.Email),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim("name", admin.Name),
            new Claim("security_stamp", admin.SecurityStamp.ToString()),
            new Claim("permissions_version", admin.PermissionsVersion.ToString()),
            new Claim("csrf", csrfToken),
            new Claim("is_super_admin", admin.IsSuperAdmin ? "1" : "0")
        };

        var token = new JwtSecurityToken(
            issuer: _config["AdminJwtSettings:Issuer"],
            audience: _config["AdminJwtSettings:Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class MfaRequiredException : Exception
{
    public MfaRequiredException() : base("MFA obrigatório.") { }
}
