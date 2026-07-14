using BlazePoint.Data;
using BlazePoint.Services;
using BlazePoint.Services.Search;
using BlazePoint.Services.Storage;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BlazePoint.Api;

public static class ApiEndpoints
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    public static void MapApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        // ---------- Documents (REST) ----------
        var docs = api.MapGroup("/documents").RequireAuthorization();

        docs.MapGet("/", async (DocumentService service, string? folder, int? siteId) =>
            Results.Ok((await service.GetByFolderAsync(folder ?? "/", siteId))
                .Select(d => new { d.Id, d.Name, d.FolderPath, d.ContentType, d.Size, d.Version, d.CreatedAt, d.UpdatedAt })));

        docs.MapGet("/{id:int}", async (DocumentService service, int id) =>
        {
            var doc = await service.GetAsync(id, includeVersions: true);
            return doc is null ? Results.NotFound() : Results.Ok(new
            {
                doc.Id, doc.Name, doc.FolderPath, doc.ContentType, doc.Size, doc.Version,
                doc.MetadataJson, doc.CreatedAt, doc.UpdatedAt,
                Versions = doc.Versions.Select(v => new { v.Version, v.Size, v.Comment, v.CreatedAt })
            });
        });

        docs.MapGet("/{id:int}/download", async (DocumentService service, int id, int? version) =>
        {
            var result = await service.OpenAsync(id, version);
            if (result is null) return Results.NotFound();
            var (stream, doc) = result.Value;
            return Results.Stream(stream, doc.ContentType, doc.Name);
        });

        docs.MapPost("/upload", async (HttpContext http, DocumentService service) =>
        {
            var form = await http.Request.ReadFormAsync();
            var file = form.Files.FirstOrDefault();
            if (file is null) return Results.BadRequest("File tidak ditemukan.");
            var folder = form["folder"].ToString();
            if (string.IsNullOrEmpty(folder)) folder = "/";
            await using var stream = file.OpenReadStream();
            var doc = await service.UploadAsync(folder, file.FileName, stream,
                file.ContentType, file.Length, http.User.FindFirstValue(ClaimTypes.NameIdentifier));
            return Results.Ok(new { doc.Id, doc.Name, doc.Version });
        });

        docs.MapDelete("/{id:int}", async (HttpContext http, DocumentService service, int id) =>
        {
            await service.SoftDeleteAsync(id, http.User.FindFirstValue(ClaimTypes.NameIdentifier));
            return Results.NoContent();
        });

        // ---------- Lists ----------
        var lists = api.MapGroup("/lists").RequireAuthorization();

        lists.MapGet("/", async (ListService service) =>
            Results.Ok((await service.GetAllAsync())
                .Select(l => new { l.Id, l.Name, l.Description, l.ColumnsJson })));

        lists.MapGet("/{id:int}/items", async (ListService service, int id) =>
            Results.Ok((await service.GetItemsAsync(id))
                .Select(i => new { i.Id, i.ValuesJson, i.CreatedAt, i.UpdatedAt })));

        lists.MapPost("/{id:int}/items", async (HttpContext http, ListService service, int id,
            Dictionary<string, object?> values) =>
        {
            var item = await service.SaveItemAsync(id, 0, values, http.User.FindFirstValue(ClaimTypes.NameIdentifier));
            return Results.Ok(new { item.Id });
        });

        // ---------- Pages ----------
        api.MapGet("/pages", async (PageService service) =>
            Results.Ok((await service.GetAllAsync()).Where(p => p.IsPublished)
                .Select(p => new { p.Id, p.Title, p.Slug, p.Layout, p.Version, p.UpdatedAt })));

        api.MapGet("/pages/{slug}", async (PageService service, string slug) =>
        {
            var page = await service.GetBySlugAsync(slug);
            return page is null || !page.IsPublished ? Results.NotFound()
                : Results.Ok(new { page.Id, page.Title, page.Slug, page.Layout, page.Version, page.PublishedJson });
        });

        // ---------- Search ----------
        api.MapGet("/search", async (SearchService service, string q, string? mode) =>
        {
            var hits = string.Equals(mode, "semantic", StringComparison.OrdinalIgnoreCase)
                ? await service.SemanticSearchAsync(q)
                : await service.FullTextSearchAsync(q);
            return Results.Ok(hits);
        }).RequireAuthorization();

        // ---------- Sites & Events ----------
        api.MapGet("/sites", async (SiteService service) =>
            Results.Ok((await service.GetAllAsync())
                .Select(s => new { s.Id, s.Name, s.Slug, s.Description, s.Department })));

        api.MapGet("/events", async (CalendarService service, DateTime? from, DateTime? to) =>
            Results.Ok(await service.GetRangeAsync(
                from ?? DateTime.Today, to ?? DateTime.Today.AddMonths(1))));

        // ICS feed — anonymous so Outlook/Google Calendar can subscribe
        api.MapGet("/calendar/feed.ics", async (CalendarService service) =>
        {
            var events = await service.GetRangeAsync(DateTime.Today.AddMonths(-1), DateTime.Today.AddYears(1));
            return Results.Text(CalendarService.ToIcs(events), "text/calendar");
        });

        // ---------- Public share download (anonymous by design) ----------
        app.MapGet("/api/share/{token}/download", async (
            ShareLinkService shares, DocumentService documents, HttpContext http, string token) =>
        {
            var link = await shares.GetByTokenAsync(token);
            if (link is null || link.Document is null) return Results.NotFound();
            if (link.ExpiresAt.HasValue && link.ExpiresAt < DateTime.UtcNow)
                return Results.BadRequest("Link sudah kedaluwarsa.");
            if (!link.IsPublic && http.User.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();

            var result = await documents.OpenAsync(link.DocumentId);
            if (result is null) return Results.NotFound();
            await shares.RegisterDownloadAsync(link.Id);
            var (stream, doc) = result.Value;
            return Results.Stream(stream, doc.ContentType, doc.Name);
        });

        // ---------- Raw file serving (chat attachments etc.) ----------
        api.MapGet("/files/{**key}", async (IFileStorage storage, string key) =>
        {
            if (!await storage.ExistsAsync(key)) return Results.NotFound();
            var stream = await storage.OpenReadAsync(key);
            ContentTypes.TryGetContentType(key, out var contentType);
            return Results.Stream(stream, contentType ?? "application/octet-stream");
        }).RequireAuthorization();
    }
}
