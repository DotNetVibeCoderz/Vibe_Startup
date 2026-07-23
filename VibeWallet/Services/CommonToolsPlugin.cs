using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using VibeWallet.Models;

namespace VibeWallet.Services;

/// <summary>
/// Plugin providing general tools and external API calls for Semantic Kernel.
/// Includes: Tavily search, web scraping, date/time, math, currency, file reading.
/// </summary>
public class CommonToolsPlugin
{
    private readonly IHttpClientFactory _http;
    private readonly ChatBotConfig _chatConfig;
    private readonly ILogger<CommonToolsPlugin> _logger;

    public CommonToolsPlugin(IHttpClientFactory http, IOptions<ChatBotConfig> chatConfig,
        ILogger<CommonToolsPlugin> logger)
    {
        _http = http;
        _chatConfig = chatConfig.Value;
        _logger = logger;
    }

    // ================================================================
    //  INTERNET SEARCH (Tavily)
    // ================================================================

    [KernelFunction("search_internet")]
    [Description("Mencari informasi terkini dari internet menggunakan Tavily Search API. Gunakan ini untuk mencari berita, promo terbaru, atau informasi yang tidak ada di database.")]
    [return: Description("Hasil pencarian dalam format JSON dengan title, url, dan content")]
    public async Task<string> SearchInternet(
        [Description("Kata kunci pencarian, gunakan bahasa Indonesia atau Inggris")] string query,
        [Description("Jumlah hasil (default 5, maks 10)")] int maxResults = 5)
    {
        var apiKey = _chatConfig.Tavily?.ApiKey;
        if (string.IsNullOrEmpty(apiKey))
            return "Maaf, fitur pencarian internet belum dikonfigurasi (Tavily API key belum diatur).";

        try
        {
            var client = _http.CreateClient();
            var payload = JsonSerializer.Serialize(new
            {
                query, api_key = apiKey,
                max_results = Math.Clamp(maxResults, 1, 10),
                search_depth = "basic"
            });

            var resp = await client.PostAsync(
                _chatConfig.Tavily?.Endpoint ?? "https://api.tavily.com/search",
                new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));

            if (!resp.IsSuccessStatusCode)
                return $"Pencarian gagal dengan kode {resp.StatusCode}.";

            var json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var results = doc.RootElement.GetProperty("results").EnumerateArray()
                .Select(r => new
                {
                    Judul = r.GetProperty("title").GetString(),
                    Url = r.GetProperty("url").GetString(),
                    Ringkasan = r.GetProperty("content").GetString()?[..Math.Min(r.GetProperty("content").GetString()?.Length ?? 0, 300)]
                });

            return JsonSerializer.Serialize(new { query, results });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tavily search failed for: {Query}", query);
            return $"Error pencarian: {ex.Message}";
        }
    }

    // ================================================================
    //  WEB SCRAPING
    // ================================================================

    [KernelFunction("scrap_web_page")]
    [Description("Mengambil dan mengekstrak teks dari halaman web. Berguna untuk membaca artikel atau halaman info.")]
    [return: Description("Teks yang diekstrak dari halaman web, maksimal 4000 karakter")]
    public async Task<string> ScrapWebPage(
        [Description("URL lengkap halaman web (termasuk https://)")] string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
            return "URL tidak valid. Harap berikan URL lengkap dengan http:// atau https://";

        try
        {
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            var html = await client.GetStringAsync(url);

            var text = System.Text.RegularExpressions.Regex.Replace(html,
                @"<script[^>]*>.*?</script>", " ",
                System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text,
                @"<style[^>]*>.*?</style>", " ",
                System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"&[a-z]+;", " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

            return text.Length > 4000
                ? text[..4000] + $"... (dipotong, total {text.Length} karakter)"
                : text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scraping failed for: {Url}", url);
            return $"Gagal mengambil halaman: {ex.Message}";
        }
    }

    // ================================================================
    //  DATE & TIME
    // ================================================================

    [KernelFunction("get_current_datetime")]
    [Description("Mendapatkan tanggal dan waktu saat ini. Default zona waktu Asia/Jakarta (WIB).")]
    [return: Description("Tanggal dan waktu dalam format lengkap Bahasa Indonesia")]
    public string GetCurrentDateTime(
        [Description("Zona waktu (default: Asia/Jakarta). Contoh: Asia/Jakarta, Asia/Makassar, Asia/Jayapura, UTC")] string timezone = "Asia/Jakarta")
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var offset = tz.GetUtcOffset(DateTime.UtcNow);
            var offsetStr = $"UTC{(offset >= TimeSpan.Zero ? "+" : "")}{offset.Hours:D2}:{offset.Minutes:D2}";

            return JsonSerializer.Serialize(new
            {
                Tanggal = local.ToString("dddd, dd MMMM yyyy"),
                Waktu = local.ToString("HH:mm:ss"),
                ZonaWaktu = timezone, Offset = offsetStr,
                HariIni = local.DayOfWeek.ToString(),
                UnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }
        catch
        {
            var now = DateTime.UtcNow.AddHours(7);
            return JsonSerializer.Serialize(new
            {
                Tanggal = now.ToString("dddd, dd MMMM yyyy"),
                Waktu = now.ToString("HH:mm:ss"),
                ZonaWaktu = "Asia/Jakarta", Offset = "UTC+07:00"
            });
        }
    }

    // ================================================================
    //  MATH CALCULATION
    // ================================================================

    [KernelFunction("calculate_math")]
    [Description("Melakukan kalkulasi matematika. Mendukung: +, -, *, /, %, pangkat, akar, sin, cos, tan, log, abs, round, pi, e. Contoh: '1000000 * 0.5 / 100', 'sqrt(144)', 'sin(pi/2)'")]
    [return: Description("Hasil kalkulasi")]
    public string CalculateMath(
        [Description("Ekspresi matematika yang akan dihitung")] string expression)
    {
        try
        {
            expression = expression.Replace("^", "**").Replace("sqrt", "Sqrt")
                                   .Replace("pi", "Pi").Replace("e", "E");
            var result = new System.Data.DataTable().Compute(expression, null);
            return JsonSerializer.Serialize(new
            {
                Ekspresi = expression, Hasil = result?.ToString() ?? "null",
                Tipe = result?.GetType().Name ?? "unknown"
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                Error = $"Tidak dapat menghitung: {ex.Message}",
                Ekspresi = expression,
                Tips = "Gunakan operator: + - * / %. Contoh: 1000 * 5, (100 + 50) * 2"
            });
        }
    }

    // ================================================================
    //  READ FILE FROM URL
    // ================================================================

    [KernelFunction("read_file_from_url")]
    [Description("Membaca isi file teks dari URL. Berguna untuk membaca CSV, TXT, atau file data.")]
    [return: Description("Isi file teks, maksimal 8000 karakter")]
    public async Task<string> ReadFileFromUrl(
        [Description("URL file yang ingin dibaca")] string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out _)) return "URL tidak valid.";
        try
        {
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var content = await client.GetStringAsync(url);
            return content.Length > 8000
                ? content[..8000] + $"\n\n... (dipotong, total {content.Length} karakter)"
                : content;
        }
        catch (Exception ex) { return $"Gagal membaca file: {ex.Message}"; }
    }

    // ================================================================
    //  CURRENCY EXCHANGE (simulated)
    // ================================================================

    [KernelFunction("get_exchange_rate")]
    [Description("Mendapatkan kurs mata uang asing ke Rupiah (IDR). Data simulasi untuk demo.")]
    [return: Description("Nilai tukar mata uang dalam format JSON")]
    public string GetExchangeRate(
        [Description("Kode mata uang: USD, SGD, EUR, JPY, MYR, AUD, GBP, CNY")] string currency = "USD")
    {
        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = 15750m, ["SGD"] = 11800m, ["EUR"] = 17200m,
            ["JPY"] = 105m,   ["MYR"] = 3400m,  ["AUD"] = 10400m,
            ["GBP"] = 20000m, ["CNY"] = 2180m
        };

        if (!rates.TryGetValue(currency.ToUpper(), out var rate))
            return $"Mata uang '{currency}' tidak dikenal. Tersedia: {string.Join(", ", rates.Keys)}";

        return JsonSerializer.Serialize(new
        {
            MataUang = currency.ToUpper(),
            NilaiTukar = $"Rp {rate:N0} per 1 {currency.ToUpper()}",
            UpdateTerakhir = DateTime.UtcNow.AddHours(7).ToString("dd MMM yyyy HH:mm") + " WIB",
            Disclaimer = "Data simulasi untuk demo. Gunakan data aktual untuk transaksi."
        });
    }
}
