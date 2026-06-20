using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace PDA.Services.LLM.KernelPlugins;

public class WebSearchPlugin
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WebSearchPlugin> _logger;
    private const string TavilySearchUrl = "https://api.tavily.com/search";

    public WebSearchPlugin(IHttpClientFactory hcf, IConfiguration cfg, ILogger<WebSearchPlugin> log)
    { _httpClient = hcf.CreateClient("DefaultClient"); _configuration = cfg; _logger = log; }

    [KernelFunction("searchInternet")]
    [Description("Search the internet via Tavily. Use for current events, news, or info beyond your knowledge cutoff.")]
    public async Task<string> SearchInternetAsync(
        [Description("Search query")] string query,
        [Description("'basic' or 'advanced'")] string searchDepth = "basic",
        [Description("Max results 1-10")] int maxResults = 5,
        [Description("Include AI summary?")] bool includeAnswer = true)
    {
        try
        {
            // 🔑 Cek Tavily:ApiKey DULU, baru fallback ke LLM:Providers:Tavily:ApiKey
            var apiKey = _configuration.GetValue<string>("Tavily:ApiKey");
            if (string.IsNullOrWhiteSpace(apiKey))
                apiKey = _configuration.GetValue<string>("LLM:Providers:Tavily:ApiKey");

            if (string.IsNullOrWhiteSpace(apiKey))
                return "⚠️ Tavily API key belum di-set. Tambahkan `Tavily:ApiKey` di appsettings.json.";

            searchDepth = searchDepth.ToLower() == "advanced" ? "advanced" : "basic";
            maxResults = Math.Clamp(maxResults, 1, 10);

            var body = new { api_key = apiKey, query, search_depth = searchDepth, include_answer = includeAnswer, max_results = maxResults };
            var json = JsonSerializer.Serialize(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var resp = await _httpClient.PostAsync(TavilySearchUrl, new StringContent(json, Encoding.UTF8, "application/json"));

            if (!resp.IsSuccessStatusCode)
            { _logger.LogError("Tavily {Code}", (int)resp.StatusCode); return $"❌ Tavily error {resp.StatusCode}"; }

            var txt = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(txt);
            var root = doc.RootElement;

            var sb = new StringBuilder();
            sb.AppendLine($"🔍 **Search:** \"{query}\"");
            if (includeAnswer && root.TryGetProperty("answer", out var a) && !string.IsNullOrWhiteSpace(a.GetString())) sb.AppendLine($"💡 {a.GetString()}\n");

            if (root.TryGetProperty("results", out var arr))
            {
                var i = 1;
                foreach (var r in arr.EnumerateArray())
                {
                    var t = r.TryGetProperty("title", out var tt) ? tt.GetString() ?? "" : "";
                    var u = r.TryGetProperty("url", out var uu) ? uu.GetString() ?? "" : "";
                    var s = r.TryGetProperty("content", out var cc) ? cc.GetString() ?? "" : "";
                    if (s.Length > 300) s = s[..300] + "...";
                    sb.AppendLine($"**{i++}. {t}**\n🔗 {u}\n📝 {s}\n");
                }
            }
            if (root.TryGetProperty("response_time", out var rt)) sb.AppendLine($"⚡ {rt.GetDouble():F2}s");
            return sb.ToString();
        }
        catch (Exception ex) { _logger.LogError(ex, "Tavily fail"); return $"❌ {ex.Message}"; }
    }

    [KernelFunction("quickSearch")]
    [Description("Quick internet search: AI summary + top 3 results.")]
    public Task<string> QuickSearchAsync([Description("Query")] string query)
        => SearchInternetAsync(query, "basic", 3, true);
}
