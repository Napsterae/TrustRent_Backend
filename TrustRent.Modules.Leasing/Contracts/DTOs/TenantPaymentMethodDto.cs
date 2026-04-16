namespace TrustRent.Modules.Leasing.Contracts.DTOs;

public record TenantPaymentMethodDto(
    Guid Id,
    string CardBrand,
    string CardLast4,
    int CardExpMonth,
    int CardExpYear,
    bool IsDefault
);

public record SetupIntentDto(
    string ClientSecret
);
