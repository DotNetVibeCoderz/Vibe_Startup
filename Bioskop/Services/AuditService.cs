using Microsoft.EntityFrameworkCore;
using Bioskop.Data;
using Bioskop.Models;

namespace Bioskop.Services;

public class AuditService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor? _httpContext;

    public AuditService(ApplicationDbContext context, IHttpContextAccessor? httpContext = null)
    {
        _context = context;
        _httpContext = httpContext;
    }

    public async Task LogAsync(string action, string entityName, string? entityId,
        string? oldValues, string? newValues, string? description = null)
    {
        var log = new AuditLog
        {
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            Description = description,
            UserId = _httpContext?.HttpContext?.User?.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            UserName = _httpContext?.HttpContext?.User?.Identity?.Name,
            IpAddress = _httpContext?.HttpContext?.Connection?.RemoteIpAddress?.ToString(),
            UserAgent = _httpContext?.HttpContext?.Request?.Headers?.UserAgent.ToString()
        };

        _context.AuditLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    public async Task LogTrafficAsync(string url, string method, int statusCode,
        string ipAddress, string userAgent, long responseTimeMs)
    {
        var log = new TrafficLog
        {
            Url = url,
            Method = method,
            StatusCode = statusCode,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            ResponseTimeMs = responseTimeMs
        };

        _context.TrafficLogs.Add(log);
        await _context.SaveChangesAsync();
    }

    public async Task<List<AuditLog>> GetAuditLogsAsync(int page = 0, int pageSize = 50)
    {
        return await _context.AuditLogs
            .OrderByDescending(a => a.CreatedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<List<TrafficLog>> GetTrafficLogsAsync(int page = 0, int pageSize = 50)
    {
        return await _context.TrafficLogs
            .OrderByDescending(t => t.CreatedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<object> GetTrafficStatsAsync()
    {
        var today = DateTime.UtcNow.Date;
        var weekAgo = today.AddDays(-7);

        var totalToday = await _context.TrafficLogs
            .Where(t => t.CreatedAt >= today)
            .CountAsync();

        var totalWeek = await _context.TrafficLogs
            .Where(t => t.CreatedAt >= weekAgo)
            .CountAsync();

        var statusCodes = await _context.TrafficLogs
            .Where(t => t.CreatedAt >= weekAgo)
            .GroupBy(t => t.StatusCode)
            .Select(g => new { Code = g.Key, Count = g.Count() })
            .ToListAsync();

        var avgResponseTime = await _context.TrafficLogs
            .Where(t => t.CreatedAt >= weekAgo)
            .AverageAsync(t => (double?)t.ResponseTimeMs) ?? 0;

        return new
        {
            TotalToday = totalToday,
            TotalWeek = totalWeek,
            StatusCodes = statusCodes,
            AvgResponseTimeMs = avgResponseTime
        };
    }
}
