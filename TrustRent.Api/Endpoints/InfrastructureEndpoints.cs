using System.Reflection;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Communications.Contracts.Database;
using TrustRent.Modules.Identity.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Shared.Infrastructure;

namespace TrustRent.Api.Endpoints;

public static class InfrastructureEndpoints
{
    private static readonly DateTimeOffset StartedAt = DateTimeOffset.UtcNow;

    public static void MapInfrastructureEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(PublicGatewaySurface.HealthLive, (IWebHostEnvironment env) =>
        {
            return Results.Ok(new
            {
                status = "healthy",
                kind = "liveness",
                environment = env.EnvironmentName,
                version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown",
                startedAt = StartedAt,
                uptimeSeconds = (long)(DateTimeOffset.UtcNow - StartedAt).TotalSeconds,
                timestamp = DateTimeOffset.UtcNow,
            });
        });

        app.MapGet(PublicGatewaySurface.Health, RunReadinessCheckAsync);
        app.MapGet(PublicGatewaySurface.HealthReady, RunReadinessCheckAsync);
    }

    private static async Task<IResult> RunReadinessCheckAsync(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IdentityDbContext identityDb,
        CatalogDbContext catalogDb,
        CommunicationsDbContext communicationsDb,
        LeasingDbContext leasingDb,
        AdminDbContext adminDb,
        CancellationToken cancellationToken)
    {
        var dbChecks = await Task.WhenAll(
            CheckDbContextAsync("identity", identityDb, cancellationToken),
            CheckDbContextAsync("catalog", catalogDb, cancellationToken),
            CheckDbContextAsync("communications", communicationsDb, cancellationToken),
            CheckDbContextAsync("leasing", leasingDb, cancellationToken),
            CheckDbContextAsync("admin", adminDb, cancellationToken));

        var configCheck = CheckConfiguration(configuration);
        var storageCheck = CheckStorage(configuration, environment);

        var checks = new[] { configCheck, storageCheck }
            .Concat(dbChecks)
            .ToArray();

        var overallStatus = checks.Any(check => check.Status == "unhealthy")
            ? "unhealthy"
            : checks.Any(check => check.Status == "degraded")
                ? "degraded"
                : "healthy";

        var response = new
        {
            status = overallStatus,
            kind = "readiness",
            environment = environment.EnvironmentName,
            timestamp = DateTimeOffset.UtcNow,
            startedAt = StartedAt,
            uptimeSeconds = (long)(DateTimeOffset.UtcNow - StartedAt).TotalSeconds,
            capabilities = new
            {
                emailMode = "stub",
                cmdMode = configuration.GetValue<bool?>("DigitalSignature:CMD:MockEnabled") != false ? "mock" : "unconfigured-real",
            },
            checks,
        };

        return overallStatus == "unhealthy"
            ? Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable)
            : Results.Ok(response);
    }

    private static async Task<HealthCheckResult> CheckDbContextAsync(string name, DbContext dbContext, CancellationToken cancellationToken)
    {
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return new HealthCheckResult(name, "unhealthy", "Database connection failed.");
            }

            var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
            if (pendingMigrations.Length > 0)
            {
                return new HealthCheckResult(
                    name,
                    "unhealthy",
                    "Pending migrations detected.",
                    new { pendingMigrations });
            }

            return new HealthCheckResult(name, "healthy", "Database reachable and up to date.");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(name, "unhealthy", ex.Message);
        }
    }

    private static HealthCheckResult CheckConfiguration(IConfiguration configuration)
    {
        var requiredKeys = new[]
        {
            "ConnectionStrings:PostgresConnection",
            "JwtSettings:SecretKey",
            "AdminJwtSettings:SecretKey",
            "Encryption:Key",
            "Encryption:IV",
        };

        var recommendedKeys = new[]
        {
            "Stripe:SecretKey",
            "Stripe:WebhookSecret",
            "CloudinarySettings:CloudName",
            "CloudinarySettings:ApiKey",
            "CloudinarySettings:ApiSecret",
            "Gemini:ApiKey",
        };

        var missingRequired = requiredKeys.Where(key => string.IsNullOrWhiteSpace(configuration[key])).ToArray();
        if (missingRequired.Length > 0)
        {
            return new HealthCheckResult(
                "configuration",
                "unhealthy",
                "Required configuration values are missing.",
                new { missingRequired, missingRecommended = Array.Empty<string>() });
        }

        var missingRecommended = recommendedKeys.Where(key => string.IsNullOrWhiteSpace(configuration[key])).ToArray();
        if (missingRecommended.Length > 0)
        {
            return new HealthCheckResult(
                "configuration",
                "degraded",
                "Recommended integration settings are missing.",
                new { missingRequired = Array.Empty<string>(), missingRecommended });
        }

        return new HealthCheckResult(
            "configuration",
            "healthy",
            "Required staging configuration is present.",
            new { missingRequired = Array.Empty<string>(), missingRecommended = Array.Empty<string>() });
    }

    private static HealthCheckResult CheckStorage(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredPath = configuration["Storage:ContractPath"];
        var usingFallbackPath = string.IsNullOrWhiteSpace(configuredPath);
        var effectivePath = configuredPath ?? "./storage/leases";
        var fullPath = Path.GetFullPath(effectivePath);

        try
        {
            Directory.CreateDirectory(fullPath);

            var tempFile = Path.Combine(fullPath, $".healthcheck-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(tempFile, DateTimeOffset.UtcNow.ToString("O"));
            File.Delete(tempFile);

            if (!environment.IsDevelopment() && usingFallbackPath)
            {
                return new HealthCheckResult(
                    "storage",
                    "degraded",
                    "Storage path is writable but still using the local fallback path. Configure Storage__ContractPath for Railway volume storage.",
                    new { path = fullPath, persistenceMode = "local-fallback" });
            }

            return new HealthCheckResult(
                "storage",
                "healthy",
                "Storage path is writable.",
                new { path = fullPath, persistenceMode = usingFallbackPath ? "local-default" : "configured" });
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(
                "storage",
                "unhealthy",
                ex.Message,
                new { path = fullPath });
        }
    }

    private sealed record HealthCheckResult(string Name, string Status, string Message, object? Details = null);
}