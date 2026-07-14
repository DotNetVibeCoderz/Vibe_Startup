using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BlazorViz.Data;
using BlazorViz.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel.ChatCompletion;

namespace BlazorViz.Services.Ai;

/// <summary>
/// AI copilot for the dashboard designer: takes a natural-language prompt plus the current layout
/// and returns an explanation and (when the model proposes changes) a full updated DashboardLayout.
/// </summary>
public sealed class DashboardAiService(
    AiKernelFactory kernelFactory,
    IDbContextFactory<ApplicationDbContext> dbFactory,
    DatasetService datasets)
{
    public async Task<(string Reply, DashboardLayout? Layout)> ComposeAsync(
        string prompt, DashboardLayout current, CancellationToken ct = default)
    {
        var schemas = await DescribeDatasetsAsync(ct);
        var kernel = kernelFactory.CreateKernel(withPlugins: false);
        var chat = kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory($$"""
            You are the BlazorViz dashboard design copilot. You modify a dashboard layout based on the user's request.

            DashboardLayout JSON schema (camelCase):
            { "tabs": [ { "id": "8-char-id", "title": "…", "panels": [ PanelDef… ] } ],
              "filters": [ { "id": "…", "type": "dropdown|multiselect|slicer|daterange", "datasetId": n, "field": "…", "label": "…" } ],
              "refreshIntervalSeconds": 0 }

            PanelDef: { "id": "8-char-id", "title": "…", "chartType": "…", "datasetId": n,
              "x": 0-11, "y": row, "w": 1-12, "h": rows (1 row = 84px, use 2 for kpi, 4-5 for charts),
              "xField": "…", "yFields": ["…"], "seriesField": null, "aggregation": "sum|avg|min|max|count|none",
              "limit": null, "sortBy": null, "sortDesc": false,
              "sizeField": null, "latField": null, "lngField": null, "valueField": null, "labelField": null,
              "customLib": null, "customCode": null, "optionsJson": null }

            Chart types: {{string.Join(", ", ChartBuilder.ChartTypes)}}.
            Grid is 12 columns; place panels without overlaps (x + w <= 12, stack rows via y).
            heatmap and stackedBar need seriesField; scatter/bubble use aggregation "none"; kpi uses one yField;
            map needs latField/lngField (+ valueField/labelField); candlestick yFields = [open, close, low, high].
            Use ONLY these datasets and exact column names:
            {{schemas}}

            Reply with a short explanation of what you changed, followed by the COMPLETE updated layout in a single
            ```json fenced block (the full DashboardLayout, not a fragment — keep untouched tabs/panels/ids as-is).
            If the user only asks a question, answer it without a JSON block.
            """);
        history.AddUserMessage($"Current layout JSON:\n{current.ToJson()}\n\nRequest: {prompt}");

        var result = await chat.GetChatMessageContentAsync(history, kernelFactory.CreateSettings(), kernel, ct);
        var text = result.Content ?? "";
        var layout = TryParseLayout(text);
        var reply = Regex.Replace(text, "```json.*?```", "_(layout updated — applied to the designer)_", RegexOptions.Singleline).Trim();
        return (reply.Length == 0 ? "Done." : reply, layout);
    }

    private DashboardLayout? TryParseLayout(string text)
    {
        var match = Regex.Match(text, "```json\\s*(?<json>.*?)```", RegexOptions.Singleline);
        var json = match.Success ? match.Groups["json"].Value : null;
        if (json is null)
        {
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            json = text[start..(end + 1)];
            if (!json.Contains("\"tabs\"", StringComparison.OrdinalIgnoreCase)) return null;
        }
        try
        {
            var layout = JsonSerializer.Deserialize<DashboardLayout>(json, TableData.JsonOpts);
            if (layout is null || layout.Tabs.Count == 0) return null;
            foreach (var tab in layout.Tabs)
                foreach (var p in tab.Panels)
                {
                    p.W = Math.Clamp(p.W, 1, 12);
                    p.H = Math.Clamp(p.H, 2, 12);
                    p.X = Math.Clamp(p.X, 0, 12 - p.W);
                    if (string.IsNullOrWhiteSpace(p.Id)) p.Id = Guid.NewGuid().ToString("N")[..8];
                }
            return layout;
        }
        catch { return null; }
    }

    private async Task<string> DescribeDatasetsAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var all = await db.Datasets.ToListAsync(ct);
        var sb = new StringBuilder();
        foreach (var ds in all)
        {
            var schema = ds.SchemaJson;
            if (string.IsNullOrWhiteSpace(schema))
            {
                try
                {
                    var data = await datasets.GetDataAsync(ds.Id, ct: ct);
                    schema = data.SchemaJson();
                }
                catch { schema = "[]"; }
            }
            var cols = "?";
            try
            {
                var parsed = JsonSerializer.Deserialize<List<ColumnDef>>(schema!, TableData.JsonOpts) ?? [];
                cols = string.Join(", ", parsed.Select(c => $"{c.Name}({c.Type})"));
            }
            catch { }
            sb.AppendLine($"- datasetId {ds.Id}: {ds.Name} — columns: {cols}");
        }
        return sb.Length == 0 ? "(no datasets)" : sb.ToString();
    }
}
