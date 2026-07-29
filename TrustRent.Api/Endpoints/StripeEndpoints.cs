using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Models;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Contracts.DTOs;
using TrustRent.Modules.Leasing.Contracts.Interfaces;

namespace TrustRent.Api.Endpoints;

public static class StripeEndpoints
{
    public static void MapStripeEndpoints(this WebApplication app)
    {
        var connect = app.MapGroup("/api/stripe/connect");
        var paymentMethods = app.MapGroup("/api/stripe/payment-methods");
        var payments = app.MapGroup("/api/stripe/payments");

        #region Connect (Proprietários)

        // GET /api/stripe/connect/return — Ponte web para regressar à app móvel
        connect.MapGet("/return", () => BuildMobileStripeBridgeResult("return"));

        // GET /api/stripe/connect/refresh — Ponte web para regressar à app e reabrir o fluxo
        connect.MapGet("/refresh", () => BuildMobileStripeBridgeResult("refresh"));

        // POST /api/stripe/connect/create — Criar conta Express
        connect.MapPost("/create",
            async ([FromBody] CreateConnectAccountDto dto,
                   IStripeAccountService accountService, IUserService userService, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var profile = await userService.GetProfileAsync(userId);
                    if (profile == null) return Results.NotFound("Perfil não encontrado.");

                    var result = await accountService.CreateConnectAccountAsync(userId, profile.Email, profile.Name, dto.PropertyId);
                    return Results.Ok(result);
                }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
                catch (StripeException e) { return Results.BadRequest(e.StripeError?.Message ?? e.Message); }
            }).RequireAuthorization();

        // POST /api/stripe/connect/onboarding-link — Gerar link de onboarding
        connect.MapPost("/onboarding-link",
            async ([FromBody] OnboardingLinkRequestDto dto,
                   IStripeAccountService accountService, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var account = await accountService.GetAccountByIdAsync(dto.StripeAccountId);
                    if (account == null || account.UserId != userId) return Results.Forbid();

                    var result = await accountService.GetOnboardingLinkAsync(dto.StripeAccountId, dto.ReturnUrl, dto.RefreshUrl);
                    return Results.Ok(result);
                }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
                catch (StripeException e) { return Results.BadRequest(e.StripeError?.Message ?? e.Message); }
            }).RequireAuthorization();

        // GET /api/stripe/connect/status — Estado da(s) conta(s) do proprietário
        connect.MapGet("/status",
            async (IStripeAccountService accountService, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                var accounts = await accountService.GetAccountsByUserAsync(userId);
                return Results.Ok(accounts);
            }).RequireAuthorization();

        // GET /api/stripe/connect/property/{propertyId}/status
        connect.MapGet("/property/{propertyId:guid}/status",
            async (Guid propertyId, IStripeAccountService accountService, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                var propertyAccount = await accountService.GetAccountForPropertyAsync(propertyId);
                var globalAccount = await accountService.GetDefaultAccountAsync(userId);

                var propertyAccountIsActive = propertyAccount is { ChargesEnabled: true, PayoutsEnabled: true };
                var globalAccountIsActive = globalAccount is { ChargesEnabled: true, PayoutsEnabled: true };

                var selectedAccount = propertyAccountIsActive
                    ? propertyAccount
                    : globalAccountIsActive
                        ? globalAccount
                        : propertyAccount ?? globalAccount;

                var selectedSource = propertyAccountIsActive
                    ? "property"
                    : globalAccountIsActive
                        ? "global"
                        : (string?)null;

                return Results.Ok(new
                {
                    configured = selectedAccount is { ChargesEnabled: true, PayoutsEnabled: true },
                    selectedSource,
                    account = selectedAccount,
                    propertyAccount,
                    globalAccount
                });
            }).RequireAuthorization();

        // POST /api/stripe/connect/{id}/refresh — Refrescar estado da conta
        connect.MapPost("/{id:guid}/refresh",
            async (Guid id, IStripeAccountService accountService, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var account = await accountService.GetAccountByIdAsync(id);
                    if (account == null || account.UserId != userId) return Results.Forbid();
                    await accountService.RefreshAccountStatusAsync(id);
                    var updated = await accountService.GetAccountByIdAsync(id);
                    return Results.Ok(updated);
                }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
                catch (StripeException e) { return Results.BadRequest(e.StripeError?.Message ?? e.Message); }
            }).RequireAuthorization();

        #endregion

        #region Payment Methods (Inquilinos)

        // POST /api/stripe/payment-methods/setup-intent
        paymentMethods.MapPost("/setup-intent",
            async (IStripePaymentService paymentService, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                var result = await paymentService.CreateSetupIntentAsync(userId);
                return Results.Ok(result);
            }).RequireAuthorization();

        // POST /api/stripe/payment-methods/save
        paymentMethods.MapPost("/save",
            async ([FromBody] SavePaymentMethodRequest dto,
                   IStripePaymentService paymentService, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var result = await paymentService.SavePaymentMethodAsync(userId, dto.StripePaymentMethodId);
                    return Results.Ok(result);
                }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
            }).RequireAuthorization();

        // GET /api/stripe/payment-methods
        paymentMethods.MapGet("/",
            async (IStripePaymentService paymentService, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                var methods = await paymentService.GetSavedPaymentMethodsAsync(userId);
                return Results.Ok(methods);
            }).RequireAuthorization();

        // DELETE /api/stripe/payment-methods/{id}
        paymentMethods.MapDelete("/{id:guid}",
            async (Guid id, IStripePaymentService paymentService, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    await paymentService.RemovePaymentMethodAsync(userId, id);
                    return Results.NoContent();
                }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
            }).RequireAuthorization();

        // PUT /api/stripe/payment-methods/{id}/default
        paymentMethods.MapPut("/{id:guid}/default",
            async (Guid id, IStripePaymentService paymentService, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                await paymentService.SetDefaultPaymentMethodAsync(userId, id);
                return Results.NoContent();
            }).RequireAuthorization();

        #endregion

        #region Payments

        // GET /api/stripe/payments/lease/{leaseId}/breakdown
        payments.MapGet("/lease/{leaseId:guid}/breakdown",
            async (Guid leaseId, IStripePaymentService paymentService, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var breakdown = await paymentService.GetInitialPaymentBreakdownAsync(leaseId);
                    return Results.Ok(breakdown);
                }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
            }).RequireAuthorization();

        // POST /api/stripe/payments/initial/{leaseId}
        payments.MapPost("/initial/{leaseId:guid}",
            async (Guid leaseId, [FromBody] CreatePaymentDto dto,
                   IStripePaymentService paymentService, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var result = await paymentService.CreateInitialPaymentAsync(leaseId, userId, dto.PaymentMethodId);
                    return Results.Ok(result);
                }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
            }).RequireAuthorization();

        // GET /api/stripe/payments/lease/{leaseId}
        payments.MapGet("/lease/{leaseId:guid}",
            async (Guid leaseId, IStripePaymentService paymentService, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var history = await paymentService.GetPaymentsByLeaseAsync(leaseId, userId);
                    return Results.Ok(history);
                }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
            }).RequireAuthorization();

        // GET /api/stripe/payments/{paymentId}
        payments.MapGet("/{paymentId:guid}",
            async (Guid paymentId, IStripePaymentService paymentService, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var payment = await paymentService.GetPaymentByIdAsync(paymentId, userId);
                    return payment == null ? Results.NotFound() : Results.Ok(payment);
                }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
            }).RequireAuthorization();

        // POST /api/stripe/payments/lease/{leaseId}/refund-deposit
        payments.MapPost("/lease/{leaseId:guid}/refund-deposit",
            async (Guid leaseId, [FromBody] RefundDepositRequest dto,
                   IStripePaymentService paymentService, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var result = await paymentService.RefundDepositAsync(leaseId, dto.Amount, userId);
                    return Results.Ok(result);
                }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
            }).RequireAuthorization();

        // POST /api/stripe/payments/{paymentId}/refund — Monthly rent refund
        payments.MapPost("/{paymentId:guid}/refund",
            async (Guid paymentId, [FromBody] RefundMonthlyRentRequest dto,
                   IStripePaymentService paymentService, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var result = await paymentService.RefundMonthlyRentPaymentAsync(paymentId, dto.Amount, userId);
                    return Results.Ok(result);
                }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
            }).RequireAuthorization();

        // GET /api/stripe/payments/{paymentId}/receipt — Generate PDF receipt
        payments.MapGet("/{paymentId:guid}/receipt",
            async (Guid paymentId, IStripePaymentService paymentService, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var pdf = await paymentService.GenerateReceiptAsync(paymentId, userId);
                    return Results.File(pdf, "application/pdf", $"recibo-{paymentId.ToString()[..8]}.pdf");
                }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
            }).RequireAuthorization();

        // POST /api/stripe/payments/monthly-rent/{leaseId}/retry
        // Tenant-initiated on-demand retry for failed monthly rent payment.
        // The idempotency + billing-period-wide check in CreateMonthlyRentPaymentAsync
        // prevents duplicate charges.
        payments.MapPost("/monthly-rent/{leaseId:guid}/retry",
            async (Guid leaseId, IStripePaymentService paymentService, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var result = await paymentService.RetryMonthlyRentPaymentAsync(leaseId, userId);
                    if (result == null)
                        return Results.NotFound("Nenhum pagamento de renda mensal encontrado para este contrato.");
                    return Results.Ok(result);
                }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
            }).RequireAuthorization();

        // GET /api/stripe/payments/{paymentId}/client-secret
        // Returns the Stripe PaymentIntent client_secret for 3DS confirmation.
        // Only returns it for payments that need authentication (status = Pending).
        payments.MapGet("/{paymentId:guid}/client-secret",
            async (Guid paymentId, IStripePaymentService paymentService, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var clientSecret = await paymentService.GetPaymentClientSecretAsync(paymentId, userId);
                    return clientSecret == null
                        ? Results.NotFound()
                        : Results.Ok(new { clientSecret });
                }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
            }).RequireAuthorization();

        #endregion

        #region Webhook

        // POST /api/stripe/webhook — NÃO requer autenticação JWT
        app.MapPost("/api/stripe/webhook",
            async (HttpContext httpContext, IStripePaymentService paymentService,
                   IStripeAccountService accountService, IConfiguration configuration,
                   AdminDbContext adminDb) =>
            {
                var json = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
                var webhookSecret = configuration["Stripe:WebhookSecret"];

                Event stripeEvent;
                try
                {
                    stripeEvent = EventUtility.ConstructEvent(json,
                        httpContext.Request.Headers["Stripe-Signature"],
                        webhookSecret);
                }
                catch (StripeException)
                {
                    return Results.BadRequest("Assinatura do webhook inválida.");
                }

                // Idempotency: check if this Stripe event was already received
                var existingEvent = await adminDb.WebhookEvents
                    .FirstOrDefaultAsync(w => w.StripeEventId == stripeEvent.Id);
                if (existingEvent != null)
                {
                    // Already processed or being processed — return 200 to stop Stripe retries
                    return Results.Ok();
                }

                // Extract the actual Stripe object ID for replay purposes
                string payloadObjectId = "unknown";
                if (stripeEvent.Data?.Object is PaymentIntent pi)
                    payloadObjectId = pi.Id;
                else if (stripeEvent.Data?.Object is Account acct)
                    payloadObjectId = acct.Id;
                else if (stripeEvent.Data?.Object is Charge ch)
                    payloadObjectId = ch.Id;

                // Log webhook event with "received" status (will be updated after processing)
                var webhookEvent = new TrustRent.Modules.Admin.Models.WebhookEvent
                {
                    Id = Guid.NewGuid(),
                    StripeEventId = stripeEvent.Id,
                    EventType = stripeEvent.Type,
                    Status = "received",
                    PayloadSummary = payloadObjectId,
                    CreatedAt = DateTime.UtcNow
                };
                adminDb.WebhookEvents.Add(webhookEvent);
                await adminDb.SaveChangesAsync();

                try
                {
                    switch (stripeEvent.Type)
                    {
                        case EventTypes.PaymentIntentSucceeded:
                            var piSucceeded = stripeEvent.Data.Object as PaymentIntent;
                            if (piSucceeded != null)
                                await paymentService.HandlePaymentSucceededAsync(piSucceeded.Id);
                            break;

                        case EventTypes.PaymentIntentPaymentFailed:
                            var piFailed = stripeEvent.Data.Object as PaymentIntent;
                            if (piFailed != null)
                                await paymentService.HandlePaymentFailedAsync(piFailed.Id,
                                    piFailed.LastPaymentError?.Message);
                            break;

                        case EventTypes.PaymentIntentRequiresAction:
                            var piAction = stripeEvent.Data.Object as PaymentIntent;
                            if (piAction != null)
                                await paymentService.HandlePaymentRequiresActionAsync(piAction.Id);
                            break;

                        case EventTypes.AccountUpdated:
                            var account = stripeEvent.Data.Object as Account;
                            if (account != null)
                                await accountService.HandleAccountUpdatedWebhookAsync(account.Id);
                            break;

                        case EventTypes.PaymentIntentProcessing:
                            var piProcessing = stripeEvent.Data.Object as PaymentIntent;
                            if (piProcessing != null)
                                await paymentService.HandlePaymentProcessingAsync(piProcessing.Id);
                            break;

                        case EventTypes.PaymentIntentCanceled:
                            var piCanceled = stripeEvent.Data.Object as PaymentIntent;
                            if (piCanceled != null)
                                await paymentService.HandlePaymentCanceledAsync(piCanceled.Id);
                            break;

                        case EventTypes.ChargeRefunded:
                            var charge = stripeEvent.Data.Object as Charge;
                            if (charge != null)
                                await paymentService.HandleChargeRefundedAsync(charge.Id, charge.AmountRefunded);
                            break;
                    }

                    webhookEvent.Status = "processed";
                    webhookEvent.ProcessedAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    webhookEvent.Status = "failed";
                    webhookEvent.Error = ex.Message;
                }

                await adminDb.SaveChangesAsync();

                return Results.Ok();
            });

        #endregion
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        userId = Guid.Empty;
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null && Guid.TryParse(claim, out userId);
    }

        private static IResult BuildMobileStripeBridgeResult(string state)
        {
                var deepLink = state == "refresh"
                        ? "trustrent://stripe?connect=refresh"
                        : "trustrent://stripe?connect=return";

                var title = state == "refresh"
                        ? "Regressar a app para continuar"
                        : "Regressar a app TrustRent";

                var body = state == "refresh"
                        ? "O link Stripe expirou ou precisa de ser renovado. Volta a app para reabrir o onboarding."
                        : "O browser esta pronto para te devolver a app TrustRent. Se nao abrir automaticamente, usa o botao abaixo.";

                var html = $$"""
<!doctype html>
<html lang="pt-PT">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>TrustRent Stripe</title>
    <meta http-equiv="refresh" content="0;url={{deepLink}}">
    <style>
        body {
            margin: 0;
            font-family: Arial, sans-serif;
            background: #f8fafc;
            color: #0f172a;
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            padding: 24px;
        }
        main {
            max-width: 420px;
            background: #ffffff;
            border: 1px solid #dbe4ee;
            border-radius: 20px;
            padding: 24px;
            box-shadow: 0 12px 28px rgba(15, 23, 42, 0.08);
        }
        h1 {
            margin: 0 0 12px;
            font-size: 24px;
        }
        p {
            margin: 0 0 18px;
            line-height: 1.6;
        }
        a {
            display: inline-block;
            background: #1d4ed8;
            color: #ffffff;
            text-decoration: none;
            font-weight: 700;
            padding: 12px 18px;
            border-radius: 12px;
        }
    </style>
    <script>
        window.location.replace('{{deepLink}}');
    </script>
</head>
<body>
    <main>
        <h1>{{title}}</h1>
        <p>{{body}}</p>
        <a href="{{deepLink}}">Abrir TrustRent</a>
    </main>
</body>
</html>
""";

                return Results.Content(html, "text/html; charset=utf-8");
        }
}

// Request DTOs para os endpoints
public record SavePaymentMethodRequest(string StripePaymentMethodId);
public record RefundDepositRequest(decimal Amount);
public record RefundMonthlyRentRequest(decimal Amount);
