using System.Globalization;
using BlazorViz.Models;
using Jint;

namespace BlazorViz.Services;

/// <summary>Applies EtlStep pipelines to TableData. Join steps resolve other datasets via callback.</summary>
public sealed class EtlService
{
    public async Task<TableData> ApplyAsync(TableData input, List<EtlStep> steps,
        Func<int, Task<TableData>>? resolveDataset = null)
    {
        var table = input;
        foreach (var step in steps)
            table = step.Op.ToLowerInvariant() switch
            {
                "filter" => Filter(table, step),
                "select" => Select(table, step.Get("fields"), keep: true),
                "drop" => Select(table, step.Get("fields"), keep: false),
                "rename" => Rename(table, step.Get("from"), step.Get("to")),
                "compute" => Compute(table, step.Get("name"), step.Get("expr")),
                "sort" => Sort(table, step.Get("field"), bool.TryParse(step.Get("desc"), out var d) && d),
                "aggregate" => Aggregate(table, step.Get("groupBy"), step.Get("aggs")),
                "join" => await Join(table, step, resolveDataset),
                "limit" => Limit(table, step.Get("n")),
                "distinct" => Distinct(table),
                "pivot" => Pivot(table, step.Get("rowField"), step.Get("columnField"), step.Get("valueField"), step.Get("agg", "sum")),
                _ => throw new InvalidOperationException($"Unknown ETL op '{step.Op}'.")
            };
        return table;
    }

    public static TableData Filter(TableData t, EtlStep step) =>
        FilterBy(t, step.Get("field"), step.Get("op", "="), step.Get("value"));

    public static TableData FilterBy(TableData t, string field, string op, string value)
    {
        var i = t.IndexOf(field);
        if (i < 0) return t;
        var result = new TableData { Columns = t.Columns };
        var values = value.Split(',').Select(v => v.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        double? numValue = double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var nv) ? nv : null;
        DateTime? dateValue = DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dv) ? dv : null;

        foreach (var row in t.Rows)
        {
            var cell = row[i];
            var s = TableData.Format(cell);
            var keep = op.ToLowerInvariant() switch
            {
                "=" => Compare(cell, s, value, numValue, dateValue) == 0,
                "!=" => Compare(cell, s, value, numValue, dateValue) != 0,
                ">" => Compare(cell, s, value, numValue, dateValue) > 0,
                ">=" => Compare(cell, s, value, numValue, dateValue) >= 0,
                "<" => Compare(cell, s, value, numValue, dateValue) is < 0 and > int.MinValue,
                "<=" => Compare(cell, s, value, numValue, dateValue) is <= 0 and > int.MinValue,
                "contains" => s.Contains(value, StringComparison.OrdinalIgnoreCase),
                "startswith" => s.StartsWith(value, StringComparison.OrdinalIgnoreCase),
                "endswith" => s.EndsWith(value, StringComparison.OrdinalIgnoreCase),
                "in" => values.Contains(s),
                "notnull" => cell is not null && s.Length > 0,
                "isnull" => cell is null || s.Length == 0,
                _ => true
            };
            if (keep) result.Rows.Add(row);
        }
        return result;

        static int Compare(object? cell, string s, string value, double? numValue, DateTime? dateValue)
        {
            if (cell is null) return int.MinValue;
            if (numValue is not null && cell is long or double or int)
                return Convert.ToDouble(cell, CultureInfo.InvariantCulture).CompareTo(numValue.Value);
            if (dateValue is not null && cell is DateTime dt) return dt.CompareTo(dateValue.Value);
            return string.Compare(s, value, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static TableData FilterDateRange(TableData t, string field, DateTime? from, DateTime? to)
    {
        var i = t.IndexOf(field);
        if (i < 0 || (from is null && to is null)) return t;
        var result = new TableData { Columns = t.Columns };
        foreach (var row in t.Rows)
        {
            DateTime? d = row[i] switch
            {
                DateTime dt => dt,
                string s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var p) => p,
                _ => null
            };
            if (d is null) continue;
            if (from is not null && d < from) continue;
            if (to is not null && d > to.Value.Date.AddDays(1).AddTicks(-1)) continue;
            result.Rows.Add(row);
        }
        return result;
    }

    public static TableData FilterIn(TableData t, string field, IReadOnlyCollection<string> values)
    {
        if (values.Count == 0) return t;
        var i = t.IndexOf(field);
        if (i < 0) return t;
        var set = values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new TableData { Columns = t.Columns };
        result.Rows.AddRange(t.Rows.Where(r => set.Contains(TableData.Format(r[i]))));
        return result;
    }

    private static TableData Select(TableData t, string fields, bool keep)
    {
        var names = fields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var wanted = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var indexes = new List<int>();
        for (var i = 0; i < t.Columns.Count; i++)
            if (wanted.Contains(t.Columns[i].Name) == keep) indexes.Add(i);
        var result = new TableData { Columns = indexes.Select(i => t.Columns[i]).ToList() };
        foreach (var row in t.Rows)
            result.Rows.Add(indexes.Select(i => row[i]).ToArray());
        return result;
    }

    private static TableData Rename(TableData t, string from, string to)
    {
        var result = t.Clone();
        var col = result.Column(from);
        if (col is not null && !string.IsNullOrWhiteSpace(to)) col.Name = to;
        return result;
    }

    /// <summary>Adds a computed column. Expression is JavaScript with row fields as variables.</summary>
    private static TableData Compute(TableData t, string name, string expr)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(expr)) return t;
        var engine = new Jint.Engine(o => o.TimeoutInterval(TimeSpan.FromSeconds(10)).LimitRecursion(64));
        var result = new TableData
        {
            Columns = [.. t.Columns, new ColumnDef { Name = name }]
        };
        foreach (var row in t.Rows)
        {
            for (var i = 0; i < t.Columns.Count; i++)
                engine.SetValue(SafeIdent(t.Columns[i].Name), row[i] is DateTime dt ? dt.ToString("yyyy-MM-dd HH:mm:ss") : row[i]);
            object? value;
            try { value = engine.Evaluate(expr).ToObject(); }
            catch (Exception ex) { throw new InvalidOperationException($"compute '{name}' failed: {ex.Message}"); }
            result.Rows.Add([.. row, TableData.Normalize(value)]);
        }
        result.InferTypes();
        return result;
    }

    private static string SafeIdent(string name)
    {
        var chars = name.Select((c, i) => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray();
        var s = new string(chars);
        return char.IsDigit(s.FirstOrDefault()) ? "_" + s : s;
    }

    public static TableData Sort(TableData t, string field, bool desc)
    {
        var i = t.IndexOf(field);
        if (i < 0) return t;
        var result = new TableData { Columns = t.Columns };
        result.Rows = [.. t.Rows.OrderBy(r => r[i], new CellComparer())];
        if (desc) result.Rows.Reverse();
        return result;
    }

    private sealed class CellComparer : IComparer<object?>
    {
        public int Compare(object? x, object? y)
        {
            if (x is null) return y is null ? 0 : -1;
            if (y is null) return 1;
            if (x is IComparable cx && x.GetType() == y.GetType()) return cx.CompareTo(y);
            var nx = ToNum(x); var ny = ToNum(y);
            if (nx is not null && ny is not null) return nx.Value.CompareTo(ny.Value);
            return string.Compare(TableData.Format(x), TableData.Format(y), StringComparison.OrdinalIgnoreCase);
        }

        private static double? ToNum(object v) => v switch
        {
            long l => l, double d => d, int i => i,
            _ => double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var r) ? r : null
        };
    }

    /// <summary>aggs format: "sum:Revenue, avg:Price, count:*" → columns sum_Revenue, avg_Price, count.</summary>
    public static TableData Aggregate(TableData t, string groupBy, string aggs)
    {
        var groupCols = groupBy.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var groupIdx = groupCols.Select(t.IndexOf).Where(i => i >= 0).ToArray();
        var aggSpecs = aggs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(a =>
            {
                var parts = a.Split(':', 2, StringSplitOptions.TrimEntries);
                return (Fn: parts[0].ToLowerInvariant(), Field: parts.Length > 1 ? parts[1] : "*");
            }).ToArray();

        var result = new TableData();
        foreach (var i in groupIdx) result.Columns.Add(new ColumnDef { Name = t.Columns[i].Name, Type = t.Columns[i].Type });
        foreach (var (fn, field) in aggSpecs)
            result.Columns.Add(new ColumnDef { Name = fn == "count" && field == "*" ? "count" : $"{fn}_{field}", Type = fn == "count" ? "integer" : "number" });

        var groups = t.Rows.GroupBy(r => string.Join("", groupIdx.Select(i => TableData.Format(r[i]))));
        foreach (var g in groups)
        {
            var first = g.First();
            var row = new List<object?>(groupIdx.Select(i => first[i]));
            foreach (var (fn, field) in aggSpecs)
            {
                var fi = t.IndexOf(field);
                var nums = fi < 0 ? [] : g.Select(r => t.NumericValue(r[fi])).Where(v => v is not null).Select(v => v!.Value).ToList();
                row.Add(fn switch
                {
                    "count" => field == "*" ? g.Count() : g.Count(r => fi >= 0 && r[fi] is not null),
                    "sum" => nums.Sum(),
                    "avg" => nums.Count > 0 ? Math.Round(nums.Average(), 4) : 0,
                    "min" => nums.Count > 0 ? nums.Min() : 0,
                    "max" => nums.Count > 0 ? nums.Max() : 0,
                    _ => (object?)null
                });
            }
            result.Rows.Add(row.ToArray());
        }
        return result;
    }

    private async Task<TableData> Join(TableData left, EtlStep step, Func<int, Task<TableData>>? resolveDataset)
    {
        if (resolveDataset is null) throw new InvalidOperationException("join requires dataset resolver.");
        if (!int.TryParse(step.Get("rightDatasetId"), out var rightId)) throw new InvalidOperationException("join: rightDatasetId missing.");
        var right = await resolveDataset(rightId);
        var leftKey = left.IndexOf(step.Get("leftKey"));
        var rightKey = right.IndexOf(step.Get("rightKey"));
        if (leftKey < 0 || rightKey < 0) throw new InvalidOperationException("join: key column not found.");
        var type = step.Get("type", "inner").ToLowerInvariant();
        var prefix = step.Get("prefix", "r_");

        var result = new TableData { Columns = [.. left.Columns] };
        var rightCols = new List<int>();
        for (var i = 0; i < right.Columns.Count; i++)
        {
            if (i == rightKey) continue;
            rightCols.Add(i);
            var name = right.Columns[i].Name;
            if (result.IndexOf(name) >= 0) name = prefix + name;
            result.Columns.Add(new ColumnDef { Name = name, Type = right.Columns[i].Type });
        }

        var lookup = right.Rows.ToLookup(r => TableData.Format(r[rightKey]), StringComparer.OrdinalIgnoreCase);
        foreach (var lrow in left.Rows)
        {
            var key = TableData.Format(lrow[leftKey]);
            var matches = lookup[key].ToList();
            if (matches.Count == 0)
            {
                if (type == "left")
                    result.Rows.Add([.. lrow, .. new object?[rightCols.Count]]);
                continue;
            }
            foreach (var rrow in matches)
                result.Rows.Add([.. lrow, .. rightCols.Select(i => rrow[i])]);
        }
        return result;
    }

    private static TableData Limit(TableData t, string n) =>
        int.TryParse(n, out var count) && count > 0 ? t.Head(count) : t;

    private static TableData Distinct(TableData t)
    {
        var result = new TableData { Columns = t.Columns };
        var seen = new HashSet<string>();
        foreach (var row in t.Rows)
            if (seen.Add(string.Join("", row.Select(TableData.Format))))
                result.Rows.Add(row);
        return result;
    }

    private static TableData Pivot(TableData t, string rowField, string columnField, string valueField, string agg)
    {
        var ri = t.IndexOf(rowField);
        var ci = t.IndexOf(columnField);
        var vi = t.IndexOf(valueField);
        if (ri < 0 || ci < 0 || vi < 0) throw new InvalidOperationException("pivot: rowField/columnField/valueField not found.");
        var colValues = t.Rows.Select(r => TableData.Format(r[ci])).Distinct().OrderBy(s => s).ToList();
        var result = new TableData();
        result.Columns.Add(new ColumnDef { Name = rowField, Type = t.Columns[ri].Type });
        foreach (var cv in colValues) result.Columns.Add(new ColumnDef { Name = cv, Type = "number" });

        foreach (var g in t.Rows.GroupBy(r => TableData.Format(r[ri])))
        {
            var row = new object?[result.Columns.Count];
            row[0] = g.First()[ri];
            for (var c = 0; c < colValues.Count; c++)
            {
                var nums = g.Where(r => TableData.Format(r[ci]) == colValues[c])
                    .Select(r => t.NumericValue(r[vi])).Where(v => v is not null).Select(v => v!.Value).ToList();
                row[c + 1] = agg switch
                {
                    "count" => nums.Count,
                    "avg" => nums.Count > 0 ? Math.Round(nums.Average(), 4) : 0,
                    "min" => nums.Count > 0 ? nums.Min() : 0,
                    "max" => nums.Count > 0 ? nums.Max() : 0,
                    _ => nums.Sum()
                };
            }
            result.Rows.Add(row);
        }
        return result;
    }
}
