using BlazorViz.Models;

namespace BlazorViz.Services.Scripting;

/// <summary>Built-in, ready-to-modify script templates for common ETL / data-manipulation use cases.</summary>
public static class ScriptTemplates
{
    public static readonly IReadOnlyList<ScriptTemplate> All =
    [
        // ---------- C# ----------
        new()
        {
            Name = "Clean nulls & trim strings", Language = "csharp",
            Description = "Removes rows where all cells are null and trims string values.",
            Code = """
                var rows = Rows
                    .Where(r => r.Values.Any(v => v is not null))
                    .Select(r => r.ToDictionary(kv => kv.Key, kv => kv.Value is string s ? (object?)s.Trim() : kv.Value))
                    .ToList();
                return rows;
                """
        },
        new()
        {
            Name = "Top N per group", Language = "csharp",
            Description = "Keeps the top 3 rows per group by a numeric column.",
            Code = """
                // change "Category" (group) and "Revenue" (metric) to your columns
                var top = Rows
                    .GroupBy(r => r["Category"])
                    .SelectMany(g => g.OrderByDescending(r => Convert.ToDouble(r["Revenue"] ?? 0)).Take(3))
                    .ToList();
                return top;
                """
        },
        new()
        {
            Name = "Add computed column", Language = "csharp",
            Description = "Adds Margin = Profit / Revenue for every row.",
            Code = """
                foreach (var r in Rows) { }
                var rows = Rows.Select(r => {
                    var revenue = Convert.ToDouble(r.GetValueOrDefault("Revenue") ?? 0);
                    var profit = Convert.ToDouble(r.GetValueOrDefault("Profit") ?? 0);
                    r["Margin"] = revenue == 0 ? 0 : Math.Round(profit / revenue, 4);
                    return r;
                }).ToList();
                return rows;
                """
        },
        new()
        {
            Name = "Unpivot (wide → long)", Language = "csharp",
            Description = "Turns columns into rows: keeps key column, melts the rest into Name/Value pairs.",
            Code = """
                var key = "Country"; // identifier column to keep
                var result = new List<Dictionary<string, object?>>();
                foreach (var r in Rows)
                    foreach (var kv in r.Where(kv => kv.Key != key))
                        result.Add(new() { [key] = r[key], ["Name"] = kv.Key, ["Value"] = kv.Value });
                return result;
                """
        },
        new()
        {
            Name = "Remove outliers (z-score)", Language = "csharp",
            Description = "Drops rows whose value is more than 3 standard deviations from the mean.",
            Code = """
                var col = "Revenue";
                var values = Rows.Select(r => Convert.ToDouble(r.GetValueOrDefault(col) ?? 0)).ToList();
                var mean = values.Average();
                var std = Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
                return Rows.Where(r => Math.Abs(Convert.ToDouble(r.GetValueOrDefault(col) ?? 0) - mean) <= 3 * std).ToList();
                """
        },
        // ---------- JavaScript ----------
        new()
        {
            Name = "Filter & map rows", Language = "javascript",
            Description = "Filters rows by a condition and reshapes them.",
            Code = """
                // `rows` is an array of objects; return the transformed array
                return rows
                  .filter(r => (r.Revenue ?? 0) > 100)
                  .map(r => ({ ...r, RevenueK: Math.round((r.Revenue ?? 0) / 100) / 10 }));
                """
        },
        new()
        {
            Name = "Group & aggregate", Language = "javascript",
            Description = "Sums a metric per group key.",
            Code = """
                const groups = {};
                for (const r of rows) {
                  const key = r.Region ?? "Unknown";
                  groups[key] = groups[key] || { Region: key, Total: 0, Count: 0 };
                  groups[key].Total += r.Revenue ?? 0;
                  groups[key].Count++;
                }
                return Object.values(groups);
                """
        },
        new()
        {
            Name = "Moving average", Language = "javascript",
            Description = "Adds a 7-point moving average column over a sorted series.",
            Code = """
                const win = 7, col = "Visitors";
                rows.sort((a, b) => String(a.Date).localeCompare(String(b.Date)));
                return rows.map((r, i) => {
                  const slice = rows.slice(Math.max(0, i - win + 1), i + 1);
                  const avg = slice.reduce((s, x) => s + (x[col] ?? 0), 0) / slice.length;
                  return { ...r, [col + "_MA"]: Math.round(avg * 100) / 100 };
                });
                """
        },
        new()
        {
            Name = "Parse dates & extract parts", Language = "javascript",
            Description = "Adds Year / Month / Weekday columns from a date column.",
            Code = """
                return rows.map(r => {
                  const d = new Date(r.OrderDate);
                  return { ...r, Year: d.getFullYear(), Month: d.getMonth() + 1,
                           Weekday: ["Sun","Mon","Tue","Wed","Thu","Fri","Sat"][d.getDay()] };
                });
                """
        },
        new()
        {
            Name = "Deduplicate rows", Language = "javascript",
            Description = "Removes duplicates based on selected key columns.",
            Code = """
                const keys = ["OrderDate", "Product"]; // change to your identity columns
                const seen = new Set();
                return rows.filter(r => {
                  const k = keys.map(c => r[c]).join("|");
                  if (seen.has(k)) return false;
                  seen.add(k);
                  return true;
                });
                """
        },
        // ---------- Python ----------
        new()
        {
            Name = "Filter & enrich (Python)", Language = "python",
            Description = "Filters rows and adds a computed column. Prints JSON back.",
            Code = """
                out = []
                for r in rows:
                    if (r.get("Revenue") or 0) > 100:
                        r["Margin"] = round((r.get("Profit") or 0) / (r.get("Revenue") or 1), 4)
                        out.append(r)
                print(json.dumps(out))
                """
        },
        new()
        {
            Name = "Group & aggregate (Python)", Language = "python",
            Description = "Aggregates a metric per group using a plain dict.",
            Code = """
                from collections import defaultdict
                totals = defaultdict(float)
                for r in rows:
                    totals[r.get("Region") or "Unknown"] += r.get("Revenue") or 0
                print(json.dumps([{"Region": k, "Total": round(v, 2)} for k, v in totals.items()]))
                """
        },
        new()
        {
            Name = "Pandas transform (Python)", Language = "python",
            Description = "Full pandas pipeline (requires `pip install pandas`).",
            Code = """
                import pandas as pd
                df = pd.DataFrame(rows)
                df = df.groupby(["Region", "Category"], as_index=False)["Revenue"].sum()
                df = df.sort_values("Revenue", ascending=False)
                print(df.to_json(orient="records"))
                """
        },
        new()
        {
            Name = "Normalize 0-1 (Python)", Language = "python",
            Description = "Min-max normalizes a numeric column.",
            Code = """
                col = "Revenue"
                vals = [r.get(col) or 0 for r in rows]
                lo, hi = min(vals), max(vals)
                for r in rows:
                    r[col + "_norm"] = 0 if hi == lo else round(((r.get(col) or 0) - lo) / (hi - lo), 4)
                print(json.dumps(rows))
                """
        },
        new()
        {
            Name = "Sample rows (Python)", Language = "python",
            Description = "Takes a deterministic 10% sample of the dataset.",
            Code = """
                import random
                random.seed(42)
                sample = [r for r in rows if random.random() < 0.10]
                print(json.dumps(sample))
                """
        }
    ];

    public static IEnumerable<ScriptTemplate> ForLanguage(string language) =>
        All.Where(t => t.Language.Equals(language, StringComparison.OrdinalIgnoreCase));
}
