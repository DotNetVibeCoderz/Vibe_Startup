using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PadelHub.Data;
using PadelHub.Models;

namespace PadelHub.Services;

/// <summary>
/// ChatBot service menggunakan Microsoft Semantic Kernel.
/// Mendukung OpenAI, Anthropic (via OpenAI compatible), Gemini, dan Ollama.
/// Kernel Functions menyediakan akses ke database, pencarian internet, scraping, kalkulasi, dll.
/// </summary>
public class ChatBotService : IDisposable
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ChatBotService> _logger;
    private Kernel? _kernel;
    private IChatCompletionService? _chatService;
    private ChatHistory? _chatHistory;
    private string _currentModel = "OpenAI";

    public ChatBotService(IConfiguration config, AppDbContext db,
        IHttpClientFactory httpClientFactory, ILogger<ChatBotService> logger)
    {
        _config = config; _db = db; _httpClientFactory = httpClientFactory; _logger = logger;
    }

    public void Initialize(string? modelProvider = null)
    {
        _currentModel = modelProvider ?? _config["AI:DefaultModel"] ?? "OpenAI";
        var builder = Kernel.CreateBuilder();

        switch (_currentModel)
        {
            case "Anthropic": RegisterAnthropic(builder); break;
            case "Gemini": RegisterGemini(builder); break;
            case "Ollama": RegisterOllama(builder); break;
            default: RegisterOpenAI(builder); break;
        }

        // Register all Kernel Function Plugins
        builder.Plugins.AddFromObject(new DatabaseQueryPlugin(_db), "DatabaseQuery");
        builder.Plugins.AddFromObject(new WebToolsPlugin(_httpClientFactory, _config, _logger), "WebTools");
        builder.Plugins.AddFromObject(new UtilityPlugin(), "Utility");

        _kernel = builder.Build();
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();
        _chatHistory = new ChatHistory(_config["ChatBot:SystemPrompt"] ?? "Kamu adalah Coach Sherly, asisten pelatih padel.");
    }

    /// <summary>
    /// Kirim pesan user dan dapatkan response dari AI dengan auto function calling.
    /// </summary>
    public async Task<string> SendMessageAsync(string userMessage)
    {
        if (_kernel == null || _chatService == null || _chatHistory == null) Initialize();
        _chatHistory!.AddUserMessage(userMessage);

        var maxHistory = _config.GetValue<int>("ChatBot:MaxHistoryMessages", 20);
        while (_chatHistory.Count > maxHistory + 2) _chatHistory.RemoveAt(1);

        // ⚡ KRITIS: FunctionChoiceBehavior.Auto() memungkinkan AI otomatis
        // memanggil Kernel Functions (database query, search, math, dll)
        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = _config.GetValue<double>("ChatBot:Temperature", 0.7),
            MaxTokens = _config.GetValue<int>("ChatBot:MaxTokens", 2000),
            TopP = _config.GetValue<double>("ChatBot:TopP", 0.9),
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(), // ⚡ Auto-invoke kernel functions
        };

        try
        {
            var response = await _chatService.GetChatMessageContentAsync(_chatHistory, settings, _kernel);
            _chatHistory.AddAssistantMessage(response.Content ?? "");
            return response.Content ?? "Maaf, tidak bisa merespon.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ChatBot error for {Model}", _currentModel);
            return $"⚠️ Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Kirim pesan dengan attachment gambar, dengan auto function calling.
    /// </summary>
    public async Task<string> SendMessageWithImageAsync(string userMessage, string imageUrl)
    {
        if (_kernel == null || _chatService == null || _chatHistory == null) Initialize();
        _chatHistory!.AddUserMessage(userMessage + $"\n[Gambar: {imageUrl}]");

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = _config.GetValue<double>("ChatBot:Temperature", 0.7),
            MaxTokens = _config.GetValue<int>("ChatBot:MaxTokens", 2000),
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(), // ⚡ Auto-invoke kernel functions
        };

        var response = await _chatService.GetChatMessageContentAsync(_chatHistory, settings, _kernel);
        _chatHistory.AddAssistantMessage(response.Content ?? "");
        return response.Content ?? "Maaf, tidak bisa menganalisis gambar.";
    }

    public void ResetSession() => _chatHistory = new ChatHistory(_config["ChatBot:SystemPrompt"] ?? "Kamu adalah Coach Sherly.");
    public void SwitchModel(string model) => Initialize(model);
    public ChatHistory? GetHistory() => _chatHistory;

    private void RegisterOpenAI(IKernelBuilder b)
    {
        var key = _config["AI:Models:OpenAI:ApiKey"]; var model = _config["AI:Models:OpenAI:ModelId"] ?? "gpt-4o";
        var ep = _config["AI:Models:OpenAI:Endpoint"];
        if (!string.IsNullOrEmpty(ep)) b.AddOpenAIChatCompletion(model, new Uri(ep), key ?? "");
        else b.AddOpenAIChatCompletion(model, key ?? "");
    }
    private void RegisterAnthropic(IKernelBuilder b) => b.AddOpenAIChatCompletion(_config["AI:Models:Anthropic:ModelId"] ?? "claude-3-opus-20240229", new Uri("https://api.anthropic.com/v1/"), _config["AI:Models:Anthropic:ApiKey"] ?? "");
    private void RegisterGemini(IKernelBuilder b) => b.AddOpenAIChatCompletion(_config["AI:Models:Gemini:ModelId"] ?? "gemini-1.5-pro", new Uri("https://generativelanguage.googleapis.com/v1beta/openai/"), _config["AI:Models:Gemini:ApiKey"] ?? "");
    private void RegisterOllama(IKernelBuilder b) => b.AddOpenAIChatCompletion(_config["AI:Models:Ollama:ModelId"] ?? "llama3", new Uri($"{_config["AI:Models:Ollama:Endpoint"] ?? "http://localhost:11434"}/v1/"), "ollama");

    public void Dispose() { _kernel = null; _chatService = null; _chatHistory = null; }
}

// ================================================================
// KERNEL FUNCTION PLUGINS (tidak diubah)
// ================================================================

/// <summary>Database Query Plugin - akses data PadelHub via Kernel Functions.</summary>
public class DatabaseQueryPlugin
{
    private readonly AppDbContext _db;
    public DatabaseQueryPlugin(AppDbContext db) => _db = db;

    [KernelFunction("get_clubs"), Description("Daftar semua klub padel di PadelHub.")]
    public async Task<string> GetClubsAsync()
    {
        var clubs = await _db.Clubs.OrderBy(c => c.Name).ToListAsync();
        if (!clubs.Any()) return "Belum ada klub terdaftar.";
        var r = "🎾 **Daftar Klub:**\n\n";
        foreach (var c in clubs) r += $"- **{c.Name}** | 📍 {c.City} | 📞 {c.Phone}\n";
        return r;
    }

    [KernelFunction("get_courts"), Description("Daftar lapangan padel, bisa difilter klub.")]
    public async Task<string> GetCourtsAsync([Description("ID klub, opsional")] int? clubId = null)
    {
        var q = _db.Courts.Include(c => c.Club).AsQueryable();
        if (clubId.HasValue) q = q.Where(c => c.ClubId == clubId.Value);
        var courts = await q.OrderBy(c => c.Club!.Name).ThenBy(c => c.Name).ToListAsync();
        if (!courts.Any()) return "Tidak ada lapangan.";
        var r = "🏟️ **Lapangan:**\n\n";
        foreach (var c in courts) r += $"- **{c.Name}** ({c.Club?.Name}) | {c.Type} | Rp {c.PricePerHour:N0}/jam\n";
        return r;
    }

    [KernelFunction("get_tournaments"), Description("Daftar turnamen, bisa filter status.")]
    public async Task<string> GetTournamentsAsync([Description("Status: Upcoming, Registration, InProgress, Completed")] string? status = null)
    {
        var q = _db.Tournaments.AsQueryable();
        if (!string.IsNullOrEmpty(status)) q = q.Where(t => t.Status == status);
        var list = await q.OrderBy(t => t.StartDate).ToListAsync();
        if (!list.Any()) return "Tidak ada turnamen.";
        var r = "🏆 **Turnamen:**\n\n";
        foreach (var t in list) r += $"- **{t.Name}** | {t.StartDate:dd MMM} | {t.Status} | Biaya: Rp {t.EntryFee:N0}\n";
        return r;
    }

    [KernelFunction("get_players"), Description("Daftar pemain + ranking, filter level.")]
    public async Task<string> GetPlayersAsync([Description("Level: Beginner,Intermediate,Advanced,Professional")] string? level = null, [Description("Max hasil")] int top = 10)
    {
        var q = _db.PlayerProfiles.Include(p => p.User).AsQueryable();
        if (!string.IsNullOrEmpty(level)) q = q.Where(p => p.Level == level);
        var list = await q.OrderBy(p => p.Ranking).Take(top).ToListAsync();
        if (!list.Any()) return "Tidak ada data.";
        var r = "👥 **Pemain:**\n\n";
        foreach (var p in list) r += $"- #{p.Ranking} **{p.User?.FullName}** | Rating:{p.Rating} | {p.Level} | W/L:{p.Wins}/{p.Losses}\n";
        return r;
    }

    [KernelFunction("get_coaches"), Description("Daftar pelatih + spesialisasi & tarif.")]
    public async Task<string> GetCoachesAsync()
    {
        var list = await _db.Coaches.Include(c => c.User).ToListAsync();
        if (!list.Any()) return "Belum ada pelatih.";
        var r = "👨‍🏫 **Pelatih:**\n\n";
        foreach (var c in list) r += $"- **{c.User?.FullName}** | {c.Specialization} | Rp {c.HourlyRate:N0}/jam\n";
        return r;
    }

    [KernelFunction("get_membership"), Description("Paket membership yang tersedia.")]
    public async Task<string> GetMembershipAsync()
    {
        var list = await _db.MembershipPackages.Where(p => p.IsActive).ToListAsync();
        if (!list.Any()) return "Belum ada paket.";
        var r = "💎 **Membership:**\n\n";
        foreach (var p in list) r += $"- **{p.Name}** | Rp {p.Price:N0}/{p.Type} | {p.MaxReservationsPerMonth} reservasi/bulan\n";
        return r;
    }

    [KernelFunction("get_financial_summary"), Description("Ringkasan keuangan PadelHub.")]
    public async Task<string> GetFinancialSummaryAsync()
    {
        var rev = await _db.Payments.Where(p => p.Status == "Success").SumAsync(p => p.Amount);
        var tx = await _db.Payments.CountAsync();
        var mem = await _db.UserMemberships.CountAsync(m => m.Status == "Active");
        return $"💰 **Keuangan:**\n- Pendapatan: **Rp {rev:N0}**\n- Transaksi: **{tx}**\n- Member Aktif: **{mem}**";
    }
}

/// <summary>Web Tools Plugin - pencarian internet & scraping.</summary>
public class WebToolsPlugin
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly ILogger _logger;
    public WebToolsPlugin(IHttpClientFactory http, IConfiguration config, ILogger logger) { _http = http; _config = config; _logger = logger; }

    [KernelFunction("search_internet"), Description("Cari info terkini via Tavily API.")]
    public async Task<string> SearchInternetAsync([Description("Query")] string query, [Description("Max hasil")] int maxResults = 5)
    {
        try
        {
            var apiKey = _config["Tavily:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
                return $"🔍 Pencarian \"{query}\": ⚙️ _Tavily API key belum dikonfigurasi._";

            var http = _http.CreateClient();
            var payload = JsonSerializer.Serialize(new { query, max_results = maxResults, api_key = apiKey });
            var resp = await http.PostAsync(_config["Tavily:Endpoint"] ?? "https://api.tavily.com/search",
                new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));
            var json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) return "⚠️ Pencarian gagal.";

            using var doc = JsonDocument.Parse(json);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"🔍 **Hasil: \"{query}\"**\n");
            if (doc.RootElement.TryGetProperty("results", out var arr))
            {
                int i = 1;
                foreach (var item in arr.EnumerateArray())
                {
                    var title = item.GetProperty("title").GetString() ?? "";
                    var url = item.GetProperty("url").GetString() ?? "";
                    var snippet = item.TryGetProperty("content", out var c) ? c.GetString() ?? "" : "";
                    if (snippet.Length > 200) snippet = snippet[..200] + "...";
                    sb.AppendLine($"{i}. **{title}**\n   {snippet}\n   🔗 {url}\n");
                    if (i++ >= maxResults) break;
                }
            }
            return sb.ToString();
        }
        catch (Exception ex) { return $"⚠️ Gagal: {ex.Message}"; }
    }

    [KernelFunction("scrape_webpage"), Description("Baca konten dari URL.")]
    public async Task<string> ScrapeWebpageAsync([Description("URL")] string url)
    {
        try
        {
            var http = _http.CreateClient("Scraper");
            var html = await (await http.GetAsync(url)).Content.ReadAsStringAsync();
            var text = StripHtml(html);
            if (text.Length > 5000) text = text[..5000] + "...";
            return $"📄 **{url}:**\n\n{text}";
        }
        catch (Exception ex) { return $"⚠️ Gagal: {ex.Message}"; }
    }

    [KernelFunction("read_file_url"), Description("Baca file dari URL.")]
    public async Task<string> ReadFileFromUrlAsync([Description("URL file")] string url)
    {
        try
        {
            var http = _http.CreateClient("FileReader");
            var resp = await http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return $"⚠️ HTTP {resp.StatusCode}";
            var bytes = await resp.Content.ReadAsByteArrayAsync();
            var ct = resp.Content.Headers.ContentType?.MediaType ?? "";
            if (ct.Contains("text") || ct.Contains("html"))
            {
                var text = StripHtml(System.Text.Encoding.UTF8.GetString(bytes));
                if (text.Length > 3000) text = text[..3000] + "...";
                return $"📄 **Isi:**\n\n{text}";
            }
            return $"📎 File: {bytes.Length / 1024} KB, tipe: {ct}\n🔗 {url}";
        }
        catch (Exception ex) { return $"⚠️ Gagal: {ex.Message}"; }
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        html = System.Text.RegularExpressions.Regex.Replace(html, @"<(script|style)[^>]*>.*?</\1>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        html = System.Text.RegularExpressions.Regex.Replace(html, @"<[^>]+>", " ");
        html = System.Net.WebUtility.HtmlDecode(html);
        html = System.Text.RegularExpressions.Regex.Replace(html, @"\s+", " ").Trim();
        return html;
    }
}

/// <summary>Utility Plugin - waktu, kalkulasi, konversi.</summary>
public class UtilityPlugin
{
    [KernelFunction("get_current_time"), Description("Waktu & tanggal saat ini.")]
    public string GetCurrentTime([Description("Timezone")] string tz = "Asia/Jakarta")
    {
        DateTime now;
        try { now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(tz switch { "Asia/Jakarta" => "SE Asia Standard Time", "UTC" => "UTC", _ => "SE Asia Standard Time" })); }
        catch { now = DateTime.Now; }
        return $"🕐 **{now:dddd, dd MMMM yyyy HH:mm:ss}** ({tz})\nUTC: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";
    }

    [KernelFunction("calculate_math"), Description("Kalkulasi matematika.")]
    public string CalculateMath([Description("Ekspresi. Contoh: 2+2*3, sqrt(144)")] string expr)
    {
        try
        {
            var e = expr.Replace("^", "**").Replace("pi", Math.PI.ToString()).Replace("Pi", Math.PI.ToString());
            e = System.Text.RegularExpressions.Regex.Replace(e, @"sqrt\(([^)]+)\)", m => Math.Sqrt(EvalSimple(m.Groups[1].Value)).ToString());
            var dt = new System.Data.DataTable();
            return $"🧮 `{expr}` = **{Convert.ToDouble(dt.Compute(e, ""))}**";
        }
        catch { return $"⚠️ Gagal menghitung \"{expr}\""; }
    }
    private static double EvalSimple(string e) { var dt = new System.Data.DataTable(); return Convert.ToDouble(dt.Compute(e.Replace("^", "**"), "")); }

    [KernelFunction("get_date_info"), Description("Info detail suatu tanggal.")]
    public string GetDateInfo([Description("yyyy-MM-dd, today, tomorrow")] string date)
    {
        var d = date.ToLower() switch { "today" => DateTime.Today, "tomorrow" => DateTime.Today.AddDays(1), "yesterday" => DateTime.Today.AddDays(-1), _ => DateTime.TryParse(date, out var p) ? p : DateTime.Today };
        var diff = (d.Date - DateTime.Today).Days;
        return $"📅 **{d:dddd, dd MMMM yyyy}** | Minggu ke-{System.Globalization.ISOWeek.GetWeekOfYear(d)} | Q{(d.Month + 2) / 3} | {(diff == 0 ? "Hari ini" : diff > 0 ? $"{diff} hari lagi" : $"{Math.Abs(diff)} hari lalu")}";
    }

    [KernelFunction("convert_currency"), Description("Konversi mata uang estimasi.")]
    public string ConvertCurrency([Description("Jumlah")] decimal amount, [Description("Dari")] string from, [Description("Ke")] string to)
    {
        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["IDR"] = 1, ["USD"] = 15700, ["EUR"] = 17000, ["SGD"] = 11800, ["JPY"] = 105, ["AUD"] = 10500, ["GBP"] = 20000 };
        if (!rates.ContainsKey(from) || !rates.ContainsKey(to)) return "⚠️ Mata uang tidak dikenal.";
        var result = (amount * rates[from]) / rates[to];
        return $"💱 {amount:N2} {from.ToUpper()} = **{result:N2} {to.ToUpper()}**\n_Rate estimasi, cek kurs terkini._";
    }
}
