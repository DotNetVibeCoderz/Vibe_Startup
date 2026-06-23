namespace EventSphere.Api;

/// <summary>
/// Middleware autentikasi API Key.
/// Validasi header X-Api-Key terhadap daftar keys di appsettings.json.
/// </summary>
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _config;
    private static readonly HashSet<string> ApiPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/", "/swagger"
    };

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _config = config;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Hanya periksa path API dan Swagger
        var path = context.Request.Path.Value ?? "";
        var requiresAuth = path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
                        || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);

        if (!requiresAuth)
        {
            await _next(context);
            return;
        }

        // Swagger UI dan OpenAPI JSON — allow tanpa API key
        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var headerName = _config.GetValue<string>("ApiKey:HeaderName") ?? "X-Api-Key";
        var validKeys = _config.GetSection("ApiKey:Keys").Get<string[]>() ?? Array.Empty<string>();

        if (!context.Request.Headers.TryGetValue(headerName, out var extractedKey))
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"error":"Unauthorized","message":"Missing X-Api-Key header. Please provide a valid API key."}""");
            return;
        }

        if (!validKeys.Contains(extractedKey.ToString()))
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"error":"Forbidden","message":"Invalid API key. Please check your X-Api-Key header."}""");
            return;
        }

        await _next(context);
    }
}
