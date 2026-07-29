using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.Text;
using TrustRent.Api.Endpoints;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Modules.Catalog.Jobs;
using TrustRent.Modules.Catalog.Repositories;
using TrustRent.Modules.Catalog.Services;
using TrustRent.Modules.Communications.Contracts.Database;
using TrustRent.Modules.Communications.Endpoints;
using TrustRent.Modules.Communications.Hubs;
using TrustRent.Modules.Identity.Contracts.Database;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Identity.Repositories;
using TrustRent.Modules.Identity.Services;
using TrustRent.Modules.Identity.Jobs;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Services;
using TrustRent.Api.Services;
using TrustRent.Api.Jobs;
using TrustRent.Modules.Identity.Seeds;
using TrustRent.Modules.Catalog.Seeds;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Seeds;
using TrustRent.Modules.Communications.Seeds;
using TrustRent.Shared.Security;
using TrustRent.Shared.Infrastructure;
using System.Threading.RateLimiting;
using TrustRent.Modules.Admin;
using TrustRent.Modules.Admin.Endpoints;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Seeds;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using TrustRent.Shared;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Instrumentation.EntityFrameworkCore;
using OpenTelemetry.Instrumentation.Runtime;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "WeKaza")
    .Enrich.WithProperty("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production")
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateLogger();

try
{
    Log.Information("Starting WeKaza API");
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddSerilog();

var railwayPort = Environment.GetEnvironmentVariable("PORT");
var runningInContainer = string.Equals(
    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
    "true",
    StringComparison.OrdinalIgnoreCase);

if (runningInContainer || !string.IsNullOrWhiteSpace(railwayPort))
{
    var listenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "http://0.0.0.0:8080"
    };

    if (!string.IsNullOrWhiteSpace(railwayPort))
    {
        listenUrls.Add($"http://0.0.0.0:{railwayPort}");
    }

    builder.WebHost.UseUrls(string.Join(';', listenUrls));
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Per-endpoint request body size limits — configurable via appsettings.json
builder.Services.Configure<RequestBodySizeOptions>(builder.Configuration.GetSection("RequestBodySize"));

// Global default: 300 KB for JSON payloads. Upload endpoints override this via middleware.
const long defaultMaxRequestBodySize = 300 * 1024; // 300 KB

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = defaultMaxRequestBodySize;
});

builder.Services.Configure<FormOptions>(options =>
{
    // MultipartBodyLengthLimit is global (not per-request). Set to the max upload limit (100 MB).
    // Per-endpoint enforcement is handled by MaxRequestBodySize (Kestrel) which rejects first,
    // and by the RequestBodySizeMiddleware which overrides MaxRequestBodySize per path.
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024; // 100 MB
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TrustRent API",
        Version = "v1"
    });

    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Insere apenas o Token JWT (sem a palavra 'Bearer')"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

var connectionString = builder.Configuration.GetConnectionString("PostgresConnection");
ValidatePostgresConnectionString(connectionString);

builder.Services.AddDbContext<IdentityDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDbContext<CommunicationsDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddDbContext<TrustRent.Modules.Leasing.Contracts.Database.LeasingDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddSignalR();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ILoginCodeService, LoginCodeService>();
builder.Services.AddScoped<IWhatsAppCodeService, WhatsAppCodeService>();
builder.Services.AddScoped<IPhoneLoginCodeService, PhoneLoginCodeService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddHttpClient<IGeminiDocumentService, GeminiDocumentService>();
builder.Services.AddHttpClient<IWhatsAppService, MetaWhatsAppService>(client =>
{
    client.BaseAddress = new Uri("https://graph.facebook.com");
});
builder.Services.AddHttpClient<ITelegramMessagingPlatformService, TelegramMessagingPlatformService>(client =>
{
    client.BaseAddress = new Uri("https://api.telegram.org");
});
builder.Services.AddHostedService<TelegramUpdatesPollingService>();
builder.Services.AddHttpClient<TrustRent.Modules.Communications.Services.IExpoPushService, TrustRent.Modules.Communications.Services.ExpoPushService>(client =>
{
    client.BaseAddress = new Uri("https://exp.host/--/api/v2/");
});
builder.Services.AddScoped<IImageService, CloudinaryImageService>();
builder.Services.AddSingleton<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddScoped<ICommunicationContentService, CommunicationContentService>();
builder.Services.AddHttpClient<IResendEmailSender, ResendEmailSender>();
builder.Services.AddScoped<IAmazonSesEmailSender, AmazonSesEmailSender>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<IEmailService, PreferenceAwareEmailService>();
builder.Services.AddScoped<ILegalDocumentNotificationJob, LegalDocumentNotificationJob>();
builder.Services.AddScoped<TrustRent.Modules.Communications.Services.NotificationService>();
builder.Services.AddScoped<INotificationService, MultiChannelNotificationService>();
builder.Services.AddScoped<ILeaseAccessService, CatalogLeaseAccessService>();
builder.Services.AddScoped<IUserContactAccessService, CatalogUserContactAccessService>();
builder.Services.AddScoped<ICatalogAccessService, CatalogAccessService>();
builder.Services.AddScoped<ILeasingAccessService, LeasingAccessService>();

/* CATALOG*/
builder.Services.AddScoped<IPropertyService, PropertyService>();
builder.Services.AddScoped<IPropertyUploadJob, PropertyUploadJob>();
builder.Services.AddScoped<IDocumentExtractionService, DocumentExtractionService>();
builder.Services.AddScoped<IPropertyRepository, PropertyRepository>();
builder.Services.AddScoped<ICatalogUnitOfWork, CatalogUnitOfWork>();
builder.Services.AddScoped<IApplicationService, ApplicationService>();
builder.Services.AddScoped<ICoTenantInviteService, CoTenantInviteService>();
builder.Services.AddScoped<IGuarantorService, GuarantorService>();
builder.Services.AddScoped<IApplicationParticipantService, ApplicationParticipantService>();
builder.Services.AddScoped<IApplicationStatusValidator, ApplicationStatusValidator>();
builder.Services.AddScoped<IIncomeValidationService, IncomeValidationService>();

/* LEASING */
builder.Services.AddScoped<TrustRent.Modules.Leasing.Contracts.Interfaces.ILeaseService, TrustRent.Modules.Leasing.Services.LeaseService>();
builder.Services.AddScoped<TrustRent.Modules.Leasing.Contracts.Interfaces.IContractGenerationService, TrustRent.Modules.Leasing.Services.ContractGenerationService>();
builder.Services.AddScoped<TrustRent.Modules.Leasing.Contracts.Interfaces.IDigitalSignatureService, TrustRent.Modules.Leasing.Services.DigitalSignatureService>();
builder.Services.AddScoped<TrustRent.Modules.Leasing.Contracts.Interfaces.ISignedPdfVerificationService, TrustRent.Modules.Leasing.Services.SignedPdfVerificationService>();
builder.Services.AddScoped<TrustRent.Modules.Leasing.Repositories.ILeasingUnitOfWork, TrustRent.Modules.Leasing.Repositories.LeasingUnitOfWork>();
builder.Services.AddScoped<TrustRent.Modules.Leasing.Contracts.Interfaces.ITicketService, TrustRent.Modules.Leasing.Services.TicketService>();
builder.Services.AddScoped<TrustRent.Modules.Leasing.Contracts.Interfaces.IReviewService, TrustRent.Modules.Leasing.Services.ReviewService>();
builder.Services.AddScoped<TrustRent.Modules.Leasing.Jobs.IContractGenerationJob, TrustRent.Modules.Leasing.Jobs.ContractGenerationJob>();
builder.Services.AddScoped<TrustRent.Modules.Leasing.Jobs.IDailyMaintenanceJob, TrustRent.Modules.Leasing.Jobs.DailyMaintenanceJob>();
builder.Services.AddScoped<TrustRent.Modules.Leasing.Jobs.IMonthlyRentCollectionJob, TrustRent.Modules.Leasing.Jobs.MonthlyRentCollectionJob>();
builder.Services.AddScoped<DataRetentionJob>();
builder.Services.AddScoped<GuarantorTokenCleanupJob>();
builder.Services.AddScoped<TrustRent.Api.Jobs.KeyRotationJob>();

/* ELECTRONIC SIGNATURE PROVIDER */
var esigProvider = builder.Configuration["ElectronicSignature:Provider"] ?? "Documenso";
builder.Services.AddSingleton<TrustRent.Modules.Leasing.Contracts.Interfaces.ISigningProvider>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    if (esigProvider == "Documenso")
    {
        var documensoLogger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TrustRent.Modules.Leasing.Services.DocumensoSigningProvider>>();
        return new TrustRent.Modules.Leasing.Services.DocumensoSigningProvider(
            config["ElectronicSignature:Documenso:BaseUrl"]!,
            config["ElectronicSignature:Documenso:ApiKey"]!,
            documensoLogger);
    }

    if (esigProvider == "DocuSeal")
    {
        var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
        var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TrustRent.Modules.Leasing.Services.DocuSealSigningProvider>>();
        return new TrustRent.Modules.Leasing.Services.DocuSealSigningProvider(
            config["ElectronicSignature:DocuSeal:BaseUrl"]!,
            config["ElectronicSignature:DocuSeal:ApiKey"]!,
            config.GetValue<bool>("ElectronicSignature:DocuSeal:QesEnabled"),
            httpClientFactory.CreateClient(),
            logger);
    }

    throw new InvalidOperationException($"Unknown signing provider: {esigProvider}");
});
builder.Services.AddScoped<TrustRent.Modules.Leasing.Services.ISigningProviderService, TrustRent.Modules.Leasing.Services.SigningProviderService>();
builder.Services.AddScoped<TrustRent.Modules.Leasing.Services.DocumentSigningPinService>();

/* STRIPE / PAYMENTS */
builder.Services.AddScoped<TrustRent.Modules.Leasing.Contracts.Interfaces.IStripeAccountService, TrustRent.Modules.Leasing.Services.StripeAccountService>();
builder.Services.AddScoped<TrustRent.Modules.Leasing.Contracts.Interfaces.IStripePaymentService, TrustRent.Modules.Leasing.Services.StripePaymentService>();
builder.Services.AddScoped<ILeaseActivationService, CatalogLeaseActivationService>();

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString)));

builder.Services.AddHangfireServer();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SecretKey"]!))
        };
        
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var path = context.HttpContext.Request.Path;
                var accessToken = context.Request.Query["access_token"];

                // Fallback temporário: SignalR pode enviar o token por query-string.
                // Será removido quando o frontend deixar de enviar accessTokenFactory.
                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/api/chathub") || path.StartsWithSegments("/api/notificationhub")))
                {
                    context.Token = accessToken;
                    return Task.CompletedTask;
                }

                // Fonte primária: cookie httpOnly definido por /api/auth/verify-code.
                if (string.IsNullOrEmpty(context.Token) &&
                    context.Request.Cookies.TryGetValue(AuthEndpoints.AuthCookieName, out var cookieToken) &&
                    !string.IsNullOrEmpty(cookieToken))
                {
                    context.Token = cookieToken;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<PlatformSettingsXmlRepository>();
builder.Services.AddDataProtection()
    .SetApplicationName("TrustRent");
builder.Services.AddOptions<KeyManagementOptions>()
    .Configure<PlatformSettingsXmlRepository>((options, repository) =>
    {
        options.XmlRepository = repository;
    });

// === ADMIN MODULE (backoffice) ===
// Adds AdminDbContext, services, second JWT scheme `AdminJwtBearer` reading cookie `trustrent_admin_auth`,
// and a custom IAuthorizationPolicyProvider that emits per-permission policies on demand.
builder.Services.AddAdminModule(builder.Configuration);
builder.Services.AddSingleton<IStagingAccessCookieService, StagingAccessCookieService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.ConfigureHttpJsonOptions(options => {
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

builder.Services.AddCors(options =>{
    static string NormalizeOrigin(string origin) => origin.Trim().TrimEnd('/');

    static bool IsTrustedLocalDevOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
        if (!uri.IsLoopback) return false;

        var isHttp = string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

        return isHttp && uri.Port is >= 5173 and <= 5199;
    }

    static void ApplyOriginsPolicy(Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicyBuilder policy, IEnumerable<string>? configuredOrigins, bool allowLocalDevOrigins)
    {
        var normalizedOrigins = (configuredOrigins ?? Array.Empty<string>())
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(NormalizeOrigin)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (allowLocalDevOrigins)
        {
            policy.SetIsOriginAllowed(origin =>
                normalizedOrigins.Contains(NormalizeOrigin(origin), StringComparer.OrdinalIgnoreCase)
                || IsTrustedLocalDevOrigin(origin));
        }
        else if (normalizedOrigins.Length > 0)
        {
            policy.WithOrigins(normalizedOrigins);
        }
        else
        {
            policy.WithOrigins("http://localhost:5173");
        }

        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    }

    // Origens permitidas via configuração (CorsSettings:AllowedOrigins). Em desenvolvimento,
    // também aceitamos Vite em localhost/127.0.0.1 com portas variáveis para não quebrar
    // quando o frontend e o backoffice sobem em ordem diferente.
    var configuredOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>();
    options.AddPolicy("AllowViteFrontend", policy =>
    {
        ApplyOriginsPolicy(policy, configuredOrigins, builder.Environment.IsDevelopment());
    });

    // Backoffice admin SPA (separate origin, separate cookie domain).
    var adminOrigins = builder.Configuration.GetSection("AdminCors:AllowedOrigins").Get<string[]>();
    options.AddPolicy("AllowBackoffice", policy =>
    {
        ApplyOriginsPolicy(policy, adminOrigins, builder.Environment.IsDevelopment());
    });
});

// Compressão (gzip + brotli) — reduz JSON de reference data ~70-80%
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults
        .MimeTypes.Concat(new[] { "application/json" });
});

// Rate limiting — protect auth endpoints from brute force attacks
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var path = httpContext.Request.Path;
        if (!path.StartsWithSegments("/api/admin") || path.StartsWithSegments("/api/admin/auth"))
        {
            return RateLimitPartition.GetNoLimiter("non-admin-api");
        }

        var key = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: key,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Validação de rendimentos por IA: limita abusos por utilizador autenticado.
    options.AddPolicy("incomeValidation", httpContext =>
    {
        var userKey = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0
            });
    });

    options.AddPolicy("cotenantInvites", httpContext =>
    {
        var userKey = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromDays(1),
                QueueLimit = 0
            });
    });

    options.AddPolicy("guarantorInvites", httpContext =>
    {
        var userKey = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromDays(1),
                QueueLimit = 0
            });
    });

    // Admin login: brute-force defence on /api/admin/auth/login (5 req/min por IP)
    options.AddPolicy("admin-auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Admin API geral (60 req/min por admin autenticado). Also applied by GlobalLimiter above.
    options.AddPolicy("admin-api", httpContext =>
    {
        var key = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: key,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});

// --- OpenTelemetry ---
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("WeKaza", serviceVersion: "1.0.0")
        .AddAttributes(new Dictionary<string, object>
        {
            ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
        }))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSource(Telemetry.Source.Name)
        .SetSampler(new OpenTelemetry.Trace.ParentBasedSampler(new OpenTelemetry.Trace.TraceIdRatioBasedSampler(0.25)))
        .AddOtlpExporter(opt =>
        {
            opt.Endpoint = new Uri(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:4318");
            opt.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter("WeKaza.*")
        .AddOtlpExporter(opt =>
        {
            opt.Endpoint = new Uri(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:4318");
            opt.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
        }));

var app = builder.Build();
var migrateOnly = Array.Exists(args, arg => string.Equals(arg, "--migrate-only", StringComparison.OrdinalIgnoreCase));
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

// Initialize encryption keys from configuration
EncryptionHelperV2.Initialize(builder.Configuration);
IpHashHelper.Initialize(builder.Configuration);

// QuestPDF license must be set globally BEFORE any Hangfire job can use it.
// Without this, QuestPDF calls Environment.Exit(1) and kills the process silently.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

startupLogger.LogInformation(
    "Starting TrustRent API in {Environment}. MigrateOnly={MigrateOnly}. RailwayPort={RailwayPort}. StoragePath={StoragePath}",
    app.Environment.EnvironmentName,
    migrateOnly,
    railwayPort ?? "not-set",
    app.Configuration["Storage:ContractPath"] ?? "./storage/leases");

app.UseForwardedHeaders();
app.UseSerilogRequestLogging();

const string requestIdHeaderName = RequestCorrelationHeaders.RequestId;

app.Use(async (context, next) =>
{
    var upstreamRequestId = context.Request.Headers[requestIdHeaderName].ToString().Trim();
    if (!string.IsNullOrWhiteSpace(upstreamRequestId))
    {
        context.TraceIdentifier = upstreamRequestId;
    }

    context.Response.Headers[requestIdHeaderName] = context.TraceIdentifier;
    await next();
});

// Global exception handler — prevents leaking internal details to clients
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.User?.FindFirst("sub")?.Value
            ?? "anonymous";

        startupLogger.LogError(
            exception,
            "Unhandled exception for {Method} {Path} (RequestId={RequestId}, UserId={UserId})",
            context.Request.Method,
            context.Request.Path,
            context.TraceIdentifier,
            userId);

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        
        var response = app.Environment.IsDevelopment()
            ? new { Error = "Ocorreu um erro interno no servidor.", Code = "internal_error", RequestId = context.TraceIdentifier, Detail = exception?.Message }
            : new { Error = "Ocorreu um erro interno no servidor.", Code = "internal_error", RequestId = context.TraceIdentifier, Detail = (string?)null };
        
        await context.Response.WriteAsJsonAsync(response);
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter() }
    });
}
else
{
    // Em produção: HSTS + redirect HTTPS + headers de segurança.
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Headers de segurança transversais
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";

    // Content Security Policy. Aplica-se sobretudo a respostas HTML servidas pela API
    // (Hangfire dashboard, páginas de erro). Mantemos uma política restritiva.
    // O frontend Vite tem o seu próprio CSP no index.html quando necessário.
    if (!context.Response.Headers.ContainsKey("Content-Security-Policy"))
    {
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: blob: https:; " +
            "font-src 'self' data:; " +
            "connect-src 'self' https: wss:; " +
            "frame-ancestors 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self'; " +
            "object-src 'none';";
    }

    await next();
});

app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (!path.StartsWithSegments(PublicGatewaySurface.ApiPrefix) || path.StartsWithSegments(PublicGatewaySurface.Health))
    {
        await next();
        return;
    }

    var stopwatch = Stopwatch.StartNew();
    context.Response.OnCompleted(() =>
    {
        stopwatch.Stop();

        var statusCode = context.Response.StatusCode;
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            startupLogger.LogError(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms (RequestId={RequestId})",
                context.Request.Method,
                path,
                statusCode,
                stopwatch.ElapsedMilliseconds,
                context.TraceIdentifier);
        }
        else if (statusCode >= StatusCodes.Status400BadRequest || stopwatch.ElapsedMilliseconds >= 1500)
        {
            startupLogger.LogWarning(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms (RequestId={RequestId})",
                context.Request.Method,
                path,
                statusCode,
                stopwatch.ElapsedMilliseconds,
                context.TraceIdentifier);
        }

        return Task.CompletedTask;
    });

    await next();
});

app.UseStaticFiles();
// Apply mutually exclusive CORS policies so admin preflight requests do not get
// short-circuited by the public frontend policy first.
app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api/admin"),
    branch => branch.UseCors("AllowBackoffice"));
app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api/admin"),
    branch => branch.UseCors("AllowViteFrontend"));
app.UseResponseCompression();
app.UseRequestBodySizeLimiter();
app.UseRateLimiter();
app.UseStagingAccessRequestGuard();

app.UseAuthentication();
app.Use(async (context, next) =>
{
    var method = context.Request.Method;
    var isMutatingAdminRequest = context.Request.Path.StartsWithSegments("/api/admin")
        && !context.Request.Path.StartsWithSegments("/api/admin/auth/login")
        && (HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method));

    if (isMutatingAdminRequest)
    {
        var adminAuth = await context.AuthenticateAsync(AdminModuleExtensions.AuthScheme);
        if (adminAuth.Succeeded && adminAuth.Principal is not null)
        {
            context.User = adminAuth.Principal;
        }

        var expected = context.User?.FindFirst("csrf")?.Value;
        var provided = context.Request.Headers["X-CSRF-Token"].ToString();
        if (string.IsNullOrWhiteSpace(expected) || !string.Equals(expected, provided, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = "Token CSRF inválido.", code = "invalid_csrf", requestId = context.TraceIdentifier });
            return;
        }
    }

    await next();
});
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var isAdminRequest = path.StartsWithSegments("/api/admin");
    var isMfaExempt = path.StartsWithSegments("/api/admin/auth/login")
        || path.StartsWithSegments("/api/admin/auth/logout")
        || path.StartsWithSegments("/api/admin/auth/me")
        || path.StartsWithSegments("/api/admin/auth/change-password")
        || path.StartsWithSegments("/api/admin/auth/sessions")
        || path.StartsWithSegments("/api/admin/auth/mfa");

    if (isAdminRequest && !isMfaExempt)
    {
        var adminAuth = await context.AuthenticateAsync(AdminModuleExtensions.AuthScheme);
        if (adminAuth.Succeeded && adminAuth.Principal is not null)
        {
            var sub = adminAuth.Principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? adminAuth.Principal.FindFirst("sub")?.Value;
            if (Guid.TryParse(sub, out var adminId))
            {
                var adminDb = context.RequestServices.GetRequiredService<AdminDbContext>();
                var admin = await adminDb.AdminUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == adminId && x.DeletedAt == null);
                if (admin?.IsSuperAdmin == true && !admin.MfaEnabled)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { error = "MFA obrigatório para super-admin.", code = "mfa_required", requestId = context.TraceIdentifier, mfaSetupRequired = true });
                    return;
                }
            }
        }
    }

    await next();
});
app.UseAuthorization();
app.MapInfrastructureEndpoints();
app.MapAuthEndpoints();
app.MapStagingAccessEndpoints();
app.MapAuthUserEndpoints();
app.MapPropertyEndpoints();
app.MapApplicationEndpoints();
app.MapCoTenantInviteEndpoints();
app.MapGuarantorEndpoints();
app.MapLeaseEndpoints();
app.MapTicketEndpoints();
app.MapStripeEndpoints();
app.MapReviewEndpoints();
app.MapCommunicationsEndpoints();

app.MapReferenceDataEndpoints();

// Backoffice admin endpoints
app.MapAdminAuthEndpoints();
app.MapAdminUsersEndpoints();
app.MapAdminReferenceDataEndpoints();
app.MapAdminSettingsEndpoints();
app.MapAdminStagingAccessEndpoints();
app.MapAdminAuditEndpoints();
app.MapAdminUsersPublicEndpoints();
app.MapAdminPropertiesEndpoints();
app.MapAdminLeasingEndpoints();
app.MapAdminTicketsReviewsEndpoints();
app.MapSupportTicketsEndpoints();
app.MapConsentEndpoints();
app.MapAgentReportsEndpoints();
app.MapAdminCommunicationsEndpoints();
app.MapAdminJobsEndpoints();

app.MapHub<ApplicationChatHub>(PublicGatewaySurface.ChatHub);
app.MapHub<NotificationHub>(PublicGatewaySurface.NotificationHub);

startupLogger.LogInformation("Starting database initialization phase.");
await InitializeDatabasesAsync(app);
startupLogger.LogInformation("Database initialization phase completed successfully.");

if (migrateOnly)
{
    startupLogger.LogInformation("Migrate-only run finished. Exiting without starting HTTP server.");
    return;
}

// Register Hangfire recurring jobs
app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<TrustRent.Modules.Leasing.Jobs.IDailyMaintenanceJob>(
    "daily-maintenance",
    job => job.ExecuteAsync(),
    Cron.Daily(7, 0));  // 7 AM UTC — runs after monthly-rent-collection (6 AM) so rent is collected before leases expire

app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<DataRetentionJob>(
    "data-retention-cleanup",
    job => job.RunCleanupAsync(),
    "0 3 * * *");  // Daily at 3 AM UTC

app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<GuarantorTokenCleanupJob>(
    "guarantor-token-cleanup",
    job => job.RunCleanupAsync(),
    "0 4 * * *");  // Daily at 4 AM UTC

app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<TrustRent.Modules.Leasing.Jobs.IMonthlyRentCollectionJob>(
    "monthly-rent-collection",
    job => job.ExecuteAsync(),
    Cron.Daily(6, 0));  // 6 AM UTC daily — checks day-of-month match

app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static async Task InitializeDatabasesAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();

    var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    var communicationsDb = scope.ServiceProvider.GetRequiredService<CommunicationsDbContext>();
    var leasingDb = scope.ServiceProvider.GetRequiredService<LeasingDbContext>();
    var adminDb = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
    var adminLogger = scope.ServiceProvider
        .GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
        .CreateLogger("AdminBootstrap");
    var initLogger = scope.ServiceProvider
        .GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
        .CreateLogger("DatabaseInitialization");

    initLogger.LogInformation("Applying EF Core migrations for application contexts.");

    // Railway runs the compiled app in pre-deploy; applying migrations here avoids depending on dotnet-ef in the runtime image.
    await identityDb.Database.MigrateAsync();
    await catalogDb.Database.MigrateAsync();
    await communicationsDb.Database.MigrateAsync();
    await leasingDb.Database.MigrateAsync();
    await adminDb.Database.MigrateAsync();

    // Reference and admin seeders are non-destructive and should exist in every environment.
    await ReferenceDataSeeder.SeedAsync(catalogDb);
    await IdentityReferenceDataSeeder.SeedAsync(identityDb);
    await AdminPermissionsSeeder.SeedAsync(adminDb);
    await AdminBootstrapSeeder.SeedAsync(adminDb, app.Configuration, app.Environment, adminLogger);

    var runDemoSeeders = app.Environment.IsDevelopment()
        || app.Configuration.GetValue<bool>("SeedSettings:RunDemoData");

    if (!runDemoSeeders)
    {
        initLogger.LogInformation("Demo seeders disabled for environment {Environment}.", app.Environment.EnvironmentName);
        return;
    }

    initLogger.LogInformation("Running demo seeders for environment {Environment}.", app.Environment.EnvironmentName);
    await IdentitySeeder.SeedAsync(identityDb);
    await CatalogSeeder.SeedAsync(catalogDb);
    await LeasingSeeder.SeedAsync(leasingDb);
    await CommunicationsSeeder.SeedAsync(communicationsDb);
    initLogger.LogInformation("Demo seeders completed successfully.");
}

static void ValidatePostgresConnectionString(string? connectionString)
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Missing ConnectionStrings:PostgresConnection. In Railway, add the backend service variable ConnectionStrings__PostgresConnection and reference the Postgres service variables from the database service.");
    }

    Npgsql.NpgsqlConnectionStringBuilder builder;
    try
    {
        builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
    }
    catch (ArgumentException ex)
    {
        throw new InvalidOperationException(
            "ConnectionStrings:PostgresConnection is invalid. Check the Railway value format and make sure it is a valid Npgsql connection string.",
            ex);
    }

    var missingParts = new List<string>();
    if (string.IsNullOrWhiteSpace(builder.Host)) missingParts.Add("Host");
    if (string.IsNullOrWhiteSpace(builder.Database)) missingParts.Add("Database");
    if (string.IsNullOrWhiteSpace(builder.Username)) missingParts.Add("Username");
    if (string.IsNullOrWhiteSpace(builder.Password)) missingParts.Add("Password");

    if (missingParts.Count == 0)
    {
        return;
    }

    throw new InvalidOperationException(
        $"ConnectionStrings:PostgresConnection is missing required parts: {string.Join(", ", missingParts)}. In Railway, this usually means the Postgres service reference name in the backend variable does not match the database service name exactly, or the variable was added in the wrong service.");
}
