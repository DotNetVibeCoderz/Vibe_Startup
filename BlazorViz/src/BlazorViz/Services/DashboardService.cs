using BlazorViz.Data;
using BlazorViz.Models;
using Microsoft.EntityFrameworkCore;

namespace BlazorViz.Services;

/// <summary>Dashboard CRUD, versioning with rollback, and share-by-link tokens.</summary>
public sealed class DashboardService(IDbContextFactory<ApplicationDbContext> dbFactory, AuditService audit)
{
    public async Task<List<Dashboard>> ListAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Dashboards.OrderByDescending(d => d.UpdatedUtc).ToListAsync();
    }

    public async Task<Dashboard?> GetAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Dashboards.FindAsync(id);
    }

    public async Task<Dashboard?> GetByShareTokenAsync(string token)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Dashboards.FirstOrDefaultAsync(d => d.ShareToken == token && d.IsPublic);
    }

    public async Task<Dashboard> CreateAsync(string name, string? description, string? ownerId, string? ownerName)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var dash = new Dashboard
        {
            Name = name,
            Description = description,
            LayoutJson = new DashboardLayout().ToJson(),
            OwnerId = ownerId,
            OwnerName = ownerName,
            ShareToken = Guid.NewGuid().ToString("N")
        };
        db.Dashboards.Add(dash);
        await db.SaveChangesAsync();
        db.DashboardVersions.Add(new DashboardVersion { DashboardId = dash.Id, Version = 1, LayoutJson = dash.LayoutJson, CreatedBy = ownerName });
        await db.SaveChangesAsync();
        await audit.LogAsync("Dashboard", "Create", $"Dashboard '{name}' (#{dash.Id})", ownerName);
        return dash;
    }

    /// <summary>Saves the layout as a new version and updates the dashboard.</summary>
    public async Task<int> SaveLayoutAsync(int dashboardId, DashboardLayout layout, string? userName,
        string? name = null, string? description = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var dash = await db.Dashboards.FindAsync(dashboardId)
                   ?? throw new InvalidOperationException($"Dashboard {dashboardId} not found.");
        dash.LayoutJson = layout.ToJson();
        if (name is not null) dash.Name = name;
        if (description is not null) dash.Description = description;
        dash.CurrentVersion += 1;
        dash.UpdatedUtc = DateTime.UtcNow;
        db.DashboardVersions.Add(new DashboardVersion
        {
            DashboardId = dashboardId,
            Version = dash.CurrentVersion,
            LayoutJson = dash.LayoutJson,
            CreatedBy = userName
        });
        // keep last 30 versions
        var stale = await db.DashboardVersions
            .Where(v => v.DashboardId == dashboardId)
            .OrderByDescending(v => v.Version).Skip(30).ToListAsync();
        db.DashboardVersions.RemoveRange(stale);
        await db.SaveChangesAsync();
        await audit.LogAsync("Dashboard", "Save", $"Dashboard '{dash.Name}' v{dash.CurrentVersion}", userName);
        return dash.CurrentVersion;
    }

    public async Task<List<DashboardVersion>> VersionsAsync(int dashboardId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.DashboardVersions.Where(v => v.DashboardId == dashboardId)
            .OrderByDescending(v => v.Version).ToListAsync();
    }

    public async Task RollbackAsync(int dashboardId, int version, string? userName)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var target = await db.DashboardVersions
            .FirstOrDefaultAsync(v => v.DashboardId == dashboardId && v.Version == version)
            ?? throw new InvalidOperationException($"Version {version} not found.");
        var dash = await db.Dashboards.FindAsync(dashboardId)
            ?? throw new InvalidOperationException($"Dashboard {dashboardId} not found.");
        dash.LayoutJson = target.LayoutJson;
        dash.CurrentVersion += 1;
        dash.UpdatedUtc = DateTime.UtcNow;
        db.DashboardVersions.Add(new DashboardVersion
        {
            DashboardId = dashboardId,
            Version = dash.CurrentVersion,
            LayoutJson = target.LayoutJson,
            CreatedBy = $"{userName} (rollback to v{version})"
        });
        await db.SaveChangesAsync();
        await audit.LogAsync("Dashboard", "Rollback", $"Dashboard '{dash.Name}' rolled back to v{version}", userName);
    }

    public async Task SetSharingAsync(int dashboardId, bool isPublic, string? userName)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var dash = await db.Dashboards.FindAsync(dashboardId)
            ?? throw new InvalidOperationException($"Dashboard {dashboardId} not found.");
        dash.IsPublic = isPublic;
        dash.ShareToken ??= Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();
        await audit.LogAsync("Dashboard", "Share", $"Dashboard '{dash.Name}' sharing = {isPublic}", userName);
    }

    public async Task DeleteAsync(int dashboardId, string? userName)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var dash = await db.Dashboards.FindAsync(dashboardId);
        if (dash is null) return;
        db.DashboardVersions.RemoveRange(db.DashboardVersions.Where(v => v.DashboardId == dashboardId));
        db.Dashboards.Remove(dash);
        await db.SaveChangesAsync();
        await audit.LogAsync("Dashboard", "Delete", $"Dashboard '{dash.Name}' (#{dashboardId})", userName);
    }
}
