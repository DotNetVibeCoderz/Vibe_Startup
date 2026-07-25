using Ngibrid.Data;
using Ngibrid.Models;
using Microsoft.EntityFrameworkCore;

namespace Ngibrid.Services;

/// <summary>
/// Audit service for logging all system activities
/// </summary>
public class AuditService
{
    private readonly NgibridDbContext _db;
    private readonly IHttpContextAccessor? _httpContext;

    public AuditService(NgibridDbContext db, IHttpContextAccessor? httpContext = null)
    {
        _db = db;
        _httpContext = httpContext;
    }

    public async Task LogAsync(string action, string entityType, long? entityId = null,
        string? propertyName = null, string? oldValue = null, string? newValue = null,
        string? notes = null, string? userId = null)
    {
        var log = new AuditLog
        {
            UserId = userId ?? GetCurrentUserId(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            PropertyName = propertyName,
            OldValue = oldValue,
            NewValue = newValue,
            Notes = notes,
            IpAddress = GetIpAddress(),
            UserAgent = _httpContext?.HttpContext?.Request.Headers.UserAgent.ToString()
        };

        _db.AuditLogs.Add(log);
        await _db.SaveChangesAsync();
    }

    public async Task<List<AuditLog>> GetLogsAsync(string? entityType = null, long? entityId = null, int take = 100)
    {
        var query = _db.AuditLogs.AsQueryable();
        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(l => l.EntityType == entityType);
        if (entityId.HasValue)
            query = query.Where(l => l.EntityId == entityId);
        
        return await query.OrderByDescending(l => l.CreatedAt)
            .Take(take).ToListAsync();
    }

    private string GetCurrentUserId()
    {
        return _httpContext?.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
            ?? "Anonymous";
    }

    private string? GetIpAddress()
    {
        return _httpContext?.HttpContext?.Connection?.RemoteIpAddress?.ToString();
    }
}
