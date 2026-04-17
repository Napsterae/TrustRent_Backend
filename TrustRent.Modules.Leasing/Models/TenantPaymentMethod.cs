namespace TrustRent.Modules.Leasing.Models;

/// <summary>
/// Método de pagamento guardado de um inquilino (via Stripe).
/// Suporta cartões, MB Way, Revolut Pay, entre outros.
/// </summary>
public class TenantPaymentMethod
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string StripePaymentMethodId { get; set; } = string.Empty; // "pm_xxx"

    /// <summary>
    /// Tipo de método: "card", "mbway", "revolut_pay"
    /// </summary>
    public string Type { get; set; } = "card";

    /// <summary>
    /// Nome de exibição (ex: "Visa •••• 4242", "MB Way +351***123", "Revolut Pay")
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    // Campos específicos de cartão (nullable para métodos não-cartão)
    public string? CardBrand { get; set; }
    public string? CardLast4 { get; set; }
    public int? CardExpMonth { get; set; }
    public int? CardExpYear { get; set; }

    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
