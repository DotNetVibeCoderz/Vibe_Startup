using System.Globalization;
using AppBender.Core.Common;
using AppBender.Core.Data;
using AppBender.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace AppBender.Core.Services;

public class FieldFilter
{
    public string Field { get; set; } = "";
    /// <summary>eq | neq | gt | gte | lt | lte | contains | startswith | isnull | notnull</summary>
    public string Op { get; set; } = "eq";
    public string? Value { get; set; }
}

public class QueryOptions
{
    public List<FieldFilter> Filters { get; set; } = [];
    /// <summary>Free-text search across all string values.</summary>
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class QueryResult
{
    public List<DataRecord> Records { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public interface IDataHubService
{
    Task<List<EntityDefinition>> GetEntitiesAsync();
    Task<EntityDefinition?> GetEntityAsync(string name);
    Task<EntityDefinition> SaveEntityAsync(EntityDefinition entity, string? versionComment = null);
    Task DeleteEntityAsync(string name);

    Task<QueryResult> QueryAsync(string entityName, QueryOptions? options = null);
    Task<DataRecord?> GetRecordAsync(string entityName, string id);
    Task<DataRecord> CreateRecordAsync(string entityName, Dictionary<string, object?> data);
    Task<DataRecord> UpdateRecordAsync(string entityName, string id, Dictionary<string, object?> data);
    Task DeleteRecordAsync(string entityName, string id);
    Task<int> CountRecordsAsync(string entityName);
}

public class DataHubService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ITenantContext tenant,
    IAuditService audit,
    IUsageService usage,
    IVersionService versions,
    EventBus events) : IDataHubService
{
    // ---------- Entities ----------

    public async Task<List<EntityDefinition>> GetEntitiesAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.EntityDefinitions.AsNoTracking()
            .Where(e => e.TenantId == tenant.TenantId)
            .OrderBy(e => e.DisplayName)
            .ToListAsync();
    }

    public async Task<EntityDefinition?> GetEntityAsync(string name)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.EntityDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(e => e.TenantId == tenant.TenantId && e.Name == name);
    }

    public async Task<EntityDefinition> SaveEntityAsync(EntityDefinition entity, string? versionComment = null)
    {
        if (string.IsNullOrWhiteSpace(entity.Name))
            throw new InvalidOperationException("Entity name is required.");
        entity.Name = Slugify(entity.Name);

        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.EntityDefinitions
            .FirstOrDefaultAsync(e => e.TenantId == tenant.TenantId && e.Id == entity.Id);

        if (existing is null)
        {
            entity.TenantId = tenant.TenantId;
            entity.Version = 1;
            db.EntityDefinitions.Add(entity);
            await db.SaveChangesAsync();
            await audit.LogAsync("create", "entity", entity.Id, entity.Name);
        }
        else
        {
            existing.Name = entity.Name;
            existing.DisplayName = entity.DisplayName;
            existing.Description = entity.Description;
            existing.Icon = entity.Icon;
            existing.FieldsJson = entity.FieldsJson;
            existing.RelationshipsJson = entity.RelationshipsJson;
            existing.Version++;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            entity = existing;
            await audit.LogAsync("update", "entity", entity.Id, entity.Name);
        }

        await versions.SnapshotAsync("entity", entity.Id, entity.Name, entity.Version,
            JsonUtil.Serialize(entity), versionComment);
        return entity;
    }

    public async Task DeleteEntityAsync(string name)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var entity = await db.EntityDefinitions
            .FirstOrDefaultAsync(e => e.TenantId == tenant.TenantId && e.Name == name)
            ?? throw new KeyNotFoundException($"Entity '{name}' not found.");
        db.EntityDefinitions.Remove(entity);
        await db.DataRecords.Where(r => r.TenantId == tenant.TenantId && r.EntityName == name).ExecuteDeleteAsync();
        await db.SaveChangesAsync();
        await audit.LogAsync("delete", "entity", entity.Id, name);
    }

    // ---------- Records ----------

    public async Task<QueryResult> QueryAsync(string entityName, QueryOptions? options = null)
    {
        options ??= new QueryOptions();
        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.DataRecords.AsNoTracking()
            .Where(r => r.TenantId == tenant.TenantId && r.EntityName == entityName)
            .ToListAsync();

        IEnumerable<(DataRecord Rec, Dictionary<string, object?> Data)> set =
            rows.Select(r => (r, r.Data));

        foreach (var f in options.Filters.Where(f => !string.IsNullOrEmpty(f.Field)))
            set = set.Where(x => MatchesFilter(GetValue(x.Rec, x.Data, f.Field), f));

        if (!string.IsNullOrWhiteSpace(options.Search))
        {
            var term = options.Search.Trim();
            set = set.Where(x => x.Data.Values.Any(v =>
                v is not null && TemplateEngine.ToText(v).Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        var list = set.ToList();
        var total = list.Count;

        if (!string.IsNullOrEmpty(options.SortBy))
        {
            list = (options.SortDesc
                ? list.OrderByDescending(x => GetValue(x.Rec, x.Data, options.SortBy!), FieldComparer.Instance)
                : list.OrderBy(x => GetValue(x.Rec, x.Data, options.SortBy!), FieldComparer.Instance)).ToList();
        }
        else
        {
            list = list.OrderByDescending(x => x.Rec.UpdatedAt).ToList();
        }

        var pageSize = Math.Clamp(options.PageSize, 1, 500);
        var page = Math.Max(1, options.Page);
        usage.TrackFireAndForget("record_op", 1, $"query:{entityName}");

        return new QueryResult
        {
            Records = list.Skip((page - 1) * pageSize).Take(pageSize).Select(x => x.Rec).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private static object? GetValue(DataRecord rec, Dictionary<string, object?> data, string field) => field switch
    {
        "id" => rec.Id,
        "createdAt" => rec.CreatedAt,
        "updatedAt" => rec.UpdatedAt,
        _ => data.TryGetValue(field, out var v) ? v : null
    };

    private static bool MatchesFilter(object? value, FieldFilter f)
    {
        switch (f.Op)
        {
            case "isnull": return value is null;
            case "notnull": return value is not null;
        }
        var text = TemplateEngine.ToText(value);
        var filterText = f.Value ?? "";
        var numeric = double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var a)
                   && double.TryParse(filterText, NumberStyles.Any, CultureInfo.InvariantCulture, out var b);
        double av = 0, bv = 0;
        if (numeric) { double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out av); double.TryParse(filterText, NumberStyles.Any, CultureInfo.InvariantCulture, out bv); }

        return f.Op switch
        {
            "eq" => numeric ? av == bv : string.Equals(text, filterText, StringComparison.OrdinalIgnoreCase),
            "neq" => numeric ? av != bv : !string.Equals(text, filterText, StringComparison.OrdinalIgnoreCase),
            "gt" => numeric ? av > bv : string.Compare(text, filterText, StringComparison.OrdinalIgnoreCase) > 0,
            "gte" => numeric ? av >= bv : string.Compare(text, filterText, StringComparison.OrdinalIgnoreCase) >= 0,
            "lt" => numeric ? av < bv : string.Compare(text, filterText, StringComparison.OrdinalIgnoreCase) < 0,
            "lte" => numeric ? av <= bv : string.Compare(text, filterText, StringComparison.OrdinalIgnoreCase) <= 0,
            "contains" => text.Contains(filterText, StringComparison.OrdinalIgnoreCase),
            "startswith" => text.StartsWith(filterText, StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    private class FieldComparer : IComparer<object?>
    {
        public static readonly FieldComparer Instance = new();
        public int Compare(object? x, object? y)
        {
            if (x is null && y is null) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            var xs = TemplateEngine.ToText(x);
            var ys = TemplateEngine.ToText(y);
            if (double.TryParse(xs, NumberStyles.Any, CultureInfo.InvariantCulture, out var xd) &&
                double.TryParse(ys, NumberStyles.Any, CultureInfo.InvariantCulture, out var yd))
                return xd.CompareTo(yd);
            return string.Compare(xs, ys, StringComparison.OrdinalIgnoreCase);
        }
    }

    public async Task<DataRecord?> GetRecordAsync(string entityName, string id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.DataRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.TenantId == tenant.TenantId && r.EntityName == entityName && r.Id == id);
    }

    public async Task<DataRecord> CreateRecordAsync(string entityName, Dictionary<string, object?> data)
    {
        var entity = await GetEntityAsync(entityName)
            ?? throw new KeyNotFoundException($"Entity '{entityName}' not found.");
        var clean = await ValidateAndCoerceAsync(entity, data, isNew: true);

        var record = new DataRecord
        {
            TenantId = tenant.TenantId,
            EntityName = entityName,
            Data = clean,
            CreatedById = tenant.UserId,
            UpdatedById = tenant.UserId
        };

        await using var db = await dbFactory.CreateDbContextAsync();
        db.DataRecords.Add(record);
        await db.SaveChangesAsync();

        usage.TrackFireAndForget("record_op", 1, $"create:{entityName}");
        await audit.LogAsync("create", "record", record.Id, entityName);
        await events.PublishAsync(new RecordChangedEvent(tenant.TenantId, entityName, "created", record));
        return record;
    }

    public async Task<DataRecord> UpdateRecordAsync(string entityName, string id, Dictionary<string, object?> data)
    {
        var entity = await GetEntityAsync(entityName)
            ?? throw new KeyNotFoundException($"Entity '{entityName}' not found.");

        await using var db = await dbFactory.CreateDbContextAsync();
        var record = await db.DataRecords
            .FirstOrDefaultAsync(r => r.TenantId == tenant.TenantId && r.EntityName == entityName && r.Id == id)
            ?? throw new KeyNotFoundException($"Record '{id}' not found in '{entityName}'.");

        var merged = record.Data;
        foreach (var (k, v) in data) merged[k] = v;
        record.Data = await ValidateAndCoerceAsync(entity, merged, isNew: false);
        record.UpdatedAt = DateTime.UtcNow;
        record.UpdatedById = tenant.UserId;
        record.Version++;
        await db.SaveChangesAsync();

        usage.TrackFireAndForget("record_op", 1, $"update:{entityName}");
        await audit.LogAsync("update", "record", record.Id, entityName);
        await events.PublishAsync(new RecordChangedEvent(tenant.TenantId, entityName, "updated", record));
        return record;
    }

    public async Task DeleteRecordAsync(string entityName, string id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var record = await db.DataRecords
            .FirstOrDefaultAsync(r => r.TenantId == tenant.TenantId && r.EntityName == entityName && r.Id == id)
            ?? throw new KeyNotFoundException($"Record '{id}' not found in '{entityName}'.");
        db.DataRecords.Remove(record);
        await db.SaveChangesAsync();

        usage.TrackFireAndForget("record_op", 1, $"delete:{entityName}");
        await audit.LogAsync("delete", "record", id, entityName);
        await events.PublishAsync(new RecordChangedEvent(tenant.TenantId, entityName, "deleted", record));
    }

    public async Task<int> CountRecordsAsync(string entityName)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.DataRecords
            .CountAsync(r => r.TenantId == tenant.TenantId && r.EntityName == entityName);
    }

    // ---------- Validation ----------

    private async Task<Dictionary<string, object?>> ValidateAndCoerceAsync(
        EntityDefinition entity, Dictionary<string, object?> data, bool isNew)
    {
        var fields = entity.Fields;
        var clean = new Dictionary<string, object?>();
        var errors = new List<string>();

        foreach (var field in fields)
        {
            data.TryGetValue(field.Name, out var raw);

            if (field.Type == FieldType.AutoNumber)
            {
                if (isNew && (raw is null || TemplateEngine.ToText(raw) == ""))
                    clean[field.Name] = await NextAutoNumberAsync(entity.Name, field.Name);
                else if (raw is not null) clean[field.Name] = raw;
                continue;
            }

            if (field.Type == FieldType.Formula)
                continue; // computed below, after all inputs are known

            if (raw is null || (raw is string es && es.Length == 0))
            {
                if (!string.IsNullOrEmpty(field.DefaultValue) && isNew)
                    clean[field.Name] = CoerceScalar(field, field.DefaultValue, errors);
                else if (field.Required && isNew)
                    errors.Add($"Field '{field.DisplayName}' is required.");
                else if (data.ContainsKey(field.Name))
                    clean[field.Name] = null;
                continue;
            }

            clean[field.Name] = Coerce(field, raw, errors);
        }

        // keep unknown keys (schema-flexible), so imports/AI output don't lose data
        foreach (var (k, v) in data)
            if (!clean.ContainsKey(k) && fields.All(f => f.Name != k))
                clean[k] = v;

        foreach (var field in fields.Where(f => f.Type == FieldType.Formula && !string.IsNullOrEmpty(f.Formula)))
            clean[field.Name] = EvaluateFormula(field.Formula!, clean);

        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));
        return clean;
    }

    private object? Coerce(FieldDefinition field, object raw, List<string> errors)
    {
        if (raw is System.Text.Json.JsonElement je) raw = JsonUtil.ToClr(je) ?? "";
        if (field.Type == FieldType.MultiChoice)
        {
            var items = raw switch
            {
                IEnumerable<object?> list => list.Select(TemplateEngine.ToText).ToList(),
                string s => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                _ => [TemplateEngine.ToText(raw)]
            };
            var invalid = items.Where(i => field.Options.Count > 0 && !field.Options.Contains(i)).ToList();
            if (invalid.Count > 0) errors.Add($"Invalid options for '{field.DisplayName}': {string.Join(", ", invalid)}.");
            return items;
        }
        if (field.Type == FieldType.Json) return raw;
        return CoerceScalar(field, TemplateEngine.ToText(raw), errors);
    }

    private object? CoerceScalar(FieldDefinition field, string text, List<string> errors)
    {
        switch (field.Type)
        {
            case FieldType.Number:
                if (!long.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var l))
                {
                    if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var dd)) return CheckRange(field, dd, errors);
                    errors.Add($"'{field.DisplayName}' must be a number."); return null;
                }
                return CheckRange(field, l, errors);
            case FieldType.Decimal:
            case FieldType.Currency:
                if (!double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                { errors.Add($"'{field.DisplayName}' must be a number."); return null; }
                return CheckRange(field, d, errors);
            case FieldType.Boolean:
                return text.Equals("true", StringComparison.OrdinalIgnoreCase) || text == "1" || text.Equals("yes", StringComparison.OrdinalIgnoreCase);
            case FieldType.Date:
            case FieldType.DateTime:
                if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                { errors.Add($"'{field.DisplayName}' must be a valid date."); return null; }
                return field.Type == FieldType.Date ? dt.ToString("yyyy-MM-dd") : dt.ToString("O");
            case FieldType.Choice:
                if (field.Options.Count > 0 && !field.Options.Contains(text))
                { errors.Add($"'{text}' is not a valid option for '{field.DisplayName}'."); return null; }
                return text;
            case FieldType.Email:
                if (!text.Contains('@')) { errors.Add($"'{field.DisplayName}' must be a valid email."); return null; }
                return text;
            default:
                if (field.MaxLength is int max && text.Length > max) text = text[..max];
                return text;
        }
    }

    private static object CheckRange(FieldDefinition field, double value, List<string> errors)
    {
        if (field.Min is double min && value < min) errors.Add($"'{field.DisplayName}' must be >= {min}.");
        if (field.Max is double max && value > max) errors.Add($"'{field.DisplayName}' must be <= {max}.");
        return value == Math.Floor(value) && Math.Abs(value) < long.MaxValue ? (long)value : value;
    }

    private async Task<long> NextAutoNumberAsync(string entityName, string fieldName)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.DataRecords.AsNoTracking()
            .Where(r => r.TenantId == tenant.TenantId && r.EntityName == entityName)
            .Select(r => r.DataJson).ToListAsync();
        long max = 0;
        foreach (var json in rows)
        {
            var data = JsonUtil.ToClrDictionary(json);
            if (data.TryGetValue(fieldName, out var v) &&
                long.TryParse(TemplateEngine.ToText(v), out var n) && n > max) max = n;
        }
        return max + 1;
    }

    /// <summary>Very small arithmetic formula evaluator over record fields (+ - * / and parentheses).</summary>
    public static object? EvaluateFormula(string formula, Dictionary<string, object?> data)
    {
        try
        {
            var expr = formula;
            foreach (var (k, v) in data.OrderByDescending(kv => kv.Key.Length))
            {
                var text = TemplateEngine.ToText(v);
                if (!double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out _)) text = "0";
                expr = System.Text.RegularExpressions.Regex.Replace(
                    expr, $@"\b{System.Text.RegularExpressions.Regex.Escape(k)}\b", text);
            }
            return MathEvaluator.Evaluate(expr);
        }
        catch { return null; }
    }

    public static string Slugify(string input)
    {
        var slug = new string(input.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        while (slug.Contains("__")) slug = slug.Replace("__", "_");
        return slug.Trim('_');
    }
}
