using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Security.Claims;
using System.Text;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Identity.Models;
using TrustRent.Shared.Security;

namespace TrustRent.Modules.Identity.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly IConfiguration _config;

    public AuthService(IUnitOfWork unitOfWork, IConfiguration config)
    {
        _uow = unitOfWork;
        _config = config;
    }

    public async Task<string> SignInWithEmailAsync(string email)
    {
        var normalizedEmail = EmailHelper.NormalizeEmail(email);
        var user = await _uow.Users.GetByEmailAsync(normalizedEmail);

        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Name = BuildDefaultName(normalizedEmail),
                Email = normalizedEmail,
                PasswordHash = string.Empty
            };

            await _uow.Users.AddAsync(user);
            await _uow.SaveChangesAsync();
        }

        return GenerateJwtToken(user);
    }

    public async Task<string> SignInWithPhoneAsync(string phoneNumber)
    {
        var user = await _uow.Users.GetByPhoneNumberAsync(phoneNumber);

        if (user == null || !user.IsPhoneNumberVerified)
            throw new UnauthorizedAccessException("Número de telemóvel inválido ou por validar.");

        return GenerateJwtToken(user);
    }

    private static string BuildDefaultName(string normalizedEmail)
    {
        var localPart = normalizedEmail.Split('@', 2)[0];
        var cleaned = Regex.Replace(localPart, @"[._+\-]+", " ");
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
            return "Utilizador Wekaza";

        var textInfo = CultureInfo.GetCultureInfo("pt-PT").TextInfo;
        var titleCased = textInfo.ToTitleCase(cleaned.ToLowerInvariant());
        return titleCased.Length > 120 ? titleCased[..120] : titleCased;
    }

    private string GenerateJwtToken(User user)
    {
        // Esta chave secreta vai estar no teu appsettings.json da API
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["JwtSettings:SecretKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiryDays = GetJwtExpiryDays();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("name", user.Name),
            new Claim("trust_score", user.TrustScore.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["JwtSettings:Issuer"],
            audience: _config["JwtSettings:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(expiryDays),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private int GetJwtExpiryDays()
    {
        var configuredDays = int.TryParse(_config["JwtSettings:ExpiryDays"], out var parsedDays)
            ? parsedDays
            : 14;
        return Math.Clamp(configuredDays, 1, 60);
    }
}
