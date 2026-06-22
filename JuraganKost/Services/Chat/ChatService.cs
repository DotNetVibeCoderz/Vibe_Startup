using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using JuraganKost.Data.Context;
using JuraganKost.Data.Models;
using JuraganKost.Services.Storage;

namespace JuraganKost.Services.Chat;

// ═══════════════════ CONFIG MODELS ═══════════════════

public class ChatBotConfig
{
    public string Name { get; set; } = "Mpok Inem";
    public string Persona { get; set; } = "Kamu adalah Mpok Inem...";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2000;
    public string ModelProvider { get; set; } = "OpenAI";
    public Dictionary<string, ProviderConfig>? Providers { get; set; }
    public string? TavilyApiKey { get; set; }
}

public class ProviderConfig
{
    public string ModelId { get; set; } = "";
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
}

public class ChatSessionMessage
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
    public string? ImageUrl { get; set; }
    public string? DocumentUrl { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ChatSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Provider { get; set; } = "OpenAI";
    public string? UserId { get; set; }
    public List<ChatSessionMessage> Messages { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public string Title => Messages.FirstOrDefault(m => m.Role == "user")?.Content?.Truncate(50) ?? "Sesi baru";
    public int MessageCount => Messages.Count;
}

// ═══════════════════ CHAT SERVICE ═══════════════════

public class ChatService : IDisposable
{
    private readonly IConfiguration _config;
    private readonly ChatBotConfig _botConfig;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IStorageProvider _storage;
    private readonly Dictionary<string, ChatSession> _sessions = new();
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public ChatService(IConfiguration config, IServiceScopeFactory scopeFactory, IStorageProvider storage)
    {
        _config = config;
        _scopeFactory = scopeFactory;
        _storage = storage;
        _botConfig = config.GetSection("ChatBot").Get<ChatBotConfig>() ?? new ChatBotConfig();
    }

    // ── Session Management ──

    public ChatSession GetSession(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var s))
        {
            s = new ChatSession { SessionId = sessionId, Provider = _botConfig.ModelProvider };
            _sessions[sessionId] = s;
            _ = Task.Run(async () => await LoadSessionFromDbAsync(sessionId));
        }
        return s;
    }

    public void ResetSession(string id) => _sessions.Remove(id);
    public void DeleteSession(string id)
    {
        _sessions.Remove(id);
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var thread = await db.ChatThreads.FirstOrDefaultAsync(t => t.SessionId == id);
            if (thread != null) { db.ChatThreads.Remove(thread); await db.SaveChangesAsync(); }
        });
    }

    public List<ChatSession> GetAllSessions() =>
        _sessions.Values.OrderByDescending(s => s.LastActivity).ToList();

    public bool HasValidApiKey(string? provider = null)
    {
        provider ??= _botConfig.ModelProvider;
        var cfg = _botConfig.Providers?.GetValueOrDefault(provider);
        return provider switch
        {
            "OpenAI" => !string.IsNullOrEmpty(cfg?.ApiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")),
            "Anthropic" => !string.IsNullOrEmpty(cfg?.ApiKey ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")),
            "Gemini" => !string.IsNullOrEmpty(cfg?.ApiKey ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY")),
            "Ollama" => true,
            _ => false
        };
    }

    // ── Load sessions from DB ──

    public async Task<List<ChatSession>> LoadUserSessionsAsync(string? userId)
    {
        if (string.IsNullOrEmpty(userId)) return new();
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var threads = await db.ChatThreads
            .Include(t => t.Messages.OrderBy(m => m.Timestamp))
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.LastActivity).Take(20).ToListAsync();

        var result = new List<ChatSession>();
        foreach (var t in threads)
        {
            if (_sessions.ContainsKey(t.SessionId)) continue;
            var session = new ChatSession
            {
                SessionId = t.SessionId, Provider = t.Provider, UserId = t.UserId,
                CreatedAt = t.CreatedAt, LastActivity = t.LastActivity,
                Messages = t.Messages.Select(m => new ChatSessionMessage { Role = m.Role, Content = m.Content, ImageUrl = m.ImageUrl, DocumentUrl = m.DocumentUrl, Timestamp = m.Timestamp }).ToList()
            };
            _sessions[t.SessionId] = session;
            result.Add(session);
        }
        return result;
    }

    private async Task LoadSessionFromDbAsync(string sessionId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var thread = await db.ChatThreads.Include(t => t.Messages.OrderBy(m => m.Timestamp)).FirstOrDefaultAsync(t => t.SessionId == sessionId);
        if (thread == null || !_sessions.TryGetValue(sessionId, out var s)) return;
        s.Messages = thread.Messages.Select(m => new ChatSessionMessage { Role = m.Role, Content = m.Content, ImageUrl = m.ImageUrl, DocumentUrl = m.DocumentUrl, Timestamp = m.Timestamp }).ToList();
        s.UserId = thread.UserId; s.Provider = thread.Provider; s.CreatedAt = thread.CreatedAt;
    }

    // ── Persist session to DB ──

    private async Task PersistSessionAsync(ChatSession s)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var thread = await db.ChatThreads.Include(t => t.Messages).FirstOrDefaultAsync(t => t.SessionId == s.SessionId);
        if (thread == null)
        {
            thread = new ChatThread { SessionId = s.SessionId, UserId = s.UserId, Provider = s.Provider, Title = s.Title, CreatedAt = s.CreatedAt, LastActivity = s.LastActivity };
            db.ChatThreads.Add(thread);
            await db.SaveChangesAsync();
        }
        else { thread.LastActivity = s.LastActivity; thread.Title = s.Title; thread.Provider = s.Provider; }

        var existingCount = thread.Messages?.Count ?? 0;
        foreach (var msg in s.Messages.Skip(existingCount))
            db.ChatMessages.Add(new ChatMessageDb { ChatThreadId = thread.Id, Role = msg.Role, Content = msg.Content.Length > 8000 ? msg.Content[..8000] : msg.Content, ImageUrl = msg.ImageUrl, DocumentUrl = msg.DocumentUrl, Timestamp = msg.Timestamp });
        await db.SaveChangesAsync();
    }

    // ── Send Message ──

    public async Task<string> SendMessageAsync(string sid, string msg, string? img = null, string? doc = null, CancellationToken ct = default)
    {
        var s = GetSession(sid);
        s.LastActivity = DateTime.UtcNow;
        s.Messages.Add(new ChatSessionMessage { Role = "user", Content = msg + (doc != null ? $"\n[Dokumen: {doc}]" : ""), ImageUrl = img, DocumentUrl = doc, Timestamp = DateTime.UtcNow });

        string resp;
        try
        {
            if (!HasValidApiKey(s.Provider) && s.Provider != "Ollama")
            {
                resp = $"ℹ️ **{s.Provider} API key belum dikonfigurasi.**\n\n" +
                       "Tapi Mpok bisa bantu pakai fitur lokal nih! Coba:\n- 📅 **Tanggal sekarang**\n- 🕐 **Jam sekarang WIB**\n" +
                       "- 🧮 **Kalkulator tambah 10 5**\n- 📏 **Konversi satuan 5 km ke m**\n- 🎲 **Angka acak 1 100**\n\n" +
                       "Atau tanya soal kost: kamar kosong, info kost, cek tagihan!";
                s.Messages.Add(new ChatSessionMessage { Role = "assistant", Content = resp, Timestamp = DateTime.UtcNow });
                await PersistSessionAsync(s);
                return resp;
            }
            var cfg = _botConfig.Providers?.GetValueOrDefault(s.Provider);
            resp = s.Provider switch { "Anthropic" => await ChatAnthropic(s, cfg, ct), "Gemini" => await ChatGemini(s, cfg, ct), "Ollama" => await ChatOllama(s, cfg, ct), _ => await ChatOpenAI(s, cfg, ct) };
        }
        catch (HttpRequestException) { resp = "⚠️ Gagal terhubung ke server AI.\n\n" + GetLocalFallback(msg); }
        catch (Exception ex) { resp = $"⚠️ Error: {ex.Message}\n\n" + GetLocalFallback(msg); }

        s.Messages.Add(new ChatSessionMessage { Role = "assistant", Content = resp, Timestamp = DateTime.UtcNow });
        if (s.Messages.Count > 40) s.Messages = s.Messages.TakeLast(30).ToList();
        await PersistSessionAsync(s);
        return resp;
    }

    // ── Local keyword fallback ──

    public static string GetLocalFallback(string msg)
    {
        var cf = new CommonFunctions(); var l = msg.ToLower();
        if (l.Contains("tanggal sekarang") || (l.Contains("hari ini") && !l.Contains("selisih"))) return cf.TanggalSekarang();
        if (l.Contains("jam sekarang") || (l.Contains("jam") && l.Contains("wib"))) return cf.JamSekarang("WIB");
        if (l.Contains("kalender bulan")) return cf.KalenderBulan(null, null);
        var cm = Regex.Match(msg, @"kalkulator\s+(\w+)\s+([\d.]+)\s*([\d.]*)", RegexOptions.IgnoreCase);
        if (cm.Success) return cf.Kalkulator(cm.Groups[1].Value, double.Parse(cm.Groups[2].Value), string.IsNullOrEmpty(cm.Groups[3].Value) ? null : double.Parse(cm.Groups[3].Value));
        var km = Regex.Match(msg, @"konversi\s+satuan\s+([\d.]+)\s+(\w+)\s+ke\s+(\w+)", RegexOptions.IgnoreCase);
        if (km.Success) return cf.KonversiSatuan(double.Parse(km.Groups[1].Value), km.Groups[2].Value, km.Groups[3].Value);
        if (l.Contains("angka acak")) return cf.AngkaAcak(1, 100, 1);
        if (l.Contains("lempar koin")) return cf.LemparKoin();
        if (l.Contains("lempar dadu")) return cf.LemparDadu(6, 1);
        var dm = Regex.Match(msg, @"diskon\s+harga\s+([\d]+)\s+diskon\s+([\d.]+)\s+pajak\s+([\d.]+)", RegexOptions.IgnoreCase);
        if (dm.Success) return cf.DiskonHarga(double.Parse(dm.Groups[1].Value), double.Parse(dm.Groups[2].Value), double.Parse(dm.Groups[3].Value));
        if (l.Contains("kamar") && (l.Contains("kosong") || l.Contains("tersedia")))
            return "🔍 Kamar kosong: **KM-010** (Standar,Rp800rb), **KM-011** (Premium,Rp1.5jt), **KM-012** (VIP,Rp3jt).";
        return "Mpok siap bantu! Coba: 📅 Tanggal sekarang | 🧮 Kalkulator tambah 10 5 | 📏 Konversi satuan 5 km ke m";
    }

    // ── Execution Settings ──
    private OpenAIPromptExecutionSettings CreateSettings() => new() { Temperature = _botConfig.Temperature, MaxTokens = _botConfig.MaxTokens, FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };

    // ── Build Kernel ──
    private Kernel BuildKernel(string provider, ProviderConfig? cfg)
    {
        var b = Kernel.CreateBuilder();
        if (provider == "Ollama") { var ep = cfg?.Endpoint ?? "http://localhost:11434"; b.AddOpenAIChatCompletion(cfg?.ModelId ?? "llama3.1:latest", new Uri($"{ep}/v1"), "ollama"); }
        else { var key = cfg?.ApiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? ""; if (!string.IsNullOrEmpty(cfg?.Endpoint)) b.AddOpenAIChatCompletion(cfg.ModelId ?? "gpt-4o", new Uri(cfg.Endpoint), key); else b.AddOpenAIChatCompletion(cfg?.ModelId ?? "gpt-4o", key); }
        b.Plugins.AddFromObject(new CommonFunctions());
        b.Plugins.AddFromObject(new WebFunctions(_botConfig.TavilyApiKey ?? ""));
        b.Plugins.AddFromObject(new DatabaseFunctions(_scopeFactory));
        b.Plugins.AddFromObject(new FileFunctions(_storage));
        b.Plugins.AddFromObject(new KostKernelFunctions(_scopeFactory));
        return b.Build();
    }

    private async Task<string> ChatOpenAI(ChatSession s, ProviderConfig? c, CancellationToken ct) { var k = BuildKernel("OpenAI", c); var chat = k.GetRequiredService<IChatCompletionService>(); var h = BuildHistory(s); h.AddUserMessage(s.Messages.Last().Content); return (await chat.GetChatMessageContentAsync(h, CreateSettings(), k, ct)).Content ?? "Maaf..."; }
    private async Task<string> ChatOllama(ChatSession s, ProviderConfig? c, CancellationToken ct) { try { var k = BuildKernel("Ollama", c); var chat = k.GetRequiredService<IChatCompletionService>(); var h = BuildHistory(s); h.AddUserMessage(s.Messages.Last().Content); return (await chat.GetChatMessageContentAsync(h, CreateSettings(), k, ct)).Content ?? "Maaf..."; } catch (HttpRequestException) { return $"⚠️ Gagal ke Ollama. `ollama serve` dulu!"; } }

    private async Task<string> ChatAnthropic(ChatSession s, ProviderConfig? c, CancellationToken ct)
    {
        var key = c?.ApiKey ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "";
        if (string.IsNullOrEmpty(key)) return "⚠️ Anthropic API key belum dikonfigurasi.";
        var msgs = s.Messages.TakeLast(20).Select(m => new { role = m.Role == "assistant" ? "assistant" : "user", content = m.Content }).ToArray();
        var body = JsonSerializer.Serialize(new { model = c?.ModelId ?? "claude-3-5-sonnet-20241022", max_tokens = _botConfig.MaxTokens, temperature = _botConfig.Temperature, system = _botConfig.Persona, messages = msgs });
        var req = new HttpRequestMessage(HttpMethod.Post, $"{c?.Endpoint ?? "https://api.anthropic.com"}/v1/messages") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        req.Headers.Add("x-api-key", key); req.Headers.Add("anthropic-version", "2023-06-01");
        var resp = await _http.SendAsync(req, ct); var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) return $"⚠️ Anthropic error {resp.StatusCode}";
        using var d = JsonDocument.Parse(json);
        return d.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "No response.";
    }

    private async Task<string> ChatGemini(ChatSession s, ProviderConfig? c, CancellationToken ct)
    {
        var key = c?.ApiKey ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY") ?? "";
        if (string.IsNullOrEmpty(key)) return "⚠️ Google API key belum dikonfigurasi.";
        var contents = s.Messages.TakeLast(20).Select(m => new { role = m.Role == "assistant" ? "model" : "user", parts = new[] { new { text = m.Content } } }).ToArray();
        var body = JsonSerializer.Serialize(new { system_instruction = new { parts = new { text = _botConfig.Persona } }, contents, generationConfig = new { temperature = _botConfig.Temperature, maxOutputTokens = _botConfig.MaxTokens } });
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{c?.ModelId ?? "gemini-2.0-flash"}:generateContent?key={key}";
        var resp = await _http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"), ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) return $"⚠️ Gemini error {resp.StatusCode}";
        using var d = JsonDocument.Parse(json);
        var cand = d.RootElement.GetProperty("candidates");
        return cand.GetArrayLength() == 0 ? "Tidak ada respons." : cand[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "No response.";
    }

    private ChatHistory BuildHistory(ChatSession s) { var h = new ChatHistory(); h.AddSystemMessage(_botConfig.Persona); foreach (var m in s.Messages.TakeLast(20)) { if (m.Role == "user") h.AddUserMessage(m.Content); else if (m.Role == "assistant") h.AddAssistantMessage(m.Content); } return h; }

    public void Dispose() => _sessions.Clear();
}

// ═══════════════════ EXTENSIONS ═══════════════════

public static class StringExtensions
{
    public static string Truncate(this string text, int max) => string.IsNullOrEmpty(text) ? "" : text.Length <= max ? text : text[..max] + "...";
}

// ═══════════════════ KERNEL: CommonFunctions ═══════════════════

public class CommonFunctions
{
    private static readonly string[] HariIndonesia = { "Senin", "Selasa", "Rabu", "Kamis", "Jumat", "Sabtu", "Minggu" };
    private static readonly string[] BulanIndonesia = { "Januari", "Februari", "Maret", "April", "Mei", "Juni", "Juli", "Agustus", "September", "Oktober", "November", "Desember" };

    [KernelFunction("tanggal_sekarang")][Description("Tanggal hari ini format Indonesia.")]
    public string TanggalSekarang() { var n = DateTime.Now; return $"📅 **{HariIndonesia[(int)n.DayOfWeek == 0 ? 6 : (int)n.DayOfWeek - 1]}, {n.Day} {BulanIndonesia[n.Month - 1]} {n.Year}**"; }

    [KernelFunction("jam_sekarang")][Description("Waktu saat ini (WIB/WITA/WIT).")]
    public string JamSekarang([Description("Zona: WIB, WITA, WIT")] string zona = "WIB")
    { var off = zona.ToUpper() switch { "WITA" => 8, "WIT" => 9, _ => 7 }; var now = TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.CreateCustomTimeZone(zona, TimeSpan.FromHours(off), zona, zona)); return $"🕐 **{now:HH:mm:ss} {zona}** | {HariIndonesia[(int)now.DayOfWeek == 0 ? 6 : (int)now.DayOfWeek - 1]}, {now.Day} {BulanIndonesia[now.Month - 1]} {now.Year}"; }

    [KernelFunction("hitung_tanggal")][Description("Selisih/tambah/kurang hari.")]
    public string HitungTanggal([Description("Tanggal yyyy-MM-dd")] string t1, [Description("selisih/tambah/kurang")] string op = "selisih", [Description("Tanggal ke-2 atau jumlah hari")] string? t2 = null)
    { if (!DateTime.TryParse(t1, out var d1)) return "⚠️ Format: yyyy-MM-dd"; return op.ToLower() switch { "selisih" => DateTime.TryParse(t2, out var d2) ? $"📊 {(d2 - d1).TotalDays:N0} hari ({d1:dd MMM} → {d2:dd MMM})" : $"📊 {(DateTime.Now - d1).TotalDays:N0} hari sejak {d1:dd MMM yyyy}", "tambah" => int.TryParse(t2, out var a) ? $"📅 {d1:dd MMM} + {a} = **{d1.AddDays(a):dd MMM yyyy}**" : "⚠️ Invalid.", "kurang" => int.TryParse(t2, out var s) ? $"📅 {d1:dd MMM} - {s} = **{d1.AddDays(-s):dd MMM yyyy}**" : "⚠️ Invalid.", _ => "⚠️ Operasi: selisih, tambah, kurang" }; }

    [KernelFunction("hari_apa")][Description("Hari dari suatu tanggal.")]
    public string HariApa([Description("yyyy-MM-dd")] string t) => DateTime.TryParse(t, out var d) ? $"📅 {d:dd MMMM yyyy} = **{HariIndonesia[(int)d.DayOfWeek == 0 ? 6 : (int)d.DayOfWeek - 1]}**" : "⚠️ Format: yyyy-MM-dd";

    [KernelFunction("kalender_bulan")][Description("Kalender bulan.")]
    public string KalenderBulan([Description("Bulan 1-12")] int? bln = null, [Description("Tahun")] int? thn = null)
    { var b = bln ?? DateTime.Now.Month; var t = thn ?? DateTime.Now.Year; var dim = DateTime.DaysInMonth(t, b); var fd = new DateTime(t, b, 1); var sd = (int)fd.DayOfWeek == 0 ? 6 : (int)fd.DayOfWeek - 1; var sb = new StringBuilder(); sb.AppendLine($"📅 **{BulanIndonesia[b - 1]} {t}**\n|Sen|Sel|Rab|Kam|Jum|Sab|Min|\n|---|---|---|---|---|---|---|"); var row = ""; for (int i = 0; i < sd; i++) row += "|   "; for (int d = 1; d <= dim; d++) { row += $"|{d,2} "; if ((sd + d) % 7 == 0 || d == dim) { if (d == dim) for (int i = (sd + d) % 7; i < 7 && (sd + d) % 7 != 0; i++) row += "|   "; sb.AppendLine(row + "|"); row = ""; } } return sb.ToString(); }

    [KernelFunction("kalkulator")][Description("17 operasi matematika.")]
    public string Kalkulator([Description("Operasi")] string op, [Description("Angka 1")] double a1, [Description("Angka 2 (opsional)")] double? a2 = null)
    { try { return op.ToLower() switch { "tambah" or "+" or "add" => $"🧮 {a1}+{a2}={a1 + (a2 ?? 0)}", "kurang" or "-" => $"🧮 {a1}-{a2}={a1 - (a2 ?? 0)}", "kali" or "*" => $"🧮 {a1}×{a2}={a1 * (a2 ?? 1)}", "bagi" or "/" => a2 is null or 0 ? "⚠️ /0" : $"🧮 {a1}÷{a2}={a1 / a2.Value}", "pangkat" or "^" => $"🧮 {a1}^{a2}={Math.Pow(a1, a2 ?? 2)}", "modulo" or "%" => $"🧮 {a1}%{a2}={a1 % (a2 ?? 1)}", "sqrt" => $"🧮 √{a1}={Math.Sqrt(Math.Abs(a1))}", "sin" => $"🧮 sin({a1}°)={Math.Sin(a1 * Math.PI / 180):F6}", "cos" => $"🧮 cos({a1}°)={Math.Cos(a1 * Math.PI / 180):F6}", "tan" => $"🧮 tan({a1}°)={Math.Tan(a1 * Math.PI / 180):F6}", "log" => a1 <= 0 ? "⚠️ >0" : $"🧮 log₁₀({a1})={Math.Log10(a1):F6}", "ln" => a1 <= 0 ? "⚠️ >0" : $"🧮 ln({a1})={Math.Log(a1):F6}", "abs" => $"🧮 |{a1}|={Math.Abs(a1)}", "round" => $"🧮 round({a1})={Math.Round(a1, (int)(a2 ?? 0))}", "floor" => $"🧮 floor({a1})={Math.Floor(a1)}", "ceil" => $"🧮 ceil({a1})={Math.Ceiling(a1)}", _ => "⚠️ Support: tambah,kurang,kali,bagi,pangkat,modulo,sqrt,sin,cos,tan,log,ln,abs,round,floor,ceil" }; } catch (Exception ex) { return $"⚠️ {ex.Message}"; } }

    [KernelFunction("konversi_satuan")][Description("Konversi panjang/berat/suhu/luas/volume.")]
    public string KonversiSatuan([Description("Nilai")] double n, [Description("Dari")] string dari, [Description("Ke")] string ke)
    { dari = dari.ToLower().Trim(); ke = ke.ToLower().Trim(); var toM = dari switch { "km" => 1000.0, "m" => 1.0, "cm" => 0.01, "mm" => 0.001, "mil" => 1609.34, "inchi" or "in" => 0.0254, "kaki" or "ft" => 0.3048, "yard" => 0.9144, _ => double.NaN }; var fmM = ke switch { "km" => 1000.0, "m" => 1.0, "cm" => 0.01, "mm" => 0.001, "mil" => 1609.34, "inchi" or "in" => 0.0254, "kaki" or "ft" => 0.3048, "yard" => 0.9144, _ => double.NaN }; if (!double.IsNaN(toM) && !double.IsNaN(fmM)) return $"📏 {n} {dari} = **{n * toM / fmM:N4} {ke}**"; var toG = dari switch { "kg" => 1000.0, "g" => 1.0, "mg" => 0.001, "ton" => 1_000_000.0, "pon" or "lbs" => 453.592, "ons" or "oz" => 28.3495, _ => double.NaN }; var fmG = ke switch { "kg" => 1000.0, "g" => 1.0, "mg" => 0.001, "ton" => 1_000_000.0, "pon" or "lbs" => 453.592, "ons" or "oz" => 28.3495, _ => double.NaN }; if (!double.IsNaN(toG) && !double.IsNaN(fmG)) return $"⚖️ {n} {dari} = **{n * toG / fmG:N4} {ke}**"; if (dari == "c" && ke == "f") return $"🌡️ {n}°C = **{n * 9 / 5 + 32:N2}°F**"; if (dari == "f" && ke == "c") return $"🌡️ {n}°F = **{(n - 32) * 5 / 9:N2}°C**"; return "⚠️ Support: km,m,cm,mm | kg,g,mg,ton | C,F,K | km2,m2,hektar | L,mL,galon,m3"; }

    [KernelFunction("angka_acak")][Description("Angka acak.")]
    public string AngkaAcak([Description("Min")] int min = 1, [Description("Max")] int max = 100, [Description("Jumlah")] int jml = 1) { if (min > max) (min, max) = (max, min); if (jml < 1) jml = 1; if (jml > 20) jml = 20; var r = Enumerable.Range(0, jml).Select(_ => Random.Shared.Next(min, max + 1)).ToList(); return jml == 1 ? $"🎲 {min}-{max}: **{r[0]}**" : $"🎲 {jml}x {min}-{max}: **{string.Join(", ", r)}**"; }

    [KernelFunction("lempar_koin")][Description("Lempar koin.")]
    public string LemparKoin() => Random.Shared.Next(2) == 0 ? "🪙 **KEPALA!**" : "🪙 **EKOR!**";

    [KernelFunction("lempar_dadu")][Description("Lempar dadu.")]
    public string LemparDadu([Description("Sisi")] int sisi = 6, [Description("Jumlah")] int jml = 1) { if (sisi < 2) sisi = 6; if (jml < 1) jml = 1; if (jml > 10) jml = 10; var r = Enumerable.Range(0, jml).Select(_ => Random.Shared.Next(1, sisi + 1)).ToList(); return jml == 1 ? $"🎲 d{sisi}: **{r[0]}**" : $"🎲 {jml}d{sisi}: {string.Join("+", r)} = **{r.Sum()}**"; }

    [KernelFunction("hitung_karakter")][Description("Statistik teks.")]
    public string HitungKarakter([Description("Teks")] string t) => string.IsNullOrWhiteSpace(t) ? "⚠️ Kosong." : $"📝 Karakter: **{t.Length}** (no spasi: {t.Count(c => !char.IsWhiteSpace(c))}) | Kata: **{t.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length}** | Kalimat: **{Regex.Matches(t, @"[.!?]+").Count}**";

    [KernelFunction("ubah_kapitalisasi")][Description("UPPER/lower/Title/Sentence.")]
    public string UbahKapitalisasi([Description("Teks")] string t, [Description("Tipe")] string tipe = "Title") => string.IsNullOrWhiteSpace(t) ? "⚠️ Kosong." : tipe.ToLower() switch { "upper" => $"🔠 {t.ToUpper()}", "lower" => $"🔡 {t.ToLower()}", "title" => $"📰 {CultureInfo.CurrentCulture.TextInfo.ToTitleCase(t.ToLower())}", "sentence" => $"📝 {Regex.Replace(t.ToLower(), @"(^\w)|\.\s+(\w)", m => m.Value.ToUpper())}", _ => "⚠️ upper/lower/title/sentence" };

    [KernelFunction("balik_teks")][Description("Reverse teks.")]
    public string BalikTeks([Description("Teks")] string t) => string.IsNullOrWhiteSpace(t) ? "⚠️ Kosong." : $"🔄 {(new string(t.ToCharArray().Reverse().ToArray()))}";

    [KernelFunction("enkripsi_sederhana")][Description("Caesar cipher.")]
    public string CaesarCipher([Description("Teks")] string t, [Description("Geser")] int g = 3) { if (string.IsNullOrWhiteSpace(t)) return "⚠️ Kosong."; g = ((g % 26) + 26) % 26; var sb = new StringBuilder(); foreach (var c in t) { if (char.IsLetter(c)) { var o = char.IsUpper(c) ? 'A' : 'a'; sb.Append((char)((c - o + g) % 26 + o)); } else sb.Append(c); } return $"🔐 Geser {g}: **{sb}**"; }

    [KernelFunction("format_mata_uang")][Description("Format ke mata uang.")]
    public string FormatMataUang([Description("Jumlah")] double jml, [Description("Kode: IDR/USD/EUR")] string kode = "IDR") { var ci = kode.ToUpper() switch { "USD" => new CultureInfo("en-US"), "EUR" => new CultureInfo("de-DE"), "JPY" => new CultureInfo("ja-JP"), _ => new CultureInfo("id-ID") }; return $"💵 **{jml.ToString("C2", ci)}** ({kode.ToUpper()})"; }

    [KernelFunction("persentase")][Description("Hitung persentase.")]
    public string Persentase([Description("Nilai")] double n, [Description("Total/%")] double t, [Description("nilai_ke_persen/dari_persen")] string m = "nilai_ke_persen") => m.ToLower() switch { "nilai_ke_persen" => t == 0 ? "⚠️ /0" : $"📊 {n}/{t} = **{n / t * 100:N2}%**", "dari_persen" => $"📊 {t}% × {n} = **{n * t / 100:N2}**", _ => "⚠️ nilai_ke_persen / dari_persen" };

    [KernelFunction("diskon_harga")][Description("Kalkulasi diskon + pajak.")]
    public string DiskonHarga([Description("Harga awal")] double h, [Description("Diskon %")] double d = 0, [Description("Pajak %")] double p = 11) { var sd = h * (1 - d / 100); var ppn = sd * p / 100; return $"🛒 Harga: Rp{h:N0} | Diskon {d}%: -Rp{h * d / 100:N0} | PPN {p}%: +Rp{ppn:N0} | **TOTAL: Rp{sd + ppn:N0}**"; }

    [KernelFunction("info_sistem")][Description("Info sistem JuraganKost.")]
    public string InfoSistem() => $"🏠 **JuraganKost**\n🕐 {DateTime.Now:dd MMM yyyy HH:mm}\n🖥️ {Environment.OSVersion}\n📦 .NET {Environment.Version}\n💻 {Environment.ProcessorCount} core | 🧠 {GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024):N0} MB";
}

// ═══════════════════ KERNEL: Web ═══════════════════

public class WebFunctions
{
    private readonly string _key; private static readonly HttpClient _h = new() { Timeout = TimeSpan.FromSeconds(20) };
    public WebFunctions(string key) => _key = key;

    [KernelFunction("search_internet")][Description("Search via Tavily.")]
    public async Task<string> SearchInternetAsync([Description("Query")] string q, [Description("Max")] int max = 5)
    { if (string.IsNullOrEmpty(_key)) return "⚠️ Tavily key belum dikonfigurasi."; try { var r = await _h.PostAsync("https://api.tavily.com/search", new StringContent(JsonSerializer.Serialize(new { api_key = _key, query = q, max_results = max, search_depth = "basic" }), Encoding.UTF8, "application/json")); using var d = JsonDocument.Parse(await r.Content.ReadAsStringAsync()); var sb = new StringBuilder(); foreach (var x in d.RootElement.GetProperty("results").EnumerateArray()) sb.AppendLine($"**{x.GetProperty("title").GetString()}**\n{x.GetProperty("content").GetString()}\n🔗 {x.GetProperty("url").GetString()}\n"); return sb.Length > 0 ? sb.ToString() : "Tidak ada hasil."; } catch (Exception ex) { return $"Gagal: {ex.Message}"; } }

    [KernelFunction("scrap_webpage")][Description("Scrape URL.")]
    public async Task<string> ScrapAsync([Description("URL")] string url)
    { try { var h = await _h.GetStringAsync(url); var doc = new HtmlAgilityPack.HtmlDocument(); doc.LoadHtml(h); foreach (var n in doc.DocumentNode.SelectNodes("//script|//style") ?? new HtmlAgilityPack.HtmlNodeCollection(null)) n.Remove(); var t = Regex.Replace(doc.DocumentNode.InnerText, @"\s+", " ").Trim(); return t.Length > 4000 ? t[..4000] + "..." : t; } catch (Exception ex) { return $"Gagal: {ex.Message}"; } }
}

// ═══════════════════ KERNEL: Database ═══════════════════

public class DatabaseFunctions
{
    private readonly IServiceScopeFactory _scopeFactory;
    public DatabaseFunctions(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    [KernelFunction("cari_kamar_kosong")][Description("Cari kamar kosong: filter jenis, maxHarga, kota.")]
    public async Task<string> CariKamarKosongAsync([Description("Jenis")] string? jk = null, [Description("Max harga")] decimal? mh = null, [Description("Kota")] string? kt = null)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var q = db.Kamar.Include(k => k.Kost).Where(k => k.Status == StatusKamar.Kosong || k.Status == StatusKamar.Booking);
            if (!string.IsNullOrEmpty(jk) && Enum.TryParse<JenisKamar>(jk, out var j)) q = q.Where(k => k.Jenis == j);
            if (mh.HasValue) q = q.Where(k => k.HargaSewa <= mh.Value);
            if (!string.IsNullOrEmpty(kt)) q = q.Where(k => k.Kost!.Kota != null && k.Kost.Kota.Contains(kt));
            // 🔑 FIX: SQLite doesn't support ORDER BY on decimal (mapped to TEXT).
            // Cast to double so SQLite can CAST to REAL for ordering.
            var r = await q.OrderBy(k => (double)k.HargaSewa).Take(10).ToListAsync();
            if (!r.Any()) return "Tidak ada kamar kosong.";
            var sb = new StringBuilder(); sb.AppendLine("🏠 **Kamar Kosong:**\n");
            foreach (var k in r) { sb.AppendLine($"- **{k.NomorKamar}** ({k.Jenis}) - {k.HargaSewa:C0}/bln 📍{k.Kost?.Nama}"); if (!string.IsNullOrEmpty(k.Fasilitas)) sb.AppendLine($"  🛋️ {k.Fasilitas}"); sb.AppendLine(); }
            return sb.ToString();
        }
        catch (Exception ex) { return $"⚠️ Gagal query kamar: {ex.Message}"; }
    }

    [KernelFunction("info_kost")][Description("Info kost by nama/lokasi.")]
    public async Task<string> InfoKostAsync([Description("Nama/lokasi")] string? s = null)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var q = db.Kost.Include(k => k.Kamar).AsQueryable();
            if (!string.IsNullOrEmpty(s)) q = q.Where(k => k.Nama.Contains(s) || (k.Kota != null && k.Kota.Contains(s)) || k.Alamat.Contains(s));
            var r = await q.Take(5).ToListAsync(); if (!r.Any()) return "Tidak ditemukan.";
            var sb = new StringBuilder(); foreach (var k in r) { var a = k.Kamar.Count(x => x.Status == StatusKamar.Kosong); sb.AppendLine($"🏠 **{k.Nama}**\n📍{k.Alamat}, {k.Kota}\n🚪{a}/{k.Kamar.Count} kamar\n📝{k.Deskripsi}\n"); }
            return sb.ToString();
        }
        catch (Exception ex) { return $"⚠️ Gagal query kost: {ex.Message}"; }
    }

    [KernelFunction("cek_tagihan")][Description("Cek tagihan penghuni.")]
    public async Task<string> CekTagihanAsync([Description("Nama")] string? n = null)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var q = db.Tagihan.Include(t => t.Penghuni).AsQueryable();
            if (!string.IsNullOrEmpty(n)) q = q.Where(t => t.Penghuni != null && t.Penghuni.NamaLengkap.Contains(n));
            var r = await q.OrderByDescending(t => t.JatuhTempo).Take(10).ToListAsync(); if (!r.Any()) return "Tidak ada tagihan.";
            var sb = new StringBuilder(); sb.AppendLine("📋 **Tagihan:**\n");
            foreach (var t in r) sb.AppendLine($"- {(t.Status == StatusTagihan.Dibayar ? "✅" : t.Status == StatusTagihan.Terlambat ? "🔴" : "⏳")} {t.NomorTagihan} - {t.Penghuni?.NamaLengkap} - {t.Total:C0} - {t.JatuhTempo:dd MMM}");
            return sb.ToString();
        }
        catch (Exception ex) { return $"⚠️ Gagal query tagihan: {ex.Message}"; }
    }
}

// ═══════════════════ KERNEL: Kost ═══════════════════

public class KostKernelFunctions
{
    public KostKernelFunctions(IServiceScopeFactory _) { }
    [KernelFunction("bandingkan_harga")][Description("Bandingkan harga.")]
    public async Task<string> BandingkanHargaAsync() { await Task.CompletedTask; return "💰 **Harga:**\n|Tipe|Range|\n|---|---|\n|Standar|Rp800rb-1.2jt|\n|Premium|Rp1.5jt-2.3jt|\n|VIP|Rp2.5jt-4.5jt|"; }
    [KernelFunction("fasilitas_kost")][Description("Fasilitas kost.")]
    public async Task<string> FasilitasAsync() { await Task.CompletedTask; return "🛋️ **Fasilitas:** 🛏️ Kasur+Lemari | 📶 WiFi | 🛡️ CCTV 24j | ❄️ AC (Premium) | 🚿 Air panas (VIP) | 🧺 Laundry"; }
}

// ═══════════════════ KERNEL: File ═══════════════════

public class FileFunctions
{
    private readonly IStorageProvider _s; public FileFunctions(IStorageProvider s) => _s = s;
    [KernelFunction("baca_file_dari_url")][Description("Baca file dari URL.")]
    public async Task<string> BacaAsync([Description("URL")] string url) { try { var st = await _s.DownloadAsync(url); if (st == null) return "File tidak ditemukan."; using var r = new StreamReader(st); var t = await r.ReadToEndAsync(); return $"📄 {(t.Length > 5000 ? t[..5000] + "..." : t)}"; } catch (Exception ex) { return $"Gagal: {ex.Message}"; } }
}
