using System.ComponentModel;
using AppBender.Core.Common;
using AppBender.Core.Services;
using Microsoft.SemanticKernel;

namespace AppBender.Core.AI;

/// <summary>Kernel function: arithmetic.</summary>
public class MathPlugin
{
    [KernelFunction("calculate")]
    [Description("Evaluates an arithmetic expression. Supports + - * / % ^, parentheses and functions like sqrt, round, min, max, abs, sin, cos, log.")]
    public string Calculate([Description("The arithmetic expression, e.g. '(2+3)*4' or 'round(19.117, 2)'")] string expression)
    {
        try { return MathEvaluator.Evaluate(expression).ToString(System.Globalization.CultureInfo.InvariantCulture); }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }
}

/// <summary>Kernel function: date & time.</summary>
public class DateTimePlugin
{
    [KernelFunction("current_datetime")]
    [Description("Gets the current date and time (local and UTC) plus the day of week.")]
    public string CurrentDateTime()
        => $"Local: {DateTime.Now:yyyy-MM-dd HH:mm:ss} ({DateTime.Now.DayOfWeek}), UTC: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";

    [KernelFunction("date_diff_days")]
    [Description("Number of days between two dates (yyyy-MM-dd).")]
    public string DateDiffDays(string fromDate, string toDate)
    {
        if (DateTime.TryParse(fromDate, out var from) && DateTime.TryParse(toDate, out var to))
            return ((to - from).TotalDays).ToString("0.##");
        return "Error: invalid date(s)";
    }
}

/// <summary>Kernel functions: internet search + page scraping (Tavily).</summary>
public class WebPlugin(IWebSearchClient search)
{
    [KernelFunction("search_internet")]
    [Description("Searches the internet and returns the top results with title, url and snippet. Use for current events or facts you don't know.")]
    public async Task<string> SearchInternet(
        [Description("The search query")] string query,
        [Description("Max results (default 5)")] int maxResults = 5)
    {
        if (!search.IsConfigured) return "Internet search is not configured (missing Tavily API key).";
        var results = await search.SearchAsync(query, Math.Clamp(maxResults, 1, 10));
        return results.Count == 0
            ? "No results."
            : string.Join("\n\n", results.Select(r => $"- {r.Title}\n  {r.Url}\n  {r.Snippet}"));
    }

    [KernelFunction("scrape_url")]
    [Description("Fetches a web page and returns its readable text content.")]
    public async Task<string> ScrapeUrl([Description("Absolute URL of the page")] string url)
    {
        try
        {
            var text = await search.ScrapeAsync(url);
            return text.Length > 8000 ? text[..8000] + "…" : text;
        }
        catch (Exception ex) { return $"Error fetching page: {ex.Message}"; }
    }
}

/// <summary>Kernel functions: query the Data Hub.</summary>
public class DatasetPlugin(IDataHubService dataHub)
{
    [KernelFunction("list_datasets")]
    [Description("Lists the Data Hub entities (datasets) with their fields.")]
    public async Task<string> ListDatasets()
    {
        var entities = await dataHub.GetEntitiesAsync();
        if (entities.Count == 0) return "No datasets exist yet.";
        return string.Join("\n", entities.Select(e =>
            $"- {e.Name} ({e.DisplayName}): fields = {string.Join(", ", e.Fields.Select(f => $"{f.Name}:{f.Type}"))}"));
    }

    [KernelFunction("query_dataset")]
    [Description("Queries records from a Data Hub entity. Returns JSON rows.")]
    public async Task<string> QueryDataset(
        [Description("Entity logical name, e.g. 'customers'")] string entity,
        [Description("Optional free-text search")] string? search = null,
        [Description("Optional filter 'field op value', ops: eq neq gt gte lt lte contains")] string? filter = null,
        [Description("Max rows (default 20)")] int top = 20)
    {
        try
        {
            var options = new QueryOptions { Search = search, PageSize = Math.Clamp(top, 1, 100) };
            if (!string.IsNullOrWhiteSpace(filter))
            {
                var parts = filter.Split(' ', 3);
                if (parts.Length >= 2)
                    options.Filters.Add(new FieldFilter { Field = parts[0], Op = parts[1], Value = parts.Length > 2 ? parts[2] : null });
            }
            var result = await dataHub.QueryAsync(entity, options);
            var rows = result.Records.Select(r =>
            {
                var d = r.Data;
                d["id"] = r.Id;
                return d;
            });
            return JsonUtil.Serialize(new { total = result.TotalCount, rows });
        }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }
}
