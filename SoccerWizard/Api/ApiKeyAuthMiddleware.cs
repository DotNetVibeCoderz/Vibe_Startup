using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace SoccerWizard.Api;

/// <summary>
/// Middleware autentikasi ApiKey untuk REST API endpoints.
/// Request harus menyertakan header: X-Api-Key atau query param ?api_key=
/// </summary>
public class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private const string ApiKeyHeaderName = "X-Api-Key";
    private const string ApiKeyQueryParam = "api_key";

    public ApiKeyAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Hanya proteksi endpoint /api/*
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Swagger & OpenAPI docs tidak perlu auth
        if (context.Request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase) ||
            context.Request.Path.StartsWithSegments("/_framework", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Cek API key
        if (!TryGetApiKey(context, out var apiKey) || !ValidateApiKey(context, apiKey))
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"error":"Unauthorized","message":"Valid X-Api-Key header or ?api_key= query parameter required"}""");
            return;
        }

        await _next(context);
    }

    private static bool TryGetApiKey(HttpContext context, out string apiKey)
    {
        // 1. Cek header
        if (context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var headerValues))
        {
            apiKey = headerValues.FirstOrDefault() ?? "";
            if (!string.IsNullOrEmpty(apiKey)) return true;
        }

        // 2. Cek query string
        if (context.Request.Query.TryGetValue(ApiKeyQueryParam, out var queryValues))
        {
            apiKey = queryValues.FirstOrDefault() ?? "";
            if (!string.IsNullOrEmpty(apiKey)) return true;
        }

        apiKey = "";
        return false;
    }

    private static bool ValidateApiKey(HttpContext context, string apiKey)
    {
        var config = context.RequestServices.GetRequiredService<IConfiguration>();
        var validKeys = config.GetSection("ApiKeys").Get<string[]>() ?? new[] { "sw-dev-key-2024" };
        return validKeys.Contains(apiKey);
    }
}

/// <summary>
/// Extension method untuk registrasi middleware.
/// </summary>
public static class ApiKeyAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyAuthMiddleware>();
    }
}
