using System.Text.Json;
using System.Text.Json.Nodes;
using BlazorViz.Models;

namespace BlazorViz.Services;

/// <summary>
/// Turns a PanelDef + TableData into an ECharts option (JSON). KPI, table and map panels
/// return data payloads consumed by their dedicated renderers instead.
/// </summary>
public sealed class ChartBuilder
{
    public static readonly string[] ChartTypes =
    [
        "line", "area", "bar", "stackedBar", "horizontalBar", "pie", "donut", "rose",
        "scatter", "bubble", "radar", "waterfall", "treemap", "sunburst", "heatmap",
        "gauge", "funnel", "sankey", "boxplot", "candlestick", "kpi", "table", "map", "custom"
    ];

    private static readonly JsonSerializerOptions Opts = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public string BuildOptionJson(PanelDef panel, TableData data)
    {
        var option = Build(panel, data);
        var json = JsonSerializer.Serialize(option, Opts);
        if (!string.IsNullOrWhiteSpace(panel.OptionsJson))
        {
            try
            {
                var baseNode = JsonNode.Parse(json)!.AsObject();
                var overrides = JsonNode.Parse(panel.OptionsJson)!.AsObject();
                Merge(baseNode, overrides);
                json = baseNode.ToJsonString(Opts);
            }
            catch { /* invalid override JSON — keep generated option */ }
        }
        return json;
    }

    private static void Merge(JsonObject target, JsonObject overrides)
    {
        foreach (var (key, value) in overrides.ToList())
        {
            if (value is JsonObject vo && target[key] is JsonObject to) Merge(to, vo);
            else target[key] = value?.DeepClone();
        }
    }

    private object Build(PanelDef p, TableData data) => p.ChartType switch
    {
        "line" or "area" => Cartesian(p, data, "line", area: p.ChartType == "area"),
        "bar" or "stackedBar" => Cartesian(p, data, "bar", stacked: p.ChartType == "stackedBar"),
        "horizontalBar" => Cartesian(p, data, "bar", horizontal: true),
        "pie" => PieLike(p, data, radius: "70%"),
        "donut" => PieLike(p, data, radius: new[] { "45%", "72%" }),
        "rose" => PieLike(p, data, radius: "70%", rose: true),
        "scatter" or "bubble" => Scatter(p, data, bubble: p.ChartType == "bubble"),
        "radar" => Radar(p, data),
        "waterfall" => Waterfall(p, data),
        "treemap" => new { tooltip = new { }, series = new[] { new { type = "treemap", data = NameValue(p, data), roam = false, label = new { show = true } } } },
        "sunburst" => Sunburst(p, data),
        "heatmap" => Heatmap(p, data),
        "gauge" => Gauge(p, data),
        "funnel" => new { tooltip = new { }, series = new[] { new { type = "funnel", data = NameValue(p, data), gap = 2, label = new { show = true, position = "inside" } } } },
        "sankey" => Sankey(p, data),
        "boxplot" => Boxplot(p, data),
        "candlestick" => Candlestick(p, data),
        _ => new { }
    };

    /// <summary>Aggregates rows by X (and optional series) producing category → series values.</summary>
    public (List<string> Categories, List<(string Name, List<double?> Values)> Series) PrepareXY(PanelDef p, TableData data)
    {
        var agg = p.Aggregation is "none" ? null : p.Aggregation;
        var t = data;

        if (agg is not null && !string.IsNullOrWhiteSpace(p.XField) && (p.YFields.Count > 0 || agg == "count"))
        {
            var groupBy = string.IsNullOrWhiteSpace(p.SeriesField) ? p.XField : $"{p.XField},{p.SeriesField}";
            var aggs = agg == "count" ? "count:*" : string.Join(",", p.YFields.Select(y => $"{agg}:{y}"));
            t = EtlService.Aggregate(data, groupBy, aggs);
        }

        string ValueCol(string y) => agg is null or "none" ? y : agg == "count" ? "count" : $"{agg}_{y}";

        if (!string.IsNullOrWhiteSpace(p.SortBy))
        {
            var sortCol = t.IndexOf(p.SortBy) >= 0 ? p.SortBy : ValueCol(p.SortBy);
            t = EtlService.Sort(t, sortCol, p.SortDesc);
        }
        if (p.Limit is int lim and > 0) t = t.Head(lim);

        var xi = t.IndexOf(p.XField ?? "");
        var categories = new List<string>();
        var catIndex = new Dictionary<string, int>();
        foreach (var row in t.Rows)
        {
            var c = xi < 0 ? "" : TableData.Format(row[xi]);
            if (catIndex.TryAdd(c, categories.Count)) categories.Add(c);
        }

        var series = new List<(string, List<double?>)>();
        List<string> yFields = agg == "count" && p.YFields.Count == 0 ? ["count"] : p.YFields;
        if (string.IsNullOrWhiteSpace(p.SeriesField))
        {
            foreach (var y in yFields)
            {
                var vi = t.IndexOf(ValueCol(y));
                if (vi < 0) continue;
                var vals = new List<double?>(new double?[categories.Count]);
                foreach (var row in t.Rows)
                {
                    var c = xi < 0 ? "" : TableData.Format(row[xi]);
                    vals[catIndex[c]] = t.NumericValue(row[vi]);
                }
                series.Add((y, vals));
            }
        }
        else
        {
            var si = t.IndexOf(p.SeriesField);
            var vi = t.IndexOf(ValueCol(p.YFields.FirstOrDefault() ?? ""));
            if (si >= 0 && vi >= 0)
            {
                var bySeries = new Dictionary<string, List<double?>>();
                foreach (var row in t.Rows)
                {
                    var s = TableData.Format(row[si]);
                    if (!bySeries.TryGetValue(s, out var vals))
                        bySeries[s] = vals = new List<double?>(new double?[categories.Count]);
                    var c = xi < 0 ? "" : TableData.Format(row[xi]);
                    vals[catIndex[c]] = t.NumericValue(row[vi]);
                }
                foreach (var (name, vals) in bySeries)
                {
                    while (vals.Count < categories.Count) vals.Add(null);
                    series.Add((name, vals));
                }
            }
        }
        return (categories, series);
    }

    private object Cartesian(PanelDef p, TableData data, string kind, bool stacked = false, bool horizontal = false, bool area = false)
    {
        var (cats, series) = PrepareXY(p, data);
        var catAxis = new { type = "category", data = cats };
        var valAxis = new { type = "value" };
        return new
        {
            tooltip = new { trigger = "axis" },
            legend = series.Count > 1 ? new { top = 0 } : null,
            grid = new { left = 48, right = 16, top = series.Count > 1 ? 32 : 16, bottom = 32, containLabel = true },
            xAxis = horizontal ? (object)valAxis : catAxis,
            yAxis = horizontal ? (object)catAxis : valAxis,
            series = series.Select(s => new
            {
                name = s.Name,
                type = kind,
                stack = stacked ? "total" : null,
                smooth = kind == "line" ? (object)true : null,
                areaStyle = area ? new { opacity = 0.25 } : null,
                data = s.Values
            }).ToList()
        };
    }

    public List<KeyValuePair<string, double>> NameValuePairs(PanelDef p, TableData data)
    {
        var (cats, series) = PrepareXY(p, data);
        var vals = series.FirstOrDefault().Values ?? [];
        var pairs = new List<KeyValuePair<string, double>>();
        for (var i = 0; i < cats.Count; i++)
            pairs.Add(new(cats[i], i < vals.Count ? vals[i] ?? 0 : 0));
        return pairs;
    }

    private List<object> NameValue(PanelDef p, TableData data) =>
        NameValuePairs(p, data).Select(kv => (object)new { name = kv.Key, value = Math.Round(kv.Value, 4) }).ToList();

    private object PieLike(PanelDef p, TableData data, object radius, bool rose = false) => new
    {
        tooltip = new { trigger = "item" },
        legend = new { top = 0, type = "scroll" },
        series = new[]
        {
            new
            {
                type = "pie",
                radius,
                roseType = rose ? "radius" : null,
                itemStyle = new { borderRadius = 6, borderWidth = 2 },
                label = new { show = true, formatter = "{b}: {d}%" },
                data = NameValue(p, data)
            }
        }
    };

    private object Scatter(PanelDef p, TableData data, bool bubble)
    {
        var xi = data.IndexOf(p.XField ?? "");
        var yi = data.IndexOf(p.YFields.FirstOrDefault() ?? "");
        var si = bubble ? data.IndexOf(p.SizeField ?? "") : -1;
        var li = data.IndexOf(p.LabelField ?? "");
        var maxSize = si >= 0 ? data.Rows.Max(r => data.NumericValue(r[si]) ?? 0) : 0;

        var points = new List<object?[]>();
        foreach (var row in data.Rows)
        {
            var x = xi >= 0 ? data.NumericValue(row[xi]) : null;
            var y = yi >= 0 ? data.NumericValue(row[yi]) : null;
            if (x is null || y is null) continue;
            points.Add([x, y, si >= 0 ? data.NumericValue(row[si]) ?? 0 : 12, li >= 0 ? TableData.Format(row[li]) : ""]);
        }
        return new
        {
            tooltip = new { trigger = "item" },
            grid = new { left = 48, right = 16, top = 16, bottom = 32, containLabel = true },
            xAxis = new { type = "value", name = p.XField, scale = true },
            yAxis = new { type = "value", name = p.YFields.FirstOrDefault(), scale = true },
            series = new[]
            {
                new
                {
                    type = "scatter",
                    symbolSizeMax = bubble && maxSize > 0 ? maxSize : (double?)null,
                    data = points
                }
            }
        };
    }

    private object Radar(PanelDef p, TableData data)
    {
        var (cats, series) = PrepareXY(p, data);
        var maxVal = series.SelectMany(s => s.Values).Max(v => v ?? 0) * 1.1;
        return new
        {
            tooltip = new { },
            legend = new { top = 0 },
            radar = new { indicator = cats.Select(c => new { name = c, max = Math.Round(maxVal, 2) }).ToList() },
            series = new[]
            {
                new
                {
                    type = "radar",
                    data = series.Select(s => new { name = s.Name, value = s.Values.Select(v => v ?? 0).ToList() }).ToList()
                }
            }
        };
    }

    private object Waterfall(PanelDef p, TableData data)
    {
        var pairs = NameValuePairs(p, data);
        var helper = new List<object>();
        var deltas = new List<object>();
        double running = 0;
        foreach (var (name, value) in pairs.Select(kv => (kv.Key, kv.Value)))
        {
            helper.Add(value >= 0 ? running : running + value);
            deltas.Add(Math.Abs(value));
            running += value;
        }
        var cats = pairs.Select(kv => kv.Key).ToList();
        cats.Add("Total");
        helper.Add(0);
        deltas.Add(Math.Round(running, 2));
        return new
        {
            tooltip = new { trigger = "axis" },
            grid = new { left = 48, right = 16, top = 16, bottom = 32, containLabel = true },
            xAxis = new { type = "category", data = cats },
            yAxis = new { type = "value" },
            series = new object[]
            {
                new { name = "helper", type = "bar", stack = "wf", itemStyle = new { borderColor = "transparent", color = "transparent" }, emphasis = new { itemStyle = new { borderColor = "transparent", color = "transparent" } }, data = helper, tooltip = new { show = false } },
                new { name = p.YFields.FirstOrDefault() ?? "value", type = "bar", stack = "wf", label = new { show = true, position = "top" }, data = deltas }
            }
        };
    }

    private object Sunburst(PanelDef p, TableData data)
    {
        // two-level: SeriesField (outer group) → XField
        var children = new List<object>();
        if (!string.IsNullOrWhiteSpace(p.SeriesField))
        {
            var si = data.IndexOf(p.SeriesField);
            foreach (var g in data.Rows.GroupBy(r => si >= 0 ? TableData.Format(r[si]) : ""))
            {
                var sub = new TableData { Columns = data.Columns, Rows = g.ToList() };
                children.Add(new { name = g.Key, children = NameValue(p, sub) });
            }
        }
        else children = NameValue(p, data);
        return new { tooltip = new { }, series = new[] { new { type = "sunburst", radius = new[] { "12%", "85%" }, data = children, label = new { rotate = "radial" } } } };
    }

    private object Heatmap(PanelDef p, TableData data)
    {
        var agg = p.Aggregation is "none" ? "sum" : p.Aggregation;
        var y = p.YFields.FirstOrDefault() ?? "";
        var t = EtlService.Aggregate(data, $"{p.XField},{p.SeriesField}", $"{agg}:{y}");
        var valCol = agg == "count" ? "count" : $"{agg}_{y}";
        var xi = t.IndexOf(p.XField ?? "");
        var si = t.IndexOf(p.SeriesField ?? "");
        var vi = t.IndexOf(valCol);
        var xCats = new List<string>(); var yCats = new List<string>();
        var cells = new List<object?[]>();
        double max = 0;
        foreach (var row in t.Rows)
        {
            var xv = TableData.Format(row[xi]); var yv = TableData.Format(row[si]);
            if (!xCats.Contains(xv)) xCats.Add(xv);
            if (!yCats.Contains(yv)) yCats.Add(yv);
            var val = t.NumericValue(row[vi]) ?? 0;
            max = Math.Max(max, val);
            cells.Add([xCats.IndexOf(xv), yCats.IndexOf(yv), Math.Round(val, 2)]);
        }
        xCats.Sort(StringComparer.Ordinal);
        // re-map x indexes after sort
        var xmap = xCats.Select((c, i) => (c, i)).ToDictionary(x => x.c, x => x.i);
        for (var i = 0; i < cells.Count; i++)
        {
            var row = t.Rows[i];
            cells[i][0] = xmap[TableData.Format(row[xi])];
        }
        return new
        {
            tooltip = new { position = "top" },
            grid = new { left = 48, right = 16, top = 16, bottom = 64, containLabel = true },
            xAxis = new { type = "category", data = xCats },
            yAxis = new { type = "category", data = yCats },
            visualMap = new { min = 0, max = Math.Round(max, 2), calculable = true, orient = "horizontal", left = "center", bottom = 0 },
            series = new[] { new { type = "heatmap", data = cells, label = new { show = false } } }
        };
    }

    private object Gauge(PanelDef p, TableData data)
    {
        var y = p.YFields.FirstOrDefault() ?? "";
        var values = data.ColumnValues(y).Select(data.NumericValue).Where(v => v is not null).Select(v => v!.Value).ToList();
        var value = p.Aggregation switch
        {
            "avg" => values.Count > 0 ? values.Average() : 0,
            "min" => values.Count > 0 ? values.Min() : 0,
            "max" => values.Count > 0 ? values.Max() : 0,
            "count" => values.Count,
            _ => values.Sum()
        };
        var max = Math.Max(100, Math.Ceiling(value * 1.25));
        return new
        {
            series = new[]
            {
                new
                {
                    type = "gauge",
                    max,
                    progress = new { show = true, width = 14 },
                    axisLine = new { lineStyle = new { width = 14 } },
                    detail = new { valueAnimation = true, formatter = "{value}" },
                    data = new[] { new { value = Math.Round(value, 1), name = p.Title } }
                }
            }
        };
    }

    private object Sankey(PanelDef p, TableData data)
    {
        var si = data.IndexOf(p.XField ?? "");
        var ti = data.IndexOf(p.SeriesField ?? "");
        var vi = data.IndexOf(p.YFields.FirstOrDefault() ?? "");
        var nodes = new HashSet<string>();
        var links = new Dictionary<(string, string), double>();
        foreach (var row in data.Rows)
        {
            var s = si >= 0 ? TableData.Format(row[si]) : "";
            var t = ti >= 0 ? TableData.Format(row[ti]) : "";
            if (s.Length == 0 || t.Length == 0 || s == t) continue;
            nodes.Add(s); nodes.Add(t);
            var v = vi >= 0 ? data.NumericValue(row[vi]) ?? 0 : 1;
            links[(s, t)] = links.GetValueOrDefault((s, t)) + v;
        }
        return new
        {
            tooltip = new { trigger = "item" },
            series = new[]
            {
                new
                {
                    type = "sankey",
                    emphasis = new { focus = "adjacency" },
                    data = nodes.Select(n => new { name = n }).ToList(),
                    links = links.Select(kv => new { source = kv.Key.Item1, target = kv.Key.Item2, value = Math.Round(kv.Value, 2) }).ToList()
                }
            }
        };
    }

    private object Boxplot(PanelDef p, TableData data)
    {
        var xi = data.IndexOf(p.XField ?? "");
        var yi = data.IndexOf(p.YFields.FirstOrDefault() ?? "");
        var cats = new List<string>();
        var boxes = new List<double[]>();
        foreach (var g in data.Rows.GroupBy(r => xi >= 0 ? TableData.Format(r[xi]) : ""))
        {
            var vals = g.Select(r => yi >= 0 ? data.NumericValue(r[yi]) : null).Where(v => v is not null).Select(v => v!.Value).OrderBy(v => v).ToList();
            if (vals.Count == 0) continue;
            cats.Add(g.Key);
            boxes.Add([vals.First(), Quantile(vals, 0.25), Quantile(vals, 0.5), Quantile(vals, 0.75), vals.Last()]);
        }
        return new
        {
            tooltip = new { trigger = "item" },
            grid = new { left = 48, right = 16, top = 16, bottom = 32, containLabel = true },
            xAxis = new { type = "category", data = cats },
            yAxis = new { type = "value", scale = true },
            series = new[] { new { type = "boxplot", data = boxes } }
        };

        static double Quantile(List<double> sorted, double q)
        {
            var pos = (sorted.Count - 1) * q;
            var lo = (int)Math.Floor(pos);
            var hi = (int)Math.Ceiling(pos);
            return Math.Round(sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo), 4);
        }
    }

    private object Candlestick(PanelDef p, TableData data)
    {
        // YFields order: open, close, low, high
        var xi = data.IndexOf(p.XField ?? "");
        var idx = p.YFields.Select(data.IndexOf).ToArray();
        var cats = new List<string>();
        var candles = new List<double[]>();
        foreach (var row in data.Rows)
        {
            if (idx.Length < 4 || idx.Any(i => i < 0)) break;
            cats.Add(xi >= 0 ? TableData.Format(row[xi]) : "");
            candles.Add(idx.Select(i => data.NumericValue(row[i]) ?? 0).ToArray());
        }
        return new
        {
            tooltip = new { trigger = "axis" },
            grid = new { left = 48, right = 16, top = 16, bottom = 32, containLabel = true },
            xAxis = new { type = "category", data = cats },
            yAxis = new { type = "value", scale = true },
            series = new[] { new { type = "candlestick", data = candles } }
        };
    }

    /// <summary>KPI value for kpi panels.</summary>
    public (double Value, string Label) KpiValue(PanelDef p, TableData data)
    {
        var y = p.YFields.FirstOrDefault() ?? "";
        var values = data.ColumnValues(y).Select(data.NumericValue).Where(v => v is not null).Select(v => v!.Value).ToList();
        var value = p.Aggregation switch
        {
            "avg" => values.Count > 0 ? values.Average() : 0,
            "min" => values.Count > 0 ? values.Min() : 0,
            "max" => values.Count > 0 ? values.Max() : 0,
            "count" => data.RowCount,
            _ => values.Sum()
        };
        return (Math.Round(value, 2), $"{p.Aggregation}({y})");
    }

    /// <summary>Rows for map panels: [lat, lng, value, label].</summary>
    public List<object?[]> MapPoints(PanelDef p, TableData data)
    {
        var lat = data.IndexOf(p.LatField ?? "");
        var lng = data.IndexOf(p.LngField ?? "");
        var val = data.IndexOf(p.ValueField ?? "");
        var lab = data.IndexOf(p.LabelField ?? "");
        var points = new List<object?[]>();
        if (lat < 0 || lng < 0) return points;
        foreach (var row in data.Rows)
        {
            var la = data.NumericValue(row[lat]);
            var ln = data.NumericValue(row[lng]);
            if (la is null || ln is null) continue;
            points.Add([la, ln, val >= 0 ? data.NumericValue(row[val]) ?? 1 : 1, lab >= 0 ? TableData.Format(row[lab]) : ""]);
        }
        return points;
    }
}
