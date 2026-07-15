using System.Text;
using System.Text.RegularExpressions;
using AppBender.Core.Common;
using Microsoft.Extensions.Configuration;

namespace AppBender.Core.AI;

/// <summary>Tavily search + simple readable-text scraping. API key: "Tavily:ApiKey".</summary>
public partial class TavilyWebSearchClient(IHttpClientFactory httpFactory, IConfiguration config) : IWebSearchClient
{
    public bool IsConfigured => !string.IsNullOrEmpty(config["Tavily:ApiKey"]);

    public async Task<List<WebSearchResult>> SearchAsync(string query, int maxResults = 5, CancellationToken ct = default)
    {
        var apiKey = config["Tavily:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("Tavily API key is not configured (Tavily:ApiKey).");

        var client = httpFactory.CreateClient("ai");
        var payload = JsonUtil.Serialize(new Dictionary<string, object?>
        {
            ["api_key"] = apiKey,
            ["query"] = query,
            ["max_results"] = Math.Clamp(maxResults, 1, 10),
            ["search_depth"] = "basic"
        });
        using var response = await client.PostAsync("https://api.tavily.com/search",
            new StringContent(payload, Encoding.UTF8, "application/json"), ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Tavily search failed ({(int)response.StatusCode}): {json}");

        var doc = System.Text.Json.JsonDocument.Parse(json);
        var results = new List<WebSearchResult>();
        if (doc.RootElement.TryGetProperty("results", out var items))
        {
            foreach (var item in items.EnumerateArray())
            {
                results.Add(new WebSearchResult(
                    item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                    item.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "",
                    item.TryGetProperty("content", out var c) ? c.GetString() ?? "" : ""));
            }
        }
        return results;
    }

    public async Task<string> ScrapeAsync(string url, CancellationToken ct = default)
    {
        var client = httpFactory.CreateClient("ai");
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (compatible; AppBender/1.0)");
        var html = await client.GetStringAsync(url, ct);
        return HtmlToText(html);
    }

    [GeneratedRegex(@"<(script|style|head|nav|footer|noscript)[^>]*>.*?</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex NonContentBlocks();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExtraNewlines();

    public static string HtmlToText(string html)
    {
        var text = NonContentBlocks().Replace(html, " ");
        text = Regex.Replace(text, @"<br\s*/?>|</p>|</div>|</h[1-6]>|</li>|</tr>", "\n", RegexOptions.IgnoreCase);
        text = Tags().Replace(text, " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = ExtraNewlines().Replace(text, "\n\n");
        return text.Trim();
    }
}
