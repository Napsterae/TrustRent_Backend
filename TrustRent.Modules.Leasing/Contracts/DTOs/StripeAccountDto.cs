namespace TrustRent.Modules.Leasing.Contracts.DTOs;

public record StripeAccountDto(
    Guid Id,
    Guid UserId,
    Guid? PropertyId,
    string StripeAccountId,
    bool IsOnboardingComplete,
    bool ChargesEnabled,
    bool PayoutsEnabled,
    bool IsDefault,
    DateTime CreatedAt
);

public record CreateConnectAccountDto(
    Guid? PropertyId = null
);

public record OnboardingLinkDto(
    string Url
);

public record OnboardingLinkRequestDto(
    Guid StripeAccountId,
    string ReturnUrl,
    string RefreshUrl
);
