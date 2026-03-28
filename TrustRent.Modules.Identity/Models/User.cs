namespace TrustRent.Modules.Identity.Models;

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    // Dados Pessoais
    public string? Nif { get; set; }
    public string? CitizenCardNumber { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public string? ProfilePictureUrl { get; set; }

    // Validações de Confiança (Read-Only no Frontend)
    public bool IsIdentityVerified { get; set; } = false;
    public DateTime? IdentityExpiryDate { get; set; }
    public bool IsNoDebtVerified { get; set; } = false;
    public DateTime? NoDebtExpiryDate { get; set; }
    public int TrustScore { get; set; } = 50;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

