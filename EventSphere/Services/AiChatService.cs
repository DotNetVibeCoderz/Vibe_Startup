using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using EventSphere.Data.Context;
using EventSphere.Data.Models;

namespace EventSphere.Services;

/// <summary>
/// AI Chat Service "Tante Sherly" - Full Semantic Kernel + AutoInvokeKernelFunctions
/// </summary>
public class AiChatService
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AiChatService> _logger;
    private Kernel? _kernel;
    private IChatCompletionService? _chatService;
    private string _currentProvider = "";

    public AiChatService(IConfiguration config, AppDbContext db,
        IHttpClientFactory httpClientFactory, ILogger<AiChatService> logger)
    { _config = config; _db = db; _httpClientFactory = httpClientFactory; _logger = logger; }

    private Kernel InitializeKernel(string? provider = null)
    {
        provider ??= _config.GetValue<string>("AI:DefaultProvider") ?? "OpenAI";
        if (_kernel != null && _currentProvider == provider) return _kernel;
        _currentProvider = provider;
        var builder = Kernel.CreateBuilder();

        switch (provider)
        {
            case "OpenAI":
                var oaKey = _config.GetValue<string>("AI:Providers:OpenAI:ApiKey") ?? "";
                var oaModel = _config.GetValue<string>("AI:Providers:OpenAI:Model") ?? "gpt-4o";
                if (!string.IsNullOrEmpty(oaKey)) builder.AddOpenAIChatCompletion(oaModel, oaKey);
                break;
            case "Gemini":
                var gmKey = _config.GetValue<string>("AI:Providers:Gemini:ApiKey") ?? "";
                var gmModel = _config.GetValue<string>("AI:Providers:Gemini:Model") ?? "gemini-2.0-flash";
                var gmEndpoint = _config.GetValue<string>("AI:Providers:Gemini:Endpoint") ?? "";
                if (!string.IsNullOrEmpty(gmKey))
                {
                    if (!string.IsNullOrEmpty(gmEndpoint))
                        builder.AddOpenAIChatCompletion(gmModel, gmEndpoint, gmKey);
                    else
                        builder.AddOpenAIChatCompletion(gmModel, gmKey);
                }
                break;
            case "Ollama":
                var olEndpoint = (_config.GetValue<string>("AI:Providers:Ollama:Endpoint") ?? "http://localhost:11434").TrimEnd('/');
                var olModel = _config.GetValue<string>("AI:Providers:Ollama:Model") ?? "llama3";
                builder.AddOpenAIChatCompletion(olModel, olEndpoint + "/v1", "ollama");
                break;
        }

        var funcs = new AiKernelFunctions(_httpClientFactory, _config, _db, _logger);
        builder.Plugins.AddFromObject(funcs, "EventSphere");

        _kernel = builder.Build();
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();
        _logger.LogInformation("SK Kernel init: {Provider}", provider);
        return _kernel;
    }

    public async Task<ChatBotMessage> ChatAsync(Guid sessionId, string userMessage,
        string? imageUrl = null, string? attachmentUrl = null,
        string? attachmentName = null, string? provider = null)
    {
        _db.ChatBotMessages.Add(new ChatBotMessage
        {
            SessionId = sessionId, Role = "User", Content = userMessage,
            ImageUrl = imageUrl, AttachmentUrl = attachmentUrl,
            AttachmentName = attachmentName, CreatedAt = DateTime.UtcNow
        });
        var session = await _db.ChatBotSessions.FindAsync(sessionId);
        if (session != null) { session.LastActivity = DateTime.UtcNow; session.ModelProvider = provider ?? _currentProvider; }
        await _db.SaveChangesAsync();

        var kernel = InitializeKernel(provider);
        var systemPrompt = _config.GetValue<string>("AI:ChatBot:SystemPrompt")
            ?? "Kamu Tante Sherly, asisten EventSphere.";

        var dbMsgs = await _db.ChatBotMessages
            .Where(m => m.SessionId == sessionId).OrderBy(m => m.CreatedAt).ToListAsync();

        var chatHistory = new ChatHistory(systemPrompt);
        foreach (var m in dbMsgs.Where(m => m.Role != "System"))
        {
            if (m.Role == "Assistant") { chatHistory.AddAssistantMessage(m.Content ?? ""); }
            else if (m.Role == "User")
            {
                if (!string.IsNullOrEmpty(m.ImageUrl))
                    chatHistory.AddUserMessage([new TextContent(m.Content ?? ""), new ImageContent(new Uri(m.ImageUrl))]);
                else
                    chatHistory.AddUserMessage(m.Content ?? "");
            }
        }

        string responseContent;
        try
        {
            var settings = new OpenAIPromptExecutionSettings
            {
                Temperature = _config.GetValue<double?>("AI:ChatBot:Temperature") ?? 0.7,
                MaxTokens = _config.GetValue<int?>("AI:ChatBot:MaxTokens") ?? 2000,
                TopP = _config.GetValue<double?>("AI:ChatBot:TopP") ?? 0.9,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            if (_currentProvider == "Anthropic")
                responseContent = await ChatWithAnthropicAsync(chatHistory, kernel);
            else if (_chatService != null)
            {
                var result = await _chatService.GetChatMessageContentAsync(chatHistory, settings, kernel);
                responseContent = result.Content ?? "Maaf, coba lagi ya!";
            }
            else
                responseContent = "⚠️ AI Service belum dikonfigurasi.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat error session={Id}", sessionId);
            responseContent = $"⚠️ Ups: {ex.Message} 🙏";
        }

        var am = new ChatBotMessage { SessionId = sessionId, Role = "Assistant", Content = responseContent, CreatedAt = DateTime.UtcNow };
        _db.ChatBotMessages.Add(am); await _db.SaveChangesAsync();
        return am;
    }

    private async Task<string> ChatWithAnthropicAsync(ChatHistory history, Kernel kernel)
    {
        var apiKey = _config.GetValue<string>("AI:Providers:Anthropic:ApiKey") ?? "";
        var model = _config.GetValue<string>("AI:Providers:Anthropic:Model") ?? "claude-3-5-sonnet-20241022";
        var endpoint = (_config.GetValue<string>("AI:Providers:Anthropic:Endpoint") ?? "https://api.anthropic.com").TrimEnd('/');
        if (string.IsNullOrEmpty(apiKey)) return "⚠️ Anthropic API key belum dikonfigurasi.";

        var sysPrompt = _config.GetValue<string>("AI:ChatBot:SystemPrompt") ?? "";
        var messages = new List<object>();
        foreach (var msg in history)
        {
            if (msg.Role == AuthorRole.System) continue;
            var imageItems = msg.Items.OfType<ImageContent>().ToList();
            var textContent = msg.Items.OfType<TextContent>().Select(t => t.Text).FirstOrDefault() ?? msg.Content ?? "";
            if (imageItems.Any())
            {
                var blocks = new List<object>();
                foreach (var img in imageItems) blocks.Add(new { type = "image", source = new { type = "url", url = img.Uri?.ToString() ?? "" } });
                blocks.Add(new { type = "text", text = textContent });
                messages.Add(new { role = msg.Role == AuthorRole.User ? "user" : "assistant", content = blocks.ToArray() });
            }
            else { messages.Add(new { role = msg.Role == AuthorRole.User ? "user" : "assistant", content = textContent }); }
        }

        var payload = new { model, system = sysPrompt, messages, max_tokens = 2000, temperature = 0.7 };
        var client = _httpClientFactory.CreateClient("Anthropic");
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        var resp = await client.PostAsync($"{endpoint}/v1/messages",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));
        if (!resp.IsSuccessStatusCode) return $"⚠️ Anthropic error: {resp.StatusCode}";

        var json = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("content")[0];
        if (content.GetProperty("type").GetString() == "tool_use")
        {
            var tName = content.GetProperty("name").GetString() ?? "";
            var tInput = content.GetProperty("input").GetRawText();
            var tResult = await ExecKernelFunc(kernel, tName, tInput);
            messages.Add(new { role = "assistant", content = new[] { new { type = "tool_use", name = tName, input = JsonSerializer.Deserialize<object>(tInput), id = content.GetProperty("id").GetString() } } });
            messages.Add(new { role = "user", content = new[] { new { type = "tool_result", tool_use_id = content.GetProperty("id").GetString(), content = tResult } } });
            var p2 = new { model, system = sysPrompt, messages, max_tokens = 2000, temperature = 0.7 };
            var r2 = await client.PostAsync($"{endpoint}/v1/messages", new StringContent(JsonSerializer.Serialize(p2), Encoding.UTF8, "application/json"));
            var j2 = await r2.Content.ReadAsStringAsync();
            using var d2 = JsonDocument.Parse(j2);
            return d2.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "Done.";
        }
        return content.GetProperty("text").GetString() ?? "No response.";
    }

    private async Task<string> ExecKernelFunc(Kernel k, string name, string inputJson)
    {
        foreach (var p in k.Plugins)
            foreach (var f in p)
                if (f.Name == name)
                {
                    var args = new KernelArguments();
                    if (!string.IsNullOrEmpty(inputJson))
                    {
                        var d = JsonSerializer.Deserialize<Dictionary<string, object>>(inputJson);
                        if (d != null) foreach (var kv in d) args[kv.Key] = kv.Value?.ToString() ?? "";
                    }
                    var r = await f.InvokeAsync(k, args);
                    return r.GetValue<string>() ?? r.ToString();
                }
        return "Function not found.";
    }

    public async Task<List<ChatBotSession>> GetSessionsForUserAsync(string uid)
        => await _db.ChatBotSessions.Where(s => s.UserId == uid && s.IsActive).OrderByDescending(s => s.LastActivity).ToListAsync();
    public async Task<ChatBotSession> CreateSessionAsync(string uid, string? title = null)
    { var s = new ChatBotSession { UserId = uid, Title = title ?? "New Chat", ModelProvider = _config.GetValue<string>("AI:DefaultProvider"), CreatedAt = DateTime.UtcNow, LastActivity = DateTime.UtcNow }; _db.ChatBotSessions.Add(s); await _db.SaveChangesAsync(); return s; }
    public async Task<List<ChatBotMessage>> GetMessagesForSessionAsync(Guid sid)
        => await _db.ChatBotMessages.Where(m => m.SessionId == sid).OrderBy(m => m.CreatedAt).ToListAsync();
    public async Task<bool> ResetSessionAsync(Guid sid)
    { var msgs = await _db.ChatBotMessages.Where(m => m.SessionId == sid).ToListAsync(); _db.ChatBotMessages.RemoveRange(msgs); _db.ChatBotMessages.Add(new ChatBotMessage { SessionId = sid, Role = "System", Content = "Session di-reset.", CreatedAt = DateTime.UtcNow }); await _db.SaveChangesAsync(); return true; }
    public async Task<bool> DeleteSessionAsync(Guid sid)
    { var s = await _db.ChatBotSessions.FindAsync(sid); if (s == null) return false; s.IsActive = false; await _db.SaveChangesAsync(); return true; }
}

// ═══════════════════════════════════════════════════════════════
// KERNEL FUNCTIONS — 25 functions, semua DB query case-insensitive
// ═══════════════════════════════════════════════════════════════

public class AiKernelFunctions
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _cfg;
    private readonly AppDbContext _db;
    private readonly ILogger _log;

    // 🔑 Semua DB field yang sering di-query via string mengandung di-normalize ke lower/upper
    //    dengan EF.Functions ILike (PostgreSQL/Npgsql) atau ToLower() untuk SQLite/SqlServer

    public AiKernelFunctions(IHttpClientFactory h, IConfiguration c, AppDbContext d, ILogger l)
    { _http = h; _cfg = c; _db = d; _log = l; }

    // ═══════════════════════════════════════════
    // 🕐 DATE & TIME
    // ═══════════════════════════════════════════

    [KernelFunction("get_current_datetime"), Description("Mendapatkan waktu dan tanggal saat ini. Gunakan saat user bertanya tentang hari/tanggal/jam sekarang.")]
    public string GetCurrentDateTime(
        [Description("Timezone: Asia/Jakarta, Asia/Makassar, Asia/Jayapura, UTC")] string timezone = "Asia/Jakarta")
    {
        var tz = timezone switch { "Asia/Makassar" or "WITA" => 8, "Asia/Jayapura" or "WIT" => 9, "UTC" => 0, _ => 7 };
        var local = DateTime.UtcNow.AddHours(tz);
        var day = local.DayOfWeek switch { DayOfWeek.Monday => "Senin", DayOfWeek.Tuesday => "Selasa", DayOfWeek.Wednesday => "Rabu", DayOfWeek.Thursday => "Kamis", DayOfWeek.Friday => "Jumat", DayOfWeek.Saturday => "Sabtu", _ => "Minggu" };
        return $"🕐 **{day}, {local:dd MMMM yyyy}** — {local:HH:mm} {timezone}";
    }

    [KernelFunction("calculate_date_difference"), Description("Menghitung selisih hari antara dua tanggal. Gunakan saat user tanya 'berapa hari lagi', countdown, dsb.")]
    public string CalculateDateDifference(
        [Description("Tanggal (yyyy-MM-dd)")] string date1,
        [Description("Tanggal kedua atau 'today'")] string date2 = "today")
    {
        if (!DateTime.TryParse(date1, out var d1)) return "❌ Format tanggal: yyyy-MM-dd.";
        var d2 = date2.ToLower() == "today" ? DateTime.Today : (DateTime.TryParse(date2, out var p) ? p : DateTime.Today);
        var diff = (d1.Date - d2.Date).Days;
        return diff > 0 ? $"📅 **{diff} hari lagi** ({d1:dd MMM yyyy})" : diff < 0 ? $"📅 Sudah **{Math.Abs(diff)} hari** berlalu ({d1:dd MMM yyyy})" : $"📅 **Hari ini!** ({d1:dd MMM yyyy})";
    }

    [KernelFunction("get_day_of_week"), Description("Mengetahui hari apa dari sebuah tanggal.")]
    public string GetDayOfWeek([Description("Tanggal (yyyy-MM-dd)")] string date)
    {
        if (!DateTime.TryParse(date, out var d)) return "❌ Format: yyyy-MM-dd.";
        var hari = d.DayOfWeek switch { DayOfWeek.Monday => "Senin", DayOfWeek.Tuesday => "Selasa", DayOfWeek.Wednesday => "Rabu", DayOfWeek.Thursday => "Kamis", DayOfWeek.Friday => "Jumat", DayOfWeek.Saturday => "Sabtu", _ => "Minggu" };
        return $"🗓️ {d:dd MMMM yyyy} = **{hari}**";
    }

    [KernelFunction("check_deadline_status"), Description("Cek status deadline: terlambat, hari ini, mepet, atau aman.")]
    public string CheckDeadlineStatus(
        [Description("Deadline (yyyy-MM-dd)")] string deadline,
        [Description("Label item (opsional)")] string? label = null)
    {
        if (!DateTime.TryParse(deadline, out var dl)) return "❌ Format: yyyy-MM-dd.";
        var today = DateTime.Today;
        var name = label ?? "Deadline";
        if (dl.Date < today) return $"🔴 **TERLAMBAT!** {name} lewat {(today - dl.Date).Days} hari ({dl:dd MMM yyyy}).";
        if (dl.Date == today) return $"🟡 **HARI INI!** {name} jatuh tempo ({dl:dd MMM yyyy}).";
        var rem = (dl.Date - today).Days;
        return rem <= 3 ? $"🟠 **MEPET!** {name} {rem} hari lagi ({dl:dd MMM yyyy})." : $"🟢 {name} masih **{rem} hari** ({dl:dd MMM yyyy}).";
    }

    [KernelFunction("add_days_to_date"), Description("Tambah/kurangi hari ke tanggal. Gunakan saat user tanya '3 minggu dari sekarang', '2 bulan lagi'.")]
    public string AddDaysToDate(
        [Description("Jumlah hari (negatif = mundur)")] int days,
        [Description("Tanggal awal (yyyy-MM-dd) atau 'today'")] string startDate = "today")
    {
        var start = startDate.ToLower() == "today" ? DateTime.Today : (DateTime.TryParse(startDate, out var d) ? d : DateTime.Today);
        return $"📅 {Math.Abs(days)} hari {(days >= 0 ? "setelah" : "sebelum")} {start:dd MMM} = **{start.AddDays(days):dddd, dd MMMM yyyy}**";
    }

    // ═══════════════════════════════════════════
    // 🧮 MATH
    // ═══════════════════════════════════════════

    [KernelFunction("calculate_math"), Description("Kalkulasi matematika: +, -, *, /, ^. Gunakan saat user minta perhitungan.")]
    public string CalculateMath(
        [Description("Ekspresi matematika, contoh: '100 + 200', '50 * 3'")] string expression)
    {
        try
        {
            var expr = expression.Replace("x", "*").Replace("×", "*").Replace(":", "/").Replace("÷", "/").Replace("^", "**");
            var result = new System.Data.DataTable().Compute(expr, null);
            return $"🧮 {expression} = **{result}**";
        }
        catch { return $"❌ Tidak bisa: '{expression}'. Gunakan: 100 + 200, 50 * 3, 1000 / 4."; }
    }

    [KernelFunction("calculate_percentage"), Description("Hitung persentase. Gunakan saat user tanya progress, porsi, statistik.")]
    public string CalculatePercentage(
        [Description("Nilai")] decimal value,
        [Description("Total")] decimal total,
        [Description("Label (opsional)")] string? label = null)
    {
        if (total == 0) return "❌ Total tidak boleh 0.";
        var pct = value / total * 100;
        return $"📊 {value:N0} / {total:N0}{(label != null ? $" ({label})" : "")} = **{pct:F2}%**";
    }

    [KernelFunction("calculate_discount"), Description("Hitung harga setelah diskon. Gunakan saat user tanya diskon vendor, promo.")]
    public string CalculateDiscount(
        [Description("Harga awal")] decimal price,
        [Description("Persen diskon (0-100)")] decimal discountPercent)
    {
        var disc = price * discountPercent / 100;
        var final = price - disc;
        return $"💰 Rp {price:N0} — {discountPercent}% = **Rp {final:N0}** (hemat Rp {disc:N0})";
    }

    [KernelFunction("calculate_average"), Description("Hitung rata-rata dari kumpulan angka. Gunakan saat user minta rata-rata.")]
    public string CalculateAverage(
        [Description("Angka dipisah koma: '10, 20, 30, 40'")] string numbers)
    {
        try
        {
            var nums = numbers.Split(',').Select(s => decimal.Parse(s.Trim())).ToList();
            if (!nums.Any()) return "❌ Tidak ada angka.";
            return $"📊 {nums.Count} angka — total {nums.Sum():N2}, **rata-rata {nums.Average():N2}**";
        }
        catch { return "❌ Format: '10, 20, 30, 40'."; }
    }

    [KernelFunction("calculate_ratio"), Description("Hitung rasio perbandingan A:B.")]
    public string CalculateRatio(
        [Description("Nilai A")] decimal a, [Description("Nilai B")] decimal b,
        [Description("Label A")] string? labelA = null, [Description("Label B")] string? labelB = null)
    {
        if (b == 0) return "❌ Nilai B tidak boleh 0.";
        return $"📊 {labelA ?? "A"}:{labelB ?? "B"} = **{a / b:F2}:1**";
    }

    // ═══════════════════════════════════════════
    // 📏 UNIT CONVERSION
    // ═══════════════════════════════════════════

    [KernelFunction("convert_currency"), Description("Konversi mata uang. Gunakan saat user tanya konversi Rupiah ke mata uang lain.")]
    public string ConvertCurrency(
        [Description("Jumlah Rupiah")] decimal amount,
        [Description("Mata uang: USD, SGD, EUR, MYR, JPY, AUD, GBP, CNY, KRW")] string target)
    {
        var rates = new Dictionary<string, (decimal r, string s)>
        { ["USD"]=(15500,"$"), ["SGD"]=(11600,"S$"), ["EUR"]=(16800,"€"), ["MYR"]=(3300,"RM"), ["JPY"]=(105,"¥"), ["AUD"]=(10200,"A$"), ["GBP"]=(19700,"£"), ["CNY"]=(2150,"¥"), ["KRW"]=(12,"₩") };
        if (!rates.TryGetValue(target.ToUpper(), out var x)) return $"❌ '{target}' tidak didukung. Pilih: {string.Join(", ", rates.Keys)}";
        return $"💱 Rp {amount:N0} = {x.s} **{amount / x.r:N2}** ({target.ToUpper()})";
    }

    [KernelFunction("convert_units"), Description("Konversi satuan: km↔m, kg↔g, liter↔ml, inch↔cm, feet↔m.")]
    public string ConvertUnits(
        [Description("Nilai")] decimal value,
        [Description("Satuan asal")] string from,
        [Description("Satuan tujuan")] string to)
    {
        var conv = new Dictionary<(string, string), decimal>
        { [("km","m")]=1000,[("m","km")]=0.001m,[("m","cm")]=100,[("cm","m")]=0.01m,[("kg","g")]=1000,[("g","kg")]=0.001m,[("lit","ml")]=1000,[("ml","lit")]=0.001m,[("inch","cm")]=2.54m,[("cm","inch")]=0.3937m,[("feet","m")]=0.3048m,[("m","feet")]=3.28084m };
        if (conv.TryGetValue((from.ToLower(), to.ToLower()), out var f))
            return $"📏 {value} {from} = **{value * f:N4} {to}**";
        return $"❌ '{from}→{to}' tidak didukung. Coba: km→m, kg→g, liter→ml, inch→cm.";
    }

    // ═══════════════════════════════════════════
    // 📝 TEXT
    // ═══════════════════════════════════════════

    [KernelFunction("format_number"), Description("Format angka besar jadi mudah dibaca (ribuan, jutaan, miliar).")]
    public string FormatNumber(
        [Description("Angka")] decimal number,
        [Description("Mata uang opsional: IDR, USD, EUR")] string? currency = null)
    {
        var sym = currency?.ToUpper() switch { "IDR" or "RP" => "Rp ", "USD" => "$ ", "EUR" => "€ ", _ => "" };
        if (Math.Abs(number) >= 1_000_000_000m) return $"🔢 {sym}{number:N0} ({sym}{number / 1_000_000_000m:F2} M)";
        if (Math.Abs(number) >= 1_000_000m) return $"🔢 {sym}{number:N0} ({sym}{number / 1_000_000m:F2} jt)";
        return $"🔢 {sym}{number:N0}";
    }

    [KernelFunction("summarize_text"), Description("Ringkas teks panjang menjadi poin-poin penting.")]
    public string SummarizeText(
        [Description("Teks")] string text,
        [Description("Maks poin")] int maxPoints = 5)
    {
        if (string.IsNullOrEmpty(text)) return "❌ Teks kosong.";
        var sentences = text.Split('.', '!', '?').Where(s => s.Trim().Length > 10).Select(s => s.Trim()).Take(maxPoints).ToList();
        return $"📝 **Ringkasan ({sentences.Count} poin):**\n{string.Join("\n", sentences.Select(s => $"• {s}."))}";
    }

    // ═══════════════════════════════════════════
    // 🎲 TIPS
    // ═══════════════════════════════════════════

    [KernelFunction("get_random_tip"), Description("Tips event planning random. Gunakan saat user minta tips/ide/inspirasi.")]
    public string GetRandomTip(
        [Description("Kategori: wedding, budget, vendor, tamu, umum")] string? category = null)
    {
        var tips = new Dictionary<string, string[]>
        {
            ["wedding"] = new[] { "💒 Mulai planning 12 bulan sebelum H-H.", "👰 Pilih tema yang mencerminkan kepribadian pasangan.", "📸 Booking fotografer 6 bulan sebelum — cepat penuh!", "🌸 Sesuaikan dekorasi dengan musim agar hemat.", "🎵 Share playlist ke DJ 2 minggu sebelum acara." },
            ["budget"] = new[] { "💰 50% venue+catering, 20% dekorasi, 15% dokumentasi, 15% lainnya.", "📊 Sisihkan 10-15% dana darurat.", "💡 Sewa dekorasi > beli — lebih hemat.", "📝 Catat SEMUA pengeluaran sekecil apapun." },
            ["vendor"] = new[] { "🏢 Cek review minimal 3 vendor.", "📋 Bandingkan 2-3 proposal.", "🔍 Minta portfolio asli + referensi client.", "📝 Baca kontrak teliti — klausul pembatalan!" },
            ["tamu"] = new[] { "👥 Kirim undangan 4-6 minggu sebelum acara.", "📲 Follow up RSVP 2 minggu sebelum event.", "🍽️ Tanyakan dietary restrictions saat RSVP.", "🪑 Atur seating berdasarkan kedekatan tamu." },
            ["umum"] = new[] { "📅 Buat timeline detail per jam untuk H-H.", "🆘 Tunjuk PIC untuk handle masalah.", "🎯 Fokus pada experience tamu.", "🧘 Jangan lupa istirahat & nikmati proses!" }
        };
        var list = tips.GetValueOrDefault(category?.ToLower() ?? "umum", tips["umum"]);
        return $"💡 **Tips:** {list[Random.Shared.Next(list.Length)]}";
    }

    [KernelFunction("get_event_checklist"), Description("Checklist perencanaan event berdasarkan tipe.")]
    public string GetEventChecklist(
        [Description("Tipe: wedding, birthday, corporate")] string eventType = "wedding")
    {
        var items = eventType.ToLower() switch
        {
            "wedding" => new[] { "📅 12 bln: Tentukan tanggal & budget", "🏢 10 bln: Booking venue", "👰 8 bln: Tema & vendor", "📸 6 bln: Foto/video", "🍽️ 4 bln: Food tasting", "📩 3 bln: Undangan", "🎵 2 bln: Hiburan", "🪑 1 bln: Seating plan", "📋 1 mg: Rehearsal", "🎉 HARI-H!" },
            "birthday" => new[] { "📅 3 bln: Tema & venue", "📩 2 bln: Undangan", "🍰 1 bln: Kue & dekorasi", "🎵 2 mg: Hiburan", "📋 1 mg: Final check", "🎉 HARI-H!" },
            _ => new[] { "📅 6 bln: Tujuan & budget", "🏢 4 bln: Venue", "📋 2 bln: Vendor", "📩 1 bln: Tamu", "🎉 HARI-H!" }
        };
        var sb = new StringBuilder($"📋 **Checklist {eventType.ToUpper()}** ({items.Length} langkah):\n\n");
        foreach (var i in items) sb.AppendLine(i);
        return sb.ToString();
    }

    // ═══════════════════════════════════════════
    // 🌐 INTERNET
    // ═══════════════════════════════════════════

    [KernelFunction("search_internet"), Description("Cari informasi di internet via Tavily API.")]
    public async Task<string> SearchInternet([Description("Query")] string query)
    {
        var key = _cfg.GetValue<string>("AI:Tavily:ApiKey") ?? "";
        if (string.IsNullOrEmpty(key)) return "[Tavily API key belum dikonfigurasi.]";
        try
        {
            var c = _http.CreateClient();
            var p = JsonSerializer.Serialize(new { api_key = key, query, search_depth = "basic", max_results = 5, include_answer = true });
            var r = await c.PostAsync("https://api.tavily.com/search", new StringContent(p, Encoding.UTF8, "application/json"));
            if (!r.IsSuccessStatusCode) return $"[Search error: {r.StatusCode}]";
            var j = await r.Content.ReadAsStringAsync(); using var d = JsonDocument.Parse(j);
            var sb = new StringBuilder();
            if (d.RootElement.TryGetProperty("answer", out var a) && a.GetString() is string ans) sb.AppendLine($"📝 {ans}");
            if (d.RootElement.TryGetProperty("results", out var res))
            { int i = 1; foreach (var item in res.EnumerateArray().Take(5)) { sb.AppendLine($"{i}. **{item.GetProperty("title").GetString()}**"); if (item.TryGetProperty("content", out var ct)) sb.AppendLine($"   {ct.GetString()?[..Math.Min(ct.GetString()?.Length ?? 0, 200)]}"); sb.AppendLine($"   🔗 {item.GetProperty("url").GetString()}"); i++; } }
            return sb.ToString();
        }
        catch (Exception ex) { _log.LogError(ex, "Tavily fail"); return $"[Error: {ex.Message}]"; }
    }

    [KernelFunction("scrape_webpage"), Description("Baca konten halaman web dari URL.")]
    public async Task<string> ScrapeWebPage([Description("URL")] string url)
    {
        try
        {
            var c = _http.CreateClient("Scraper"); c.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 EventSphere/1.0"); c.Timeout = TimeSpan.FromSeconds(15);
            var h = await c.GetStringAsync(url);
            var t = Regex.Replace(h, @"<script[^>]*>.*?</script>", "", RegexOptions.Singleline);
            t = Regex.Replace(t, @"<style[^>]*>.*?</style>", "", RegexOptions.Singleline);
            t = Regex.Replace(t, "<[^>]+>", " ").Replace("&nbsp;", " ").Replace("&amp;", "&");
            t = Regex.Replace(t, @"\s+", " ").Trim();
            return t.Length > 4000 ? $"📄 {url}:\n\n{t[..4000]}..." : $"📄 {url}:\n\n{t}";
        }
        catch (Exception ex) { return $"[Scrape error: {ex.Message}]"; }
    }

    // ═══════════════════════════════════════════════════
    // 🗄️ DATABASE QUERIES — SEMUA CASE-INSENSITIVE
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Helper: normalisasi string ke lowercase untuk perbandingan case-insensitive.
    /// Working di SQLite, SqlServer, MySQL, PostgreSQL.
    /// </summary>
    private string Norm(string? s) => (s ?? "").ToLowerInvariant().Trim();

    [KernelFunction("query_events"), Description("Cari event di database. Gunakan keyword parsial — case insensitive.")]
    public async Task<string> QueryEvents(
        [Description("Kata kunci nama event (bisa parsial, case insensitive)")] string keyword)
    {
        var kw = Norm(keyword);
        // 🔑 Gunakan ToLower() pada kedua sisi agar case-insensitive di semua DB provider
        var l = await _db.Events
            .Where(e => string.IsNullOrEmpty(kw) || e.Name.ToLower().Contains(kw))
            .OrderByDescending(e => e.EventDate)
            .Take(10)
            .Select(e => new { e.Name, e.EventDate, e.Status, e.Location, e.EventType, e.BudgetTotal, e.ConfirmedGuests, e.ExpectedGuests })
            .ToListAsync();

        if (!l.Any()) return $"📭 Tidak ada event dengan kata kunci '{keyword}'.";
        var sb = new StringBuilder($"📅 **{l.Count} event ditemukan:**\n\n");
        foreach (var e in l)
        {
            var status = e.Status switch { EventStatus.Draft => "📝 Draft", EventStatus.Planned => "📅 Planned", EventStatus.Confirmed => "✅ Confirmed", EventStatus.InProgress => "🔄 In Progress", EventStatus.Completed => "🏁 Completed", EventStatus.Cancelled => "❌ Cancelled", _ => e.Status.ToString() };
            sb.AppendLine($"- **{e.Name}** — {status}");
            sb.AppendLine($"  📍 {e.Location} | 🗓️ {e.EventDate:dd MMM yyyy} | 🏷️ {e.EventType}");
            sb.AppendLine($"  💰 Rp {e.BudgetTotal:N0} | 👥 {e.ConfirmedGuests}/{e.ExpectedGuests} tamu");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    [KernelFunction("query_vendors"), Description("Cari vendor di database. Case insensitive — 'catering', 'CATERING', 'Catering' semua sama.")]
    public async Task<string> QueryVendors(
        [Description("Kategori vendor (Catering, Dekorasi, Fotografi, Musik, Venue, dll) atau nama vendor")] string keyword)
    {
        var kw = Norm(keyword);

        // 🔑 Cari di Category DAN Name — case insensitive
        var l = await _db.Vendors
            .Where(v => string.IsNullOrEmpty(kw)
                || v.Category!.ToLower().Contains(kw)
                || v.Name.ToLower().Contains(kw))
            .OrderByDescending(v => v.Rating)
            .Take(10)
            .Select(v => new { v.Name, v.Category, v.Rating, v.ReviewCount, v.PriceRange, v.City, v.Phone, v.IsVerified })
            .ToListAsync();

        if (!l.Any()) return $"📭 Tidak ada vendor dengan kata kunci '{keyword}'.";
        var sb = new StringBuilder($"🏢 **{l.Count} vendor ditemukan:**\n\n");
        int i = 1;
        foreach (var v in l)
        {
            sb.AppendLine($"{i}. **{v.Name}** {(v.IsVerified ? "✅" : "")} — {v.Category}");
            sb.AppendLine($"   ⭐ {v.Rating:F1} ({v.ReviewCount} ulasan) | 💲 {v.PriceRange} | 📍 {v.City}");
            sb.AppendLine($"   📞 {v.Phone}");
            sb.AppendLine();
            i++;
        }
        return sb.ToString();
    }

    [KernelFunction("query_budget"), Description("Lihat budget & pengeluaran event. Case insensitive — cari nama event parsial.")]
    public async Task<string> QueryBudget(
        [Description("Nama event (bisa parsial, case insensitive)")] string eventName)
    {
        var kw = Norm(eventName);

        // 🔑 Case insensitive search di Name
        var e = await _db.Events
            .Include(x => x.BudgetItems)
            .FirstOrDefaultAsync(x => x.Name.ToLower().Contains(kw));

        if (e == null) return $"📭 Event dengan nama '{eventName}' tidak ditemukan.";

        var sb = new StringBuilder();
        sb.AppendLine($"💰 **Budget: {e.Name}**");
        sb.AppendLine($"📊 Status: {e.Status} | 🗓️ {e.EventDate:dd MMM yyyy}");
        sb.AppendLine($"💵 Total Budget: Rp {e.BudgetTotal:N0}");
        sb.AppendLine($"💸 Terpakai: Rp {e.BudgetSpent:N0} ({(e.BudgetTotal > 0 ? e.BudgetSpent / e.BudgetTotal * 100 : 0):F1}%)");
        sb.AppendLine($"🏦 Sisa: Rp {(e.BudgetTotal - e.BudgetSpent):N0}");
        sb.AppendLine();

        if (e.BudgetItems.Any())
        {
            sb.AppendLine("**Rincian Pengeluaran:**");
            foreach (var bi in e.BudgetItems.OrderBy(b => b.SortOrder))
            {
                var icon = bi.IsPaid ? "✅" : "⏳";
                sb.AppendLine($"- {icon} **{bi.Name}** ({bi.Category}): Estimasi Rp {bi.EstimatedCost:N0} / Aktual Rp {bi.ActualCost:N0}");
            }
        }
        else { sb.AppendLine("_Belum ada item budget._"); }

        return sb.ToString();
    }

    [KernelFunction("query_tasks"), Description("Lihat task/checklist event. Case insensitive — cari nama event parsial.")]
    public async Task<string> QueryTasks(
        [Description("Nama event (bisa parsial, case insensitive)")] string eventName)
    {
        var kw = Norm(eventName);

        // 🔑 Case insensitive
        var e = await _db.Events
            .Include(x => x.TaskItems)
            .FirstOrDefaultAsync(x => x.Name.ToLower().Contains(kw));

        if (e == null) return $"📭 Event '{eventName}' tidak ditemukan.";

        var tasks = e.TaskItems.OrderBy(t => t.SortOrder).ToList();
        if (!tasks.Any()) return $"✅ Event **{e.Name}** belum memiliki task.";

        var done = tasks.Count(t => t.Status == TaskItemStatus.Done);
        var total = tasks.Count;

        var sb = new StringBuilder();
        sb.AppendLine($"✅ **Task: {e.Name}** — {done}/{total} selesai ({(total > 0 ? done * 100 / total : 0)}%)\n");

        foreach (var t in tasks)
        {
            var icon = t.Status switch { TaskItemStatus.Done => "✅", TaskItemStatus.InProgress => "🔄", TaskItemStatus.Review => "👀", _ => "⏳" };
            var prio = t.Priority switch { TaskPriority.Urgent => "🔴", TaskPriority.High => "🟠", TaskPriority.Medium => "🟡", _ => "🟢" };

            sb.Append($"- {icon} {prio} **{t.Title}**");
            if (t.DueDate.HasValue) sb.Append($" — 📅 {t.DueDate:dd MMM}");
            if (t.Progress > 0) sb.Append($" — {t.Progress}%");
            if (t.AssignedTo != null) sb.Append($" — 👤 {t.AssignedTo.FullName}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    [KernelFunction("query_seating"), Description("Lihat seating arrangement. Case insensitive.")]
    public async Task<string> QuerySeating(
        [Description("Nama event")] string eventName)
    {
        var kw = Norm(eventName);

        // 🔑 Case insensitive
        var e = await _db.Events
            .Include(x => x.TableArrangements)
            .Include(x => x.Attendees).ThenInclude(a => a.User)
            .FirstOrDefaultAsync(x => x.Name.ToLower().Contains(kw));

        if (e == null) return $"📭 Event '{eventName}' tidak ditemukan.";
        if (!e.TableArrangements.Any()) return $"🪑 Event **{e.Name}** belum memiliki seating plan.";

        var sb = new StringBuilder();
        sb.AppendLine($"🪑 **Seating Plan: {e.Name}** — {e.TableArrangements.Count} meja\n");
        foreach (var t in e.TableArrangements.OrderBy(t => t.SortOrder))
        {
            var occupancy = t.Capacity > 0 ? (double)t.FilledSeats / t.Capacity * 100 : 0;
            var bar = occupancy >= 100 ? "🔴" : occupancy >= 75 ? "🟡" : "🟢";
            sb.AppendLine($"- {bar} **{t.TableName}** ({t.Shape}): {t.FilledSeats}/{t.Capacity} kursi");

            // Tampilkan nama tamu di meja ini
            var guestNames = e.Attendees.Where(a => a.TableId == t.Id).Select(a => a.User?.FullName).ToList();
            if (guestNames.Any()) sb.AppendLine($"  👥 {string.Join(", ", guestNames)}");
        }
        return sb.ToString();
    }

    [KernelFunction("calculate_budget_estimate"), Description("Estimasi budget event berdasarkan jumlah tamu & tipe acara.")]
    public string BudgetEstimate(
        [Description("Jumlah tamu")] int guests,
        [Description("Tipe: Wedding, Birthday, Corporate")] string type)
    {
        var pp = type.ToLower() switch { "wedding" => 750_000m, "birthday" => 350_000m, "corporate" => 500_000m, _ => 450_000m };
        var food = guests * pp; var venue = guests * 50_000m; var decor = guests * 40_000m;
        var photo = 5_000_000m; var ent = 7_500_000m; var misc = guests * 25_000m;
        var total = food + venue + decor + photo + ent + misc;
        return $"""
            💰 **Estimasi Budget: {type} — {guests} tamu**

            | Item | Estimasi |
            |------|----------|
            | 🍽️ Catering ({guests} × Rp {pp:N0}) | Rp {food:N0} |
            | 🏢 Venue | Rp {venue:N0} |
            | 🎨 Dekorasi | Rp {decor:N0} |
            | 📸 Fotografi | Rp {photo:N0} |
            | 🎵 Hiburan | Rp {ent:N0} |
            | 📦 Lain-lain | Rp {misc:N0} |
            | **TOTAL** | **Rp {total:N0}** |

            💡 Estimasi kasar per orang: ±Rp {(total / guests):N0}/tamu
            """;
    }

    [KernelFunction("get_dashboard_summary"), Description("Ringkasan statistik EventSphere. Gunakan untuk overview sistem.")]
    public async Task<string> DashboardSummary()
    {
        var now = DateTime.UtcNow;
        var te = await _db.Events.CountAsync();
        var ue = await _db.Events.CountAsync(e => e.EventDate >= now && e.Status != EventStatus.Cancelled);
        var tv = await _db.Vendors.CountAsync();
        var tu = await _db.Users.CountAsync();
        var rev = await _db.VendorContracts.SumAsync(vc => vc.Amount);
        var avg = await _db.Feedbacks.AnyAsync() ? await _db.Feedbacks.AverageAsync(f => (double)f.Rating) : 0;
        var td = await _db.TaskItems.CountAsync(t => t.Status == TaskItemStatus.Done);
        var tt = await _db.TaskItems.CountAsync();

        return $"""
            📊 **EventSphere — Dashboard Ringkasan**

            | Metrik | Nilai |
            |--------|-------|
            | 📅 Total Events | **{te}** ({ue} upcoming) |
            | 🏢 Total Vendors | **{tv}** |
            | 👥 Total Users | **{tu}** |
            | 💰 Total Revenue | **Rp {rev:N0}** |
            | ⭐ Kepuasan Tamu | **{avg:F1}/5.0** |
            | ✅ Task Selesai | **{td}/{tt}** |

            💡 _Data real-time dari database._
            """;
    }
}
