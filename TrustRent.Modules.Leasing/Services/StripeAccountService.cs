using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stripe;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.DTOs;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Models;

namespace TrustRent.Modules.Leasing.Services;

public class StripeAccountService : IStripeAccountService
{
    private readonly LeasingDbContext _db;
    private readonly ILogger<StripeAccountService> _logger;

    public StripeAccountService(
        LeasingDbContext db,
        IConfiguration configuration,
        ILogger<StripeAccountService> logger)
    {
        _db = db;
        _logger = logger;
        StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
    }

    public async Task<StripeAccountDto> CreateConnectAccountAsync(Guid userId, string email, string name, Guid? propertyId)
    {
        // Verificar se já existe conta para este user/property
        var existing = await _db.StripeAccounts
            .FirstOrDefaultAsync(s => s.UserId == userId && s.PropertyId == propertyId);

        if (existing != null)
            return MapToDto(existing);

        // Criar conta Express no Stripe
        var options = new AccountCreateOptions
        {
            Type = "express",
            Country = "PT",
            Email = email,
            BusinessType = "individual",
            Capabilities = new AccountCapabilitiesOptions
            {
                CardPayments = new AccountCapabilitiesCardPaymentsOptions { Requested = true },
                Transfers = new AccountCapabilitiesTransfersOptions { Requested = true },
            },
            BusinessProfile = new AccountBusinessProfileOptions
            {
                ProductDescription = "Recebimento de rendas via TrustRent"
            },
            Metadata = new Dictionary<string, string>
            {
                ["trustrent_user_id"] = userId.ToString(),
                ["trustrent_property_id"] = propertyId?.ToString() ?? "global"
            }
        };

        var service = new AccountService();
        var account = await service.CreateAsync(options);

        var isDefault = propertyId == null;

        // Se é global e já existe outra global, não marcar como default
        if (isDefault)
        {
            var existingDefault = await _db.StripeAccounts
                .AnyAsync(s => s.UserId == userId && s.IsDefault);
            if (existingDefault) isDefault = false;
        }

        var stripeAccount = new Models.StripeAccount
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PropertyId = propertyId,
            StripeAccountId = account.Id,
            IsOnboardingComplete = false,
            ChargesEnabled = account.ChargesEnabled,
            PayoutsEnabled = account.PayoutsEnabled,
            IsDefault = isDefault,
            CreatedAt = DateTime.UtcNow
        };

        _db.StripeAccounts.Add(stripeAccount);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Stripe Connect account {StripeAccountId} created for user {UserId}", account.Id, userId);

        return MapToDto(stripeAccount);
    }

    public async Task<OnboardingLinkDto> GetOnboardingLinkAsync(Guid stripeAccountDbId, string returnUrl, string refreshUrl)
    {
        var stripeAccount = await _db.StripeAccounts.FindAsync(stripeAccountDbId)
            ?? throw new InvalidOperationException("Conta Stripe não encontrada.");

        var options = new AccountLinkCreateOptions
        {
            Account = stripeAccount.StripeAccountId,
            RefreshUrl = refreshUrl,
            ReturnUrl = returnUrl,
            Type = "account_onboarding",
        };

        var service = new AccountLinkService();
        var link = await service.CreateAsync(options);

        return new OnboardingLinkDto(link.Url);
    }

    public async Task<StripeAccountDto?> GetDefaultAccountAsync(Guid userId)
    {
        var account = await _db.StripeAccounts
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsDefault && s.PropertyId == null);

        return account == null ? null : MapToDto(account);
    }

    public async Task<StripeAccountDto?> GetAccountForPropertyAsync(Guid propertyId)
    {
        // Primeiro procurar conta específica do imóvel
        var propertyAccount = await _db.StripeAccounts
            .FirstOrDefaultAsync(s => s.PropertyId == propertyId);

        if (propertyAccount != null)
            return MapToDto(propertyAccount);

        return null;
    }

    public async Task<StripeAccountDto?> GetAccountByIdAsync(Guid id)
    {
        var account = await _db.StripeAccounts.FindAsync(id);
        return account == null ? null : MapToDto(account);
    }

    public async Task<IEnumerable<StripeAccountDto>> GetAccountsByUserAsync(Guid userId)
    {
        var accounts = await _db.StripeAccounts
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.IsDefault)
            .ThenByDescending(s => s.CreatedAt)
            .ToListAsync();

        return accounts.Select(MapToDto);
    }

    public async Task RefreshAccountStatusAsync(Guid stripeAccountDbId)
    {
        var stripeAccount = await _db.StripeAccounts.FindAsync(stripeAccountDbId)
            ?? throw new InvalidOperationException("Conta Stripe não encontrada.");

        var service = new AccountService();
        var account = await service.GetAsync(stripeAccount.StripeAccountId);

        stripeAccount.ChargesEnabled = account.ChargesEnabled;
        stripeAccount.PayoutsEnabled = account.PayoutsEnabled;
        stripeAccount.IsOnboardingComplete = account.ChargesEnabled && account.PayoutsEnabled;
        stripeAccount.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task HandleAccountUpdatedWebhookAsync(string stripeAccountId)
    {
        var stripeAccount = await _db.StripeAccounts
            .FirstOrDefaultAsync(s => s.StripeAccountId == stripeAccountId);

        if (stripeAccount == null)
        {
            _logger.LogWarning("Webhook account.updated para conta desconhecida: {StripeAccountId}", stripeAccountId);
            return;
        }

        var service = new AccountService();
        var account = await service.GetAsync(stripeAccountId);

        stripeAccount.ChargesEnabled = account.ChargesEnabled;
        stripeAccount.PayoutsEnabled = account.PayoutsEnabled;
        stripeAccount.IsOnboardingComplete = account.ChargesEnabled && account.PayoutsEnabled;
        stripeAccount.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Stripe account {StripeAccountId} updated: charges={Charges}, payouts={Payouts}",
            stripeAccountId, account.ChargesEnabled, account.PayoutsEnabled);
    }

    private static StripeAccountDto MapToDto(Models.StripeAccount account) => new(
        account.Id,
        account.UserId,
        account.PropertyId,
        account.StripeAccountId,
        account.IsOnboardingComplete,
        account.ChargesEnabled,
        account.PayoutsEnabled,
        account.IsDefault,
        account.CreatedAt
    );
}
