using System.ComponentModel;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LandLord.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace LandLord.Plugins;

/// <summary>
/// Semantic Kernel Plugin — Internet Search (Tavily), Web Scraper, File Reader
/// </summary>
public class InternetPlugin
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;
    private readonly ILogger<InternetPlugin>? _logger;

    private readonly string? _tavilyKey;
    private readonly string _tavilyUrl;
    private readonly string _tavilyDepth;
    private readonly bool _tavilyAnswer;
    private readonly int _tavilyMax, _tavilyTimeout;

    public InternetPlugin(HttpClient http, IConfiguration cfg, ILogger<InternetPlugin>? logger = null)
    {
        _http = http; _cfg = cfg; _logger = logger;

        _tavilyKey = cfg.GetValue<string>("Tavily:ApiKey");
        _tavilyUrl = cfg.GetValue<string>("Tavily:BaseUrl") ?? "https://api.tavily.com";
        _tavilyDepth = cfg.GetValue<string>("Tavily:SearchDepth") ?? "advanced";
        _tavilyAnswer = cfg.GetValue<bool>("Tavily:IncludeAnswer", true);
        _tavilyMax = cfg.GetValue<int>("Tavily:MaxResults", 5);
        _tavilyTimeout = cfg.GetValue<int>("Tavily:TimeoutSeconds", 30);
    }

    [KernelFunction("tavily_search")]
    [Description("Search the internet using Tavily API. Returns AI-summarized results with links.")]
    public async Task<string> TavilySearch(
        [Description("Search query")] string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return "❌ Query kosong.";
        if (string.IsNullOrWhiteSpace(_tavilyKey))
            return "⚠️ Tavily API key belum dikonfigurasi. Setup di Settings → Tavily.\n🔍 Query: \"" + query + "\"";

        try
        {
            var req = new TavilySearchRequest
            {
                Query = query,
                SearchDepth = _tavilyDepth,
                IncludeAnswer = _tavilyAnswer,
                MaxResults = _tavilyMax
            };

            var json = JsonSerializer.Serialize(req);
            var msg = new HttpRequestMessage(HttpMethod.Post, $"{_tavilyUrl}/search")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            // ✅ Tavily API pakai Authorization: Bearer, BUKAN api-key
            msg.Headers.Add("Authorization", $"Bearer {_tavilyKey}");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_tavilyTimeout));
            var resp = await _http.SendAsync(msg, cts.Token);

            if (!resp.IsSuccessStatusCode)
            {
                var errBody = await resp.Content.ReadAsStringAsync();
                _logger?.LogError("Tavily HTTP {Status}: {Error}", (int)resp.StatusCode, errBody);
                return $"❌ Tavily API error (Status: {(int)resp.StatusCode}). Pastikan API key valid.";
            }

            var result = await resp.Content.ReadFromJsonAsync<TavilySearchResponse>(cancellationToken: cts.Token);
            if (result == null) return "❌ Hasil kosong.";

            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(result.Answer))
            {
                sb.AppendLine($"💡 **Ringkasan AI:**");
                sb.AppendLine(result.Answer);
                sb.AppendLine();
            }

            sb.AppendLine($"🔍 **{result.Results.Count} hasil ({result.ResponseTime:F2}s):**");
            sb.AppendLine();

            for (int i = 0; i < result.Results.Count; i++)
            {
                var r = result.Results[i];
                sb.AppendLine($"**{i + 1}. [{r.Title}]({r.Url})** ⭐{r.Score:F2}");
                sb.AppendLine($"> {r.Content.Trim()}");
                sb.AppendLine();
            }

            sb.AppendLine($"---");
            sb.AppendLine($"📊 *Via Tavily Search • {DateTime.Now:dd MMM HH:mm}*");

            return sb.ToString();
        }
        catch (OperationCanceledException) { return $"❌ Timeout ({_tavilyTimeout}s)."; }
        catch (Exception ex) { _logger?.LogError(ex, "Tavily error"); return $"❌ {ex.Message}"; }
    }

    [KernelFunction("scrape_webpage")]
    [Description("Scrape and extract text from a web page URL.")]
    public async Task<string> ScrapeWebpage(
        [Description("Full URL to scrape")] string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return "❌ URL tidak valid.";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var resp = await _http.GetAsync(uri, cts.Token);
            if (!resp.IsSuccessStatusCode) return $"❌ Status: {(int)resp.StatusCode}.";
            var html = await resp.Content.ReadAsStringAsync(cts.Token);
            var text = StripHtml(html);
            if (text.Length > 5000) text = text[..5000] + "\n... (dipotong)";
            var title = GetTitle(html) ?? uri.Host;
            return $"📄 **{title}**\n🔗 {url}\n\n{text}";
        }
        catch (Exception ex) { return $"❌ {ex.Message}"; }
    }

    [KernelFunction("read_file_from_url")]
    [Description("Read a file (PDF/DOC/TXT/HTML) from URL.")]
    public async Task<string> ReadFileFromUrl(
        [Description("File URL")] string url,
        [Description("File type hint (pdf, doc, txt, html)")] string? fileType = null)
    {
        if (string.IsNullOrWhiteSpace(url)) return "❌ URL kosong.";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var resp = await _http.GetAsync(url, cts.Token);
            if (!resp.IsSuccessStatusCode) return $"❌ Status: {(int)resp.StatusCode}.";
            var ct = resp.Content.Headers.ContentType?.MediaType ?? "";
            if (ct.Contains("pdf") || fileType == "pdf" || url.EndsWith(".pdf"))
                return $"📎 **PDF terdeteksi**\n🔗 {url}\n📏 {resp.Content.Headers.ContentLength ?? 0:N0} bytes\n\n_Gunakan library PDF reader untuk ekstrak teks._";
            var text = await resp.Content.ReadAsStringAsync(cts.Token);
            if (ct.Contains("html") || url.EndsWith(".html")) text = StripHtml(text);
            if (text.Length > 5000) text = text[..5000] + "\n...";
            return $"📎 **File Content**\n🔗 {url}\n📋 {ct}\n\n{text}";
        }
        catch (Exception ex) { return $"❌ {ex.Message}"; }
    }

    private static string StripHtml(string h)
    {
        h = Regex.Replace(h, @"<script[^>]*>.*?</script>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        h = Regex.Replace(h, @"<style[^>]*>.*?</style>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        h = Regex.Replace(h, @"<[^>]+>", " ");
        h = Regex.Replace(h, @"\s+", " ");
        return h.Trim();
    }
    private static string? GetTitle(string h) => Regex.Match(h, @"<title[^>]*>(.*?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase).Groups[1].Value.Trim() is { Length: > 0 } t ? t : null;
}
