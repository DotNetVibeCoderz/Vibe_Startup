using Comblang.Data;
using Comblang.Models;
using System.Security.Claims;

namespace Comblang.Middleware;

/// <summary>
/// Middleware to log all incoming requests for audit and traffic analytics.
/// </summary>
public class AuditMiddleware
{
    private readonly RequestDelegate _next;

    public AuditMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        // Log traffic
        var trafficLog = new TrafficLog
        {
            PageUrl = context.Request.Path,
            IpAddress = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers.UserAgent.ToString(),
            SessionId = context.Request.Cookies[".AspNetCore.Session"] ?? "",
            Timestamp = DateTime.UtcNow
        };

        // Attach user ID if authenticated
        var userIdClaim = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            trafficLog.UserId = userId;
        }

        db.TrafficLogs.Add(trafficLog);

        // Log significant actions
        if (context.Request.Method == "POST" || context.Request.Method == "PUT" || context.Request.Method == "DELETE")
        {
            var auditLog = new AuditLog
            {
                UserId = trafficLog.UserId,
                Action = context.Request.Method,
                Entity = context.Request.Path,
                IpAddress = trafficLog.IpAddress,
                Details = $"Method: {context.Request.Method}, Path: {context.Request.Path}",
                Timestamp = DateTime.UtcNow
            };
            db.AuditLogs.Add(auditLog);
        }

        await db.SaveChangesAsync();
        await _next(context);
    }
}

/// <summary>
/// Extension method to easily add the audit middleware.
/// </summary>
public static class AuditMiddlewareExtensions
{
    public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuditMiddleware>();
    }
}
