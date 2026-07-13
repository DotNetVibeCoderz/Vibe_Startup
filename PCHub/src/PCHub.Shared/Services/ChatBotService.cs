using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PCHub.Shared.Data;
using PCHub.Shared.DTOs;
using PCHub.Shared.Enums;
using PCHub.Shared.Interfaces;

namespace PCHub.Shared.Services;

public class ChatBotService : IChatBotService
{
    private readonly ConcurrentDictionary<Guid, ChatSession> _sessions = new();
    private readonly IServiceProvider _serviceProvider;
    private Kernel? _kernel;
    private IChatCompletionService? _chatService;
    private ChatBotSettings _settings = new();
    private bool _aiReady;
    private KernelFunctions? _kernelFunctions;

    public ChatBotService(IServiceProvider? serviceProvider = null)
    {
        _serviceProvider = serviceProvider!;
    }

    public void UpdateSettings(ChatBotSettings s) { _settings = s; InitKernel(); }

    private void InitKernel()
    {
        try
        {
            var b = Kernel.CreateBuilder();
            var p = _settings.Provider?.ToLower();
            var key = _settings.ApiKey;
            var ep = _settings.Endpoint;
            var model = _settings.Model;

            if (p == "openai" && !string.IsNullOrEmpty(key))
            { b.AddOpenAIChatCompletion(modelId: model ?? "gpt-4o-mini", apiKey: key); _aiReady = true; }
            else if (p == "ollama" && !string.IsNullOrEmpty(ep))
            { b.AddOpenAIChatCompletion(modelId: model ?? "llama3.2:latest", apiKey: "ollama", endpoint: new Uri(ep + "/v1")); _aiReady = true; }
            else if (p == "gemini" && !string.IsNullOrEmpty(key))
            { b.AddOpenAIChatCompletion(modelId: model ?? "gemini-1.5-flash", apiKey: key, endpoint: new Uri(ep ?? "https://generativelanguage.googleapis.com/v1beta/openai/")); _aiReady = true; }
            else if (p == "anthropic" && !string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(ep))
            { b.AddOpenAIChatCompletion(modelId: model ?? "claude-3-haiku-20240307", apiKey: key, endpoint: new Uri(ep)); _aiReady = true; }

            // Register KernelFunctions instance with service provider untuk akses DB
            _kernelFunctions = new KernelFunctions(_serviceProvider);
            b.Plugins.AddFromObject(_kernelFunctions, "pchub");

            if (_aiReady) { _kernel = b.Build(); _chatService = _kernel.GetRequiredService<IChatCompletionService>(); }
        }
        catch { _aiReady = false; }
    }

    public async Task<ChatMessageDto> SendMessageAsync(SendChatMessageRequest r)
    {
        if (!_sessions.ContainsKey(r.SessionId))
            _sessions[r.SessionId] = new ChatSession { Id = r.SessionId, Title = "Chat", CreatedAt = DateTime.UtcNow };
        var s = _sessions[r.SessionId];
        s.Messages.Add(new ChatMessageDto(r.SessionId, "user", r.Message, DateTime.UtcNow));
        if (s.Title == "Chat" && r.Message.Length > 0)
            s.Title = r.Message.Length > 40 ? r.Message[..40] + "..." : r.Message;

        var resp = (_aiReady && _kernel != null && _chatService != null)
            ? await AiRespond(r) : LocalRespond(r.Message);

        var msg = new ChatMessageDto(r.SessionId, "assistant", resp, DateTime.UtcNow);
        s.Messages.Add(msg);
        return msg;
    }

    private async Task<string> AiRespond(SendChatMessageRequest r)
    {
        try
        {
            var s = _sessions[r.SessionId];
            var h = new ChatHistory(_settings.SystemPrompt ??
                "Anda adalah Koh Dedi, asisten virtual PCHub Game Center. " +
                "Gunakan fungsi yang tersedia: query_database untuk data PC/game/user/billing, " +
                "search_internet untuk mencari info terkini, scrape_page untuk membaca halaman web, " +
                "serta fungsi-fungsi utility lainnya. Jawab dengan ramah dan informatif dalam bahasa Indonesia.");

            var mh = Math.Min(_settings.MaxHistoryMessages > 0 ? _settings.MaxHistoryMessages : 10, 20);
            foreach (var m in s.Messages.TakeLast(mh))
            { if (m.Role == "user") h.AddUserMessage(m.Content); else h.AddAssistantMessage(m.Content); }
            h.AddUserMessage(r.Message);

            var ex = new OpenAIPromptExecutionSettings
            {
                Temperature = _settings.Temperature,
                MaxTokens = _settings.MaxTokens > 0 ? _settings.MaxTokens : 2048,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };
            var result = await _chatService!.GetChatMessageContentAsync(h, ex, _kernel);
            return result?.Content ?? LocalRespond(r.Message);
        }
        catch { return LocalRespond(r.Message); }
    }

    public Task<List<ChatSessionDto>> GetSessionsAsync() =>
        Task.FromResult(_sessions.Values.Select(x => new ChatSessionDto(x.Id, x.Title, x.CreatedAt, x.Messages.Count)).OrderByDescending(x => x.CreatedAt).ToList());
    public Task<ChatSessionDto> CreateSessionAsync(string? title = null) { var s = new ChatSession { Id = Guid.NewGuid(), Title = title ?? "New Chat", CreatedAt = DateTime.UtcNow }; _sessions[s.Id] = s; return Task.FromResult(new ChatSessionDto(s.Id, s.Title, s.CreatedAt, 0)); }
    public Task DeleteSessionAsync(Guid id) { _sessions.TryRemove(id, out _); return Task.CompletedTask; }
    public Task ResetSessionAsync(Guid id) { if (_sessions.TryGetValue(id, out var s)) s.Messages.Clear(); return Task.CompletedTask; }
    public Task<List<ChatMessageDto>> GetSessionMessagesAsync(Guid id) => Task.FromResult(_sessions.TryGetValue(id, out var s) ? s.Messages.ToList() : []);

    private static string LocalRespond(string m)
    {
        var l = m.ToLower();
        if (l.Contains("harga")) return "💰 Tarif PCHub: Rp 6.000-12.000/jam. Membership: Silver Rp 50k, Gold Rp 150k, Platinum Rp 350k, VIP Rp 750k/bulan.";
        if (l.Contains("pc") || l.Contains("spek")) return "🖥️ 15 PC Gaming: 2 Premium (RTX 4090), 4 High (RTX 4080), 2 Streaming, 7 Standard.";
        if (l.Contains("game")) return "🎮 12 Game: Valorant, CS2, Dota 2, LoL, PUBG, Apex, Genshin, Elden Ring, FIFA 25, Minecraft, Forza, Resident Evil.";
        if (l.Contains("turnamen")) return "🏆 Turnamen: Valorant Championship (Rp 5jt), CS2 Clash (Rp 3jt), MLBB Cup (Rp 2jt).";
        if (l.Contains("jam") || l.Contains("buka")) return "🏢 BUKA 24 JAM! 📍 Jl. Gaming No. 123, Jakarta.";
        if (l.Contains("halo") || l.Contains("hai")) return "🤖 Halo! Saya Koh Dedi, asisten PCHub. Ada yang bisa saya bantu? 😊";
        return "🤖 Tanya lebih spesifik ya! Hubungi staff kami. 😊";
    }

    private class ChatSession { public Guid Id; public string Title = ""; public DateTime CreatedAt; public List<ChatMessageDto> Messages = []; }
}

// ========================================================================
// KERNEL FUNCTIONS - Dipanggil otomatis oleh AI model via function calling
// ========================================================================
public class KernelFunctions
{
    private readonly IServiceProvider _sp;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    public KernelFunctions(IServiceProvider sp) { _sp = sp; }

    // ================================================================
    // 🔍 SEARCH INTERNET (Tavily API + Google fallback)
    // ================================================================
    [Description("Mencari informasi terkini di internet. Gunakan untuk berita, tren game, info turnamen, atau hal yang tidak tersedia di database PCHub.")]
    [KernelFunction("search_internet")]
    public async Task<string> SearchInternet([Description("Kata kunci pencarian")] string query)
    {
        // Coba Tavily API dulu
        var config = _sp.GetService<IConfiguration>();
        var tavilyKey = config?["ChatBot:TavilyApiKey"] ?? "";

        if (!string.IsNullOrEmpty(tavilyKey))
        {
            try
            {
                var payload = JsonSerializer.Serialize(new { query, search_depth = "basic", max_results = 3 });
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                _http.DefaultRequestHeaders.Clear();
                _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {tavilyKey}");
                var resp = await _http.PostAsync("https://api.tavily.com/search", content);
                if (resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(body);
                    var results = doc.RootElement.GetProperty("results");
                    var sb = new StringBuilder();
                    sb.AppendLine($"🔍 Hasil pencarian untuk \"{query}\":\n");
                    int i = 1;
                    foreach (var r in results.EnumerateArray().Take(5))
                    {
                        sb.AppendLine($"**{i}.** {r.GetProperty("title").GetString()}");
                        sb.AppendLine($"   {Truncate(r.GetProperty("content").GetString() ?? "", 300)}");
                        sb.AppendLine($"   🔗 {r.GetProperty("url").GetString()}\n");
                        i++;
                    }
                    return sb.ToString();
                }
            }
            catch { }
        }

        // Fallback: Google search scrape
        try
        {
            var eq = Uri.EscapeDataString(query);
            var html = await _http.GetStringAsync($"https://www.google.com/search?q={eq}&hl=id");
            var snippets = ExtractGoogleSnippets(html);
            if (snippets.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"🔍 Hasil pencarian untuk \"{query}\":\n");
                for (int i = 0; i < Math.Min(snippets.Count, 5); i++)
                    sb.AppendLine($"**{i + 1}.** {Truncate(snippets[i], 300)}\n");
                return sb.ToString();
            }
        }
        catch { }

        return $"🔍 Maaf, tidak bisa mencari \"{query}\" saat ini. Coba gunakan scrape_page dengan URL spesifik, atau tanyakan data dari database PCHub.";
    }

    // ================================================================
    // 📄 SCRAPE PAGE HTML
    // ================================================================
    [Description("Membaca dan mengekstrak konten teks dari halaman web. Gunakan untuk membaca artikel, dokumentasi, atau halaman tertentu.")]
    [KernelFunction("scrape_page")]
    public async Task<string> ScrapePage([Description("URL halaman web")] string url)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return "❌ URL tidak valid. Gunakan format https://example.com";

            var html = await _http.GetStringAsync(uri);
            var text = ExtractTextFromHtml(html);
            text = CleanText(text);
            if (string.IsNullOrWhiteSpace(text)) return "❌ Tidak dapat mengekstrak konten.";
            if (text.Length > 3000) text = text[..3000] + $"\n\n... (dipotong, total {text.Length} karakter)";
            return $"📄 **Konten dari {uri.Host}:**\n\n{text}";
        }
        catch (HttpRequestException ex) { return $"❌ Gagal akses: {ex.Message}"; }
        catch (TaskCanceledException) { return "❌ Timeout - halaman terlalu lama."; }
        catch (Exception ex) { return $"❌ Error: {ex.Message}"; }
    }

    /// <summary>Baca file dari URL</summary>
    [Description("Membaca konten file dari URL (PDF, dokumen, teks). Gunakan saat user memberikan link file.")]
    [KernelFunction("read_file_from_url")]
    public async Task<string> ReadFileFromUrl([Description("URL file")] string fileUrl)
    {
        try
        {
            if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out _)) return "❌ URL file tidak valid.";
            var resp = await _http.GetAsync(fileUrl);
            if (!resp.IsSuccessStatusCode) return $"❌ HTTP {(int)resp.StatusCode}";
            var bytes = await resp.Content.ReadAsByteArrayAsync();
            var ct = resp.Content.Headers.ContentType?.MediaType ?? "";

            if (ct.Contains("text/")) return $"📄 Konten:\n\n{Truncate(ExtractTextFromHtml(Encoding.UTF8.GetString(bytes)), 3000)}";
            if (fileUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                var text = ExtractTextFromPdfBytes(bytes);
                if (!string.IsNullOrWhiteSpace(text)) return $"📄 PDF:\n\n{Truncate(text, 3000)}";
            }
            return $"📎 File diunduh ({bytes.Length / 1024} KB, tipe: {ct}). Gunakan scrape_page untuk konten web, query_database untuk data PCHub.";
        }
        catch (Exception ex) { return $"❌ Error: {ex.Message}"; }
    }

    // ================================================================
    // 📊 QUERY DATABASE
    // ================================================================
    [Description("Query data dari database PCHub. Kategori: pcs, games, users, billing, reservations, tournaments, memberships, promos.")]
    [KernelFunction("query_database")]
    public async Task<string> QueryDatabase(
        [Description("Kategori: pcs|games|users|billing|reservations|tournaments|memberships|promos")] string queryType,
        [Description("Filter: all|available|active|popular|pending (default: all)")] string filter = "all")
    {
        try
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return queryType.ToLower() switch
            {
                "pcs" => await QueryPcsAsync(db, filter),
                "games" => await QueryGamesAsync(db, filter),
                "users" => await QueryUsersAsync(db, filter),
                "billing" => await QueryBillingAsync(db, filter),
                "reservations" => await QueryReservationsAsync(db, filter),
                "tournaments" => await QueryTournamentsAsync(db, filter),
                "memberships" => await QueryMembershipsAsync(db, filter),
                "promos" => await QueryPromosAsync(db, filter),
                _ => $"❌ Kategori \"{queryType}\" tidak dikenal. Gunakan: pcs, games, users, billing, reservations, tournaments, memberships, promos"
            };
        }
        catch (Exception ex) { return $"❌ Error query: {ex.Message}"; }
    }

    [Description("Cek ketersediaan PC spesifik berdasarkan nama atau nomor.")]
    [KernelFunction("check_pc_availability")]
    public async Task<string> CheckPcAvailability([Description("Nama atau nomor PC")] string pcNameOrNumber)
    {
        try
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pc = await db.Pcs.FirstOrDefaultAsync(p => p.Name.Contains(pcNameOrNumber) || p.PcNumber.Contains(pcNameOrNumber));
            if (pc == null) return $"🔍 PC \"{pcNameOrNumber}\" tidak ditemukan. Gunakan query_database -> pcs untuk lihat daftar.";
            var emoji = pc.Status switch { PcStatus.Available => "🟢", PcStatus.InUse => "🔵", PcStatus.Maintenance => "🟡", PcStatus.Broken => "🔴", _ => "⚪" };
            return $"🖥️ **{pc.Name}** ({pc.PcNumber})\n{emoji} Status: **{pc.Status}**\n⚡ {pc.Specifications}\n💰 Rp {pc.HourlyRate:N0}/jam";
        }
        catch (Exception ex) { return $"❌ Error: {ex.Message}"; }
    }

    [Description("Cek sesi billing aktif user tertentu.")]
    [KernelFunction("check_active_billing")]
    public async Task<string> CheckActiveBilling([Description("Username atau nama user")] string username)
    {
        try
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username.Contains(username) || u.FullName.Contains(username));
            if (user == null) return $"🔍 User \"{username}\" tidak ditemukan.";
            var billing = await db.BillingSessions.Include(b => b.Pc).FirstOrDefaultAsync(b => b.UserId == user.Id && b.Status == BillingStatus.Active);
            if (billing == null) return $"👤 {user.FullName}: Tidak ada sesi aktif.";
            var dur = DateTime.UtcNow - billing.StartTime;
            return $"👤 **{user.FullName}** | 🖥️ {billing.Pc?.Name} | ⏱️ {dur.Hours}j {dur.Minutes}m | 💰 Rp {(decimal)dur.TotalHours * billing.HourlyRate:N0}";
        }
        catch (Exception ex) { return $"❌ Error: {ex.Message}"; }
    }

    // ================================================================
    // 🛠️ COMMON FUNCTIONS
    // ================================================================
    [Description("Waktu saat ini."), KernelFunction("get_time")]
    public static string GetTime() { var n = DateTime.Now; return $"🕐 {n:dddd, dd MMM yyyy HH:mm:ss} WIB"; }

    [Description("Hitung matematika."), KernelFunction("calculate")]
    public static string Calc([Description("Ekspresi")] string e)
    { try { return $"🧮 {e} = {new System.Data.DataTable().Compute(e, null)}"; } catch { return "❌ Error kalkulasi"; } }

    [Description("Harga membership PCHub."), KernelFunction("get_membership_prices")]
    public static string Prices() => "👑 Membership: Basic (Gratis) | 🥈 Silver (Rp 50k) | 🥇 Gold (Rp 150k) | 💎 Platinum (Rp 350k) | 👑 VIP (Rp 750k)";

    [Description("Jam operasional."), KernelFunction("get_operational_hours")]
    public static string Hours() => "🏢 PCHub BUKA 24 JAM! 📍 Jl. Gaming No. 123, Jakarta.";

    [Description("Format Rupiah."), KernelFunction("format_rupiah")]
    public static string FormatRp([Description("Jumlah")] decimal amt) => $"💰 Rp {amt:N0}";

    // ================================================================
    // PRIVATE QUERY HELPERS
    // ================================================================
    private static async Task<string> QueryPcsAsync(AppDbContext db, string filter)
    {
        var q = db.Pcs.AsQueryable();
        q = filter.ToLower() switch { "available" => q.Where(p => p.Status == PcStatus.Available), "inuse" => q.Where(p => p.Status == PcStatus.InUse), _ => q };
        var pcs = await q.OrderBy(p => p.PcNumber).ToListAsync();
        if (!pcs.Any()) return "📭 Tidak ada PC.";
        var sb = new StringBuilder(); sb.AppendLine($"🖥️ **{pcs.Count} PC:**\n");
        foreach (var p in pcs) { var e = p.Status == PcStatus.Available ? "🟢" : p.Status == PcStatus.InUse ? "🔵" : "⚪"; sb.AppendLine($"{e} {p.Name} ({p.PcNumber}) - {p.Status} - Rp {p.HourlyRate:N0}/jam"); }
        return sb.ToString();
    }
    private static async Task<string> QueryGamesAsync(AppDbContext db, string filter)
    {
        var q = db.Games.AsQueryable(); if (filter != "all") q = q.Where(g => g.Name.Contains(filter) || g.Genre.ToString().Contains(filter));
        var games = await q.OrderBy(g => g.Name).ToListAsync();
        if (!games.Any()) return "📭 Tidak ada game.";
        var sb = new StringBuilder(); sb.AppendLine($"🎮 **{games.Count} Game:**\n");
        foreach (var g in games) sb.AppendLine($"{(g.IsInstalled ? "✅" : "⚠️")} {g.Name} ({g.Genre}){(g.IsPopular ? " ⭐" : "")}");
        return sb.ToString();
    }
    private static async Task<string> QueryUsersAsync(AppDbContext db, string filter)
    {
        var q = db.Users.AsQueryable(); if (filter != "all") q = q.Where(u => u.Username.Contains(filter) || u.FullName.Contains(filter));
        var users = await q.OrderBy(u => u.Username).Take(20).ToListAsync();
        if (!users.Any()) return "📭 Tidak ada user.";
        var sb = new StringBuilder(); sb.AppendLine($"👥 **{users.Count} User:**\n");
        foreach (var u in users) sb.AppendLine($"• {u.FullName} (@{u.Username}) - {u.Role} - {u.MembershipTier}");
        return sb.ToString();
    }
    private static async Task<string> QueryBillingAsync(AppDbContext db, string filter)
    {
        var q = db.BillingSessions.Include(b => b.Pc).Include(b => b.User).AsQueryable();
        if (filter == "active") q = q.Where(b => b.Status == BillingStatus.Active);
        var billings = await q.OrderByDescending(b => b.StartTime).Take(15).ToListAsync();
        if (!billings.Any()) return "📭 Tidak ada billing.";
        var sb = new StringBuilder(); sb.AppendLine("💰 **Billing Terbaru:**\n");
        foreach (var b in billings) sb.AppendLine($"• {b.User?.Username} | {b.Pc?.Name} | {b.StartTime:dd/MM HH:mm} | {(b.EndTime != null ? (b.EndTime.Value - b.StartTime).ToString(@"hh\:mm") : "Aktif")} | Rp {b.TotalCost:N0}");
        return sb.ToString();
    }
    private static async Task<string> QueryReservationsAsync(AppDbContext db, string filter)
    {
        var q = db.Reservations.Include(r => r.User).Include(r => r.Pc).AsQueryable();
        if (filter == "pending") q = q.Where(r => r.Status == ReservationStatus.Pending);
        var items = await q.OrderByDescending(r => r.ReservationDate).Take(15).ToListAsync();
        if (!items.Any()) return "📭 Tidak ada reservasi.";
        var sb = new StringBuilder(); sb.AppendLine("📅 **Reservasi:**\n");
        foreach (var r in items) sb.AppendLine($"• {r.User?.Username} | {r.ReservationDate:dd/MM HH:mm} | {r.DurationMinutes}min | {r.Status}");
        return sb.ToString();
    }
    private static async Task<string> QueryTournamentsAsync(AppDbContext db, string filter)
    {
        var q = db.Tournaments.Include(t => t.Game).Include(t => t.Participants).AsQueryable();
        if (filter == "active") { var n = DateTime.UtcNow; q = q.Where(t => t.IsActive && t.EndDate >= n); }
        var items = await q.OrderByDescending(t => t.StartDate).Take(10).ToListAsync();
        if (!items.Any()) return "📭 Tidak ada turnamen.";
        var sb = new StringBuilder(); sb.AppendLine("🏆 **Turnamen:**\n");
        foreach (var t in items) sb.AppendLine($"• {t.Name} | {t.Game?.Name} | {t.StartDate:dd/MM}-{t.EndDate:dd/MM} | {t.Participants.Count}/{t.MaxParticipants} | Rp {t.PrizePool:N0}");
        return sb.ToString();
    }
    private static async Task<string> QueryMembershipsAsync(AppDbContext db, string filter)
    {
        var items = await db.Memberships.Where(m => m.IsActive).OrderBy(m => m.MonthlyPrice).ToListAsync();
        if (!items.Any()) return "📭 Tidak ada membership.";
        var sb = new StringBuilder(); sb.AppendLine("👑 **Membership:**\n");
        foreach (var m in items) sb.AppendLine($"• {m.Name} ({m.Tier}) - Rp {m.MonthlyPrice:N0}/bln - Diskon {m.DiscountPercentage}%");
        return sb.ToString();
    }
    private static async Task<string> QueryPromosAsync(AppDbContext db, string filter)
    {
        var n = DateTime.UtcNow;
        var items = await db.Promos.Where(p => p.IsActive && p.StartDate <= n && p.EndDate >= n).OrderByDescending(p => p.DiscountPercentage).ToListAsync();
        if (!items.Any()) return "📭 Tidak ada promo aktif.";
        var sb = new StringBuilder(); sb.AppendLine("🎉 **Promo:**\n");
        foreach (var p in items) sb.AppendLine($"• {p.Name} - Diskon {p.DiscountPercentage}%" + (p.PromoCode != null ? $" | `{p.PromoCode}`" : "") + $" | s/d {p.EndDate:dd MMM}");
        return sb.ToString();
    }

    // ================================================================
    // HTML HELPERS
    // ================================================================
    private static List<string> ExtractGoogleSnippets(string html)
    {
        var snippets = new List<string>();
        foreach (Match m in Regex.Matches(html, @"<span[^>]*class=""[^""]*st[^""]*""[^>]*>(.*?)</span>", RegexOptions.Singleline))
        { var t = StripHtml(m.Groups[1].Value); if (!string.IsNullOrWhiteSpace(t) && t.Length > 20) snippets.Add(t.Trim()); }
        return snippets;
    }
    private static string ExtractTextFromHtml(string html)
    {
        html = Regex.Replace(html, @"<(script|style)[^>]*>.*?</\1>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<!--.*?-->", "", RegexOptions.Singleline);
        return StripHtml(html);
    }
    private static string StripHtml(string html)
    {
        html = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"</?(p|div|li|h[1-6])[^>]*>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<[^>]+>", "");
        return WebUtility.HtmlDecode(html);
    }
    private static string CleanText(string text)
    {
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        text = Regex.Replace(text, @"[ \t]+", " ");
        return text.Trim();
    }
    private static string ExtractTextFromPdfBytes(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        var sb = new StringBuilder();
        foreach (Match m in Regex.Matches(text, @"\(([^)]*)\)\s*Tj")) sb.Append(m.Groups[1].Value);
        return sb.ToString();
    }
    private static string Truncate(string value, int max) => string.IsNullOrEmpty(value) || value.Length <= max ? value ?? "" : value[..(max - 3)] + "...";
}
