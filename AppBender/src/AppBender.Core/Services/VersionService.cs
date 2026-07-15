using AppBender.Core.Data;
using AppBender.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace AppBender.Core.Services;

public interface IVersionService
{
    Task SnapshotAsync(string itemType, string itemId, string itemName, int version, string snapshotJson, string? comment = null);
    Task<List<VersionSnapshot>> GetHistoryAsync(string itemType, string itemId);
    Task<VersionSnapshot?> GetSnapshotAsync(string snapshotId);
}

public class VersionService(IDbContextFactory<ApplicationDbContext> dbFactory, ITenantContext tenant) : IVersionService
{
    public async Task SnapshotAsync(string itemType, string itemId, string itemName, int version, string snapshotJson, string? comment = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.VersionSnapshots.Add(new VersionSnapshot
        {
            TenantId = tenant.TenantId,
            ItemType = itemType,
            ItemId = itemId,
            ItemName = itemName,
            Version = version,
            SnapshotJson = snapshotJson,
            Comment = comment,
            CreatedBy = tenant.UserName
        });
        await db.SaveChangesAsync();
    }

    public async Task<List<VersionSnapshot>> GetHistoryAsync(string itemType, string itemId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.VersionSnapshots.AsNoTracking()
            .Where(v => v.TenantId == tenant.TenantId && v.ItemType == itemType && v.ItemId == itemId)
            .OrderByDescending(v => v.Version)
            .ToListAsync();
    }

    public async Task<VersionSnapshot?> GetSnapshotAsync(string snapshotId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.VersionSnapshots.AsNoTracking().FirstOrDefaultAsync(v => v.Id == snapshotId);
    }
}
