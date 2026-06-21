using System.Security.Cryptography;

namespace RentalBoil.Api;

/// <summary>
/// Middleware untuk autentikasi API Key pada REST API endpoints.
/// API Key dikirim via header X-Api-Key atau query parameter api_key.
/// </summary>
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private const string ApiKeyHeaderName = "X-Api-Key";
    private const string ApiKeyQueryName = "api_key";

    private static readonly string DefaultApiKey = "rntl-2025-secure-api-key-change-in-production";

    public ApiKeyMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IConfiguration config)
    {
        // Skip auth for Swagger UI and non-API paths
        if (IsSwaggerPath(context.Request.Path) || !context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        // Extract API key from header or query
        var apiKey = context.Request.Headers[ApiKeyHeaderName].FirstOrDefault()
                  ?? context.Request.Query[ApiKeyQueryName].FirstOrDefault();

        var configuredKey = config.GetValue<string>("ApiSettings:ApiKey") ?? DefaultApiKey;

        if (string.IsNullOrWhiteSpace(apiKey) || !ConstantTimeEquals(apiKey, configuredKey))
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"error":"Unauthorized","message":"Invalid or missing API Key. Use header 'X-Api-Key' or query 'api_key'."}""");
            return;
        }

        await _next(context);
    }

    private static bool IsSwaggerPath(string path)
        => path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Constant-time string comparison to prevent timing attacks
    /// </summary>
    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        int result = 0;
        for (int i = 0; i < a.Length; i++) result |= a[i] ^ b[i];
        return result == 0;
    }

    /// <summary>
    /// Generate a cryptographically secure random API key
    /// </summary>
    public static string GenerateApiKey()
    {
        return "rntl-" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .Replace("+", "").Replace("/", "").Replace("=", "")[..32];
    }
}
