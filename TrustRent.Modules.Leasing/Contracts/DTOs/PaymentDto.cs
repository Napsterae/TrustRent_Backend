using TrustRent.Modules.Leasing.Models;

namespace TrustRent.Modules.Leasing.Contracts.DTOs;

public record PaymentDto(
    Guid Id,
    Guid LeaseId,
    PaymentType Type,
    decimal Amount,
    decimal PlatformFee,
    decimal LandlordAmount,
    decimal RentAmount,
    decimal DepositAmount,
    decimal AdvanceRentAmount,
    string Currency,
    PaymentStatus Status,
    string? FailureReason,
    DateTime? PaidAt,
    DateTime CreatedAt
);

public record PaymentBreakdownDto(
    decimal MonthlyRent,
    decimal AdvanceRent,
    int AdvanceRentMonths,
    decimal Deposit,
    decimal PlatformFee,
    decimal Total,
    decimal LandlordReceives
);

public record CreatePaymentDto(
    string? PaymentMethodId,
    bool SavePaymentMethod
);

public record PaymentClientSecretDto(
    string ClientSecret,
    Guid PaymentId,
    decimal Amount,
    string Currency
);
