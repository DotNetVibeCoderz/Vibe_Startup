using System.Diagnostics;
using System.Security.Claims;
using AppBender.Core.Services;

namespace AppBender.Web.Middleware;

/// <summary>
/// Authenticates API requests carrying a valid X-Api-Key header as a synthetic
/// "api-client" Developer user (for machine-to-machine Data API access).
/// Keys are configured under "Api:Keys": [{ "key": "...", "tenantId": "default", "name": "ci" }].
/// </summary>
public class ApiKeyMiddleware(RequestDelegate next, IConfiguration config)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true &&
            context.Request.Headers.TryGetValue("X-Api-Key", out var provided))
        {
            var keys = config.GetSection("Api:Keys").GetChildren();
            foreach (var entry in keys)
            {
                if (string.Equals(entry["key"], provided.ToString(), StringComparison.Ordinal))
                {
                    var identity = new ClaimsIdentity("ApiKey");
                    identity.AddClaim(new Claim(ClaimTypes.Name, entry["name"] ?? "api-client"));
                    identity.AddClaim(new Claim(ClaimTypes.Role, "Developer"));
                    identity.AddClaim(new Claim("org", entry["tenantId"] ?? TenantContext.DefaultTenant));
                    context.User = new ClaimsPrincipal(identity);
                    break;
                }
            }
        }
        await next(context);
    }
}

/// <summary>Populates the scoped ITenantContext from the authenticated user's claims.</summary>
public class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenant)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            tenant.Set(
                user.FindFirstValue("org") ?? TenantContext.DefaultTenant,
                user.FindFirstValue(ClaimTypes.NameIdentifier),
                user.FindFirstValue("displayname") ?? user.Identity.Name ?? "user",
                user.FindAll(ClaimTypes.Role).Select(c => c.Value));
        }
        await next(context);
    }
}

/// <summary>Tracks page views, API calls and response times as usage metrics.</summary>
public class UsageTrackingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IUsageService usage)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            var path = context.Request.Path.Value ?? "/";
            // Skip static assets and framework endpoints to keep metrics meaningful.
            if (!path.StartsWith("/_") && !path.Contains('.') && context.Response.StatusCode < 400)
            {
                var type = path.StartsWith("/api") ? "api_request" : "page_view";
                usage.TrackFireAndForget(type, 1, path);
                usage.TrackFireAndForget("response_ms", sw.ElapsedMilliseconds, path);
            }
        }
    }
}
