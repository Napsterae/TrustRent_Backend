using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stripe;
using System.Text.Json;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.DTOs;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Modules.Leasing.Services;

public class StripePaymentService : IStripePaymentService
{
    private readonly LeasingDbContext _db;
    private readonly ILeaseAccessService _leaseAccessService;
    private readonly ILeaseActivationService _leaseActivationService;
    private readonly IStripeAccountService _stripeAccountService;
    private readonly ILogger<StripePaymentService> _logger;
    private readonly int _platformFeePerMonth;

    public StripePaymentService(
        LeasingDbContext db,
        ILeaseAccessService leaseAccessService,
        ILeaseActivationService leaseActivationService,
        IStripeAccountService stripeAccountService,
        IConfiguration configuration,
        ILogger<StripePaymentService> logger)
    {
        _db = db;
        _leaseAccessService = leaseAccessService;
        _leaseActivationService = leaseActivationService;
        _stripeAccountService = stripeAccountService;
        _logger = logger;
        _platformFeePerMonth = configuration.GetValue<int>("Stripe:PlatformFeePerMonth", 3000);
        StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
    }

    #region Customer & Payment Methods

    public async Task<string> EnsureCustomerAsync(Guid userId, string email, string name)
    {
        // Verificar se já existe TenantPaymentMethod com este user (implica customer existente)
        // O StripeCustomerId está no User model (Identity), mas gerimos via parâmetro
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
            IsDefault = isFirst,
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

        if (existingPayment != null)
        {
            if (existingPayment.Status == PaymentStatus.Succeeded)
                throw new InvalidOperationException("Pagamento inicial já foi efetuado.");

            // Retornar o client secret existente
            var piService = new PaymentIntentService();
            var existingPi = await piService.GetAsync(existingPayment.StripePaymentIntentId);
            return new PaymentClientSecretDto(existingPi.ClientSecret, existingPayment.Id, existingPayment.Amount, existingPayment.Currency);
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
        }

        var piServiceCreate = new PaymentIntentService();
        var paymentIntent = await piServiceCreate.CreateAsync(piOptions);

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

        return new PaymentClientSecretDto(paymentIntent.ClientSecret, payment.Id, payment.Amount, payment.Currency);
    }

    public async Task<PaymentDto?> GetPaymentByIdAsync(Guid paymentId, Guid userId)
    {
        var payment = await _db.Payments.FindAsync(paymentId);
        if (payment == null) return null;

        if (payment.TenantId != userId && payment.LandlordId != userId)
            throw new UnauthorizedAccessException("Sem permissão para ver este pagamento.");

        return MapPaymentToDto(payment);
    }

    public async Task<IEnumerable<PaymentDto>> GetPaymentsByLeaseAsync(Guid leaseId, Guid userId)
    {
        var lease = await _leaseAccessService.GetLeaseAccessContextAsync(leaseId)
            ?? throw new InvalidOperationException("Lease não encontrado.");

        if (lease.TenantId != userId && lease.LandlordId != userId)
            throw new UnauthorizedAccessException("Sem permissão para ver pagamentos deste lease.");

        var payments = await _db.Payments
            .Where(p => p.LeaseId == leaseId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return payments.Select(MapPaymentToDto);
    }

    #endregion

    #region Webhooks

    public async Task HandlePaymentSucceededAsync(string paymentIntentId)
    {
        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId);

        if (payment == null)
        {
            _logger.LogWarning("Webhook payment_intent.succeeded para PI desconhecido: {PaymentIntentId}", paymentIntentId);
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
        }
    }

    public async Task HandlePaymentFailedAsync(string paymentIntentId, string? failureMessage)
    {
        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntentId);

        if (payment == null)
        {
            _logger.LogWarning("Webhook payment_intent.payment_failed para PI desconhecido: {PaymentIntentId}", paymentIntentId);
            return;
        }

        payment.Status = PaymentStatus.Failed;
        payment.FailureReason = failureMessage;
        payment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogWarning("Pagamento {PaymentId} falhou para lease {LeaseId}: {Reason}", payment.Id, payment.LeaseId, failureMessage);
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
            CreatedAt = DateTime.UtcNow
        };

        _db.Payments.Add(refundPayment);

        // Atualizar status do pagamento original se reembolso total
        if (amount >= initialPayment.DepositAmount)
            initialPayment.Status = PaymentStatus.PartiallyRefunded;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Reembolso de caução {Amount}€ efetuado para lease {LeaseId}", amount, leaseId);

        return MapPaymentToDto(refundPayment);
    }

    #endregion

    #region Private Helpers

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
        p.Currency, p.Status, p.FailureReason, p.PaidAt, p.CreatedAt
    );

    private static TenantPaymentMethodDto MapPaymentMethodToDto(TenantPaymentMethod m) => new(
        m.Id, m.Type, m.DisplayName, m.StripePaymentMethodId,
        m.CardBrand, m.CardLast4, m.CardExpMonth, m.CardExpYear, m.IsDefault
    );

    #endregion
}
