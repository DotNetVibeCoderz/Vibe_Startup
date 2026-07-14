using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorViz.Models;

public sealed class DashboardLayout
{
    public List<DashboardTab> Tabs { get; set; } = [new DashboardTab { Title = "Main" }];
    public List<FilterDef> Filters { get; set; } = [];
    /// <summary>0 = off, otherwise seconds between automatic data refreshes.</summary>
    public int RefreshIntervalSeconds { get; set; }

    public static DashboardLayout Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new DashboardLayout();
        try { return JsonSerializer.Deserialize<DashboardLayout>(json, TableData.JsonOpts) ?? new DashboardLayout(); }
        catch { return new DashboardLayout(); }
    }

    public string ToJson() => JsonSerializer.Serialize(this, TableData.JsonOpts);
}

public sealed class DashboardTab
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Title { get; set; } = "Tab";
    public List<PanelDef> Panels { get; set; } = [];
}

public sealed class PanelDef
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Title { get; set; } = "Panel";
    /// <summary>
    /// line | area | bar | stackedBar | horizontalBar | pie | donut | rose | scatter | bubble |
    /// radar | waterfall | treemap | sunburst | heatmap | gauge | funnel | sankey | boxplot |
    /// candlestick | kpi | table | map | mapHeat | custom
    /// </summary>
    public string ChartType { get; set; } = "bar";
    public int DatasetId { get; set; }

    // grid position (12-column grid)
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; } = 6;
    public int H { get; set; } = 4;

    public string? XField { get; set; }
    public List<string> YFields { get; set; } = [];
    /// <summary>Optional series/legend split field.</summary>
    public string? SeriesField { get; set; }
    /// <summary>sum | avg | min | max | count | none</summary>
    public string Aggregation { get; set; } = "sum";
    public int? Limit { get; set; }
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; }

    // bubble / candlestick / boxplot extras
    public string? SizeField { get; set; }

    // geo map
    public string? LatField { get; set; }
    public string? LngField { get; set; }
    public string? ValueField { get; set; }
    public string? LabelField { get; set; }

    // custom visuals (chartjs | d3 | echarts raw)
    /// <summary>chartjs | d3 | echarts</summary>
    public string? CustomLib { get; set; }
    /// <summary>JS body: receives (el, rows, columns, lib) and renders into el.</summary>
    public string? CustomCode { get; set; }

    /// <summary>Raw ECharts option overrides merged on top of generated option (JSON).</summary>
    public string? OptionsJson { get; set; }
}

public sealed class FilterDef
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    /// <summary>slicer | dropdown | multiselect | daterange</summary>
    public string Type { get; set; } = "dropdown";
    public int DatasetId { get; set; }
    public string Field { get; set; } = "";
    public string Label { get; set; } = "";
}

/// <summary>Runtime state of one dashboard filter (selected values / range).</summary>
public sealed class FilterState
{
    public string FilterId { get; set; } = "";
    public List<string> Values { get; set; } = [];
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    [JsonIgnore]
    public bool IsActive => Values.Count > 0 || From.HasValue || To.HasValue;
}
