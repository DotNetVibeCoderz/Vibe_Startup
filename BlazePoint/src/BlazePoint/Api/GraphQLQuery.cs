using BlazePoint.Data;
using Microsoft.EntityFrameworkCore;

namespace BlazePoint.Api;

/// <summary>GraphQL root query (HotChocolate) — read-only access to portal data at /graphql.</summary>
public class GraphQLQuery
{
    public async Task<List<SiteDto>> GetSites([Service] IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Sites
            .Select(s => new SiteDto(s.Id, s.Name, s.Slug, s.Description, s.Department))
            .ToListAsync();
    }

    public async Task<List<DocumentDto>> GetDocuments(
        [Service] IDbContextFactory<ApplicationDbContext> dbFactory, string? folder)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.Documents.Where(d => !d.IsDeleted);
        if (!string.IsNullOrEmpty(folder)) q = q.Where(d => d.FolderPath == folder);
        return await q.Select(d => new DocumentDto(d.Id, d.Name, d.FolderPath, d.ContentType, d.Size, d.Version, d.UpdatedAt))
            .ToListAsync();
    }

    public async Task<List<PageDto>> GetPages([Service] IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CmsPages.Where(p => p.IsPublished)
            .Select(p => new PageDto(p.Id, p.Title, p.Slug, p.Layout, p.Version, p.UpdatedAt))
            .ToListAsync();
    }

    public async Task<List<ListDto>> GetLists([Service] IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Lists
            .Select(l => new ListDto(l.Id, l.Name, l.Description, l.Items.Count))
            .ToListAsync();
    }

    public async Task<List<EventDto>> GetEvents(
        [Service] IDbContextFactory<ApplicationDbContext> dbFactory, DateTime? from, DateTime? to)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var start = from ?? DateTime.Today;
        var end = to ?? DateTime.Today.AddMonths(3);
        return await db.CalendarEvents
            .Where(e => e.Start < end && e.End > start)
            .Select(e => new EventDto(e.Id, e.Title, e.Location, e.Start, e.End, e.AllDay))
            .ToListAsync();
    }

    public async Task<List<DiscussionDto>> GetDiscussions([Service] IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.DiscussionThreads
            .Select(t => new DiscussionDto(t.Id, t.Title, t.Posts.Count, t.CreatedAt))
            .ToListAsync();
    }
}

public record SiteDto(int Id, string Name, string Slug, string Description, string Department);
public record DocumentDto(int Id, string Name, string FolderPath, string ContentType, long Size, int Version, DateTime UpdatedAt);
public record PageDto(int Id, string Title, string Slug, string Layout, int Version, DateTime UpdatedAt);
public record ListDto(int Id, string Name, string Description, int ItemCount);
public record EventDto(int Id, string Title, string Location, DateTime Start, DateTime End, bool AllDay);
public record DiscussionDto(int Id, string Title, int PostCount, DateTime CreatedAt);
