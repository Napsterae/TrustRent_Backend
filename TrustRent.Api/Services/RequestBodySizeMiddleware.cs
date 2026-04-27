using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace TrustRent.Api.Services;

public class RequestBodySizeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RequestBodySizeOptions _options;

    public RequestBodySizeMiddleware(RequestDelegate next, IOptions<RequestBodySizeOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;
        var method = context.Request.Method;

        if (method != "POST" && method != "PUT" && method != "PATCH")
        {
            await _next(context);
            return;
        }

        var match = path is null
            ? null
            : _options.Rules.FirstOrDefault(r =>
            {
                if (string.IsNullOrEmpty(r.PathSegment)) return false;
                var needle = "/" + r.PathSegment.Trim('/');
                // Match on a '/' boundary so "/avatar" doesn't match "/avatarfoo".
                return path.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0 &&
                       (path.EndsWith(needle, StringComparison.OrdinalIgnoreCase) ||
                        path.Contains(needle + "/", StringComparison.OrdinalIgnoreCase) ||
                        path.Contains(needle + "?", StringComparison.OrdinalIgnoreCase));
            });

        if (match is { Limit: > 0 })
        {
            var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (feature != null)
            {
                feature.MaxRequestBodySize = match.Limit;
            }
        }

        await _next(context);
    }
}

public class RequestBodySizeOptions
{
    public List<RequestBodySizeRule> Rules { get; set; } = new();
}

public class RequestBodySizeRule
{
    public string PathSegment { get; set; } = string.Empty;
    public long Limit { get; set; }
}

public static class RequestBodySizeMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestBodySizeLimiter(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestBodySizeMiddleware>();
    }
}
