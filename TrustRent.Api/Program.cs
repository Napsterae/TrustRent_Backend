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

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 104857600; // 100 MB
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104857600; // 100 MB
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

/* LEASING */
builder.Services.AddScoped<TrustRent.Modules.Leasing.Contracts.Interfaces.ILeaseService, TrustRent.Modules.Leasing.Services.LeaseService>();
builder.Services.AddScoped<TrustRent.Modules.Leasing.Contracts.Interfaces.IContractGenerationService, TrustRent.Modules.Leasing.Services.ContractGenerationService>();
builder.Services.AddScoped<TrustRent.Modules.Leasing.Contracts.Interfaces.IDigitalSignatureService, TrustRent.Modules.Leasing.Services.DigitalSignatureService>();
builder.Services.AddScoped<TrustRent.Modules.Leasing.Contracts.Interfaces.ISignedPdfVerificationService, TrustRent.Modules.Leasing.Services.SignedPdfVerificationService>();
builder.Services.AddScoped<TrustRent.Modules.Leasing.Repositories.ILeasingUnitOfWork, TrustRent.Modules.Leasing.Repositories.LeasingUnitOfWork>();
builder.Services.AddScoped<TrustRent.Modules.Leasing.Contracts.Interfaces.ITicketService, TrustRent.Modules.Leasing.Services.TicketService>();

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
                var accessToken = context.Request.Query["access_token"];

                // Se o pedido for para o nosso hub (SignalR envia o token por query string)
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/api/chathub") || path.StartsWithSegments("/api/notificationhub")))
                {
                    // Lê o token do query string
                    context.Token = accessToken;
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

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowViteFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173") 
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHangfireDashboard();
}

app.UseStaticFiles();
app.UseCors("AllowViteFrontend");

app.UseAuthentication();
app.UseAuthorization();
app.MapAuthEndpoints();
app.MapAuthUserEndpoints();
app.MapPropertyEndpoints();
app.MapApplicationEndpoints();
app.MapLeaseEndpoints();
app.MapTicketEndpoints();
app.MapStripeEndpoints();
app.MapCommunicationsEndpoints();

app.MapHub<ApplicationChatHub>("/api/chathub");
app.MapHub<NotificationHub>("/api/notificationhub");

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

app.Run();
