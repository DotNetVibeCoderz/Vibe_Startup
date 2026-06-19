using Comblang.Services.Auth;

namespace Comblang.API;

/// <summary>
/// API Key authentication filter for Minimal API endpoints.
/// </summary>
public static class ApiKeyAuth
{
    public static void AddApiKeyAuth(this RouteHandlerBuilder builder)
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var authService = context.HttpContext.RequestServices.GetRequiredService<AuthService>();
            var apiKey = context.HttpContext.Request.Headers["X-Api-Key"].FirstOrDefault();

            if (!authService.ValidateApiKey(apiKey))
            {
                return Results.Json(new { error = "Invalid or missing API key" }, statusCode: 401);
            }

            return await next(context);
        });
    }
}
