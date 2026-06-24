using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using HolySafar.Data;
using HolySafar.Models;
using Markdig;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace HolySafar.Services;

public class ChatbotService
{
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;

    public ChatbotService(IConfiguration config, IServiceScopeFactory scopeFactory)
    { _config = config; _scopeFactory = scopeFactory; }

    public string ChatbotName => _config["Chatbot:Name"] ?? "Syeikh Jenggot";
    public string Provider => _config["Chatbot:Provider"] ?? "OpenAI";

    private Kernel CreateKernel()
    {
        var builder = Kernel.CreateBuilder();
        var provider = _config["Chatbot:Provider"] ?? "OpenAI";
        switch (provider)
        {
            case "OpenAI": ConfigureOpenAI(builder); break;
            case "Anthropic": ConfigureOpenAICompat(builder, "Anthropic"); break;
            case "Gemini": ConfigureOpenAICompat(builder, "Gemini"); break;
            case "Ollama": ConfigureOpenAICompat(builder, "Ollama"); break;
            default: ConfigureOpenAI(builder); break;
        }
        builder.Plugins.AddFromType<TimePlugin>("TimePlugin");
        builder.Plugins.AddFromType<CalculationsPlugin>("MathPlugin");
        builder.Plugins.AddFromType<WebSearchPlugin>("WebSearchPlugin");
        builder.Plugins.AddFromType<HijriCalendarPlugin>("HijriCalendarPlugin");
        builder.Plugins.AddFromType<TravelInfoPlugin>("TravelInfoPlugin");
        var dbPlugin = new DatabaseQueryPlugin(_scopeFactory);
        builder.Plugins.AddFromObject(dbPlugin, "DatabaseQueryPlugin");
        return builder.Build();
    }

    private void ConfigureOpenAI(IKernelBuilder builder)
    {
        var apiKey = _config["Chatbot:Providers:OpenAI:ApiKey"] ?? "";
        var model = _config["Chatbot:Providers:OpenAI:Model"] ?? "gpt-4o";
        var endpoint = _config["Chatbot:Providers:OpenAI:Endpoint"] ?? "";
        if (!string.IsNullOrEmpty(endpoint)) builder.AddOpenAIChatCompletion(model, new Uri(endpoint), apiKey);
        else builder.AddOpenAIChatCompletion(model, apiKey);
    }
    private void ConfigureOpenAICompat(IKernelBuilder builder, string providerKey)
    {
        var apiKey = _config[$"Chatbot:Providers:{providerKey}:ApiKey"] ?? "not-needed";
        var model = _config[$"Chatbot:Providers:{providerKey}:Model"] ?? "llama3.1";
        var endpoint = _config[$"Chatbot:Providers:{providerKey}:Endpoint"] ?? "";
        if (providerKey == "Ollama" && string.IsNullOrEmpty(endpoint)) endpoint = "http://localhost:11434/v1";
        if (!string.IsNullOrEmpty(endpoint)) builder.AddOpenAIChatCompletion(model, new Uri(endpoint), apiKey);
        else builder.AddOpenAIChatCompletion(model, apiKey);
    }

    public async Task<(ChatSession Session, List<ChatbotMessage> Messages)> GetOrCreateSessionAsync(int userId, int? sessionId = null)
    { using var sc = _scopeFactory.CreateScope(); var db = sc.ServiceProvider.GetRequiredService<AppDbContext>(); var s = sessionId.HasValue ? await db.ChatSessions.Include(x => x.Messages).FirstOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId) ?? await Make(db, userId) : await Make(db, userId); return (s, s.Messages.OrderBy(m => m.CreatedAt).ToList()); }
    private async Task<ChatSession> Make(AppDbContext db, int uid) { var s = new ChatSession { UserId = uid, Title = "New Chat", CreatedAt = DateTime.UtcNow, LastActivity = DateTime.UtcNow }; db.ChatSessions.Add(s); await db.SaveChangesAsync(); return s; }
    public async Task<List<ChatSession>> GetUserSessionsAsync(int uid) { using var s = _scopeFactory.CreateScope(); return await s.ServiceProvider.GetRequiredService<AppDbContext>().ChatSessions.Where(x => x.UserId == uid).OrderByDescending(x => x.LastActivity).ToListAsync(); }
    public async Task DeleteSessionAsync(int sid, int uid) { using var s = _scopeFactory.CreateScope(); var db = s.ServiceProvider.GetRequiredService<AppDbContext>(); var x = await db.ChatSessions.FirstOrDefaultAsync(c => c.Id == sid && c.UserId == uid); if (x != null) { db.ChatbotMessages.RemoveRange(x.Messages); db.ChatSessions.Remove(x); await db.SaveChangesAsync(); } }
    public async Task ResetSessionAsync(int sid, int uid) { using var sc = _scopeFactory.CreateScope(); var db = sc.ServiceProvider.GetRequiredService<AppDbContext>(); var x = await db.ChatSessions.Include(c => c.Messages).FirstOrDefaultAsync(c => c.Id == sid && c.UserId == uid); if (x != null) { db.ChatbotMessages.RemoveRange(x.Messages); x.Title = "New Chat"; x.LastActivity = DateTime.UtcNow; await db.SaveChangesAsync(); } }

    public async Task<string> SendMessageAsync(int sid, int uid, string message, string? imageUrl = null, string? attachmentUrl = null)
    {
        using var sc = _scopeFactory.CreateScope(); var db = sc.ServiceProvider.GetRequiredService<AppDbContext>();
        var s = await db.ChatSessions.Include(x => x.Messages).FirstOrDefaultAsync(x => x.Id == sid && x.UserId == uid);
        if (s == null) return "Session not found.";
        db.ChatbotMessages.Add(new ChatbotMessage { SessionId = sid, Role = "user", Content = message, ImageUrl = imageUrl, AttachmentUrl = attachmentUrl, CreatedAt = DateTime.UtcNow });
        s.LastActivity = DateTime.UtcNow; if (s.Title == "New Chat" && !string.IsNullOrEmpty(message)) s.Title = message.Length > 50 ? message[..50] + "..." : message;
        await db.SaveChangesAsync();
        try
        {
            var k = CreateKernel(); var cc = k.GetRequiredService<IChatCompletionService>(); var h = new ChatHistory();
            h.AddSystemMessage(_config["Chatbot:SystemPrompt"] ?? "Kamu adalah Syeikh Jenggot.");
            var ctx = await Ctx(db, uid); if (!string.IsNullOrEmpty(ctx)) h.AddSystemMessage(ctx);
            foreach (var m in s.Messages.OrderByDescending(m => m.CreatedAt).Take(20).Reverse())
            { if (m.Role == "user") h.AddUserMessage(m.Content + (m.AttachmentUrl != null ? " [Attachment]" : "")); else if (m.Role == "assistant") h.AddAssistantMessage(m.Content); }
            if (!string.IsNullOrEmpty(imageUrl) && Uri.TryCreate(imageUrl, UriKind.Absolute, out var img))
            { var items = new ChatMessageContentItemCollection(); if (!string.IsNullOrEmpty(message)) items.Add(new TextContent(message)); items.Add(new ImageContent(img)); if (!string.IsNullOrEmpty(attachmentUrl)) items.Add(new TextContent($"\n[Attach: {attachmentUrl}]")); h.AddUserMessage(items); }
            else h.AddUserMessage(message + (attachmentUrl != null ? $" [Attach: {attachmentUrl}]" : ""));
            var st = new OpenAIPromptExecutionSettings { Temperature = _config.GetValue<double>("Chatbot:Temperature", 0.7), MaxTokens = _config.GetValue<int>("Chatbot:MaxTokens", 4096), ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions };
            var r = await cc.GetChatMessageContentAsync(h, st, k); var t = r.Content ?? "*Maaf.*";
            db.ChatbotMessages.Add(new ChatbotMessage { SessionId = sid, Role = "assistant", Content = t, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(); return t;
        }
        catch (Exception ex) { var fb = $"*Error.*\n{ex.Message}"; db.ChatbotMessages.Add(new ChatbotMessage { SessionId = sid, Role = "assistant", Content = fb, CreatedAt = DateTime.UtcNow }); await db.SaveChangesAsync(); return fb; }
    }

    private async Task<string> Ctx(AppDbContext db, int uid) { var u = await db.Users.FindAsync(uid); if (u == null) return ""; var l = new List<string> { $"User: {u.FullName}" }; var j = await db.Jamaah.FirstOrDefaultAsync(x => x.UserId == uid); if (j != null) { l.Add($"Jamaah: {j.NamaLengkap}"); var p = await db.Paket.FindAsync(j.PaketId); if (p != null) l.Add($"Paket: {p.NamaPaket} Rp{p.Harga:N0}"); } return string.Join("\n", l); }

    public static string RenderMarkdown(string md) => string.IsNullOrEmpty(md) ? "" : Markdown.ToHtml(md, new MarkdownPipelineBuilder().UseAdvancedExtensions().UseEmojiAndSmiley().Build());
}


// ================================================================
// KERNEL PLUGINS
// ================================================================

public class TimePlugin
{
    [KernelFunction, Description("Waktu UTC")] public string GetUtcTime() => $"{DateTime.UtcNow:dddd, dd MMMM yyyy HH:mm:ss} UTC";
    [KernelFunction, Description("Waktu Jakarta/WIB")] public string GetJakartaTime() { try { var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); return $"{TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz):dddd, dd MMMM yyyy HH:mm} WIB"; } catch { return $"{DateTime.UtcNow.AddHours(7):dddd, dd MMMM yyyy HH:mm} WIB"; } }
    [KernelFunction, Description("Waktu Saudi/AST")] public string GetSaudiTime() { try { var tz = TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time"); return $"{TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz):dddd, dd MMMM yyyy HH:mm} AST"; } catch { return $"{DateTime.UtcNow.AddHours(3):dddd, dd MMMM yyyy HH:mm} AST"; } }
    [KernelFunction, Description("Hitung hari (yyyy-MM-dd)")] public string CountDaysUntil(string d) { if (!DateTime.TryParse(d, out var t)) return "Format: yyyy-MM-dd"; var days = (t.Date - DateTime.UtcNow.Date).Days; return days >= 0 ? $"Tersisa {days} hari" : $"Lewat {-days} hari"; }
    [KernelFunction, Description("Nama hari")] public string GetDayOfWeek(string d) { if (!DateTime.TryParse(d, out var t)) return "Format: yyyy-MM-dd"; return $"{t:dd MMMM yyyy} = {new CultureInfo("id-ID").DateTimeFormat.GetDayName(t.DayOfWeek)}"; }
}

public class CalculationsPlugin
{
    [KernelFunction, Description("Kalkulasi")] public string Calculate(string e) { try { return $"{e} = {new System.Data.DataTable().Compute(e, null):N0}"; } catch { return $"Tidak bisa: {e}"; } }
    [KernelFunction, Description("Konversi mata uang (amount,from,to)")] public string ConvertCurrency(decimal a, string f, string t) { var r = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["USD"] = 1, ["IDR"] = 15700, ["SAR"] = 3.75m, ["EUR"] = 0.92m, ["MYR"] = 4.70m }; if (!r.ContainsKey(f) || !r.ContainsKey(t)) return "Gunakan: USD,IDR,SAR,EUR,MYR."; return $"{a:N2} {f} = {(a / r[f]) * r[t]:N2} {t}"; }
    [KernelFunction, Description("Persentase")] public string CalcPercentage(decimal p, decimal t) => $"{p}% x {t:N0} = {t * p / 100:N0}";
}

public class WebSearchPlugin
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly IConfiguration _cfg;
    public WebSearchPlugin() : this(null!) { }
    public WebSearchPlugin(IConfiguration cfg) { _cfg = cfg ?? new ConfigurationBuilder().AddJsonFile("appsettings.json", optional: true).Build(); }
    [KernelFunction, Description("Cari internet via Tavily")] public async Task<string> SearchInternet(string q) { var k = _cfg["Chatbot:TavilyApiKey"]; if (string.IsNullOrEmpty(k)) return "Tavily key belum dikonfig."; try { var r = await _http.PostAsJsonAsync("https://api.tavily.com/search", new { api_key = k, query = q, search_depth = "advanced", max_results = 5 }); var j = await r.Content.ReadAsStringAsync(); using var d = JsonDocument.Parse(j); var res = d.RootElement.GetProperty("results"); var ans = d.RootElement.TryGetProperty("answer", out var a) ? a.GetString() : null; var l = new List<string>(); if (!string.IsNullOrEmpty(ans)) l.Add($"**Ringkasan:** {ans}"); l.Add("**Hasil:**"); int i = 1; foreach (var x in res.EnumerateArray().Take(5)) { l.Add($"{i}. **{x.GetProperty("title").GetString()}** — {x.GetProperty("content").GetString()} — [Link]({x.GetProperty("url").GetString()})"); i++; } return string.Join("\n", l); } catch (Exception e) { return $"Gagal: {e.Message}"; } }
    [KernelFunction, Description("Scrape halaman web")] public async Task<string> ScrapeWebpage(string u) { try { var h = await _http.GetStringAsync(u); var t = System.Text.RegularExpressions.Regex.Replace(h, @"<(script|style)[^>]*>.*?</\1>", "", System.Text.RegularExpressions.RegexOptions.Singleline); t = System.Text.RegularExpressions.Regex.Replace(t, "<[^>]+>", " "); t = System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ").Trim(); t = System.Net.WebUtility.HtmlDecode(t); if (t.Length > 4000) t = t[..4000] + "..."; return $"**{u}:**\n{t}"; } catch (Exception e) { return $"Gagal: {e.Message}"; } }
    [KernelFunction, Description("Baca file dari URL")] public async Task<string> ReadFileFromUrl(string u) { try { var b = await _http.GetByteArrayAsync(u); var t = System.Text.Encoding.UTF8.GetString(b); if (t.Length > 5000) t = t[..5000] + $"...({t.Length} chars)"; return $"**File:**\n```\n{t}\n```"; } catch (Exception e) { return $"Gagal: {e.Message}"; } }
}

public class DatabaseQueryPlugin
{
    private readonly IServiceScopeFactory _sf;
    public DatabaseQueryPlugin(IServiceScopeFactory sf) { _sf = sf; }

    [KernelFunction, Description("Info jamaah by nama atau NIK")]
    public async Task<string> GetJamaahInfo(string search)
    {
        using var sc = _sf.CreateScope(); var db = sc.ServiceProvider.GetRequiredService<AppDbContext>();
        var term = search.Trim().ToLower();
        var all = await db.Jamaah.Include(x => x.Paket).ToListAsync();
        var j = all.FirstOrDefault(x =>
            (!string.IsNullOrEmpty(x.NamaLengkap) && x.NamaLengkap.ToLower().Contains(term)) ||
            (!string.IsNullOrEmpty(x.Nik) && x.Nik.ToLower().Contains(term)));
        if (j == null) return $"❌ Jamaah '{search}' tidak ditemukan.";
        return $"✅ **{j.NamaLengkap}**\n• NIK: {j.Nik}\n• Paspor: {j.NoPaspor}\n• Paket: {j.Paket?.NamaPaket ?? "(belum pilih)"}\n• Dokumen: {j.StatusDokumen}\n• Keberangkatan: {j.StatusKeberangkatan}";
    }

    [KernelFunction, Description("Daftar paket tersedia")]
    public async Task<string> GetAvailablePackages()
    { using var sc = _sf.CreateScope(); var db = sc.ServiceProvider.GetRequiredService<AppDbContext>(); var p = await db.Paket.Where(x => x.IsActive && x.IsPublished).OrderBy(x => x.Harga).ToListAsync(); return !p.Any() ? "Belum ada paket." : "**🕋 Paket Tersedia:**\n" + string.Join("\n", p.Select(x => $"- **{x.NamaPaket}** ({x.JenisPaket}) — Rp{x.Harga:N0} — {x.DurasiHari} hari — 🏨 {x.NamaHotelMekkah} — ✈️ {x.Maskapai} — Kuota: {x.Terisi}/{x.Kuota}")); }

    [KernelFunction, Description("Pengumuman terkini")]
    public async Task<string> GetAnnouncements()
    { using var sc = _sf.CreateScope(); var db = sc.ServiceProvider.GetRequiredService<AppDbContext>(); var a = await db.Pengumuman.Where(x => x.IsActive).OrderByDescending(x => x.CreatedAt).Take(5).ToListAsync(); return !a.Any() ? "Tidak ada." : "**📢 Pengumuman:**\n" + string.Join("\n", a.Select(x => $"- **{x.Judul}**\n  {x.Isi}")) + $"\n\nTotal: {a.Count} pengumuman."; }

    [KernelFunction, Description("Kontak darurat")]
    public async Task<string> GetEmergencyContacts()
    { using var sc = _sf.CreateScope(); var db = sc.ServiceProvider.GetRequiredService<AppDbContext>(); var c = await db.KontakDarurat.Where(x => x.IsActive).ToListAsync(); return !c.Any() ? "Tidak ada." : "**📞 Kontak Darurat:**\n" + string.Join("\n", c.Select(x => $"- **{x.Nama}** — 📞 {x.Telepon} — {x.Peran}")); }

    [KernelFunction, Description("Produk marketplace")]
    public async Task<string> GetMarketplaceProducts()
    { using var sc = _sf.CreateScope(); var db = sc.ServiceProvider.GetRequiredService<AppDbContext>(); var p = await db.Produk.Where(x => x.IsActive).ToListAsync(); return !p.Any() ? "Tidak ada." : "**🛍️ Marketplace:**\n" + string.Join("\n", p.Select(x => $"- **{x.NamaProduk}** [{x.Kategori}] — Rp{x.Harga:N0} — Stok: {x.Stok}")); }

    [KernelFunction, Description("Status pembayaran by nama")]
    public async Task<string> GetPaymentStatus(string nama)
    {
        using var sc = _sf.CreateScope(); var db = sc.ServiceProvider.GetRequiredService<AppDbContext>();
        var term = nama.Trim().ToLower();
        var j = await db.Jamaah.Include(x => x.Paket).FirstOrDefaultAsync(x => x.NamaLengkap.ToLower().Contains(term));
        if (j == null) return $"❌ '{nama}' tidak ditemukan.";
        var pm = await db.Pembayaran.Include(x => x.Paket).FirstOrDefaultAsync(x => x.JamaahId == j.Id);
        var cc = await db.Cicilan.CountAsync(x => x.PembayaranId == (pm != null ? pm.Id : 0));
        if (pm == null) return $"**{j.NamaLengkap}** — Paket: {j.Paket?.NamaPaket ?? "-"} — Belum ada data pembayaran.";
        return $"**💰 {j.NamaLengkap}**\n• Paket: {pm.Paket?.NamaPaket ?? j.Paket?.NamaPaket ?? "-"}\n• Total: Rp{pm.TotalBiaya:N0}\n• Dibayar: Rp{pm.TotalDibayar:N0}\n• Sisa: Rp{(pm.TotalBiaya - pm.TotalDibayar):N0}\n• Status: {pm.Status}\n• Cicilan: {cc}x";
    }
}

public class HijriCalendarPlugin
{
    [KernelFunction, Description("Tanggal Hijriyah (±1-2 hari)")] public string GetHijriDate() { var g = DateTime.UtcNow; var y = (int)((g.Year - 622) * 1.0307); var m = new[] { "Muharram","Safar","R.Awwal","R.Akhir","J.Awwal","J.Akhir","Rajab","Sya'ban","Ramadhan","Syawal","Dzulqa'dah","Dzulhijjah" }; var d = (g - new DateTime(622, 7, 16)).TotalDays * (354.367 / 365.25); var hm = Math.Clamp((int)((d % 354.367) / 29.53) % 12, 0, 11); var hd = (int)((d % 354.367) % 29.53) + 1; return $"📅 {hd} {m[hm]} {y} H (±1-2 hari)"; }
    [KernelFunction, Description("Waktu sholat")] public string GetPrayerTimesInfo() => "**🕌 Sholat:**\nMakkah: Subuh~04:45 Dzuhur~12:15 Ashar~15:30 Maghrib~18:45 Isya~20:15\nMadinah: +5 menit";
}

public class TravelInfoPlugin
{
    [KernelFunction, Description("Tips perjalanan")] public string GetTravelTips() => "**💡 Tips:**\n✅ Jalan kaki rutin\n✅ Paspor min 6 bln\n✅ Pakaian ringan\n✅ Bawa obat & vitamin\n✅ Riyal cash\n✅ SIM lokal\n✅ Hafal doa";
    [KernelFunction, Description("Persyaratan visa")] public string GetVisaRequirements() => "**📋 Visa:**\n- Paspor min 6 bln\n- Foto 4x6 putih\n- Buku nikah (pasangan)\n- Vaksin meningitis\n- Biaya ~Rp2-3.5jt\n⚠️ Cek search_internet untuk update.";
}
