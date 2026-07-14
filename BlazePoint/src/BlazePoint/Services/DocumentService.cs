using BlazePoint.Data;
using BlazePoint.Services.Search;
using BlazePoint.Services.Storage;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BlazePoint.Services;

public class DocumentService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    IFileStorage storage,
    SearchService search,
    AuditService audit)
{
    // ---------- Browse ----------
    public async Task<List<Document>> GetByFolderAsync(string folderPath, int? siteId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.Documents.Where(d => !d.IsDeleted && d.FolderPath == folderPath);
        if (siteId.HasValue) q = q.Where(d => d.SiteId == siteId);
        return await q.OrderBy(d => d.Name).ToListAsync();
    }

    public async Task<List<string>> GetSubfoldersAsync(string folderPath, int? siteId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var prefix = folderPath.TrimEnd('/') + "/";
        var q = db.Documents.Where(d => !d.IsDeleted && d.FolderPath.StartsWith(prefix));
        if (siteId.HasValue) q = q.Where(d => d.SiteId == siteId);
        var paths = await q.Select(d => d.FolderPath).Distinct().ToListAsync();
        return paths
            .Select(p => prefix + p[prefix.Length..].Split('/')[0])
            .Where(p => p != folderPath)
            .Distinct().OrderBy(p => p).ToList();
    }

    public async Task<Document?> GetAsync(int id, bool includeVersions = false)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = includeVersions
            ? db.Documents.Include(d => d.Versions.OrderByDescending(v => v.Version))
            : db.Documents.AsQueryable();
        return await q.FirstOrDefaultAsync(d => d.Id == id);
    }

    // ---------- Upload / Versioning ----------
    public async Task<Document> UploadAsync(
        string folderPath, string fileName, Stream content, string contentType,
        long size, string? userId, int? siteId = null, string comment = "")
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.Documents.FirstOrDefaultAsync(d =>
            !d.IsDeleted && d.FolderPath == folderPath && d.Name == fileName && d.SiteId == siteId);

        var key = $"documents/{Guid.NewGuid():N}/{fileName}";
        await storage.SaveAsync(key, content, contentType);

        Document doc;
        if (existing is null)
        {
            doc = new Document
            {
                SiteId = siteId, FolderPath = folderPath, Name = fileName,
                ContentType = contentType, Size = size, StorageKey = key,
                CreatedById = userId
            };
            db.Documents.Add(doc);
            await db.SaveChangesAsync();
            db.DocumentVersions.Add(new DocumentVersion
            {
                DocumentId = doc.Id, Version = 1, StorageKey = key, Size = size,
                Comment = string.IsNullOrEmpty(comment) ? "Initial upload" : comment, CreatedById = userId
            });
        }
        else
        {
            // auto-versioning: keep previous file, bump version
            existing.Version++;
            existing.StorageKey = key;
            existing.Size = size;
            existing.ContentType = contentType;
            existing.UpdatedAt = DateTime.UtcNow;
            db.DocumentVersions.Add(new DocumentVersion
            {
                DocumentId = existing.Id, Version = existing.Version, StorageKey = key, Size = size,
                Comment = string.IsNullOrEmpty(comment) ? $"Version {existing.Version}" : comment, CreatedById = userId
            });
            doc = existing;
        }
        await db.SaveChangesAsync();

        await search.IndexAsync("Document", doc.Id, doc.Name,
            $"{doc.FolderPath} {doc.ContentType} {doc.MetadataJson}", $"/documents/{doc.Id}");
        await audit.LogAsync("Document", $"Upload '{fileName}' v{doc.Version} ke {folderPath}", userId);
        return doc;
    }

    public async Task RollbackAsync(int documentId, int toVersion, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var doc = await db.Documents.Include(d => d.Versions).FirstAsync(d => d.Id == documentId);
        var target = doc.Versions.First(v => v.Version == toVersion);
        doc.Version++;
        doc.StorageKey = target.StorageKey;
        doc.Size = target.Size;
        doc.UpdatedAt = DateTime.UtcNow;
        db.DocumentVersions.Add(new DocumentVersion
        {
            DocumentId = doc.Id, Version = doc.Version, StorageKey = target.StorageKey,
            Size = target.Size, Comment = $"Rollback ke v{toVersion}", CreatedById = userId
        });
        await db.SaveChangesAsync();
        await audit.LogAsync("Document", $"Rollback '{doc.Name}' ke v{toVersion}", userId);
    }

    // ---------- Metadata ----------
    public async Task UpdateMetadataAsync(int documentId, Dictionary<string, string> metadata, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var doc = await db.Documents.FindAsync(documentId);
        if (doc is null) return;
        doc.MetadataJson = JsonSerializer.Serialize(metadata);
        doc.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await search.IndexAsync("Document", doc.Id, doc.Name,
            $"{doc.FolderPath} {doc.ContentType} {doc.MetadataJson}", $"/documents/{doc.Id}");
    }

    public static Dictionary<string, string> ParseMetadata(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? []; }
        catch { return []; }
    }

    // ---------- Recycle bin ----------
    public async Task SoftDeleteAsync(int documentId, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var doc = await db.Documents.FindAsync(documentId);
        if (doc is null) return;
        doc.IsDeleted = true;
        doc.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await search.RemoveAsync("Document", documentId);
        await audit.LogAsync("Document", $"Hapus '{doc.Name}' (ke recycle bin)", userId);
    }

    public async Task RestoreAsync(int documentId, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var doc = await db.Documents.FindAsync(documentId);
        if (doc is null) return;
        doc.IsDeleted = false;
        doc.DeletedAt = null;
        await db.SaveChangesAsync();
        await search.IndexAsync("Document", doc.Id, doc.Name,
            $"{doc.FolderPath} {doc.ContentType} {doc.MetadataJson}", $"/documents/{doc.Id}");
        await audit.LogAsync("Document", $"Restore '{doc.Name}' dari recycle bin", userId);
    }

    public async Task PurgeAsync(int documentId, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var doc = await db.Documents.Include(d => d.Versions).FirstOrDefaultAsync(d => d.Id == documentId);
        if (doc is null) return;
        foreach (var v in doc.Versions)
            try { await storage.DeleteAsync(v.StorageKey); } catch { /* best-effort */ }
        db.Documents.Remove(doc);
        await db.SaveChangesAsync();
        await audit.LogAsync("Document", $"Hapus permanen '{doc.Name}'", userId);
    }

    public async Task<List<Document>> GetRecycleBinAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Documents.Where(d => d.IsDeleted)
            .OrderByDescending(d => d.DeletedAt).ToListAsync();
    }

    // ---------- Content access ----------
    public async Task<(Stream Stream, Document Doc)?> OpenAsync(int documentId, int? version = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var doc = await db.Documents.Include(d => d.Versions).FirstOrDefaultAsync(d => d.Id == documentId);
        if (doc is null) return null;
        var key = version.HasValue
            ? doc.Versions.FirstOrDefault(v => v.Version == version)?.StorageKey ?? doc.StorageKey
            : doc.StorageKey;
        return (await storage.OpenReadAsync(key), doc);
    }

    public static string PreviewKind(string contentType, string name)
    {
        var ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
        if (contentType.StartsWith("image/")) return "image";
        if (contentType.StartsWith("video/")) return "video";
        if (contentType.StartsWith("audio/")) return "audio";
        if (contentType == "application/pdf" || ext == ".pdf") return "pdf";
        if (contentType.StartsWith("text/") || ext is ".md" or ".txt" or ".json" or ".xml" or ".csv" or ".log" or ".cs" or ".js" or ".css" or ".html") return "text";
        return "none";
    }
}
