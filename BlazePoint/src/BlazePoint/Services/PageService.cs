using BlazePoint.Data;
using BlazePoint.Services.Search;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BlazePoint.Services;

public class PageService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    SearchService search,
    AuditService audit)
{
    public async Task<List<CmsPage>> GetAllAsync(int? siteId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.CmsPages.AsQueryable();
        if (siteId.HasValue) q = q.Where(p => p.SiteId == siteId);
        return await q.OrderBy(p => p.Title).ToListAsync();
    }

    public async Task<CmsPage?> GetAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CmsPages.FindAsync(id);
    }

    public async Task<CmsPage?> GetBySlugAsync(string slug)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CmsPages.FirstOrDefaultAsync(p => p.Slug == slug);
    }

    public async Task<CmsPage> CreateAsync(string title, string layout, string? userId, int? siteId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var slug = Slugify(title);
        var baseSlug = slug;
        var n = 1;
        while (await db.CmsPages.AnyAsync(p => p.Slug == slug)) slug = $"{baseSlug}-{++n}";
        var page = new CmsPage { Title = title, Slug = slug, Layout = layout, SiteId = siteId, CreatedById = userId };
        db.CmsPages.Add(page);
        await db.SaveChangesAsync();
        await audit.LogAsync("Page", $"Buat halaman '{title}'", userId);
        return page;
    }

    public async Task SaveDraftAsync(int pageId, string title, string layout, List<WebPartModel> parts, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var page = await db.CmsPages.FirstAsync(p => p.Id == pageId);
        page.Title = title;
        page.Layout = layout;
        page.ContentJson = JsonSerializer.Serialize(parts);
        page.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>Publish creates a new immutable version (auto-versioning) and makes the draft live.</summary>
    public async Task PublishAsync(int pageId, string? userId, string comment = "")
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var page = await db.CmsPages.FirstAsync(p => p.Id == pageId);
        page.Version++;
        page.PublishedJson = page.ContentJson;
        page.IsPublished = true;
        page.UpdatedAt = DateTime.UtcNow;
        db.CmsPageVersions.Add(new CmsPageVersion
        {
            PageId = page.Id, Version = page.Version, ContentJson = page.ContentJson,
            Comment = string.IsNullOrEmpty(comment) ? $"Publish v{page.Version}" : comment,
            CreatedById = userId
        });
        await db.SaveChangesAsync();
        var text = string.Join(" ", ParseParts(page.PublishedJson)
            .Select(p => p.Title + " " + p.Settings.GetValueOrDefault("content", "")));
        await search.IndexAsync("Page", page.Id, page.Title, text, $"/p/{page.Slug}");
        await audit.LogAsync("Page", $"Publish halaman '{page.Title}' v{page.Version}", userId);
    }

    public async Task RollbackAsync(int pageId, int toVersion, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var page = await db.CmsPages.FirstAsync(p => p.Id == pageId);
        var version = await db.CmsPageVersions
            .FirstAsync(v => v.PageId == pageId && v.Version == toVersion);
        page.ContentJson = version.ContentJson;
        await db.SaveChangesAsync();
        await PublishAsync(pageId, userId, $"Rollback ke v{toVersion}");
    }

    public async Task<List<CmsPageVersion>> GetVersionsAsync(int pageId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CmsPageVersions.Where(v => v.PageId == pageId)
            .OrderByDescending(v => v.Version).ToListAsync();
    }

    public async Task DeleteAsync(int pageId, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var page = await db.CmsPages.FindAsync(pageId);
        if (page is null) return;
        db.CmsPages.Remove(page);
        await db.SaveChangesAsync();
        await search.RemoveAsync("Page", pageId);
        await audit.LogAsync("Page", $"Hapus halaman '{page.Title}'", userId);
    }

    public static List<WebPartModel> ParseParts(string json)
    {
        try { return JsonSerializer.Deserialize<List<WebPartModel>>(json) ?? []; }
        catch { return []; }
    }

    public static string Slugify(string text) =>
        new string(text.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray())
            .Trim('-').Replace("--", "-");
}

public class NavigationService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public event Action? Changed;

    public async Task<List<NavigationItem>> GetAsync(NavLocation location, int? siteId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.NavigationItems
            .Where(n => n.Location == location && n.SiteId == siteId)
            .OrderBy(n => n.Order).ToListAsync();
    }

    public async Task SaveAsync(NavigationItem item)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (item.Id == 0) db.NavigationItems.Add(item);
        else db.NavigationItems.Update(item);
        await db.SaveChangesAsync();
        Changed?.Invoke();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var item = await db.NavigationItems.FindAsync(id);
        if (item is null) return;
        db.NavigationItems.Remove(item);
        await db.SaveChangesAsync();
        Changed?.Invoke();
    }

    public async Task MoveAsync(int id, int direction)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var item = await db.NavigationItems.FindAsync(id);
        if (item is null) return;
        var siblings = await db.NavigationItems
            .Where(n => n.Location == item.Location && n.SiteId == item.SiteId)
            .OrderBy(n => n.Order).ToListAsync();
        var idx = siblings.FindIndex(n => n.Id == id);
        var newIdx = idx + direction;
        if (newIdx < 0 || newIdx >= siblings.Count) return;
        (siblings[idx].Order, siblings[newIdx].Order) = (siblings[newIdx].Order, siblings[idx].Order);
        await db.SaveChangesAsync();
        Changed?.Invoke();
    }
}

public class SiteService(IDbContextFactory<ApplicationDbContext> dbFactory, AuditService audit)
{
    public async Task<List<Site>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Sites.OrderBy(s => s.Name).ToListAsync();
    }

    public async Task<Site?> GetBySlugAsync(string slug)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Sites.FirstOrDefaultAsync(s => s.Slug == slug);
    }

    public async Task<Site> SaveAsync(Site site, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (site.Id == 0)
        {
            site.Slug = PageService.Slugify(string.IsNullOrEmpty(site.Slug) ? site.Name : site.Slug);
            site.CreatedById = userId;
            db.Sites.Add(site);
        }
        else db.Sites.Update(site);
        await db.SaveChangesAsync();
        await audit.LogAsync("Site", $"Simpan site '{site.Name}'", userId);
        return site;
    }

    public async Task DeleteAsync(int id, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var site = await db.Sites.FindAsync(id);
        if (site is null) return;
        db.Sites.Remove(site);
        await db.SaveChangesAsync();
        await audit.LogAsync("Site", $"Hapus site '{site.Name}'", userId);
    }
}
