using TrustRent.Modules.Admin.Contracts.DTOs;
using TrustRent.Modules.Admin.Contracts.Interfaces;

namespace TrustRent.Api.Services;

public sealed class StagingAccessRequestGuardMiddleware
{
    public const string FrontendGatewayHeaderName = "X-TrustRent-Frontend-Gateway";
    public const string FrontendGatewayHeaderValue = "web";

    private readonly RequestDelegate _next;

    public StagingAccessRequestGuardMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IWebHostEnvironment env, IStagingAccessCookieService cookieService)
    {
        if (!env.IsStaging() || !ShouldGuard(context.Request))
        {
            await _next(context);
            return;
        }

        var session = cookieService.ReadSession(context);
        if (session is null)
        {
            cookieService.ClearCookie(context);
            await WriteDeniedResponseAsync(context);
            return;
        }

        var stagingAccessService = context.RequestServices.GetRequiredService<IStagingAccessService>();
        var user = await stagingAccessService.GetActiveUserAsync(session.Username, context.RequestAborted);
        if (user is null)
        {
            cookieService.ClearCookie(context);
            await WriteDeniedResponseAsync(context);
            return;
        }

        context.Items[nameof(StagingAccessUserSessionDto)] = user;
        await _next(context);
    }

    private static bool ShouldGuard(HttpRequest request)
    {
        if (HttpMethods.IsOptions(request.Method)) return false;
        if (!request.Path.StartsWithSegments("/api")) return false;
        if (!request.Headers.TryGetValue(FrontendGatewayHeaderName, out var gatewayValues)) return false;
        if (!string.Equals(gatewayValues.ToString(), FrontendGatewayHeaderValue, StringComparison.OrdinalIgnoreCase)) return false;
        if (request.Path.StartsWithSegments("/api/staging-access")) return false;
        if (request.Path.StartsWithSegments("/api/stripe/webhook")) return false;
        return true;
    }

    private static async Task WriteDeniedResponseAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Acesso staging requerido.",
            code = "staging_access_required",
            requestId = context.TraceIdentifier,
        });
    }
}

public static class StagingAccessRequestGuardMiddlewareExtensions
{
    public static IApplicationBuilder UseStagingAccessRequestGuard(this IApplicationBuilder app)
    {
        return app.UseMiddleware<StagingAccessRequestGuardMiddleware>();
    }
}