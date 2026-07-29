using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Hangfire;
using QuestPDF;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Stripe;
using System.Globalization;
using System.Text.Json;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.DTOs;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Jobs;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Shared;
using TrustRent.Shared.Communications;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;

namespace TrustRent.Modules.Leasing.Services;

public class StripePaymentService : IStripePaymentService
{
    private readonly LeasingDbContext _db;
    private readonly ILeaseAccessService _leaseAccessService;
    private readonly ILeaseActivationService _leaseActivationService;
    private readonly IStripeAccountService _stripeAccountService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<StripePaymentService> _logger;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ICommunicationContentService? _communicationContentService;
    private readonly IEmailService? _emailService;
    private readonly IUserService? _userService;
    private readonly ICatalogAccessService? _catalogAccess;
    private readonly IConfiguration _configuration;
    private readonly int _platformFeePerMonth;

    public StripePaymentService(
        LeasingDbContext db,
        ILeaseAccessService leaseAccessService,
        ILeaseActivationService leaseActivationService,
        IStripeAccountService stripeAccountService,
        INotificationService notificationService,
        IConfiguration configuration,
        ILogger<StripePaymentService> logger,
        IBackgroundJobClient backgroundJobs,
        ICommunicationContentService? communicationContentService = null,
        IEmailService? emailService = null,
        IUserService? userService = null,
        ICatalogAccessService? catalogAccess = null)
    {
        _db = db;
        _leaseAccessService = leaseAccessService;
        _leaseActivationService = leaseActivationService;
        _stripeAccountService = stripeAccountService;
        _notificationService = notificationService;
        _logger = logger;
        _backgroundJobs = backgroundJobs;
        _communicationContentService = communicationContentService;
        _emailService = emailService;
        _userService = userService;
        _catalogAccess = catalogAccess;
        _configuration = configuration;
        _platformFeePerMonth = configuration.GetValue<int>("Stripe:PlatformFeePerMonth", 3000);
        StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
    }

    #region Customer & Payment Methods

    public async Task<string> EnsureCustomerAsync(Guid userId, string email, string name)
    {
        // Search for existing customer by metadata before creating a new one
        var searchService = new CustomerSearchOptions { Query = $"metadata['trustrent_user_id']:'{userId}'" };
        var searchResult = await new CustomerService().SearchAsync(searchService);
        if (searchResult.Data.Count > 0)
            return searchResult.Data[0].Id;

        var options = new CustomerCreateOptions
        {
            Email = email,
            Name = name,
            Metadata = new Dictionary<string, string> { ["trustrent_user_id"] = userId.ToString() }
        };

        var service = new CustomerService();
        var customer = await service.CreateAsync(options);
        return customer.Id;
    }

    public async Task<SetupIntentDto> CreateSetupIntentAsync(Guid userId)
    {
        var options = new SetupIntentCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card", "revolut_pay" },
            Metadata = new Dictionary<string, string> { ["trustrent_user_id"] = userId.ToString() }
        };

        var service = new SetupIntentService();
        var intent = await service.CreateAsync(options);

        return new SetupIntentDto(intent.ClientSecret);
    }

    public async Task<TenantPaymentMethodDto> SavePaymentMethodAsync(Guid userId, string stripePaymentMethodId)
    {
        // Obter detalhes do método de pagamento do Stripe
        var pmService = new PaymentMethodService();
        var pm = await pmService.GetAsync(stripePaymentMethodId);

        // Verificar duplicação
        var existing = await _db.TenantPaymentMethods
            .FirstOrDefaultAsync(t => t.StripePaymentMethodId == stripePaymentMethodId);
        if (existing != null)
            return MapPaymentMethodToDto(existing);

        var isFirst = !await _db.TenantPaymentMethods.AnyAsync(t => t.UserId == userId);

        // Determinar tipo e dados de exibição
        var type = pm.Type ?? "card";
        string displayName;
        string? cardBrand = null, cardLast4 = null;
        int? cardExpMonth = null, cardExpYear = null;

        switch (type)
        {
            case "card":
                cardBrand = pm.Card?.Brand ?? "unknown";
                cardLast4 = pm.Card?.Last4 ?? "0000";
                cardExpMonth = (int)(pm.Card?.ExpMonth ?? 0);
                cardExpYear = (int)(pm.Card?.ExpYear ?? 0);
                displayName = $"{cardBrand?.ToUpperInvariant()} •••• {cardLast4}";
                break;
            case "revolut_pay":
                displayName = "Revolut Pay";
                break;
            default:
                displayName = type.Replace("_", " ").ToUpperInvariant();
                break;
        }

        // If this is the user's first method, it's automatically the default.
        // If the user already has methods, unset the old default and set this one as default —
        // the user explicitly chose to save and use this method.
        if (!isFirst)
        {
            var oldDefaults = await _db.TenantPaymentMethods
                .Where(t => t.UserId == userId && t.IsDefault)
                .ToListAsync();
            foreach (var old in oldDefaults)
                old.IsDefault = false;
        }

        var method = new TenantPaymentMethod
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StripePaymentMethodId = stripePaymentMethodId,
            Type = type,
            DisplayName = displayName,
            CardBrand = cardBrand,
            CardLast4 = cardLast4,
            CardExpMonth = cardExpMonth,
            CardExpYear = cardExpYear,
            IsDefault = true, // Always set as default — user explicitly saved this method
            CreatedAt = DateTime.UtcNow
        };

        _db.TenantPaymentMethods.Add(method);
        await _db.SaveChangesAsync();

        return MapPaymentMethodToDto(method);
    }

    public async Task<IEnumerable<TenantPaymentMethodDto>> GetSavedPaymentMethodsAsync(Guid userId)
    {
        var methods = await _db.TenantPaymentMethods
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.IsDefault)
            .ThenByDescending(t => t.CreatedAt)
            .ToListAsync();

        return methods.Select(MapPaymentMethodToDto);
    }

    public async Task RemovePaymentMethodAsync(Guid userId, Guid paymentMethodDbId)
    {
        var method = await _db.TenantPaymentMethods
            .FirstOrDefaultAsync(t => t.Id == paymentMethodDbId && t.UserId == userId)
            ?? throw new InvalidOperationException("Método de pagamento não encontrado.");

        // Desanexar do Stripe
        var pmService = new PaymentMethodService();
        await pmService.DetachAsync(method.StripePaymentMethodId);

        _db.TenantPaymentMethods.Remove(method);

        // Se era default, promover outro
        if (method.IsDefault)
        {
            var next = await _db.TenantPaymentMethods
                .Where(t => t.UserId == userId && t.Id != paymentMethodDbId)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();
            if (next != null)
                next.IsDefault = true;
        }

        await _db.SaveChangesAsync();
    }

    public async Task SetDefaultPaymentMethodAsync(Guid userId, Guid paymentMethodDbId)
    {
        var methods = await _db.TenantPaymentMethods
            .Where(t => t.UserId == userId)
            .ToListAsync();

        foreach (var m in methods)
            m.IsDefault = m.Id == paymentMethodDbId;

        await _db.SaveChangesAsync();
    }

    #endregion

    #region Payments

    public async Task<PaymentBreakdownDto> GetInitialPaymentBreakdownAsync(Guid leaseId)
    {
        var lease = await _leaseAccessService.GetLeaseAccessContextAsync(leaseId)
            ?? throw new InvalidOperationException("Lease não encontrado.");

        return CalculateBreakdown(lease.MonthlyRent, lease.AdvanceRentMonths, lease.Deposit ?? 0);
    }

    public async Task<PaymentClientSecretDto> CreateInitialPaymentAsync(Guid leaseId, Guid tenantId, string? paymentMethodId)
    {
        using var activity = Telemetry.Source.StartActivity("StripePayment");
        activity?.SetTag("payment.type", "initial");
        activity?.SetTag("lease.id", leaseId.ToString());
        var lease = await _leaseAccessService.GetLeaseAccessContextAsync(leaseId)
            ?? throw new InvalidOperationException("Lease não encontrado.");

        if (lease.TenantId != tenantId)
            throw new UnauthorizedAccessException("Apenas o inquilino pode efetuar este pagamento.");

        if (lease.LeaseStatus != "AwaitingPayment")
            throw new InvalidOperationException($"O lease não está em estado de pagamento pendente. Estado atual: {lease.LeaseStatus}");

        // Verificar se já existe pagamento pendente ou concluído
        var existingPayment = await _db.Payments
            .FirstOrDefaultAsync(p => p.LeaseId == leaseId && p.Type == PaymentType.InitialPayment
                && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Processing || p.Status == PaymentStatus.Succeeded));

        var piService = new PaymentIntentService();

        if (existingPayment != null)
        {
            if (existingPayment.Status == PaymentStatus.Succeeded)
                throw new InvalidOperationException("Pagamento inicial já foi efetuado.");

            var existingPi = await piService.GetAsync(existingPayment.StripePaymentIntentId);
            existingPi = await ConfirmPaymentIntentIfNeededAsync(piService, existingPi, paymentMethodId);

            return new PaymentClientSecretDto(
                existingPi.ClientSecret,
                existingPayment.Id,
                existingPayment.Amount,
                existingPayment.Currency,
                existingPi.Status
            );
        }

        var breakdown = CalculateBreakdown(lease.MonthlyRent, lease.AdvanceRentMonths, lease.Deposit ?? 0);

        // Encontrar conta Stripe do proprietário para este imóvel
        var stripeAccount = await _stripeAccountService.GetAccountForPropertyAsync(lease.PropertyId);
        if (stripeAccount == null || !stripeAccount.ChargesEnabled || !stripeAccount.PayoutsEnabled)
        {
            stripeAccount = await _stripeAccountService.GetDefaultAccountAsync(lease.LandlordId);
        }

        if (stripeAccount == null || !stripeAccount.ChargesEnabled || !stripeAccount.PayoutsEnabled)
            throw new InvalidOperationException("O proprietário não tem uma conta de recebimento configurada. Contacte o proprietário.");

        var amountInCents = (long)(breakdown.Total * 100);
        var feeInCents = (long)(breakdown.PlatformFee * 100);

        var metadata = new Dictionary<string, string>
        {
            ["trustrent_lease_id"] = leaseId.ToString(),
            ["trustrent_tenant_id"] = tenantId.ToString(),
            ["trustrent_landlord_id"] = lease.LandlordId.ToString(),
            ["rent_amount"] = breakdown.MonthlyRent.ToString("F2"),
            ["advance_rent_amount"] = breakdown.AdvanceRent.ToString("F2"),
            ["deposit_amount"] = breakdown.Deposit.ToString("F2"),
            ["platform_fee"] = breakdown.PlatformFee.ToString("F2"),
            ["type"] = "initial_payment"
        };

        var piOptions = new PaymentIntentCreateOptions
        {
            Amount = amountInCents,
            Currency = "eur",
            ApplicationFeeAmount = feeInCents,
            TransferData = new PaymentIntentTransferDataOptions
            {
                Destination = stripeAccount.StripeAccountId
            },
            Metadata = metadata,
            PaymentMethodTypes = new List<string> { "card", "mbway", "multibanco", "revolut_pay" }
        };

        // Se foi fornecido um paymentMethodId, anexar
        if (!string.IsNullOrEmpty(paymentMethodId))
        {
            piOptions.PaymentMethod = paymentMethodId;
            piOptions.Confirm = true;
        }

        var paymentIntent = await piService.CreateAsync(piOptions);

        var payment = new Models.Payment
        {
            Id = Guid.NewGuid(),
            LeaseId = leaseId,
            TenantId = tenantId,
            LandlordId = lease.LandlordId,
            StripePaymentIntentId = paymentIntent.Id,
            Type = PaymentType.InitialPayment,
            Amount = breakdown.Total,
            PlatformFee = breakdown.PlatformFee,
            LandlordAmount = breakdown.LandlordReceives,
            RentAmount = breakdown.MonthlyRent,
            DepositAmount = breakdown.Deposit,
            AdvanceRentAmount = breakdown.AdvanceRent,
            Currency = "eur",
            Status = PaymentStatus.Pending,
            Metadata = JsonSerializer.Serialize(metadata),
            CreatedAt = DateTime.UtcNow
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "PaymentIntent {PaymentIntentId} criado para lease {LeaseId}: {Amount}€",
            paymentIntent.Id, leaseId, breakdown.Total);

        return new PaymentClientSecretDto(
            paymentIntent.ClientSecret,
            payment.Id,
            payment.Amount,
            payment.Currency,
            paymentIntent.Status
        );
    }

    public async Task<PaymentDto?> CreateMonthlyRentPaymentAsync(Guid leaseId, Guid tenantId, DateTime billingPeriod, int attempt = 0, decimal? customAmount = null)
    {
        // Validate customAmount — must be positive if provided (for split payments)
        if (customAmount.HasValue && customAmount.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(customAmount), "Custom amount must be positive.");

        using var activity = Telemetry.Source.StartActivity("StripePayment");
        activity?.SetTag("payment.type", "monthly_rent");
        activity?.SetTag("lease.id", leaseId.ToString());
        activity?.SetTag("billing.period", billingPeriod.ToString("yyyy-MM"));
        activity?.SetTag("payment.attempt", attempt.ToString());
        if (customAmount.HasValue)
            activity?.SetTag("payment.custom_amount", customAmount.Value.ToString("F2"));

        var lease = await _db.Leases
            .FirstOrDefaultAsync(l => l.Id == leaseId);

        if (lease == null)
            throw new InvalidOperationException("Lease não encontrado.");

        if (lease.TenantId != tenantId && lease.CoTenantId != tenantId)
            throw new UnauthorizedAccessException("Apenas inquilinos podem pagar renda.");

        if (lease.Status != LeaseStatus.Active)
            throw new InvalidOperationException("Lease não está ativo.");

        // Billing-period-wide check: if ANY payment for this lease + tenant + billing period
        // already succeeded, skip — regardless of idempotency key or attempt number.
        // This prevents duplicate charges across retries.
        var periodStart = new DateTime(billingPeriod.Year, billingPeriod.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);
        var anySucceeded = await _db.Payments.AnyAsync(p =>
            p.LeaseId == leaseId && p.Type == PaymentType.MonthlyRent && p.TenantId == tenantId
            && p.CreatedAt >= periodStart && p.CreatedAt < periodEnd
            && p.Status == PaymentStatus.Succeeded);
        if (anySucceeded)
        {
            _logger.LogInformation("Monthly rent already succeeded for lease {LeaseId} period {Period} — skipping attempt {Attempt}",
                leaseId, billingPeriod.ToString("yyyy-MM"), attempt);
            return null;
        }

        // Generate deterministic idempotency key for this lease + tenant + billing period + attempt.
        // Attempt 0 uses the base key; retries append _r{attempt} so each retry gets a fresh
        // PaymentIntent on Stripe (the previous one failed/canceled).
        var idempotencyKey = attempt == 0
            ? $"rent_{leaseId}_{tenantId}_{billingPeriod:yyyy-MM}"
            : $"rent_{leaseId}_{tenantId}_{billingPeriod:yyyy-MM}_r{attempt}";

        // 1. Check local DB for existing payment with this idempotency key
        var existingPayment = await _db.Payments
            .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey);

        if (existingPayment != null)
        {
            // Already succeeded — skip
            if (existingPayment.Status == PaymentStatus.Succeeded)
            {
                _logger.LogInformation("Monthly rent already succeeded for lease {LeaseId} period {Period} (idempotency key {Key})",
                    leaseId, billingPeriod.ToString("yyyy-MM"), idempotencyKey);
                return MapPaymentToDto(existingPayment);
            }

            // Pending or Processing — check Stripe for the actual status.
            // Skip the Stripe API call if the PI ID is still a pending_ placeholder
            // (Stripe call hasn't completed yet — can't reconcile, return as-is).
            if (!string.IsNullOrEmpty(existingPayment.StripePaymentIntentId) && !existingPayment.StripePaymentIntentId.StartsWith("pending_"))
            {
                try
                {
                    var existingPi = await StripeGetPaymentIntentAsync(existingPayment.StripePaymentIntentId);
                    existingPayment.Status = existingPi.Status switch
                    {
                        "succeeded" => PaymentStatus.Succeeded,
                        "processing" => PaymentStatus.Processing,
                        "requires_action" => PaymentStatus.Pending, // needs 3DS — handled separately
                        "requires_payment_method" => PaymentStatus.Failed, // payment method detached/invalid
                        "canceled" => PaymentStatus.Failed,
                        _ => existingPayment.Status
                    };
                    if (existingPayment.Status == PaymentStatus.Succeeded)
                        existingPayment.PaidAt = DateTime.UtcNow;
                    if (existingPayment.Status == PaymentStatus.Failed)
                        existingPayment.FailureReason = $"Stripe status: {existingPi.Status}";
                    existingPayment.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();

                    if (existingPayment.Status == PaymentStatus.Succeeded)
                    {
                        await NotifyPaymentSucceededAsync(existingPayment);
                    }

                    _logger.LogInformation("Reconciled payment {PaymentId} from Stripe status {StripeStatus}",
                        existingPayment.Id, existingPi.Status);
                    return MapPaymentToDto(existingPayment);
                }
                catch (StripeException ex)
                {
                    _logger.LogWarning(ex, "Failed to reconcile payment {PaymentId} from Stripe — returning as-is", existingPayment.Id);
                    return MapPaymentToDto(existingPayment);
                }
            }
            else
            {
                // PI ID is pending_ placeholder or empty — Stripe call hasn't completed.
                // Return as-is; the next run will try again.
                _logger.LogInformation("Payment {PaymentId} still has pending_ placeholder — returning as-is", existingPayment.Id);
                return MapPaymentToDto(existingPayment);
            }
        }

        // 2. Pre-check Stripe for existing PaymentIntents with matching metadata.
        // This catches payments that were created on Stripe but never saved locally (connection drop).
        try
        {
            var searchOptions = new PaymentIntentSearchOptions
            {
                Query = $"metadata['trustrent_lease_id']:'{leaseId}' AND metadata['billing_period']:'{billingPeriod:yyyy-MM}' AND metadata['trustrent_tenant_id']:'{tenantId}'"
            };
            var searchResult = await StripeSearchPaymentIntentsAsync(searchOptions);

            if (searchResult.Data.Count > 0)
            {
                var stripePi = searchResult.Data[0];
                _logger.LogWarning("Found orphaned Stripe PaymentIntent {PiId} for lease {LeaseId} period {Period} — reconciling",
                    stripePi.Id, leaseId, billingPeriod.ToString("yyyy-MM"));

                // Create a local Payment record for the orphaned Stripe PaymentIntent
                var reconciledPayment = new Payment
                {
                    Id = Guid.NewGuid(),
                    LeaseId = leaseId,
                    TenantId = tenantId,
                    LandlordId = lease.LandlordId,
                    StripePaymentIntentId = stripePi.Id,
                    IdempotencyKey = idempotencyKey,
                    Type = PaymentType.MonthlyRent,
                    Amount = (decimal)stripePi.Amount / 100m,
                    PlatformFee = stripePi.ApplicationFeeAmount.HasValue ? (decimal)stripePi.ApplicationFeeAmount.Value / 100m : 0m,
                    LandlordAmount = (decimal)stripePi.Amount / 100m - (stripePi.ApplicationFeeAmount.HasValue ? (decimal)stripePi.ApplicationFeeAmount.Value / 100m : 0m),
                    RentAmount = lease.MonthlyRent,
                    Currency = "eur",
                    Status = stripePi.Status switch
                    {
                        "succeeded" => PaymentStatus.Succeeded,
                        "processing" => PaymentStatus.Processing,
                        "requires_action" => PaymentStatus.Pending,
                        "requires_payment_method" => PaymentStatus.Failed,
                        "canceled" => PaymentStatus.Failed,
                        _ => PaymentStatus.Pending
                    },
                    PaidAt = stripePi.Status == "succeeded" ? DateTime.UtcNow : null,
                    CreatedAt = DateTime.UtcNow,
                    RetryAttempt = attempt,
                    Metadata = JsonSerializer.Serialize(stripePi.Metadata)
                };

                _db.Payments.Add(reconciledPayment);
                await _db.SaveChangesAsync();

                if (reconciledPayment.Status == PaymentStatus.Succeeded)
                    await NotifyPaymentSucceededAsync(reconciledPayment);

                return MapPaymentToDto(reconciledPayment);
            }
        }
        catch (StripeException ex)
        {
            // Stripe Search API might not be available or might fail — log and continue to creation
            _logger.LogWarning(ex, "Stripe PaymentIntent search failed for lease {LeaseId} — proceeding to create new PaymentIntent", leaseId);
        }

        // 3. No existing payment found — create a new one
        var paymentMethod = await _db.TenantPaymentMethods
            .FirstOrDefaultAsync(pm => pm.UserId == tenantId && pm.IsDefault);

        if (paymentMethod == null)
            throw new InvalidOperationException("Nenhum método de pagamento configurado.");

        var stripeAccount = await _stripeAccountService.GetAccountForPropertyAsync(lease.PropertyId);
        if (stripeAccount == null || !stripeAccount.ChargesEnabled || !stripeAccount.PayoutsEnabled)
        {
            stripeAccount = await _stripeAccountService.GetDefaultAccountAsync(lease.LandlordId);
        }

        if (stripeAccount == null || !stripeAccount.ChargesEnabled || !stripeAccount.PayoutsEnabled)
            throw new InvalidOperationException("O proprietário não tem uma conta de recebimento configurada. Contacte o proprietário.");

        var rent = customAmount ?? lease.MonthlyRent;
        if (rent <= 0)
            throw new InvalidOperationException("Valor da renda inválido.");
        var amountInCents = (long)(rent * 100);
        var feeInCents = customAmount.HasValue
            ? (int)Math.Round(lease.MonthlyRent > 0 ? _platformFeePerMonth * (rent / lease.MonthlyRent) : _platformFeePerMonth)
            : _platformFeePerMonth;

        var user = await _userService!.GetProfileAsync(tenantId);
        if (user == null || string.IsNullOrWhiteSpace(user.StripeCustomerId))
            throw new InvalidOperationException("Inquilino não tem conta Stripe configurada.");

        // Save Payment record with Processing status BEFORE the Stripe call.
        // This ensures the idempotency key is in the DB even if the connection drops.
        // Use a unique placeholder for StripePaymentIntentId (pending_{guid}) because the column
        // has a unique index — string.Empty would collide if two payments are processed concurrently.
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            LeaseId = leaseId,
            TenantId = tenantId,
            LandlordId = lease.LandlordId,
            StripePaymentIntentId = $"pending_{Guid.NewGuid()}", // updated with real PI ID after Stripe call
            IdempotencyKey = idempotencyKey,
            Type = PaymentType.MonthlyRent,
            Amount = rent,
            PlatformFee = feeInCents / 100m,
            LandlordAmount = rent - (feeInCents / 100m),
            RentAmount = rent,
            Currency = "eur",
            Status = PaymentStatus.Processing,
            RetryAttempt = attempt,
            CreatedAt = DateTime.UtcNow
        };

        _db.Payments.Add(payment);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // A concurrent call may have already created this payment — check and return it
            var raceWinner = await _db.Payments.FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey);
            if (raceWinner != null)
            {
                _logger.LogInformation("Concurrent monthly rent payment for lease {LeaseId} period {Period} — returning existing payment {PaymentId}",
                    leaseId, billingPeriod.ToString("yyyy-MM"), raceWinner.Id);
                return MapPaymentToDto(raceWinner);
            }
            throw; // different DbUpdateException — rethrow
        }

        // Create the PaymentIntent on Stripe with the idempotency key
        PaymentIntent pi;
        try
        {
            pi = await StripeCreatePaymentIntentAsync(new PaymentIntentCreateOptions
            {
                Amount = amountInCents,
                Currency = "eur",
                Customer = user.StripeCustomerId,
                PaymentMethod = paymentMethod.StripePaymentMethodId,
                OffSession = true,
                Confirm = true,
                ApplicationFeeAmount = feeInCents,
                TransferData = new PaymentIntentTransferDataOptions { Destination = stripeAccount.StripeAccountId },
                Metadata = new Dictionary<string, string>
                {
                    ["trustrent_lease_id"] = leaseId.ToString(),
                    ["trustrent_tenant_id"] = tenantId.ToString(),
                    ["type"] = "monthly_rent",
                    ["billing_period"] = billingPeriod.ToString("yyyy-MM")
                }
            }, new RequestOptions { IdempotencyKey = idempotencyKey });
        }
        catch (StripeException ex)
        {
            // Stripe call failed — mark payment as Failed
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = ex.StripeError?.Message ?? ex.Message;
            payment.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            _logger.LogError(ex, "Stripe PaymentIntent creation failed for lease {LeaseId} period {Period}",
                leaseId, billingPeriod.ToString("yyyy-MM"));
            throw;
        }

        // Update the Payment record with the Stripe result
        payment.StripePaymentIntentId = pi.Id;
        payment.Status = pi.Status switch
        {
            "succeeded" => PaymentStatus.Succeeded,
            "processing" => PaymentStatus.Processing,
            "requires_action" => PaymentStatus.Pending, // needs 3DS
            "requires_payment_method" => PaymentStatus.Failed, // payment method detached/invalid
            "canceled" => PaymentStatus.Failed,
            _ => PaymentStatus.Pending
        };
        if (payment.Status == PaymentStatus.Failed)
            payment.FailureReason = $"Stripe status: {pi.Status}";
        payment.PaidAt = pi.Status == "succeeded" ? DateTime.UtcNow : null;
        payment.UpdatedAt = DateTime.UtcNow;
        payment.Metadata = JsonSerializer.Serialize(pi.Metadata);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Monthly rent payment {PaymentIntentId} created for lease {LeaseId}: {Amount}€ (billing period {Period}, status {Status})",
            pi.Id, leaseId, rent, billingPeriod.ToString("yyyy-MM"), payment.Status);

        // If succeeded immediately, send notifications
        if (payment.Status == PaymentStatus.Succeeded)
        {
            await NotifyPaymentSucceededAsync(payment);
        }

        return MapPaymentToDto(payment);
    }

    public async Task<PaymentDto?> GetPaymentByIdAsync(Guid paymentId, Guid userId)
    {
        var payment = await _db.Payments.FindAsync(paymentId);
        if (payment == null) return null;

        if (payment.TenantId != userId && payment.LandlordId != userId)
            throw new UnauthorizedAccessException("Sem permissão para ver este pagamento.");

        return MapPaymentToDto(payment);
    }

    public async Task<string?> GetPaymentClientSecretAsync(Guid paymentId, Guid userId)
    {
        var payment = await _db.Payments.FindAsync(paymentId);
        if (payment == null) return null;

        // Only the tenant or landlord can access the client secret
        if (payment.TenantId != userId && payment.LandlordId != userId)
            throw new UnauthorizedAccessException("Sem permissão para ver este pagamento.");

        // Only return the client secret for payments that need 3DS authentication
        if (payment.Status != PaymentStatus.Pending)
            return null;

        if (string.IsNullOrEmpty(payment.StripePaymentIntentId) || payment.StripePaymentIntentId.StartsWith("pending_"))
            return null;

        try
        {
            var pi = await StripeGetPaymentIntentAsync(payment.StripePaymentIntentId);
            if (pi.Status == "requires_action" || pi.Status == "requires_confirmation")
            {
                return pi.ClientSecret;
            }
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Failed to get client secret for payment {PaymentId}", paymentId);
        }

        return null;
    }

    public async Task<IEnumerable<PaymentDto>> GetPaymentsByLeaseAsync(Guid leaseId, Guid userId)
    {
        var lease = await _leaseAccessService.GetLeaseAccessContextAsync(leaseId)
            ?? throw new InvalidOperationException("Lease não encontrado.");

        // Check if user is the tenant, co-tenant, or landlord
        var fullLease = await _db.Leases.AsNoTracking().FirstOrDefaultAsync(l => l.Id == leaseId);
        var isCoTenant = fullLease?.CoTenantId == userId;

        if (lease.TenantId != userId && !isCoTenant && lease.LandlordId != userId)
            throw new UnauthorizedAccessException("Sem permissão para ver pagamentos deste lease.");

        var payments = await _db.Payments
            .Where(p => p.LeaseId == leaseId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return payments.Select(MapPaymentToDto);
    }

    public async Task<PaymentDto?> RetryMonthlyRentPaymentAsync(Guid leaseId, Guid userId)
    {
        var lease = await _db.Leases.FirstOrDefaultAsync(l => l.Id == leaseId)
            ?? throw new InvalidOperationException("Lease não encontrado.");

        if (lease.TenantId != userId && lease.CoTenantId != userId)
            throw new UnauthorizedAccessException("Apenas o inquilino pode pagar renda.");

        // Find the most recent MonthlyRent payment for this lease + tenant
        var recentPayment = await _db.Payments
            .Where(p => p.LeaseId == leaseId && p.TenantId == userId && p.Type == PaymentType.MonthlyRent)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();

        if (recentPayment == null)
            return null; // No payment to retry

        // Already succeeded — return it
        if (recentPayment.Status == PaymentStatus.Succeeded)
            return MapPaymentToDto(recentPayment);

        // Pending or Processing — check Stripe for actual status
        if (recentPayment.Status == PaymentStatus.Pending || recentPayment.Status == PaymentStatus.Processing)
        {
            // If the PI ID is still a pending_ placeholder, the Stripe call hasn't completed yet.
            // Return the payment as-is — don't create a duplicate.
            if (string.IsNullOrEmpty(recentPayment.StripePaymentIntentId) || recentPayment.StripePaymentIntentId.StartsWith("pending_"))
            {
                _logger.LogInformation("Payment {PaymentId} is still in-flight (pending_ placeholder) — returning as-is", recentPayment.Id);
                return MapPaymentToDto(recentPayment);
            }

            try
            {
                var pi = await StripeGetPaymentIntentAsync(recentPayment.StripePaymentIntentId);
                recentPayment.Status = pi.Status switch
                {
                    "succeeded" => PaymentStatus.Succeeded,
                    "processing" => PaymentStatus.Processing,
                    "requires_action" => PaymentStatus.Pending,
                    "canceled" => PaymentStatus.Failed,
                    _ => recentPayment.Status
                };
                if (recentPayment.Status == PaymentStatus.Succeeded)
                    recentPayment.PaidAt = DateTime.UtcNow;
                recentPayment.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                if (recentPayment.Status == PaymentStatus.Succeeded)
                    await NotifyPaymentSucceededAsync(recentPayment);

                return MapPaymentToDto(recentPayment);
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Failed to check Stripe status for payment {PaymentId}", recentPayment.Id);
            }
        }

        // Failed — create a new payment attempt with incremented attempt number
        // The billing-period-wide success check in CreateMonthlyRentPaymentAsync prevents
        // duplicate charges if a prior attempt already succeeded.
        // Pass the original payment's RentAmount as customAmount so split payments charge the correct share.
        var nextAttempt = Math.Max(1, recentPayment.RetryAttempt + 1);
        var billingPeriod = recentPayment.CreatedAt.Date;
        var customAmount = recentPayment.RentAmount > 0 ? (decimal?)recentPayment.RentAmount : null;

        _logger.LogInformation("Tenant {UserId} initiating on-demand retry for lease {LeaseId} (attempt {Attempt})",
            userId, leaseId, nextAttempt);

        // Update the original failed payment's RetryAttempt so the admin dashboard
        // reflects the correct attempt count and exhaustion status.
        recentPayment.RetryAttempt = nextAttempt;
        recentPayment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await CreateMonthlyRentPaymentAsync(leaseId, userId, billingPeriod, nextAttempt, customAmount);
    }

    public async Task<byte[]> GenerateReceiptAsync(Guid paymentId, Guid userId)
    {
        var payment = await _db.Payments.FindAsync(paymentId)
            ?? throw new InvalidOperationException("Pagamento não encontrado.");

        if (payment.TenantId != userId && payment.LandlordId != userId)
            throw new UnauthorizedAccessException("Sem permissão para ver este pagamento.");

        if (payment.Status != PaymentStatus.Succeeded && payment.Status != PaymentStatus.Refunded && payment.Status != PaymentStatus.PartiallyRefunded)
            throw new InvalidOperationException("Recibo disponível apenas para pagamentos concluídos.");

        var lease = await _db.Leases.AsNoTracking().FirstOrDefaultAsync(l => l.Id == payment.LeaseId);
        var propertyTitle = "Contrato de arrendamento";
        if (lease is not null && _catalogAccess is not null)
        {
            var applicationContext = await _catalogAccess.GetApplicationContextAsync(lease.ApplicationId);
            if (!string.IsNullOrWhiteSpace(applicationContext?.PropertyTitle))
                propertyTitle = applicationContext.PropertyTitle!;
        }

        var tenant = _userService is not null ? await _userService.GetProfileAsync(payment.TenantId) : null;
        var landlord = _userService is not null ? await _userService.GetProfileAsync(payment.LandlordId) : null;

        var receiptNumber = $"REC-{payment.CreatedAt:yyyy}-{payment.Id.ToString()[..8].ToUpper()}";
        var paymentDate = payment.PaidAt ?? payment.CreatedAt;
        var amountText = FormatCurrency(Math.Abs(payment.Amount));
        var paymentTypeLabel = payment.Type switch
        {
            PaymentType.InitialPayment => "Pagamento Inicial",
            PaymentType.MonthlyRent => $"Renda Mensal - {payment.CreatedAt:yyyy-MM}",
            PaymentType.DepositRefund => "Reembolso de Caução",
            PaymentType.MonthlyRentRefund => "Reembolso de Renda",
            _ => "Pagamento"
        };

        // Generate PDF using QuestPDF
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(50);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(col =>
                {
                    col.Item().Text("WeKaza").FontSize(20).Bold();
                    col.Item().Text("Recibo de Renda").FontSize(14).SemiBold();
                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().Column(col =>
                {
                    col.Spacing(10);
                    col.Item().Text($"Recibo Nº: {receiptNumber}").SemiBold();
                    col.Item().Text($"Data: {paymentDate:dd 'de' MMMM 'de' yyyy}");
                    col.Item().PaddingVertical(5);
                    col.Item().Text("Senhorio:").SemiBold();
                    col.Item().Text(landlord?.Name ?? "—");
                    col.Item().PaddingVertical(5);
                    col.Item().Text("Inquilino:").SemiBold();
                    col.Item().Text(tenant?.Name ?? "—");
                    col.Item().PaddingVertical(5);
                    col.Item().Text("Imóvel:").SemiBold();
                    col.Item().Text(propertyTitle);
                    col.Item().PaddingVertical(5);
                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Descrição").SemiBold();
                        row.ConstantItem(120).AlignRight().Text("Valor").SemiBold();
                    });
                    col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten3);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text(paymentTypeLabel);
                        row.ConstantItem(120).AlignRight().Text(amountText);
                    });
                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Footer().AlignCenter().Text(t => t.Span($"WeKaza — Plataforma de Arrendamento • Recibo gerado em {DateTime.UtcNow:dd/MM/yyyy}").FontSize(8).FontColor(Colors.Grey.Medium));
            });
        });

        return document.GeneratePdf();
    }

    #endregion

    #region Webhooks

    public async Task HandlePaymentSucceededAsync(string paymentIntentId)
    {
        using var activity = Telemetry.Source.StartActivity("StripeWebhook");
        activity?.SetTag("payment.type", "payment_succeeded");
        activity?.SetTag("payment.intent_id", paymentIntentId);
        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId);

        if (payment == null)
        {
            _logger.LogWarning("Webhook payment_intent.succeeded para PI desconhecido: {PaymentIntentId}", paymentIntentId);
            return;
        }

        if (payment.Status == PaymentStatus.Succeeded)
        {
            _logger.LogInformation("Webhook duplicado payment_intent.succeeded ignorado para pagamento {PaymentId}", payment.Id);
            return;
        }

        payment.Status = PaymentStatus.Succeeded;
        payment.PaidAt = DateTime.UtcNow;
        payment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Pagamento {PaymentId} confirmado para lease {LeaseId}", payment.Id, payment.LeaseId);

        // Se é pagamento inicial, ativar o lease
        if (payment.Type == PaymentType.InitialPayment)
        {
            await _leaseActivationService.ActivateLeaseAfterPaymentAsync(payment.LeaseId);
            return;
        }

        await NotifyPaymentSucceededAsync(payment);
    }

    public async Task HandlePaymentFailedAsync(string paymentIntentId, string? failureMessage)
    {
        using var activity = Telemetry.Source.StartActivity("StripeWebhook");
        activity?.SetTag("payment.type", "payment_failed");
        activity?.SetTag("payment.intent_id", paymentIntentId);
        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId);

        if (payment == null)
        {
            _logger.LogWarning("Webhook payment_intent.payment_failed para PI desconhecido: {PaymentIntentId}", paymentIntentId);
            return;
        }

        if (payment.Status == PaymentStatus.Failed || payment.Status == PaymentStatus.Succeeded
            || payment.Status == PaymentStatus.Refunded || payment.Status == PaymentStatus.PartiallyRefunded)
        {
            _logger.LogInformation("Webhook payment_intent.payment_failed ignorado para pagamento {PaymentId} (estado terminal: {Status})", payment.Id, payment.Status);
            return;
        }

        payment.Status = PaymentStatus.Failed;
        payment.FailureReason = failureMessage;
        payment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogWarning("Pagamento {PaymentId} falhou para lease {LeaseId}: {Reason}", payment.Id, payment.LeaseId, failureMessage);

        // Schedule retry for monthly rent payments (not initial payments or refunds)
        if (payment.Type == PaymentType.MonthlyRent && payment.RetryAttempt < 3)
        {
            var nextAttempt = payment.RetryAttempt + 1;
            var delay = nextAttempt switch
            {
                1 => TimeSpan.FromDays(3),
                2 => TimeSpan.FromDays(7),
                3 => TimeSpan.FromDays(14),
                _ => TimeSpan.FromDays(14)
            };

            // Update the payment record with the next retry attempt number
            payment.RetryAttempt = nextAttempt;
            await _db.SaveChangesAsync();

            // Schedule the retry via Hangfire — pass the attempt number and the original
            // payment's RentAmount as customAmount so split payments charge the correct share
            _backgroundJobs.Schedule<MonthlyRentCollectionJob>(
                job => job.RetryRentAsync(payment.LeaseId, payment.TenantId, payment.CreatedAt.Date, nextAttempt, payment.RentAmount > 0 ? payment.RentAmount : null),
                delay);

            _logger.LogInformation(
                "Scheduled retry {Attempt}/3 for monthly rent payment {PaymentId} (lease {LeaseId}) in {Delay} days",
                nextAttempt, payment.Id, payment.LeaseId, delay.TotalDays);
        }

        // Notify after the retry attempt is bumped, so the notification includes the correct
        // retry schedule info (RetryAttempt reflects the next attempt number).
        await NotifyPaymentFailedAsync(payment);
    }

    public async Task HandlePaymentRequiresActionAsync(string paymentIntentId)
    {
        using var activity = Telemetry.Source.StartActivity("StripeWebhook");
        activity?.SetTag("payment.type", "requires_action");
        activity?.SetTag("payment.intent_id", paymentIntentId);

        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId);

        if (payment == null)
        {
            _logger.LogWarning("Webhook payment_intent.requires_action para PI desconhecido: {PaymentIntentId}", paymentIntentId);
            return;
        }

        // Guard: if the payment already succeeded (webhook ordering race —
        // payment_intent.succeeded arrived before payment_intent.requires_action),
        // don't overwrite the success status.
        if (payment.Status == PaymentStatus.Succeeded)
        {
            _logger.LogInformation("Payment {PaymentId} already succeeded — ignoring requires_action webhook", payment.Id);
            return;
        }

        // Guard: if already in Pending, only send a reminder if >1 hour since last notification
        // (prevents notification spam from duplicate Stripe webhooks)
        if (payment.Status == PaymentStatus.Pending)
        {
            if (payment.UpdatedAt.HasValue && (DateTime.UtcNow - payment.UpdatedAt.Value).TotalMinutes < 60)
            {
                _logger.LogInformation("Payment {PaymentId} already Pending — skipping notification (sent <1h ago)", payment.Id);
                return;
            }
            _logger.LogInformation("Payment {PaymentId} already Pending — sending reminder (>1h since last)", payment.Id);
        }

        payment.Status = PaymentStatus.Pending; // needs 3DS authentication
        payment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Payment {PaymentId} requires 3D Secure authentication (lease {LeaseId})", payment.Id, payment.LeaseId);

        // Notify tenant: they need to authenticate the payment in the app
        var amount = FormatCurrency(payment.Amount);
        var paymentUrl = BuildFrontendUrl($"/payments/{payment.Id}");
        var tenantMessage = payment.Type == PaymentType.MonthlyRent
            ? $"O teu pagamento de renda mensal de {amount} precisa de autenticação 3D Secure. Confirma em: {paymentUrl}"
            : $"O teu pagamento de {amount} precisa de autenticação 3D Secure. Confirma em: {paymentUrl}";

        var landlordMessage = payment.Type == PaymentType.MonthlyRent
            ? $"A cobrança da renda mensal de {amount} requer autenticação do inquilino. O inquilino foi notificado."
            : $"Um pagamento de {amount} associado ao arrendamento requer autenticação do inquilino.";

        await _notificationService.SendNotificationAsync(payment.TenantId, "payment", tenantMessage, payment.Id);
        await _notificationService.SendNotificationAsync(payment.LandlordId, "payment", landlordMessage, payment.Id);
        await TrySendPaymentStatusEmailAsync(payment, payment.TenantId, CommunicationEmailTemplateKeys.PaymentFailed, "requer autenticação");
        await TrySendPaymentStatusEmailAsync(payment, payment.LandlordId, CommunicationEmailTemplateKeys.PaymentFailed, "requer autenticação");
    }

    public async Task HandlePaymentProcessingAsync(string paymentIntentId)
    {
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId);
        if (payment == null) return;
        if (payment.Status == PaymentStatus.Succeeded || payment.Status == PaymentStatus.Failed
            || payment.Status == PaymentStatus.Refunded || payment.Status == PaymentStatus.PartiallyRefunded) return; // don't overwrite terminal states
        payment.Status = PaymentStatus.Processing;
        payment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Payment {PaymentId} entered processing state", payment.Id);
    }

    public async Task HandlePaymentCanceledAsync(string paymentIntentId)
    {
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId);
        if (payment == null) return;
        if (payment.Status == PaymentStatus.Succeeded || payment.Status == PaymentStatus.Failed
            || payment.Status == PaymentStatus.Refunded || payment.Status == PaymentStatus.PartiallyRefunded) return; // don't overwrite terminal states
        payment.Status = PaymentStatus.Failed;
        payment.FailureReason = "PaymentIntent cancelado";
        payment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _logger.LogWarning("Payment {PaymentId} was canceled", payment.Id);
    }

    public async Task HandleChargeRefundedAsync(string chargeId, long amountRefunded)
    {
        // The charge.refunded webhook sends a Charge object, not a PaymentIntent.
        // We need to retrieve the charge to get its PaymentIntentId, then find our Payment record.
        try
        {
            var chargeService = new ChargeService();
            var charge = await chargeService.GetAsync(chargeId);

            if (string.IsNullOrEmpty(charge.PaymentIntentId))
            {
                _logger.LogWarning("Charge {ChargeId} has no PaymentIntentId — cannot match to payment", chargeId);
                return;
            }

            var payment = await _db.Payments.FirstOrDefaultAsync(p => p.StripePaymentIntentId == charge.PaymentIntentId);
            if (payment == null)
            {
                _logger.LogWarning("Charge {ChargeId} refunded but no matching payment found for PI {PiId}", chargeId, charge.PaymentIntentId);
                return;
            }

            // Don't overwrite if already in a terminal refund state
            if (payment.Status == PaymentStatus.Refunded || payment.Status == PaymentStatus.PartiallyRefunded)
            {
                _logger.LogInformation("Payment {PaymentId} already in {Status} — ignoring charge.refunded", payment.Id, payment.Status);
                return;
            }

            // Determine if this is a full or partial refund
            var originalAmountCents = (long)(payment.Amount * 100);
            var refundAmountEuros = amountRefunded / 100m;
            payment.Status = amountRefunded >= originalAmountCents
                ? PaymentStatus.Refunded
                : PaymentStatus.PartiallyRefunded;
            payment.UpdatedAt = DateTime.UtcNow;

            // Create a separate refund Payment record for audit trail (mirrors RefundMonthlyRentPaymentAsync pattern)
            var refundPayment = new Payment
            {
                Id = Guid.NewGuid(),
                LeaseId = payment.LeaseId,
                TenantId = payment.TenantId,
                LandlordId = payment.LandlordId,
                StripePaymentIntentId = payment.StripePaymentIntentId,
                StripeTransferId = chargeId,
                Type = payment.Type == PaymentType.MonthlyRent ? PaymentType.MonthlyRentRefund : PaymentType.DepositRefund,
                Amount = -refundAmountEuros,
                PlatformFee = 0,
                LandlordAmount = -refundAmountEuros,
                RentAmount = 0,
                Currency = "eur",
                Status = PaymentStatus.Refunded,
                PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Payments.Add(refundPayment);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Payment {PaymentId} updated to {Status} from charge.refunded webhook (charge {ChargeId}, refunded {Amount} cents)",
                payment.Id, payment.Status, chargeId, amountRefunded);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Failed to process charge.refunded for charge {ChargeId}", chargeId);
        }
    }

    #endregion

    #region Refunds

    public async Task<PaymentDto> RefundDepositAsync(Guid leaseId, decimal amount, Guid userId)
    {
        var lease = await _leaseAccessService.GetLeaseAccessContextAsync(leaseId)
            ?? throw new InvalidOperationException("Lease não encontrado.");

        if (lease.LandlordId != userId)
            throw new UnauthorizedAccessException("Apenas o proprietário pode autorizar reembolso de caução.");

        var initialPayment = await _db.Payments
            .FirstOrDefaultAsync(p => p.LeaseId == leaseId && p.Type == PaymentType.InitialPayment && p.Status == PaymentStatus.Succeeded)
            ?? throw new InvalidOperationException("Pagamento inicial não encontrado.");

        if (amount > initialPayment.DepositAmount)
            throw new InvalidOperationException($"O valor de reembolso ({amount}€) excede a caução ({initialPayment.DepositAmount}€).");

        var refundOptions = new RefundCreateOptions
        {
            PaymentIntent = initialPayment.StripePaymentIntentId,
            Amount = (long)(amount * 100),
            ReverseTransfer = true,
            Metadata = new Dictionary<string, string>
            {
                ["type"] = "deposit_refund",
                ["trustrent_lease_id"] = leaseId.ToString()
            }
        };

        var refundService = new RefundService();
        var refund = await refundService.CreateAsync(refundOptions);

        var refundPayment = new Models.Payment
        {
            Id = Guid.NewGuid(),
            LeaseId = leaseId,
            TenantId = lease.TenantId,
            LandlordId = lease.LandlordId,
            StripePaymentIntentId = refund.PaymentIntentId ?? initialPayment.StripePaymentIntentId,
            StripeTransferId = refund.Id,
            Type = PaymentType.DepositRefund,
            Amount = -amount,
            PlatformFee = 0,
            LandlordAmount = -amount,
            RentAmount = 0,
            DepositAmount = -amount,
            AdvanceRentAmount = 0,
            Currency = "eur",
            Status = PaymentStatus.Succeeded,
            PaidAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Payments.Add(refundPayment);

        // Atualizar status do pagamento original
        if (amount >= initialPayment.DepositAmount)
            initialPayment.Status = PaymentStatus.Refunded;
        else
            initialPayment.Status = PaymentStatus.PartiallyRefunded;
        initialPayment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Reembolso de caução {Amount}€ efetuado para lease {LeaseId}", amount, leaseId);

        await NotifyDepositRefundAsync(refundPayment);

        return MapPaymentToDto(refundPayment);
    }

    private async Task NotifyPaymentSucceededAsync(Payment payment)
    {
        var amount = FormatCurrency(payment.Amount);
        var tenantMessage = payment.Type == PaymentType.MonthlyRent
            ? $"A tua renda mensal de {amount} foi confirmada com sucesso."
            : $"O teu pagamento de {amount} foi confirmado com sucesso."
            ;
        var landlordMessage = payment.Type == PaymentType.MonthlyRent
            ? $"Recebeste a confirmação da renda mensal de {amount}."
            : $"Foi confirmado um pagamento de {amount} associado ao teu arrendamento."
            ;

        await _notificationService.SendNotificationAsync(payment.TenantId, "payment", tenantMessage, payment.Id);
        await _notificationService.SendNotificationAsync(payment.LandlordId, "payment", landlordMessage, payment.Id);
        await TrySendPaymentStatusEmailAsync(payment, payment.TenantId, CommunicationEmailTemplateKeys.PaymentSucceeded, "confirmado");
        await TrySendPaymentStatusEmailAsync(payment, payment.LandlordId, CommunicationEmailTemplateKeys.PaymentSucceeded, "confirmado");
    }

    private async Task NotifyPaymentFailedAsync(Payment payment)
    {
        var amount = FormatCurrency(payment.Amount);
        var reasonSuffix = string.IsNullOrWhiteSpace(payment.FailureReason)
            ? string.Empty
            : $" Motivo: {payment.FailureReason}.";

        var paymentUrl = BuildFrontendUrl($"/payments/{payment.Id}");
        // RetryAttempt was already bumped to the NEXT attempt number by HandlePaymentFailedAsync
        // before calling this method. A retry is only scheduled if RetryAttempt < 3 (checked in
        // the scheduling block above). So RetryAttempt == 3 means retries are exhausted.
        var retryScheduled = payment.Type == PaymentType.MonthlyRent && payment.RetryAttempt > 0 && payment.RetryAttempt < 3;
        var retryInfo = retryScheduled
            ? $" Será feita uma nova tentativa automática em {payment.RetryAttempt switch { 1 => "3 dias", 2 => "7 dias", _ => "14 dias" }}. Podes pagar agora em: {paymentUrl}"
            : payment.Type == PaymentType.MonthlyRent
                ? $" Podes pagar agora em: {paymentUrl}"
                : string.Empty;

        var tenantMessage = payment.Type == PaymentType.MonthlyRent
            ? $"A renda mensal de {amount} falhou.{reasonSuffix}{retryInfo}"
            : $"O pagamento de {amount} não foi concluído.{reasonSuffix} Verifica o método de pagamento.";

        var landlordMessage = payment.Type == PaymentType.MonthlyRent
            ? retryScheduled
                ? $"A cobrança da renda mensal de {amount} falhou (tentativa {payment.RetryAttempt}/3).{reasonSuffix} Será feita uma nova tentativa em {payment.RetryAttempt switch { 1 => "3 dias", 2 => "7 dias", _ => "14 dias" }}."
                : $"A cobrança da renda mensal de {amount} falhou após 3 tentativas.{reasonSuffix} Contacta o inquilino."
            : $"Um pagamento de {amount} associado ao arrendamento falhou.{reasonSuffix}";

        await _notificationService.SendNotificationAsync(payment.TenantId, "payment", tenantMessage, payment.Id);
        await _notificationService.SendNotificationAsync(payment.LandlordId, "payment", landlordMessage, payment.Id);
        await TrySendPaymentStatusEmailAsync(payment, payment.TenantId, CommunicationEmailTemplateKeys.PaymentFailed, "falhou");
        await TrySendPaymentStatusEmailAsync(payment, payment.LandlordId, CommunicationEmailTemplateKeys.PaymentFailed, "falhou");
    }

    private async Task NotifyDepositRefundAsync(Payment refundPayment)
    {
        var amount = FormatCurrency(Math.Abs(refundPayment.Amount));
        await _notificationService.SendNotificationAsync(
            refundPayment.TenantId,
            "payment",
            $"Foi emitido um reembolso de caução no valor de {amount}.",
            refundPayment.Id);

        await _notificationService.SendNotificationAsync(
            refundPayment.LandlordId,
            "payment",
            $"O reembolso de caução de {amount} foi processado com sucesso.",
            refundPayment.Id);

        await TrySendPaymentStatusEmailAsync(refundPayment, refundPayment.TenantId, CommunicationEmailTemplateKeys.PaymentDepositRefunded, "reembolsado");
        await TrySendPaymentStatusEmailAsync(refundPayment, refundPayment.LandlordId, CommunicationEmailTemplateKeys.PaymentDepositRefunded, "reembolsado");
    }

    public async Task<PaymentDto> RefundMonthlyRentPaymentAsync(Guid paymentId, decimal amount, Guid userId)
    {
        var payment = await _db.Payments.FindAsync(paymentId)
            ?? throw new InvalidOperationException("Pagamento não encontrado.");

        if (payment.LandlordId != userId)
            throw new UnauthorizedAccessException("Apenas o proprietário pode autorizar reembolsos.");

        if (payment.Type != PaymentType.MonthlyRent)
            throw new InvalidOperationException("Apenas pagamentos de renda mensal podem ser reembolsados.");

        if (payment.Status != PaymentStatus.Succeeded)
            throw new InvalidOperationException("Apenas pagamentos concluídos podem ser reembolsados.");

        if (amount > payment.Amount)
            throw new InvalidOperationException($"O valor de reembolso ({amount}€) excede o pagamento ({payment.Amount}€).");

        var refundOptions = new RefundCreateOptions
        {
            PaymentIntent = payment.StripePaymentIntentId,
            Amount = (long)(amount * 100),
            ReverseTransfer = true,
            Metadata = new Dictionary<string, string>
            {
                ["type"] = "monthly_rent_refund",
                ["trustrent_lease_id"] = payment.LeaseId.ToString(),
                ["original_payment_id"] = paymentId.ToString()
            }
        };

        var refund = await new RefundService().CreateAsync(refundOptions);

        var refundPayment = new Payment
        {
            Id = Guid.NewGuid(),
            LeaseId = payment.LeaseId,
            TenantId = payment.TenantId,
            LandlordId = payment.LandlordId,
            StripePaymentIntentId = refund.PaymentIntentId ?? payment.StripePaymentIntentId,
            StripeTransferId = refund.Id,
            Type = PaymentType.MonthlyRentRefund,
            Amount = -amount,
            PlatformFee = 0,
            LandlordAmount = -amount,
            RentAmount = 0,
            Currency = "eur",
            Status = PaymentStatus.Refunded,
            PaidAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Payments.Add(refundPayment);

        if (amount >= payment.Amount)
            payment.Status = PaymentStatus.Refunded;
        else
            payment.Status = PaymentStatus.PartiallyRefunded;
        payment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Monthly rent refund {Amount}€ for payment {PaymentId} (lease {LeaseId})", amount, paymentId, payment.LeaseId);

        // Send refund-specific notifications (not deposit refund wording)
        var refundAmount = FormatCurrency(amount);
        await _notificationService.SendNotificationAsync(
            refundPayment.TenantId, "payment",
            $"Foi emitido um reembolso de renda no valor de {refundAmount}.",
            refundPayment.Id);
        await _notificationService.SendNotificationAsync(
            refundPayment.LandlordId, "payment",
            $"O reembolso de renda de {refundAmount} foi processado com sucesso.",
            refundPayment.Id);

        return MapPaymentToDto(refundPayment);
    }

    private async Task TrySendPaymentStatusEmailAsync(Payment payment, Guid recipientId, string templateKey, string paymentStatusLabel)
    {
        if (_communicationContentService is null || _emailService is null || _userService is null)
            return;

        var recipient = await _userService.GetProfileAsync(recipientId);
        if (recipient is null || string.IsNullOrWhiteSpace(recipient.Email))
            return;

        var lease = await _db.Leases.AsNoTracking().FirstOrDefaultAsync(item => item.Id == payment.LeaseId);
        var propertyTitle = "Contrato de arrendamento";
        if (lease is not null && _catalogAccess is not null)
        {
            var applicationContext = await _catalogAccess.GetApplicationContextAsync(lease.ApplicationId);
            if (!string.IsNullOrWhiteSpace(applicationContext?.PropertyTitle))
                propertyTitle = applicationContext.PropertyTitle!;
        }

        var renderedTemplate = await _communicationContentService.RenderEmailTemplateAsync(
            templateKey,
            new Dictionary<string, string?>
            {
                ["PropertyTitle"] = propertyTitle,
                ["PaymentAmount"] = FormatCurrency(Math.Abs(payment.Amount)),
                ["PaymentTypeLabel"] = DescribePaymentType(payment.Type),
                ["PaymentStatusLabel"] = paymentStatusLabel,
                ["FailureReason"] = string.IsNullOrWhiteSpace(payment.FailureReason) ? "Sem detalhe adicional fornecido pelo processador de pagamentos." : payment.FailureReason,
                ["LeaseUrl"] = BuildFrontendUrl($"/payments/{payment.Id}")
            });

        await _emailService.SendEmailAsync(recipient.Email, renderedTemplate.Subject, renderedTemplate.BodyHtml);
    }

    private string BuildFrontendUrl(string relativePath)
    {
        var frontendBaseUrl = _configuration["Frontend:BaseUrl"]
            ?? _configuration["App:FrontendBaseUrl"]
            ?? "http://localhost:5173";

        return $"{frontendBaseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
    }

    private static string DescribePaymentType(PaymentType type)
        => type switch
        {
            PaymentType.InitialPayment => "pagamento inicial",
            PaymentType.MonthlyRent => "renda mensal",
            PaymentType.DepositRefund => "reembolso de caução",
            PaymentType.MonthlyRentRefund => "reembolso de renda",
            _ => "pagamento"
        };

    private static string FormatCurrency(decimal amount)
        => amount.ToString("C2", CultureInfo.GetCultureInfo("pt-PT"));

    #endregion

    #region Stripe API wrappers

    /// <summary>
    /// Creates a PaymentIntent on Stripe. Extracted as virtual for unit testing.
    /// </summary>
    protected virtual async Task<PaymentIntent> StripeCreatePaymentIntentAsync(
        PaymentIntentCreateOptions options, RequestOptions requestOptions)
    {
        return await new PaymentIntentService().CreateAsync(options, requestOptions);
    }

    /// <summary>
    /// Retrieves a PaymentIntent from Stripe by ID. Extracted as virtual for unit testing.
    /// </summary>
    protected virtual async Task<PaymentIntent> StripeGetPaymentIntentAsync(string paymentIntentId)
    {
        return await new PaymentIntentService().GetAsync(paymentIntentId);
    }

    /// <summary>
    /// Searches Stripe for PaymentIntents matching the query. Extracted as virtual for unit testing.
    /// </summary>
    protected virtual async Task<StripeSearchResult<PaymentIntent>> StripeSearchPaymentIntentsAsync(
        PaymentIntentSearchOptions options)
    {
        return await new PaymentIntentService().SearchAsync(options);
    }

    #endregion

    #region Private Helpers

    private static async Task<PaymentIntent> ConfirmPaymentIntentIfNeededAsync(
        PaymentIntentService piService,
        PaymentIntent paymentIntent,
        string? paymentMethodId)
    {
        if (string.IsNullOrWhiteSpace(paymentMethodId))
            return paymentIntent;

        var confirmableStatuses = new[] { "requires_payment_method", "requires_confirmation" };
        if (!confirmableStatuses.Contains(paymentIntent.Status))
            return paymentIntent;

        return await piService.ConfirmAsync(paymentIntent.Id, new PaymentIntentConfirmOptions
        {
            PaymentMethod = paymentMethodId
        });
    }

    private PaymentBreakdownDto CalculateBreakdown(decimal monthlyRent, int advanceRentMonths, decimal deposit)
    {
        var advanceRent = monthlyRent * advanceRentMonths;
        var total = monthlyRent + advanceRent + deposit;

        // Taxa plataforma: 30€ (PlatformFeePerMonth em cêntimos / 100) por cada mês de renda
        // Mês corrente + meses antecipados. Caução não é taxada.
        var feePerMonth = _platformFeePerMonth / 100m;
        var platformFee = feePerMonth * (1 + advanceRentMonths);

        var landlordReceives = total - platformFee;

        return new PaymentBreakdownDto(
            MonthlyRent: monthlyRent,
            AdvanceRent: advanceRent,
            AdvanceRentMonths: advanceRentMonths,
            Deposit: deposit,
            PlatformFee: platformFee,
            Total: total,
            LandlordReceives: landlordReceives
        );
    }

    private static PaymentDto MapPaymentToDto(Models.Payment p) => new(
        p.Id, p.LeaseId, p.Type, p.Amount, p.PlatformFee, p.LandlordAmount,
        p.RentAmount, p.DepositAmount, p.AdvanceRentAmount,
        p.Currency, p.Status, p.FailureReason, p.PaidAt, p.CreatedAt,
        p.StripePaymentIntentId, p.RetryAttempt, p.UpdatedAt,
        // Derive billing period from CreatedAt (yyyy-MM format) for monthly rent;
        // for initial payments, use the full date as there's no billing period concept.
        p.Type == PaymentType.MonthlyRent ? p.CreatedAt.ToString("yyyy-MM") : null
    );

    private static TenantPaymentMethodDto MapPaymentMethodToDto(TenantPaymentMethod m) => new(
        m.Id, m.Type, m.DisplayName, m.StripePaymentMethodId,
        m.CardBrand, m.CardLast4, m.CardExpMonth, m.CardExpYear, m.IsDefault
    );

    #endregion
}
