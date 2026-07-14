using BlazorViz.Models;

namespace BlazorViz.Services;

public sealed record ChartSuggestion(string ChartType, string Title, string Reason, string? XField, List<string> YFields, string? SeriesField, string Aggregation);

/// <summary>Suggests suitable visualizations from the shape of a dataset's columns.</summary>
public sealed class RecommendationService
{
    public List<ChartSuggestion> Suggest(TableData data)
    {
        var suggestions = new List<ChartSuggestion>();
        var numeric = data.Columns.Where(c => c.Type is "number" or "integer").Select(c => c.Name).ToList();
        var dates = data.Columns.Where(c => c.Type == "datetime").Select(c => c.Name).ToList();
        var categorical = data.Columns
            .Where(c => c.Type is "string" or "boolean")
            .Where(c => CardinalityOf(data, c.Name) is > 1 and <= 50)
            .Select(c => c.Name).ToList();
        var geo = FindGeo(data);

        if (dates.Count > 0 && numeric.Count > 0)
            suggestions.Add(new("line", $"{numeric[0]} over {dates[0]}",
                "Time column + numeric metric → trend line.", dates[0], [numeric[0]], null, "sum"));

        if (categorical.Count > 0 && numeric.Count > 0)
        {
            var cat = categorical[0];
            suggestions.Add(new("bar", $"{numeric[0]} by {cat}",
                "Categorical dimension + metric → ranked bars.", cat, [numeric[0]], null, "sum"));
            if (CardinalityOf(data, cat) <= 8)
                suggestions.Add(new("donut", $"{numeric[0]} share by {cat}",
                    "Few categories → part-of-whole donut.", cat, [numeric[0]], null, "sum"));
            suggestions.Add(new("treemap", $"{numeric[0]} treemap by {cat}",
                "Hierarchical share comparison.", cat, [numeric[0]], null, "sum"));
        }

        if (categorical.Count >= 2 && numeric.Count > 0)
            suggestions.Add(new("heatmap", $"{numeric[0]} by {categorical[0]} × {categorical[1]}",
                "Two dimensions + metric → density heatmap.", categorical[0], [numeric[0]], categorical[1], "sum"));

        if (numeric.Count >= 2)
            suggestions.Add(new("scatter", $"{numeric[0]} vs {numeric[1]}",
                "Two numeric columns → correlation scatter.", numeric[0], [numeric[1]], null, "none"));

        if (numeric.Count >= 3)
            suggestions.Add(new("bubble", $"{numeric[0]} vs {numeric[1]} (size {numeric[2]})",
                "Three metrics → bubble chart.", numeric[0], [numeric[1]], null, "none"));

        if (geo is not null && numeric.Count > 0)
            suggestions.Add(new("map", $"Map of {numeric[0]}",
                "Latitude/longitude detected → geo map.", null, [numeric[0]], null, "none"));

        if (numeric.Count > 0 && suggestions.Count == 0)
            suggestions.Add(new("kpi", $"Total {numeric[0]}", "Single metric → KPI card.", null, [numeric[0]], null, "sum"));

        return suggestions;
    }

    private static int CardinalityOf(TableData data, string col) =>
        data.ColumnValues(col).Select(TableData.Format).Distinct(StringComparer.OrdinalIgnoreCase).Count();

    private static (string Lat, string Lng)? FindGeo(TableData data)
    {
        string? lat = null, lng = null;
        foreach (var c in data.Columns)
        {
            var n = c.Name.ToLowerInvariant();
            if (n is "lat" or "latitude") lat = c.Name;
            if (n is "lng" or "lon" or "long" or "longitude") lng = c.Name;
        }
        return lat is not null && lng is not null ? (lat, lng) : null;
    }
}
