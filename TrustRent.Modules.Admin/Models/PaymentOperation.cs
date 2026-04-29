namespace TrustRent.Modules.Admin.Models;

public enum AdminPaymentOperationType
{
    Refund = 0,
    ManualCharge = 1,
    ManualMarkPaid = 2
}

public enum AdminPaymentOperationStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2
}

public class PaymentOperation
{
    public Guid Id { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? LeaseId { get; set; }
    public Guid AdminUserId { get; set; }
    public AdminPaymentOperationType OperationType { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string? StripeObjectId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public AdminPaymentOperationStatus Status { get; set; } = AdminPaymentOperationStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Error { get; set; }
}
