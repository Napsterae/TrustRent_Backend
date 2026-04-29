using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TrustRent.Modules.Admin.Authorization;
using TrustRent.Modules.Admin.Contracts;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Contracts.Interfaces;
using TrustRent.Modules.Admin.Services;

namespace TrustRent.Modules.Admin;

public static class AdminModuleExtensions
{
    public const string AuthScheme = "AdminJwtBearer";
    public const string AdminCookieName = "trustrent_admin_auth";
    public const string AdminPolicy = "AdminOnly";

    public static IServiceCollection AddAdminModule(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("PostgresConnection");

        services.AddDbContext<AdminDbContext>(options => options.UseNpgsql(connectionString));
        services.AddMemoryCache();

        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IPermissionCatalogService, PermissionCatalogService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IAdminAuthService, AdminAuthService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IRoleService, RoleService>();

        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, AdminSessionAuthorizationHandler>();

        services.AddAuthentication()
            .AddJwtBearer(AuthScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = config["AdminJwtSettings:Issuer"],
                    ValidAudience = config["AdminJwtSettings:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["AdminJwtSettings:SecretKey"]!))
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        // Read ONLY admin cookie. Do not fall back to public auth cookie.
                        if (string.IsNullOrEmpty(ctx.Token) &&
                            ctx.Request.Cookies.TryGetValue(AdminCookieName, out var t) &&
                            !string.IsNullOrEmpty(t))
                        {
                            ctx.Token = t;
                        }
                        return Task.CompletedTask;
                    },
                    OnChallenge = ctx =>
                    {
                        // Always return JSON 401, never HTML redirect.
                        ctx.HandleResponse();
                        ctx.Response.StatusCode = 401;
                        ctx.Response.ContentType = "application/json";
                        return ctx.Response.WriteAsync("{\"error\":\"Sessão administrativa requerida.\"}");
                    }
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicy, policy =>
            {
                policy.AuthenticationSchemes = new[] { AuthScheme };
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new AdminSessionRequirement());
            });

        // Pre-register named per-permission policies for every code in the catalog.
        services.AddSingleton<IAuthorizationPolicyProvider, AdminPermissionPolicyProvider>();

        return services;
    }
}

internal class AdminPermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;
    public AdminPermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(AdminAuthorizationExtensions.AdminPolicyPrefix, StringComparison.Ordinal))
        {
            var code = policyName[AdminAuthorizationExtensions.AdminPolicyPrefix.Length..];
            var policy = new AuthorizationPolicyBuilder(AdminModuleExtensions.AuthScheme)
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(code))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
        return _fallback.GetPolicyAsync(policyName);
    }
}

