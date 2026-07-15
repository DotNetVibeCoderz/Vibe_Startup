using AppBender.Core.Data;
using AppBender.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace AppBender.Core.Services;

public interface IAuditService
{
    Task LogAsync(string action, string itemType, string itemId, string? details = null);
    Task<List<AuditLog>> GetRecentAsync(int count = 200, string? itemType = null);
}

public class AuditService(IDbContextFactory<ApplicationDbContext> dbFactory, ITenantContext tenant) : IAuditService
{
    public async Task LogAsync(string action, string itemType, string itemId, string? details = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenant.TenantId,
            UserId = tenant.UserId,
            UserName = tenant.UserName,
            Action = action,
            ItemType = itemType,
            ItemId = itemId,
            Details = details
        });
        await db.SaveChangesAsync();
    }

    public async Task<List<AuditLog>> GetRecentAsync(int count = 200, string? itemType = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.AuditLogs.AsNoTracking().Where(a => a.TenantId == tenant.TenantId);
        if (!string.IsNullOrEmpty(itemType)) q = q.Where(a => a.ItemType == itemType);
        return await q.OrderByDescending(a => a.Timestamp).Take(count).ToListAsync();
    }
}

public interface IUsageService
{
    Task TrackAsync(string type, double value = 1, string? detail = null);
    void TrackFireAndForget(string type, double value = 1, string? detail = null);
    Task<List<UsageMetric>> QueryAsync(string? type, DateTime from, DateTime to);
    Task<Dictionary<string, double>> TotalsAsync(DateTime from, DateTime to);
}

public class UsageService(IDbContextFactory<ApplicationDbContext> dbFactory, ITenantContext tenant) : IUsageService
{
    public async Task TrackAsync(string type, double value = 1, string? detail = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.UsageMetrics.Add(new UsageMetric { TenantId = tenant.TenantId, Type = type, Value = value, Detail = detail });
        await db.SaveChangesAsync();
    }

    public void TrackFireAndForget(string type, double value = 1, string? detail = null)
    {
        var tenantId = tenant.TenantId;
        _ = Task.Run(async () =>
        {
            try
            {
                await using var db = await dbFactory.CreateDbContextAsync();
                db.UsageMetrics.Add(new UsageMetric { TenantId = tenantId, Type = type, Value = value, Detail = detail });
                await db.SaveChangesAsync();
            }
            catch { /* metrics must never break the request */ }
        });
    }

    public async Task<List<UsageMetric>> QueryAsync(string? type, DateTime from, DateTime to)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.UsageMetrics.AsNoTracking()
            .Where(u => u.TenantId == tenant.TenantId && u.Timestamp >= from && u.Timestamp <= to);
        if (!string.IsNullOrEmpty(type)) q = q.Where(u => u.Type == type);
        return await q.OrderBy(u => u.Timestamp).ToListAsync();
    }

    public async Task<Dictionary<string, double>> TotalsAsync(DateTime from, DateTime to)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.UsageMetrics.AsNoTracking()
            .Where(u => u.TenantId == tenant.TenantId && u.Timestamp >= from && u.Timestamp <= to)
            .GroupBy(u => u.Type)
            .Select(g => new { g.Key, Total = g.Sum(x => x.Value) })
            .ToDictionaryAsync(x => x.Key, x => x.Total);
    }
}
