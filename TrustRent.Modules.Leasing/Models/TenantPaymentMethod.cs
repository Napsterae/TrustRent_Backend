namespace TrustRent.Modules.Leasing.Models;

/// <summary>
/// Método de pagamento guardado de um inquilino (via Stripe).
/// </summary>
public class TenantPaymentMethod
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string StripePaymentMethodId { get; set; } = string.Empty; // "pm_xxx"
    public string CardBrand { get; set; } = string.Empty; // "visa", "mastercard"
    public string CardLast4 { get; set; } = string.Empty;
    public int CardExpMonth { get; set; }
    public int CardExpYear { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
