using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace FitnessCenter.Services.ChatBot;

/// <summary>
/// Kernel Functions untuk web search (Tavily), web scraping, dan membaca file dari URL.
/// </summary>
public class WebPlugin
{
    private readonly IHttpClientFactory _httpClient;
    private readonly IConfiguration _config;

    public WebPlugin(IHttpClientFactory httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;
    }

    [KernelFunction("search_internet")]
    [Description("Mencari informasi di internet menggunakan Tavily Search API.")]
    public async Task<string> SearchInternetAsync(
        [Description("Query pencarian")] string query,
        [Description("Jumlah hasil (default 5, max 10)")] int maxResults = 5)
    {
        var apiKey = _config.GetValue<string>("Tavily:ApiKey");
        if (string.IsNullOrEmpty(apiKey))
            return "⚠️ Tavily API key belum dikonfigurasi di appsettings.json.";

        try
        {
            var client = _httpClient.CreateClient();
            var payload = new
            {
                api_key = apiKey, query,
                max_results = Math.Min(maxResults, 10),
                search_depth = "basic", include_answer = true
            };

            var response = await client.PostAsJsonAsync("https://api.tavily.com/search", payload);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var result = "🔍 Hasil Pencarian:\n\n";
            if (root.TryGetProperty("answer", out var answer) && !string.IsNullOrWhiteSpace(answer.GetString()))
                result += $"💡 Ringkasan: {answer.GetString()}\n\n";

            if (root.TryGetProperty("results", out var results))
            {
                int i = 1;
                foreach (var r in results.EnumerateArray())
                {
                    var title = r.TryGetProperty("title", out var t) ? t.GetString() : "No title";
                    var url = r.TryGetProperty("url", out var u) ? u.GetString() : "#";
                    var content = r.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                    if (content.Length > 200) content = content[..200] + "...";
                    result += $"{i}. 📰 {title}\n   🔗 {url}\n   📝 {content}\n\n";
                    i++;
                }
            }
            return result.TrimEnd();
        }
        catch (Exception ex) { return $"❌ Gagal mencari: {ex.Message}"; }
    }

    [KernelFunction("scrape_webpage")]
    [Description("Mengambil dan membaca konten dari halaman web.")]
    public async Task<string> ScrapeWebpageAsync(
        [Description("URL halaman web")] string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
            return "❌ URL tidak valid.";

        try
        {
            var client = _httpClient.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();
            var text = ExtractTextFromHtml(html);
            if (text.Length > 3000) text = text[..3000] + $"\n\n... (total {html.Length} karakter)";
            return $"📄 Konten dari {url}:\n\n{text}";
        }
        catch (Exception ex) { return $"❌ Gagal scrape: {ex.Message}"; }
    }

    [KernelFunction("read_file_from_url")]
    [Description("Membaca konten file dari URL (TXT, JSON, CSV, dll).")]
    public async Task<string> ReadFileFromUrlAsync(
        [Description("URL file")] string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl) || !Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
            return "❌ URL file tidak valid.";

        try
        {
            var client = _httpClient.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            var response = await client.GetAsync(fileUrl);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            var extension = Path.GetExtension(uri.AbsolutePath).ToLower();

            if (contentType.StartsWith("text/") || extension is ".txt" or ".csv" or ".json" or ".xml" or ".md")
            {
                var text = await response.Content.ReadAsStringAsync();
                if (text.Length > 3000) text = text[..3000] + $"\n\n... (total {text.Length} karakter)";
                return $"📄 {Path.GetFileName(uri.AbsolutePath)} ({contentType}):\n\n{text}";
            }

            return $"📎 {Path.GetFileName(uri.AbsolutePath)} — Tipe: {contentType}, Size: {(response.Content.Headers.ContentLength ?? 0) / 1024} KB";
        }
        catch (Exception ex) { return $"❌ Gagal baca file: {ex.Message}"; }
    }

    [KernelFunction("get_fitness_news")]
    [Description("Mendapatkan berita terbaru seputar fitness dan kesehatan.")]
    public async Task<string> GetFitnessNewsAsync()
        => await SearchInternetAsync("berita fitness kesehatan terbaru Indonesia", 5);

    [KernelFunction("get_exercise_info")]
    [Description("Mencari informasi teknik dan tips latihan tertentu.")]
    public async Task<string> GetExerciseInfoAsync(
        [Description("Nama latihan, misal: push-up, squat, deadlift")] string exercise)
        => await SearchInternetAsync($"how to do {exercise} correctly technique benefits", 5);

    private static string ExtractTextFromHtml(string html)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(html,
            @"<(script|style|noscript|iframe|svg)[^>]*?>.*?</\1>",
            "", System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text,
            @"</?(div|p|h[1-6]|br|li|tr|article|section)[^>]*?>",
            "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", "");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\n\s*\n", "\n\n");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"[ \t]+", " ");
        return text.Trim();
    }
}
