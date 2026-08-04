using System.Text.Json;
using FastRide.Api.Services;
using FastRide.Data;
using FastRide.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace FastRide.Api.Security;

/// <summary>
/// Rejects tokens issued before the user last logged out or changed their password.
///
/// A JWT is valid until it expires, so without this a "logout" would only clear the client's
/// copy of a token that still works. The current stamp is cached, so the common path costs
/// no database round-trip.
/// </summary>
public sealed class SecurityStampMiddleware(RequestDelegate next, ILogger<SecurityStampMiddleware> logger)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task InvokeAsync(HttpContext context, ICacheService cache, FastRideDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var userId = context.User.UserId();
        var tokenStamp = context.User.FindFirst(TokenService.SecurityStampClaim)?.Value;

        if (userId is null || tokenStamp is null)
        {
            await Reject(context, "Token is missing required claims.");
            return;
        }

        var current = await cache.GetOrCreateAsync(
            CacheKeys.SecurityStamp(userId.Value),
            CacheTtl,
            async ct => await db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId.Value && u.IsActive)
                .Select(u => (int?)u.SecurityStamp)
                .FirstOrDefaultAsync(ct),
            context.RequestAborted);

        if (current is null)
        {
            await Reject(context, "This account is no longer active.");
            return;
        }

        if (current.Value.ToString() != tokenStamp)
        {
            logger.LogInformation("Rejected a stale token for user {UserId}.", userId);
            await Reject(context, "Session has ended. Please sign in again.");
            return;
        }

        await next(context);
    }

    private static async Task Reject(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(
            new ApiError("Unauthorized", message),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
