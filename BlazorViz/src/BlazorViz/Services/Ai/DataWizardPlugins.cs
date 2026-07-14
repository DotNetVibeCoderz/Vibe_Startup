using System.ComponentModel;
using System.Text.RegularExpressions;
using BlazorViz.Data;
using BlazorViz.Models;
using Jint;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace BlazorViz.Services.Ai;

/// <summary>Math helpers for the Data Wizard.</summary>
public sealed class MathPlugin
{
    [KernelFunction("evaluate")]
    [Description("Evaluates a mathematical expression, e.g. '2 * (3 + 4)' or 'Math.sqrt(144) * 0.5'. Returns the numeric result.")]
    public string Evaluate([Description("JavaScript-style math expression")] string expression)
    {
        try
        {
            var engine = new Engine(o => o.TimeoutInterval(TimeSpan.FromSeconds(5)));
            var result = engine.Evaluate(expression).ToObject();
            return Convert.ToString(result, System.Globalization.CultureInfo.InvariantCulture) ?? "null";
        }
        catch (Exception ex) { return "Error: " + ex.Message; }
    }

    [KernelFunction("percentage_change")]
    [Description("Computes the percentage change from an old value to a new value.")]
    public string PercentageChange(double oldValue, double newValue) =>
        oldValue == 0 ? "undefined (old value is 0)" : $"{Math.Round((newValue - oldValue) / Math.Abs(oldValue) * 100, 2)}%";
}

/// <summary>Date & time helpers.</summary>
public sealed class DateTimePlugin
{
    [KernelFunction("now")]
    [Description("Returns the current date and time (local server time and UTC).")]
    public string Now() => $"Local: {DateTime.Now:yyyy-MM-dd HH:mm:ss (dddd)} | UTC: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";

    [KernelFunction("add_days")]
    [Description("Adds (or subtracts, if negative) days to a date. Date format: yyyy-MM-dd.")]
    public string AddDays(string date, int days) =>
        DateTime.TryParse(date, out var d) ? d.AddDays(days).ToString("yyyy-MM-dd (dddd)") : "Invalid date.";

    [KernelFunction("days_between")]
    [Description("Number of days between two dates (yyyy-MM-dd).")]
    public string DaysBetween(string from, string to) =>
        DateTime.TryParse(from, out var f) && DateTime.TryParse(to, out var t)
            ? ((t - f).Days).ToString() : "Invalid date(s).";
}

/// <summary>Internet search (Tavily) and URL scraping.</summary>
public sealed class WebPlugin(IHttpClientFactory httpFactory, Microsoft.Extensions.Options.IOptionsMonitor<AiOptions> aiOptions)
{
    [KernelFunction("search_internet")]
    [Description("Searches the internet via the Tavily API and returns a short answer plus the top results as 'title — url: snippet' lines.")]
    public async Task<string> SearchAsync([Description("search query")] string query)
    {
        var opts = aiOptions.CurrentValue.Tavily;
        if (string.IsNullOrWhiteSpace(opts.ApiKey))
            return "Internet search is not configured: set Ai:Tavily:ApiKey in appsettings.json (get a key at https://tavily.com).";

        var client = httpFactory.CreateClient("connector");
        using var req = new HttpRequestMessage(HttpMethod.Post, opts.Endpoint)
        {
            Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new
            {
                query,
                search_depth = opts.SearchDepth,
                max_results = Math.Clamp(opts.MaxResults, 1, 10),
                include_answer = true
            }), System.Text.Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiKey);

        using var res = await client.SendAsync(req);
        if (!res.IsSuccessStatusCode)
            return $"Tavily search failed ({(int)res.StatusCode} {res.ReasonPhrase}). Check the Ai:Tavily:ApiKey configuration.";

        using var doc = System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var sb = new System.Text.StringBuilder();

        if (root.TryGetProperty("answer", out var answer) &&
            answer.ValueKind == System.Text.Json.JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(answer.GetString()))
            sb.AppendLine($"Answer: {answer.GetString()}\n");

        if (root.TryGetProperty("results", out var results) &&
            results.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var r in results.EnumerateArray())
            {
                var title = r.TryGetProperty("title", out var t) ? t.GetString() : null;
                var url = r.TryGetProperty("url", out var u) ? u.GetString() : null;
                var content = r.TryGetProperty("content", out var c) ? c.GetString() : null;
                if (content is { Length: > 300 }) content = content[..300] + "…";
                sb.AppendLine($"- {title} — {url}: {content}");
            }
        }
        return sb.Length == 0 ? "No results found." : sb.ToString().TrimEnd();
    }

    [KernelFunction("scrape_url")]
    [Description("Fetches a web page and returns its readable text content (truncated).")]
    public async Task<string> ScrapeAsync([Description("absolute http(s) URL")] string url)
    {
        var client = httpFactory.CreateClient("connector");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (BlazorViz DataWizard)");
        var html = await client.GetStringAsync(url);
        var text = Strip(Regex.Replace(html, "<(script|style|nav|footer)[^>]*>.*?</\\1>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase));
        return text.Length > 6000 ? text[..6000] + " …(truncated)" : text;
    }

    private static string Strip(string html)
    {
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }
}

/// <summary>Lets the Data Wizard inspect and query the user's datasets and dashboards.</summary>
public sealed class DataQueryPlugin(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    DatasetService datasets)
{
    [KernelFunction("list_datasets")]
    [Description("Lists all datasets with their id, name and description.")]
    public async Task<string> ListDatasetsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var all = await db.Datasets.Select(d => new { d.Id, d.Name, d.Description }).ToListAsync();
        return all.Count == 0 ? "No datasets." :
            string.Join("\n", all.Select(d => $"- [{d.Id}] {d.Name}: {d.Description}"));
    }

    [KernelFunction("get_dataset_schema")]
    [Description("Returns the column names and types of a dataset.")]
    public async Task<string> GetSchemaAsync([Description("dataset id")] int datasetId)
    {
        var data = await datasets.GetDataAsync(datasetId);
        return string.Join("\n", data.Columns.Select(c => $"- {c.Name} ({c.Type})")) + $"\nRows: {data.RowCount}";
    }

    [KernelFunction("preview_dataset")]
    [Description("Returns the first rows of a dataset as JSON.")]
    public async Task<string> PreviewAsync([Description("dataset id")] int datasetId, [Description("max rows, default 10")] int limit = 10)
    {
        var data = await datasets.GetDataAsync(datasetId);
        return data.Head(Math.Clamp(limit, 1, 50)).ToJson();
    }

    [KernelFunction("aggregate_dataset")]
    [Description("Groups a dataset and aggregates metrics. aggs format: 'sum:Revenue,avg:Price,count:*'. Returns JSON rows.")]
    public async Task<string> AggregateAsync(
        [Description("dataset id")] int datasetId,
        [Description("comma separated group-by columns")] string groupBy,
        [Description("aggregations like sum:Revenue,count:*")] string aggs)
    {
        var data = await datasets.GetDataAsync(datasetId);
        var result = EtlService.Aggregate(data, groupBy, aggs);
        return result.Head(100).ToJson();
    }

    [KernelFunction("filter_dataset")]
    [Description("Filters a dataset by a condition and returns matching rows as JSON. Operators: =, !=, >, >=, <, <=, contains, in.")]
    public async Task<string> FilterAsync(int datasetId, string field, string op, string value, int limit = 20)
    {
        var data = await datasets.GetDataAsync(datasetId);
        return EtlService.FilterBy(data, field, op, value).Head(Math.Clamp(limit, 1, 100)).ToJson();
    }

    [KernelFunction("list_dashboards")]
    [Description("Lists all dashboards with id, name, description and tab/panel counts.")]
    public async Task<string> ListDashboardsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var all = await db.Dashboards.ToListAsync();
        return all.Count == 0 ? "No dashboards." : string.Join("\n", all.Select(d =>
        {
            var layout = DashboardLayout.Parse(d.LayoutJson);
            return $"- [{d.Id}] {d.Name}: {d.Description} ({layout.Tabs.Count} tabs, {layout.Tabs.Sum(t => t.Panels.Count)} panels)";
        }));
    }
}
