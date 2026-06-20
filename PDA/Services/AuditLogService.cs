using Microsoft.EntityFrameworkCore;
using PDA.Data;
using PDA.Models;

namespace PDA.Services;

/// <summary>
/// Service for recording and querying audit logs
/// </summary>
public class AuditLogService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(AppDbContext db, IHttpContextAccessor httpContextAccessor, ILogger<AuditLogService> logger)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Log an activity to the audit trail
    /// </summary>
    public async Task LogAsync(
        string category,
        string action,
        string? description = null,
        string? details = null,
        double? durationMs = null,
        bool isSuccess = true,
        string? errorMessage = null,
        string? userId = null)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var log = new AuditLog
            {
                Category = category,
                Action = action,
                Description = description,
                Details = details,
                DurationMs = durationMs,
                IsSuccess = isSuccess,
                ErrorMessage = errorMessage,
                UserId = userId ?? GetCurrentUserId(),
                IpAddress = httpContext?.Connection?.RemoteIpAddress?.ToString(),
                UserAgent = httpContext?.Request?.Headers["User-Agent"].ToString(),
                Timestamp = DateTime.UtcNow
            };

            _db.AuditLogs.Add(log);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log: {Action}", action);
        }
    }

    /// <summary>
    /// Get paginated audit logs with filters
    /// </summary>
    public async Task<(List<AuditLog> Logs, int TotalCount)> GetLogsAsync(
        int page = 1,
        int pageSize = 50,
        string? category = null,
        string? action = null,
        string? userId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? sortBy = "Timestamp",
        bool sortDesc = true)
    {
        var query = _db.AuditLogs
            .Include(l => l.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(category))
            query = query.Where(l => l.Category == category);

        if (!string.IsNullOrEmpty(action))
            query = query.Where(l => l.Action == action);

        if (!string.IsNullOrEmpty(userId))
            query = query.Where(l => l.UserId == userId);

        if (fromDate.HasValue)
            query = query.Where(l => l.Timestamp >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(l => l.Timestamp <= toDate.Value);

        // Sorting
        query = sortBy switch
        {
            "Category" => sortDesc ? query.OrderByDescending(l => l.Category) : query.OrderBy(l => l.Category),
            "Action" => sortDesc ? query.OrderByDescending(l => l.Action) : query.OrderBy(l => l.Action),
            "DurationMs" => sortDesc ? query.OrderByDescending(l => l.DurationMs) : query.OrderBy(l => l.DurationMs),
            _ => sortDesc ? query.OrderByDescending(l => l.Timestamp) : query.OrderBy(l => l.Timestamp),
        };

        var totalCount = await query.CountAsync();
        var logs = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (logs, totalCount);
    }

    private string? GetCurrentUserId()
    {
        return _httpContextAccessor.HttpContext?.User?.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }
}
