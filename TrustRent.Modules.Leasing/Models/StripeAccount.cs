namespace TrustRent.Modules.Leasing.Models;

/// <summary>
/// Conta Stripe Connect Express de um proprietário.
/// PropertyId == null → configuração global do perfil.
/// PropertyId != null → configuração específica para um imóvel.
/// </summary>
public class StripeAccount
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? PropertyId { get; set; }
    public string StripeAccountId { get; set; } = string.Empty; // "acct_xxx"
    public bool IsOnboardingComplete { get; set; }
    public bool ChargesEnabled { get; set; }
    public bool PayoutsEnabled { get; set; }
    public bool IsDefault { get; set; } // true = conta global padrão
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
