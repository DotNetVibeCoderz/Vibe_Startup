using System.ComponentModel;
using BlazorViz.Data;
using BlazorViz.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace BlazorViz.Services.Ai;

/// <summary>
/// Lets the Data Wizard create and extend dashboards on the user's behalf
/// (create_dashboard → add_panel/add_filter → link).
/// </summary>
public sealed class DashboardBuilderPlugin(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    DashboardService dashboards)
{
    [KernelFunction("create_dashboard")]
    [Description("Creates a new empty dashboard and returns its id and link. Follow up with add_panel calls to fill it.")]
    public async Task<string> CreateDashboardAsync(
        [Description("dashboard name")] string name,
        [Description("short description")] string? description = null)
    {
        var dash = await dashboards.CreateAsync(name, description, null, "Data Wizard");
        return $"Dashboard '{name}' created with id {dash.Id}. Link: /dashboards/{dash.Id} (edit: /dashboards/{dash.Id}/edit).";
    }

    [KernelFunction("add_panel")]
    [Description("Adds a chart panel to a dashboard. chartType: line, area, bar, stackedBar, horizontalBar, pie, donut, rose, scatter, bubble, radar, waterfall, treemap, sunburst, heatmap, gauge, funnel, sankey, boxplot, candlestick, kpi, table, map. Check the dataset schema first so field names are exact. heatmap/stackedBar need seriesField; kpi needs one yField; map needs the dataset to have Lat/Lng columns (set them via latField/lngField).")]
    public async Task<string> AddPanelAsync(
        [Description("dashboard id")] int dashboardId,
        [Description("panel title")] string title,
        [Description("chart type, e.g. bar")] string chartType,
        [Description("dataset id")] int datasetId,
        [Description("X / category field")] string? xField = null,
        [Description("comma separated numeric value fields")] string? yFields = null,
        [Description("optional series/legend field")] string? seriesField = null,
        [Description("sum | avg | min | max | count | none")] string aggregation = "sum",
        [Description("tab title; created if missing")] string tabTitle = "Main",
        [Description("panel width 1-12 grid columns")] int width = 6,
        [Description("panel height in grid rows (1 row = 84px)")] int height = 4,
        [Description("latitude field (map only)")] string? latField = null,
        [Description("longitude field (map only)")] string? lngField = null,
        [Description("value field (map only)")] string? valueField = null,
        [Description("label field (map only)")] string? labelField = null)
    {
        if (!ChartBuilder.ChartTypes.Contains(chartType))
            return $"Unknown chartType '{chartType}'. Valid: {string.Join(", ", ChartBuilder.ChartTypes)}.";

        await using var db = await dbFactory.CreateDbContextAsync();
        var dash = await db.Dashboards.FindAsync(dashboardId);
        if (dash is null) return $"Dashboard {dashboardId} not found.";
        if (await db.Datasets.FindAsync(datasetId) is null) return $"Dataset {datasetId} not found — call list_datasets.";

        var layout = DashboardLayout.Parse(dash.LayoutJson);
        var tab = layout.Tabs.FirstOrDefault(t => t.Title.Equals(tabTitle, StringComparison.OrdinalIgnoreCase));
        if (tab is null)
        {
            tab = new DashboardTab { Title = tabTitle };
            layout.Tabs.Add(tab);
        }

        var maxY = tab.Panels.Count == 0 ? 0 : tab.Panels.Max(p => p.Y + p.H);
        tab.Panels.Add(new PanelDef
        {
            Title = title,
            ChartType = chartType,
            DatasetId = datasetId,
            XField = xField,
            YFields = (yFields ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            SeriesField = string.IsNullOrWhiteSpace(seriesField) ? null : seriesField,
            Aggregation = aggregation,
            LatField = latField, LngField = lngField, ValueField = valueField, LabelField = labelField,
            X = 0, Y = maxY,
            W = Math.Clamp(width, 1, 12),
            H = Math.Clamp(height, 2, 12)
        });

        await dashboards.SaveLayoutAsync(dashboardId, layout, "Data Wizard");
        return $"Panel '{title}' ({chartType}) added to tab '{tab.Title}' of dashboard {dashboardId}. Link: /dashboards/{dashboardId}.";
    }

    [KernelFunction("add_filter")]
    [Description("Adds an interactive filter to a dashboard, applied to all panels using the same dataset. type: dropdown | multiselect | slicer | daterange (daterange needs a date field).")]
    public async Task<string> AddFilterAsync(
        [Description("dashboard id")] int dashboardId,
        [Description("dataset id")] int datasetId,
        [Description("column to filter on")] string field,
        [Description("dropdown | multiselect | slicer | daterange")] string type = "dropdown",
        [Description("label shown to the user")] string? label = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var dash = await db.Dashboards.FindAsync(dashboardId);
        if (dash is null) return $"Dashboard {dashboardId} not found.";

        var layout = DashboardLayout.Parse(dash.LayoutJson);
        layout.Filters.Add(new FilterDef { DatasetId = datasetId, Field = field, Type = type, Label = label ?? field });
        await dashboards.SaveLayoutAsync(dashboardId, layout, "Data Wizard");
        return $"Filter '{label ?? field}' ({type}) on {field} added to dashboard {dashboardId}.";
    }
}
