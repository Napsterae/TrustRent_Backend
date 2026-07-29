using Stripe;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Services;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Tests.Leasing;

/// <summary>
/// Testable subclass of StripePaymentService that overrides Stripe API calls
/// with configurable responses. No network calls are made.
/// </summary>
public class TestableStripePaymentService : StripePaymentService
{
    private PaymentIntent? _createResult;
    private PaymentIntent? _getResult;
    private StripeSearchResult<PaymentIntent>? _searchResult;
    private StripeException? _createException;
    private StripeException? _getException;
    private StripeException? _searchException;
    private Func<PaymentIntentCreateOptions, RequestOptions, Task<PaymentIntent>>? _createFunc;
    private Func<string, Task<PaymentIntent>>? _getFunc;

    public TestableStripePaymentService(
        LeasingDbContext db,
        ILeaseAccessService leaseAccess,
        ILeaseActivationService leaseActivation,
        IStripeAccountService stripeAccount,
        INotificationService notification,
        Microsoft.Extensions.Configuration.IConfiguration config,
        Microsoft.Extensions.Logging.ILogger<StripePaymentService> logger,
        Hangfire.IBackgroundJobClient backgroundJobs,
        ICommunicationContentService? commContent = null,
        IEmailService? emailService = null,
        TrustRent.Modules.Identity.Contracts.Interfaces.IUserService? userService = null,
        ICatalogAccessService? catalogAccess = null)
        : base(db, leaseAccess, leaseActivation, stripeAccount, notification, config, logger,
               backgroundJobs, commContent, emailService, userService, catalogAccess)
    {
    }

    /// <summary>Set the PaymentIntent that CreateAsync will return.</summary>
    public void SetCreateResult(PaymentIntent result) => _createResult = result;

    /// <summary>Set the PaymentIntent that GetAsync will return.</summary>
    public void SetGetResult(PaymentIntent result) => _getResult = result;

    /// <summary>Set the search result that SearchAsync will return.</summary>
    public void SetSearchResult(StripeSearchResult<PaymentIntent> result) => _searchResult = result;

    /// <summary>Set an exception that CreateAsync will throw.</summary>
    public void SetCreateException(StripeException ex) => _createException = ex;

    /// <summary>Set an exception that GetAsync will throw.</summary>
    public void SetGetException(StripeException ex) => _getException = ex;

    /// <summary>Set an exception that SearchAsync will throw.</summary>
    public void SetSearchException(StripeException ex) => _searchException = ex;

    /// <summary>Set a custom function for CreateAsync (for dynamic responses).</summary>
    public void SetCreateFunc(Func<PaymentIntentCreateOptions, RequestOptions, Task<PaymentIntent>> func) => _createFunc = func;

    /// <summary>Set a custom function for GetAsync (for dynamic responses).</summary>
    public void SetGetFunc(Func<string, Task<PaymentIntent>> func) => _getFunc = func;

    protected override Task<PaymentIntent> StripeCreatePaymentIntentAsync(
        PaymentIntentCreateOptions options, RequestOptions requestOptions)
    {
        if (_createException != null) throw _createException;
        if (_createFunc != null) return _createFunc(options, requestOptions);
        if (_createResult != null) return Task.FromResult(_createResult);
        // Default: return a succeeded PI with the options' amount
        return Task.FromResult(new PaymentIntent
        {
            Id = "pi_test_" + Guid.NewGuid().ToString("N")[..12],
            Amount = options.Amount ?? 0,
            Currency = options.Currency,
            Status = "succeeded",
            ClientSecret = "pi_test_secret_" + Guid.NewGuid().ToString("N")[..12],
            Metadata = options.Metadata ?? new Dictionary<string, string>()
        });
    }

    protected override Task<PaymentIntent> StripeGetPaymentIntentAsync(string paymentIntentId)
    {
        if (_getException != null) throw _getException;
        if (_getFunc != null) return _getFunc(paymentIntentId);
        if (_getResult != null) return Task.FromResult(_getResult);
        // Default: return a succeeded PI
        return Task.FromResult(new PaymentIntent
        {
            Id = paymentIntentId,
            Amount = 85000,
            Currency = "eur",
            Status = "succeeded",
            ClientSecret = paymentIntentId + "_secret_test"
        });
    }

    protected override Task<StripeSearchResult<PaymentIntent>> StripeSearchPaymentIntentsAsync(
        PaymentIntentSearchOptions options)
    {
        if (_searchException != null) throw _searchException;
        if (_searchResult != null) return Task.FromResult(_searchResult);
        // Default: no results found
        return Task.FromResult(new StripeSearchResult<PaymentIntent>
        {
            Data = new List<PaymentIntent>()
        });
    }
}
