using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
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

    public async Task<string> RegisterAsync(string name, string email, string password)
    {
        // Strict normalization here: rejects malformed emails at registration boundary.
        var normalizedEmail = EmailHelper.NormalizeEmail(email);
        var existingUser = await _uow.Users.GetByEmailAsync(normalizedEmail);
        if (existingUser != null) throw new Exception("Email já está em uso.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
        };

        await _uow.Users.AddAsync(user);
        await _uow.SaveChangesAsync(); // Commit na BD

        return GenerateJwtToken(user);
    }

    public async Task<string> LoginAsync(string email, string password)
    {
        // Repository handles normalization tolerantly; malformed input -> null user -> generic 401.
        var user = await _uow.Users.GetByEmailAsync(email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            throw new Exception("Credenciais inválidas.");
        }

        return GenerateJwtToken(user);
    }

    private string GenerateJwtToken(User user)
    {
        // Esta chave secreta vai estar no teu appsettings.json da API
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["JwtSettings:SecretKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("name", user.Name),
            new Claim("trust_score", user.TrustScore.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["JwtSettings:Issuer"],
            audience: _config["JwtSettings:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
