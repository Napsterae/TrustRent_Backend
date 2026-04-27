using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Services;
using TrustRent.Api.Services;
using TrustRent.Modules.Identity.Seeds;
using TrustRent.Modules.Catalog.Seeds;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Seeds;
using TrustRent.Modules.Communications.Seeds;
using TrustRent.Shared.Security;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddHttpClient<IGeminiDocumentService, GeminiDocumentService>();
builder.Services.AddScoped<IImageService, CloudinaryImageService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationService, TrustRent.Modules.Communications.Services.NotificationService>();
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

                // Fonte primária: cookie httpOnly definido por /api/auth/login e /api/auth/register.
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
    // Origens permitidas via configuração (CorsSettings:AllowedOrigins) com fallback para dev local.
    var configuredOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>();
    var allowedOrigins = (configuredOrigins is { Length: > 0 })
        ? configuredOrigins
        : new[] { "http://localhost:5173" };

    options.AddPolicy("AllowViteFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
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
});

var app = builder.Build();

// Initialize encryption keys from configuration
EncryptionHelper.Initialize(builder.Configuration);

// QuestPDF license must be set globally BEFORE any Hangfire job can use it.
// Without this, QuestPDF calls Environment.Exit(1) and kills the process silently.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// Global exception handler — prevents leaking internal details to clients
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        
        var response = app.Environment.IsDevelopment()
            ? new { Error = "Ocorreu um erro interno no servidor.", Detail = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error?.Message }
            : new { Error = "Ocorreu um erro interno no servidor.", Detail = (string?)null };
        
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

app.UseStaticFiles();
app.UseCors("AllowViteFrontend");
app.UseResponseCompression();
app.UseRequestBodySizeLimiter();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.MapAuthEndpoints();
app.MapAuthUserEndpoints();
app.MapPropertyEndpoints();
app.MapApplicationEndpoints();
app.MapLeaseEndpoints();
app.MapTicketEndpoints();
app.MapStripeEndpoints();
app.MapReviewEndpoints();
app.MapCommunicationsEndpoints();

app.MapReferenceDataEndpoints();

app.MapHub<ApplicationChatHub>("/api/chathub");
app.MapHub<NotificationHub>("/api/notificationhub");

// Reference data seeders — correm em TODOS os ambientes (produzem/actualizam tabelas lookup
// sem nunca apagar ou sobrescrever registos existentes, para preservar edições de back-office)
using (var refScope = app.Services.CreateScope())
{
    var catalogDbForRef = refScope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    var identityDbForRef = refScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await TrustRent.Modules.Catalog.Seeds.ReferenceDataSeeder.SeedAsync(catalogDbForRef);
    await TrustRent.Modules.Identity.Seeds.IdentityReferenceDataSeeder.SeedAsync(identityDbForRef);
}

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var identityDb = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    var catalogDb = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    var communicationsDb = scope.ServiceProvider.GetRequiredService<CommunicationsDbContext>();
    var leasingDb = scope.ServiceProvider.GetRequiredService<LeasingDbContext>();

    await IdentitySeeder.SeedAsync(identityDb);
    await CatalogSeeder.SeedAsync(catalogDb);
    await LeasingSeeder.SeedAsync(leasingDb);
    await CommunicationsSeeder.SeedAsync(communicationsDb);
}

// Register Hangfire recurring jobs
RecurringJob.AddOrUpdate<TrustRent.Modules.Leasing.Jobs.IDailyMaintenanceJob>(
    "daily-maintenance",
    job => job.ExecuteAsync(),
    Cron.Daily(2, 0));

app.Run();
