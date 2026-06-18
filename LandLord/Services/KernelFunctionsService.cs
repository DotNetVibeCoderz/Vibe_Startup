using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LandLord.Data;
using LandLord.Models;
using Microsoft.EntityFrameworkCore;

namespace LandLord.Services;

/// <summary>
/// Implementasi kernel functions untuk chatbot Frengky Ganteng
/// Mendukung: Tavily Search, Web Scraping, File Reading, Database Query
/// </summary>
public class KernelFunctionsService : IKernelFunctionsService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KernelFunctionsService> _logger;

    private readonly string? _tavilyApiKey;
    private readonly string _tavilyBaseUrl;
    private readonly string _tavilySearchDepth;
    private readonly bool _tavilyIncludeAnswer;
    private readonly int _tavilyMaxResults;
    private readonly int _tavilyTimeoutSeconds;

    private static readonly string[] _internetSearchTriggers = new[]
    {
        "cari di internet", "search", "googling", "cari tahu", "cari informasi",
        "berita tentang", "berita terbaru", "info terkini", "tren", "trending",
        "harga pasar", "harga properti", "harga tanah", "nilai properti",
        "berita", "news", "update", "latest", "what is", "apa itu",
        "regulasi", "peraturan", "uu", "undang-undang", "kebijakan",
        "pajak terbaru", "aturan", "permen", "pp", "perpres"
    };

    public KernelFunctionsService(HttpClient httpClient, IConfiguration configuration,
        IServiceScopeFactory scopeFactory, ILogger<KernelFunctionsService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
        _logger = logger;

        _tavilyApiKey = configuration.GetValue<string>("Tavily:ApiKey");
        _tavilyBaseUrl = configuration.GetValue<string>("Tavily:BaseUrl") ?? "https://api.tavily.com";
        _tavilySearchDepth = configuration.GetValue<string>("Tavily:SearchDepth") ?? "advanced";
        _tavilyIncludeAnswer = configuration.GetValue<bool>("Tavily:IncludeAnswer", true);
        _tavilyMaxResults = configuration.GetValue<int>("Tavily:MaxResults", 5);
        _tavilyTimeoutSeconds = configuration.GetValue<int>("Tavily:TimeoutSeconds", 30);
    }

    public List<KernelFunctionDefinition> GetAvailableFunctions()
    {
        return new List<KernelFunctionDefinition>
        {
            new() { Name = "tavily_search", DisplayName = "🌐 Cari di Internet (Tavily)", Description = "Mencari informasi terkini via Tavily Search API.", Parameters = new List<KernelFunctionParameter> { new() { Name = "query", Description = "Kata kunci pencarian", Required = true } } },
            new() { Name = "scrape_webpage", DisplayName = "📄 Baca Halaman Web", Description = "Mengambil konten dari URL.", Parameters = new List<KernelFunctionParameter> { new() { Name = "url", Description = "URL halaman web", Required = true } } },
            new() { Name = "read_file_from_url", DisplayName = "📎 Baca File dari URL", Description = "Membaca file dari URL.", Parameters = new List<KernelFunctionParameter> { new() { Name = "url", Description = "URL file", Required = true }, new() { Name = "fileType", Description = "Tipe: pdf, doc, txt", Required = false } } },
            new() { Name = "query_tanah_database", DisplayName = "🏞️ Cari Data Tanah", Description = "Mencari data tanah.", Parameters = new List<KernelFunctionParameter> { new() { Name = "keyword", Description = "Keyword", Required = true } } },
            new() { Name = "query_bangunan_database", DisplayName = "🏗️ Cari Data Bangunan", Description = "Mencari data bangunan.", Parameters = new List<KernelFunctionParameter> { new() { Name = "keyword", Description = "Keyword", Required = true } } }
        };
    }

    public async Task<FunctionResult> ExecuteAsync(string functionName, Dictionary<string, object?> parameters)
    {
        return functionName switch
        {
            "tavily_search" => await TavilySearchAsync(parameters),
            "scrape_webpage" => await ScrapeWebpageAsync(parameters),
            "read_file_from_url" => await ReadFileFromUrlAsync(parameters),
            "query_tanah_database" => await QueryTanahDatabaseAsync(parameters),
            "query_bangunan_database" => await QueryBangunanDatabaseAsync(parameters),
            _ => FunctionResult.Fail(functionName, $"Fungsi '{functionName}' tidak dikenal.")
        };
    }

    // ================================================================
    // ✅ TAVILY — Header Authorization: Bearer
    // ================================================================

    private async Task<FunctionResult> TavilySearchAsync(Dictionary<string, object?> parameters)
    {
        var query = parameters.GetValueOrDefault("query")?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(query)) return FunctionResult.Fail("tavily_search", "Query kosong.");

        if (string.IsNullOrWhiteSpace(_tavilyApiKey))
            return FunctionResult.Ok("tavily_search", "⚠️ **Tavily API key belum dikonfigurasi.**\nSetup di Settings → Tavily API Key.\n🔍 Query: \"" + query + "\"");

        try
        {
            var req = new TavilySearchRequest { Query = query, SearchDepth = _tavilySearchDepth, IncludeAnswer = _tavilyIncludeAnswer, MaxResults = _tavilyMaxResults };
            var json = JsonSerializer.Serialize(req);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_tavilyTimeoutSeconds));
            var httpReq = new HttpRequestMessage(HttpMethod.Post, $"{_tavilyBaseUrl}/search") { Content = content };

            // ✅ Tavily API: Authorization: Bearer <key>
            httpReq.Headers.Add("Authorization", $"Bearer {_tavilyApiKey}");

            var resp = await _httpClient.SendAsync(httpReq, cts.Token);
            if (!resp.IsSuccessStatusCode) return FunctionResult.Fail("tavily_search", $"Status: {(int)resp.StatusCode}.");

            var result = await resp.Content.ReadFromJsonAsync<TavilySearchResponse>(cancellationToken: cts.Token);
            if (result == null) return FunctionResult.Fail("tavily_search", "Hasil kosong.");

            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(result.Answer)) { sb.AppendLine($"💡 **Ringkasan:**\n{result.Answer}\n"); }
            sb.AppendLine($"🔍 **{result.Results.Count} hasil ({result.ResponseTime:F2}s):**\n");
            for (int i = 0; i < result.Results.Count; i++)
            { var r = result.Results[i]; sb.AppendLine($"**{i + 1}. [{r.Title}]({r.Url})** ⭐{r.Score:F2}\n> {r.Content.Trim()}\n"); }
            sb.AppendLine($"---\n📊 *Tavily • {DateTime.Now:dd MMM HH:mm}*");
            return FunctionResult.Ok("tavily_search", sb.ToString());
        }
        catch (OperationCanceledException) { return FunctionResult.Fail("tavily_search", $"Timeout ({_tavilyTimeoutSeconds}s)."); }
        catch (Exception ex) { _logger.LogError(ex, "Tavily error"); return FunctionResult.Fail("tavily_search", ex.Message); }
    }

    private async Task<FunctionResult> ScrapeWebpageAsync(Dictionary<string, object?> parameters)
    {
        var url = parameters.GetValueOrDefault("url")?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return FunctionResult.Fail("scrape_webpage", "URL tidak valid.");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var resp = await _httpClient.GetAsync(uri, cts.Token);
            if (!resp.IsSuccessStatusCode) return FunctionResult.Fail("scrape_webpage", $"Status: {(int)resp.StatusCode}.");
            var html = await resp.Content.ReadAsStringAsync(cts.Token);
            var text = ExtractTextFromHtml(html); if (text.Length > 5000) text = text[..5000] + "\n...";
            return FunctionResult.Ok("scrape_webpage", $"📄 **{ExtractTitleFromHtml(html) ?? uri.Host}**\n🔗 {url}\n\n{text}");
        }
        catch (Exception ex) { return FunctionResult.Fail("scrape_webpage", ex.Message); }
    }

    private async Task<FunctionResult> ReadFileFromUrlAsync(Dictionary<string, object?> parameters)
    {
        var url = parameters.GetValueOrDefault("url")?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(url)) return FunctionResult.Fail("read_file_from_url", "URL kosong.");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var resp = await _httpClient.GetAsync(url, cts.Token);
            if (!resp.IsSuccessStatusCode) return FunctionResult.Fail("read_file_from_url", $"Status: {(int)resp.StatusCode}.");
            var ct = resp.Content.Headers.ContentType?.MediaType ?? "";
            if (ct.Contains("pdf") || url.EndsWith(".pdf")) return FunctionResult.Ok("read_file_from_url", $"📎 PDF: {url}\n📏 {resp.Content.Headers.ContentLength ?? 0:N0} bytes");
            var text = await resp.Content.ReadAsStringAsync(cts.Token);
            if (ct.Contains("html")) text = ExtractTextFromHtml(text);
            if (text.Length > 5000) text = text[..5000] + "\n...";
            return FunctionResult.Ok("read_file_from_url", $"📎 **File**\n🔗 {url}\n\n{text}");
        }
        catch (Exception ex) { return FunctionResult.Fail("read_file_from_url", ex.Message); }
    }

    private async Task<FunctionResult> QueryTanahDatabaseAsync(Dictionary<string, object?> parameters)
    {
        var kw = (parameters.GetValueOrDefault("keyword")?.ToString() ?? "").ToLower();
        if (string.IsNullOrWhiteSpace(kw)) return FunctionResult.Fail("query_tanah_database", "Keyword kosong.");
        using var scope = _scopeFactory.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var results = await db.Tanah.Where(t => t.NomorSertifikat.ToLower().Contains(kw) || t.Lokasi.ToLower().Contains(kw) || t.Pemilik.ToLower().Contains(kw) || (t.NIB != null && t.NIB.ToLower().Contains(kw)) || (t.Kelurahan != null && t.Kelurahan.ToLower().Contains(kw))).Take(5).ToListAsync();
        if (!results.Any()) return FunctionResult.Ok("query_tanah_database", $"🔍 Tidak ditemukan: \"{kw}\".");
        var sb = new StringBuilder(); sb.AppendLine($"🏞️ **{results.Count} data tanah:**\n");
        foreach (var t in results) sb.AppendLine($"📋 **{t.NomorSertifikat}** | {t.JenisHak} | {t.Luas:N0} m² | {t.Lokasi} | {t.Pemilik}");
        return FunctionResult.Ok("query_tanah_database", sb.ToString());
    }

    private async Task<FunctionResult> QueryBangunanDatabaseAsync(Dictionary<string, object?> parameters)
    {
        var kw = (parameters.GetValueOrDefault("keyword")?.ToString() ?? "").ToLower();
        if (string.IsNullOrWhiteSpace(kw)) return FunctionResult.Fail("query_bangunan_database", "Keyword kosong.");
        using var scope = _scopeFactory.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var results = await db.Bangunan.Where(b => b.NomorIimbPbg.ToLower().Contains(kw) || b.Lokasi.ToLower().Contains(kw) || (b.NamaPemilik != null && b.NamaPemilik.ToLower().Contains(kw)) || b.JenisBangunan.ToLower().Contains(kw)).Take(5).ToListAsync();
        if (!results.Any()) return FunctionResult.Ok("query_bangunan_database", $"🔍 Tidak ditemukan: \"{kw}\".");
        var sb = new StringBuilder(); sb.AppendLine($"🏗️ **{results.Count} data bangunan:**\n");
        foreach (var b in results) sb.AppendLine($"📋 **{b.NomorIimbPbg}** | {b.JenisBangunan} | {b.LuasBangunan:N0} m² | {b.Lokasi}");
        return FunctionResult.Ok("query_bangunan_database", sb.ToString());
    }

    public bool ShouldSearchInternet(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage)) return false;
        var lower = userMessage.ToLower();
        foreach (var t in _internetSearchTriggers) if (lower.Contains(t)) return true;
        if (lower.Contains("http://") || lower.Contains("https://")) return true;
        return (lower.Contains("harga") || lower.Contains("nilai")) && (lower.Contains("sekarang") || lower.Contains("saat ini") || lower.Contains("terkini") || lower.Contains("terbaru"));
    }

    private static string ExtractTextFromHtml(string html)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(html, @"<script[^>]*>.*?</script>", "", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<style[^>]*>.*?</style>", "", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }

    private static string? ExtractTitleFromHtml(string html) => System.Text.RegularExpressions.Regex.Match(html, @"<title[^>]*>(.*?)</title>", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase).Groups[1].Value.Trim() is { Length: > 0 } t ? t : null;
}
