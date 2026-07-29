using TrustRent.Modules.Leasing.Contracts.DTOs;

namespace TrustRent.Modules.Leasing.Contracts.Interfaces;

/// <summary>
/// Gestão de pagamentos via Stripe: métodos de pagamento, PaymentIntents, reembolsos.
/// </summary>
public interface IStripePaymentService
{
    // Customer & Payment Methods
    Task<string> EnsureCustomerAsync(Guid userId, string email, string name);
    Task<SetupIntentDto> CreateSetupIntentAsync(Guid userId);
    Task<TenantPaymentMethodDto> SavePaymentMethodAsync(Guid userId, string stripePaymentMethodId);
    Task<IEnumerable<TenantPaymentMethodDto>> GetSavedPaymentMethodsAsync(Guid userId);
    Task RemovePaymentMethodAsync(Guid userId, Guid paymentMethodDbId);
    Task SetDefaultPaymentMethodAsync(Guid userId, Guid paymentMethodDbId);

    // Payments
    Task<PaymentBreakdownDto> GetInitialPaymentBreakdownAsync(Guid leaseId);
    Task<PaymentClientSecretDto> CreateInitialPaymentAsync(Guid leaseId, Guid tenantId, string? paymentMethodId);
    Task<PaymentDto?> CreateMonthlyRentPaymentAsync(Guid leaseId, Guid tenantId, DateTime billingPeriod, int attempt = 0, decimal? customAmount = null);
    Task<PaymentDto?> RetryMonthlyRentPaymentAsync(Guid leaseId, Guid userId);
    Task<PaymentDto?> GetPaymentByIdAsync(Guid paymentId, Guid userId);
    Task<IEnumerable<PaymentDto>> GetPaymentsByLeaseAsync(Guid leaseId, Guid userId);

    // Webhooks
    Task HandlePaymentSucceededAsync(string paymentIntentId);
    Task HandlePaymentFailedAsync(string paymentIntentId, string? failureMessage);
    Task HandlePaymentRequiresActionAsync(string paymentIntentId);
    Task HandlePaymentProcessingAsync(string paymentIntentId);
    Task HandlePaymentCanceledAsync(string paymentIntentId);
    Task HandleChargeRefundedAsync(string chargeId, long amountRefunded);

    // Client secret for 3DS
    Task<string?> GetPaymentClientSecretAsync(Guid paymentId, Guid userId);

    // Refunds
    Task<PaymentDto> RefundDepositAsync(Guid leaseId, decimal amount, Guid userId);
    Task<PaymentDto> RefundMonthlyRentPaymentAsync(Guid paymentId, decimal amount, Guid userId);

    // Receipt
    Task<byte[]> GenerateReceiptAsync(Guid paymentId, Guid userId);
}
