using TrustRent.Modules.Leasing.Contracts.DTOs;

namespace TrustRent.Modules.Leasing.Contracts.Interfaces;

/// <summary>
/// Gestão de contas Stripe Connect Express dos proprietários.
/// </summary>
public interface IStripeAccountService
{
    Task<StripeAccountDto> CreateConnectAccountAsync(Guid userId, string email, string name, Guid? propertyId);
    Task<OnboardingLinkDto> GetOnboardingLinkAsync(Guid stripeAccountDbId, string returnUrl, string refreshUrl);
    Task<StripeAccountDto?> GetDefaultAccountAsync(Guid userId);
    Task<StripeAccountDto?> GetAccountForPropertyAsync(Guid propertyId);
    Task<StripeAccountDto?> GetAccountByIdAsync(Guid id);
    Task<IEnumerable<StripeAccountDto>> GetAccountsByUserAsync(Guid userId);
    Task RefreshAccountStatusAsync(Guid stripeAccountDbId);
    Task HandleAccountUpdatedWebhookAsync(string stripeAccountId);
}
