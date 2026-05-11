using System.Diagnostics;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using TrustRent.Shared.Infrastructure;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

var builder = WebApplication.CreateBuilder(args);

var railwayPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(railwayPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{railwayPort}");
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

const long defaultMaxRequestBodySize = 100 * 1024 * 1024;
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = defaultMaxRequestBodySize;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = defaultMaxRequestBodySize;
});

var backendBaseUrl = ResolveBackendBaseUrl(builder.Configuration);
var backendBaseUri = new Uri(EnsureTrailingSlash(backendBaseUrl), UriKind.Absolute);
var allowedPublicHosts = builder.Configuration.GetSection("Gateway:PublicHosts")
    .Get<string[]>()?
    .Where(host => !string.IsNullOrWhiteSpace(host))
    .Select(NormalizeHost)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray()
    ?? Array.Empty<string>();

builder.Services.AddHttpClient("backend-health", client =>
{
    client.BaseAddress = backendBaseUri;
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var path = httpContext.Request.Path;

        if (path.StartsWithSegments(PublicGatewaySurface.Health))
        {
            return RateLimitPartition.GetNoLimiter("health");
        }

        var clientKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (path.StartsWithSegments(PublicGatewaySurface.StripeWebhook))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"webhook:{clientKey}",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
        }

        if (path.StartsWithSegments("/api/auth/login")
            || path.StartsWithSegments("/api/auth/register"))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"auth:{clientKey}",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
        }

        if (path.StartsWithSegments(PublicGatewaySurface.ApiPrefix))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"api:{clientKey}",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 240,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
        }

        return RateLimitPartition.GetNoLimiter("other");
    });
});

var routes = BuildPublicRoutes();

var clusters = new[]
{
    new ClusterConfig
    {
        ClusterId = "backend",
        Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["primary"] = new() { Address = backendBaseUri.ToString() },
        },
        HttpRequest = new ForwarderRequestConfig
        {
            ActivityTimeout = TimeSpan.FromMinutes(10),
        },
    },
};

builder.Services.AddReverseProxy().LoadFromMemory(routes, clusters);

var app = builder.Build();
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("GatewayStartup");

startupLogger.LogInformation(
    "Starting TrustRent Gateway. BackendBaseUrl={BackendBaseUrl}. RailwayPort={RailwayPort}. AllowedPublicHosts={AllowedPublicHosts}",
    backendBaseUri,
    railwayPort ?? "not-set",
    allowedPublicHosts.Length == 0 ? "not-configured" : string.Join(",", allowedPublicHosts));

app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    if (!app.Environment.IsDevelopment() && allowedPublicHosts.Length > 0)
    {
        var requestHost = NormalizeHost(context.Request.Host.Host);
        if (!allowedPublicHosts.Contains(requestHost, StringComparer.OrdinalIgnoreCase))
        {
            startupLogger.LogWarning(
                "Rejected gateway request for unexpected host {Host} (Path={Path})",
                context.Request.Host.Host,
                context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Route not exposed by TrustRent.Gateway.",
                code = "route_not_exposed",
            });
            return;
        }
    }

    await next();
});

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;

        startupLogger.LogError(
            exception,
            "Unhandled gateway exception for {Method} {Path} (RequestId={RequestId})",
            context.Request.Method,
            context.Request.Path,
            context.TraceIdentifier);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Gateway request failed.",
            code = "gateway_error",
            requestId = context.TraceIdentifier,
        });
    });
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    var upstreamRequestId = context.Request.Headers[RequestCorrelationHeaders.RequestId].ToString().Trim();
    if (!string.IsNullOrWhiteSpace(upstreamRequestId))
    {
        context.TraceIdentifier = upstreamRequestId;
    }

    context.Request.Headers[RequestCorrelationHeaders.RequestId] = context.TraceIdentifier;
    context.Request.Headers["X-TrustRent-Gateway"] = "public-api";
    context.Response.Headers[RequestCorrelationHeaders.RequestId] = context.TraceIdentifier;

    await next();
});

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";

    if (!context.Response.Headers.ContainsKey("Content-Security-Policy"))
    {
        headers["Content-Security-Policy"] =
            "default-src 'none'; " +
            "connect-src 'self' https: wss:; " +
            "img-src 'self' data: https:; " +
            "frame-ancestors 'none'; " +
            "base-uri 'none'; " +
            "form-action 'self';";
    }

    await next();
});

app.Use(async (context, next) =>
{
    var path = context.Request.Path;

    if (path.StartsWithSegments("/api/admin")
        || path.StartsWithSegments("/hangfire")
        || path.StartsWithSegments("/swagger"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Route not exposed by TrustRent.Gateway.",
            code = "route_not_exposed",
        });
        return;
    }

    if (!path.StartsWithSegments(PublicGatewaySurface.ApiPrefix))
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
                "Gateway HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms (RequestId={RequestId})",
                context.Request.Method,
                path,
                statusCode,
                stopwatch.ElapsedMilliseconds,
                context.TraceIdentifier);
        }
        else if (statusCode >= StatusCodes.Status400BadRequest || stopwatch.ElapsedMilliseconds >= 1500)
        {
            startupLogger.LogWarning(
                "Gateway HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms (RequestId={RequestId})",
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

app.UseWebSockets();
app.UseRateLimiter();

app.MapGet(PublicGatewaySurface.HealthLive, (IWebHostEnvironment env) =>
{
    return Results.Ok(new
    {
        status = "healthy",
        kind = "gateway-liveness",
        environment = env.EnvironmentName,
        backendBaseUrl = backendBaseUri.ToString(),
        timestamp = DateTimeOffset.UtcNow,
    });
});

app.MapGet(PublicGatewaySurface.HealthReady, async (IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await ForwardBackendHealthAsync(httpClientFactory, startupLogger, PublicGatewaySurface.HealthReady, cancellationToken);
});

app.MapGet(PublicGatewaySurface.Health, async (IHttpClientFactory httpClientFactory, CancellationToken cancellationToken) =>
{
    return await ForwardBackendHealthAsync(httpClientFactory, startupLogger, PublicGatewaySurface.Health, cancellationToken);
});

app.MapReverseProxy();

app.MapFallback(() => Results.NotFound(new
{
    error = "Route not exposed by TrustRent.Gateway.",
    code = "route_not_exposed",
}));

app.Run();

static string ResolveBackendBaseUrl(IConfiguration configuration)
{
    var baseUrl = Environment.GetEnvironmentVariable("BACKEND_INTERNAL_URL")
        ?? configuration["Gateway:BackendBaseUrl"];

    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        throw new InvalidOperationException(
            "Missing backend destination. Configure BACKEND_INTERNAL_URL or Gateway:BackendBaseUrl for TrustRent.Gateway.");
    }

    if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsed)
        || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
    {
        throw new InvalidOperationException(
            "TrustRent.Gateway backend destination must be an absolute http or https URL.");
    }

    return parsed.ToString().TrimEnd('/');
}

static string EnsureTrailingSlash(string url) => url.EndsWith('/') ? url : $"{url}/";

static string NormalizeHost(string host) => host.Trim().TrimEnd('.').ToLowerInvariant();

static RouteConfig[] BuildPublicRoutes()
{
    var routeConfigs = new List<RouteConfig>();

    AddPrefixRoutes(routeConfigs, "auth", "/api/auth");
    AddPrefixRoutes(routeConfigs, "user", "/api/user");
    AddPrefixRoutes(routeConfigs, "properties", "/api/properties");
    AddPrefixRoutes(routeConfigs, "applications", "/api/applications");
    AddPrefixRoutes(routeConfigs, "me", "/api/me");
    AddPrefixRoutes(routeConfigs, "cotenant-invites", "/api/cotenant-invites");
    AddPrefixRoutes(routeConfigs, "guarantors", "/api/guarantors");
    AddPrefixRoutes(routeConfigs, "guarantor-guest", "/api/guarantor-guest");
    AddPrefixRoutes(routeConfigs, "leases", "/api/leases");
    AddPrefixRoutes(routeConfigs, "tickets", "/api/tickets");
    AddPrefixRoutes(routeConfigs, "reference", "/api/reference");
    AddPrefixRoutes(routeConfigs, "reviews", "/api/reviews");
    AddPrefixRoutes(routeConfigs, "notifications", "/api/notifications");
    AddPrefixRoutes(routeConfigs, "stripe", "/api/stripe");
    AddPrefixRoutes(routeConfigs, "support", "/api/support");

    routeConfigs.Add(CreateExactRoute("amenities", "/api/amenities"));
    routeConfigs.Add(CreateExactRoute("chat-hub", PublicGatewaySurface.ChatHub));
    routeConfigs.Add(CreateExactRoute("notification-hub", PublicGatewaySurface.NotificationHub));

    return routeConfigs.ToArray();
}

static void AddPrefixRoutes(ICollection<RouteConfig> routeConfigs, string routeId, string prefix)
{
    routeConfigs.Add(CreateExactRoute($"{routeId}-root", prefix));
    routeConfigs.Add(CreatePrefixRoute($"{routeId}-children", prefix));
}

static RouteConfig CreatePrefixRoute(string routeId, string prefix)
{
    return new RouteConfig
    {
        RouteId = routeId,
        ClusterId = "backend",
        Match = new RouteMatch { Path = $"{prefix}/{{**catchall}}" },
    };
}

static RouteConfig CreateExactRoute(string routeId, string path)
{
    return new RouteConfig
    {
        RouteId = routeId,
        ClusterId = "backend",
        Match = new RouteMatch { Path = path },
    };
}

static async Task<IResult> ForwardBackendHealthAsync(
    IHttpClientFactory httpClientFactory,
    ILogger logger,
    string path,
    CancellationToken cancellationToken)
{
    try
    {
        var client = httpClientFactory.CreateClient("backend-health");
        using var request = new HttpRequestMessage(HttpMethod.Get, path.TrimStart('/'));
        using var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
        return Results.Content(payload, contentType: contentType, statusCode: (int)response.StatusCode);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Gateway readiness check failed when querying backend route {Path}", path);
        return Results.Json(new
        {
            status = "unhealthy",
            kind = "gateway-readiness",
            message = "Gateway could not reach the backend health endpoint.",
            path,
            timestamp = DateTimeOffset.UtcNow,
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}