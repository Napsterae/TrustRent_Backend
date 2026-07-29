namespace TrustRent.Modules.Leasing.Models;

/// <summary>
/// Registo de cada pagamento processado pelo Stripe.
/// </summary>
public class Payment
{
    public Guid Id { get; set; }
    public Guid LeaseId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LandlordId { get; set; }

    // Stripe IDs
    public string StripePaymentIntentId { get; set; } = string.Empty;
    public string? StripeTransferId { get; set; }

    // Tipo de pagamento
    public PaymentType Type { get; set; }

    // Valores (em euros)
    public decimal Amount { get; set; }          // Total cobrado ao inquilino
    public decimal PlatformFee { get; set; }     // Taxa da plataforma
    public decimal LandlordAmount { get; set; }  // Valor recebido pelo proprietário
    public decimal RentAmount { get; set; }       // Parcela de renda corrente
    public decimal DepositAmount { get; set; }    // Parcela de caução
    public decimal AdvanceRentAmount { get; set; } // Parcela de renda antecipada
    public string Currency { get; set; } = "eur";

    // Estado
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? FailureReason { get; set; }
    public string? Metadata { get; set; } // JSON com breakdown para referência

    // Idempotency — prevents duplicate charges if connection drops between Stripe call and DB save
    public string? IdempotencyKey { get; set; }

    // Retry tracking — 0 = initial attempt, 1-3 = retry attempts
    public int RetryAttempt { get; set; } = 0;

    // Datas
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public enum PaymentType
{
    InitialPayment,    // Pagamento inicial (renda + antecipada + caução)
    MonthlyRent,       // Renda mensal recorrente
    DepositRefund,     // Reembolso de caução
    MonthlyRentRefund  // Reembolso de renda mensal
}

public enum PaymentStatus
{
    Pending,
    Processing,
    Succeeded,
    Failed,
    Refunded,
    PartiallyRefunded
}
