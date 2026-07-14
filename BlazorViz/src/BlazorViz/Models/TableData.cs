using System.Globalization;
using System.Text;
using System.Text.Json;

namespace BlazorViz.Models;

public sealed class ColumnDef
{
    public string Name { get; set; } = "";
    /// <summary>string | integer | number | datetime | boolean</summary>
    public string Type { get; set; } = "string";
}

/// <summary>Lightweight column/row table used across connectors, ETL, charts and export.</summary>
public sealed class TableData
{
    public List<ColumnDef> Columns { get; set; } = [];
    public List<object?[]> Rows { get; set; } = [];

    public int RowCount => Rows.Count;

    public int IndexOf(string column) =>
        Columns.FindIndex(c => string.Equals(c.Name, column, StringComparison.OrdinalIgnoreCase));

    public ColumnDef? Column(string name) =>
        Columns.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<object?> ColumnValues(string column)
    {
        var i = IndexOf(column);
        if (i < 0) yield break;
        foreach (var row in Rows) yield return row[i];
    }

    public TableData Clone()
    {
        var t = new TableData
        {
            Columns = Columns.Select(c => new ColumnDef { Name = c.Name, Type = c.Type }).ToList(),
            Rows = Rows.Select(r => (object?[])r.Clone()).ToList()
        };
        return t;
    }

    public static string TypeOf(object? value) => value switch
    {
        null => "string",
        sbyte or byte or short or ushort or int or uint or long or ulong => "integer",
        float or double or decimal => "number",
        DateTime or DateTimeOffset or DateOnly => "datetime",
        bool => "boolean",
        _ => "string"
    };

    /// <summary>Build from a sequence of dictionaries (e.g. JSON rows / script output).</summary>
    public static TableData FromDictionaries(IEnumerable<IDictionary<string, object?>> rows)
    {
        var t = new TableData();
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var list = new List<IDictionary<string, object?>>();
        foreach (var row in rows)
        {
            list.Add(row);
            foreach (var key in row.Keys)
                if (!index.ContainsKey(key))
                {
                    index[key] = t.Columns.Count;
                    t.Columns.Add(new ColumnDef { Name = key });
                }
        }
        foreach (var row in list)
        {
            var arr = new object?[t.Columns.Count];
            foreach (var (key, val) in row)
                arr[index[key]] = Normalize(val);
            t.Rows.Add(arr);
        }
        t.InferTypes();
        return t;
    }

    public static object? Normalize(object? v) => v switch
    {
        JsonElement je => FromJsonElement(je),
        DateTimeOffset dto => dto.UtcDateTime,
        DateOnly d => d.ToDateTime(TimeOnly.MinValue),
        decimal m => (double)m,
        float f => (double)f,
        sbyte or byte or short or ushort or int or uint or ulong => Convert.ToInt64(v),
        _ => v
    };

    public static object? FromJsonElement(JsonElement je) => je.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => je.TryGetInt64(out var l) ? l : je.GetDouble(),
        JsonValueKind.String => je.GetString(),
        _ => je.GetRawText()
    };

    /// <summary>Re-infer column types by scanning values (also promotes string columns holding numbers/dates).</summary>
    public void InferTypes()
    {
        for (var c = 0; c < Columns.Count; c++)
        {
            string? type = null;
            var all = true;
            foreach (var row in Rows)
            {
                var v = row[c];
                if (v is null) continue;
                var t = TypeOf(v);
                if (t == "string") { all = false; break; }
                if (type is null) type = t;
                else if (type != t) type = type is "integer" or "number" && t is "integer" or "number" ? "number" : "string";
                if (type == "string") { all = false; break; }
            }
            Columns[c].Type = all && type is not null ? type : InferStringColumn(c);
        }
    }

    private string InferStringColumn(int c)
    {
        bool anyValue = false, allNum = true, allInt = true, allDate = true, allBool = true;
        foreach (var row in Rows)
        {
            if (row[c] is null) continue;
            var s = Convert.ToString(row[c], CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(s)) continue;
            anyValue = true;
            if (allNum && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            {
                if (allInt && !long.TryParse(s, out _)) allInt = false;
            }
            else { allNum = false; allInt = false; }
            if (allDate && !DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) allDate = false;
            if (allBool && !bool.TryParse(s, out _)) allBool = false;
            if (!allNum && !allDate && !allBool) return "string";
        }
        if (!anyValue) return "string";
        var type = allBool ? "boolean" : allInt ? "integer" : allNum ? "number" : allDate ? "datetime" : "string";
        if (type == "string") return type;
        // convert stored values in place so downstream consumers get real types
        var idx = c;
        foreach (var row in Rows)
        {
            var s = Convert.ToString(row[idx], CultureInfo.InvariantCulture);
            if (string.IsNullOrWhiteSpace(s)) { row[idx] = null; continue; }
            row[idx] = type switch
            {
                "boolean" => bool.Parse(s),
                "integer" => long.Parse(s, CultureInfo.InvariantCulture),
                "number" => double.Parse(s, NumberStyles.Any, CultureInfo.InvariantCulture),
                "datetime" => DateTime.Parse(s, CultureInfo.InvariantCulture),
                _ => row[idx]
            };
        }
        return type;
    }

    public double? NumericValue(object? v) => v switch
    {
        null => null,
        long l => l,
        double d => d,
        int i => i,
        bool b => b ? 1 : 0,
        DateTime dt => dt.ToOADate(),
        _ => double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : null
    };

    public List<Dictionary<string, object?>> ToDictionaries()
    {
        var result = new List<Dictionary<string, object?>>(Rows.Count);
        foreach (var row in Rows)
        {
            var d = new Dictionary<string, object?>(Columns.Count);
            for (var i = 0; i < Columns.Count; i++) d[Columns[i].Name] = row[i];
            result.Add(d);
        }
        return result;
    }

    public string ToCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", Columns.Select(c => Escape(c.Name))));
        foreach (var row in Rows)
            sb.AppendLine(string.Join(",", row.Select(v => Escape(Format(v)))));
        return sb.ToString();

        static string Escape(string s) =>
            s.Contains(',') || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
    }

    public static string Format(object? v) => v switch
    {
        null => "",
        DateTime dt => dt.TimeOfDay == TimeSpan.Zero
            ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        double d => d.ToString("0.####", CultureInfo.InvariantCulture),
        _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? ""
    };

    public string ToJson(int? limit = null)
    {
        var rows = limit is int n ? ToDictionaries().Take(n) : ToDictionaries();
        return JsonSerializer.Serialize(rows, JsonOpts);
    }

    public string SchemaJson() => JsonSerializer.Serialize(Columns, JsonOpts);

    public TableData Head(int n)
    {
        var t = new TableData { Columns = Columns };
        t.Rows = Rows.Take(n).ToList();
        return t;
    }

    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
