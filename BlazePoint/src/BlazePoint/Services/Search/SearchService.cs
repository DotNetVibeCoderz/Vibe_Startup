using BlazePoint.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace BlazePoint.Services.Search;

public record SearchHit(string EntityType, int EntityId, string Title, string Snippet, string Link, double Score);

public class SearchService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    IVectorIndex? vectorIndex,
    ILogger<SearchService> logger)
{
    // ---------- Indexing ----------
    public async Task IndexAsync(string entityType, int entityId, string title, string content, string link)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var entry = await db.SearchIndex
                .FirstOrDefaultAsync(e => e.EntityType == entityType && e.EntityId == entityId);
            if (entry is null)
            {
                entry = new SearchIndexEntry { EntityType = entityType, EntityId = entityId };
                db.SearchIndex.Add(entry);
            }
            entry.Title = title.Length > 500 ? title[..500] : title;
            entry.Content = content.Length > 20000 ? content[..20000] : content;
            entry.Link = link;
            entry.UpdatedAt = DateTime.UtcNow;

            var vector = await EmbedAsync($"{title}\n{content}");
            entry.Embedding = ToBytes(vector);
            await db.SaveChangesAsync();

            if (vectorIndex is not null)
                await vectorIndex.UpsertAsync(entry.Id, vector);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to index {Type}/{Id}", entityType, entityId);
        }
    }

    public async Task RemoveAsync(string entityType, int entityId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var entry = await db.SearchIndex
            .FirstOrDefaultAsync(e => e.EntityType == entityType && e.EntityId == entityId);
        if (entry is null) return;
        db.SearchIndex.Remove(entry);
        await db.SaveChangesAsync();
        if (vectorIndex is not null)
            try { await vectorIndex.DeleteAsync(entry.Id); } catch { /* index cleanup is best-effort */ }
    }

    // ---------- Query ----------
    public async Task<List<SearchHit>> FullTextSearchAsync(string query, int top = 25)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var q = db.SearchIndex.AsQueryable();
        foreach (var term in terms)
        {
            var t = $"%{term}%";
            q = q.Where(e => EF.Functions.Like(e.Title, t) || EF.Functions.Like(e.Content, t));
        }
        var results = await q.OrderByDescending(e => e.UpdatedAt).Take(top).ToListAsync();
        return results.Select(e => new SearchHit(e.EntityType, e.EntityId, e.Title,
            Snippet(e.Content, terms.FirstOrDefault() ?? ""), e.Link, 1.0)).ToList();
    }

    public async Task<List<SearchHit>> SemanticSearchAsync(string query, int top = 10)
    {
        var queryVector = await EmbedAsync(query);
        await using var db = await dbFactory.CreateDbContextAsync();

        if (vectorIndex is not null)
        {
            var hits = await vectorIndex.SearchAsync(queryVector, top);
            var ids = hits.Select(h => h.Id).ToList();
            var entries = await db.SearchIndex.Where(e => ids.Contains(e.Id)).ToListAsync();
            return hits
                .Select(h => (h, entry: entries.FirstOrDefault(e => e.Id == h.Id)))
                .Where(x => x.entry is not null)
                .Select(x => new SearchHit(x.entry!.EntityType, x.entry.EntityId, x.entry.Title,
                    Snippet(x.entry.Content, ""), x.entry.Link, x.h.Score))
                .ToList();
        }

        // Local mode: cosine similarity over stored embeddings
        var all = await db.SearchIndex.Where(e => e.Embedding != null).ToListAsync();
        return all
            .Select(e => (entry: e, score: Cosine(queryVector, FromBytes(e.Embedding!))))
            .OrderByDescending(x => x.score)
            .Take(top)
            .Where(x => x.score > 0.05)
            .Select(x => new SearchHit(x.entry.EntityType, x.entry.EntityId, x.entry.Title,
                Snippet(x.entry.Content, ""), x.entry.Link, Math.Round(x.score, 3)))
            .ToList();
    }

    public async Task<int> ReindexAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var count = 0;
        foreach (var d in await db.Documents.Where(d => !d.IsDeleted).ToListAsync())
        {
            await IndexAsync("Document", d.Id, d.Name, $"{d.FolderPath} {d.MetadataJson}", $"/documents?path={Uri.EscapeDataString(d.FolderPath)}");
            count++;
        }
        foreach (var p in await db.CmsPages.ToListAsync())
        {
            await IndexAsync("Page", p.Id, p.Title, p.PublishedJson, $"/p/{p.Slug}");
            count++;
        }
        foreach (var t in await db.DiscussionThreads.Include(t => t.Posts).ToListAsync())
        {
            await IndexAsync("Discussion", t.Id, t.Title,
                t.Body + "\n" + string.Join("\n", t.Posts.Select(p => p.Body)), $"/discussions/{t.Id}");
            count++;
        }
        foreach (var i in await db.ListItems.ToListAsync())
        {
            var list = await db.Lists.FindAsync(i.ListId);
            await IndexAsync("ListItem", i.Id, $"{list?.Name} item #{i.Id}", i.ValuesJson, $"/lists/{i.ListId}");
            count++;
        }
        return count;
    }

    // ---------- Helpers ----------
    private async Task<float[]> EmbedAsync(string text)
    {
        var result = await embedder.GenerateAsync([text]);
        return result[0].Vector.ToArray();
    }

    private static string Snippet(string content, string term)
    {
        if (string.IsNullOrEmpty(content)) return "";
        var idx = string.IsNullOrEmpty(term) ? 0
            : Math.Max(0, content.IndexOf(term, StringComparison.OrdinalIgnoreCase));
        var start = Math.Max(0, idx - 60);
        var length = Math.Min(200, content.Length - start);
        return (start > 0 ? "…" : "") + content.Substring(start, length) + (start + length < content.Length ? "…" : "");
    }

    public static double Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        return na == 0 || nb == 0 ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    private static byte[] ToBytes(float[] vector)
    {
        var bytes = new byte[vector.Length * 4];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] FromBytes(byte[] bytes)
    {
        var vector = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
    }
}
