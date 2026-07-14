using BlazePoint.Data;
using BlazePoint.Services.Search;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BlazePoint.Services;

public class ListService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    SearchService search,
    AuditService audit)
{
    public async Task<List<ListDefinition>> GetAllAsync(int? siteId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.Lists.AsQueryable();
        if (siteId.HasValue) q = q.Where(l => l.SiteId == siteId);
        return await q.OrderBy(l => l.Name).ToListAsync();
    }

    public async Task<ListDefinition?> GetAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Lists.FindAsync(id);
    }

    public async Task<ListDefinition> SaveAsync(ListDefinition list, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (list.Id == 0) db.Lists.Add(list);
        else db.Lists.Update(list);
        await db.SaveChangesAsync();
        await audit.LogAsync("List", $"Simpan list '{list.Name}'", userId);
        return list;
    }

    public async Task DeleteAsync(int id, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var list = await db.Lists.FindAsync(id);
        if (list is null) return;
        db.Lists.Remove(list);
        await db.SaveChangesAsync();
        await audit.LogAsync("List", $"Hapus list '{list.Name}'", userId);
    }

    public static List<ListColumn> ParseColumns(string json)
    {
        try { return JsonSerializer.Deserialize<List<ListColumn>>(json) ?? []; }
        catch { return []; }
    }

    public static string SerializeColumns(List<ListColumn> columns) => JsonSerializer.Serialize(columns);

    // ---------- Items ----------
    public async Task<List<ListItemEntity>> GetItemsAsync(int listId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ListItems.Where(i => i.ListId == listId)
            .OrderByDescending(i => i.UpdatedAt).ToListAsync();
    }

    public async Task<ListItemEntity> SaveItemAsync(int listId, int itemId, Dictionary<string, object?> values, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        ListItemEntity item;
        if (itemId == 0)
        {
            item = new ListItemEntity { ListId = listId, CreatedById = userId };
            db.ListItems.Add(item);
        }
        else
        {
            item = await db.ListItems.FirstAsync(i => i.Id == itemId);
        }
        item.ValuesJson = JsonSerializer.Serialize(values);
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var list = await db.Lists.FindAsync(listId);
        await search.IndexAsync("ListItem", item.Id, $"{list?.Name}: item #{item.Id}", item.ValuesJson, $"/lists/{listId}");
        return item;
    }

    public async Task DeleteItemAsync(int itemId, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var item = await db.ListItems.FindAsync(itemId);
        if (item is null) return;
        db.ListItems.Remove(item);
        await db.SaveChangesAsync();
        await search.RemoveAsync("ListItem", itemId);
    }

    public static Dictionary<string, JsonElement> ParseValues(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? []; }
        catch { return []; }
    }

    public static string DisplayValue(Dictionary<string, JsonElement> values, string column)
    {
        if (!values.TryGetValue(column, out var v)) return "";
        return v.ValueKind switch
        {
            JsonValueKind.True => "✔",
            JsonValueKind.False => "✘",
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.Null or JsonValueKind.Undefined => "",
            _ => v.GetString() ?? v.GetRawText()
        };
    }
}
