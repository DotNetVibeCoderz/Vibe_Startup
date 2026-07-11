using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SmartDrive.Data;
using SmartDrive.Models.Entities;
using SmartDrive.Models.Enums;
using SmartDrive.Models.ViewModels;

namespace SmartDrive.Services;

public class ChatBotService
{
    private readonly IConfiguration _cfg; private readonly IServiceProvider _sp;
    private readonly ILogger<ChatBotService> _log; private readonly IHttpClientFactory _hf;
    private Kernel? _kernel; private IChatCompletionService? _chat;
    private ChatBotConfig _config = new();
    private bool _initialized;

    public ChatBotService(IConfiguration cfg, IServiceProvider sp, ILogger<ChatBotService> log, IHttpClientFactory hf)
    { _cfg = cfg; _sp = sp; _log = log; _hf = hf; LoadConfig(); }

    private void LoadConfig()
    {
        _config = new ChatBotConfig
        {
            ModelProvider = _cfg.GetValue("ChatBot:ModelProvider", "OpenAI")!,
            ModelId = _cfg.GetValue("ChatBot:ModelId", "gpt-4")!,
            ApiKey = _cfg.GetValue("ChatBot:ApiKey", "")!,
            Endpoint = _cfg.GetValue("ChatBot:Endpoint", "")!,
            SystemPrompt = _cfg.GetValue("ChatBot:SystemPrompt",
                @"Kamu adalah Om Bambang, asisten virtual SmartDrive Academy - sekolah mengemudi terpercaya.
Gunakan bahasa Indonesia santai & ramah. Bantu siswa, instruktur, dan admin.
Kamu bisa akses data SmartDrive (kendaraan, lokasi, statistik, produk) via fungsi yang tersedia.
Jawab dengan Markdown jika perlu (tabel, list, bold, dll).")!,
            Temperature = _cfg.GetValue("ChatBot:Temperature", 0.7),
            MaxTokens = _cfg.GetValue("ChatBot:MaxTokens", 2000),
            TopP = _cfg.GetValue("ChatBot:TopP", 1)
        };
    }

    public bool Initialize()
    {
        if (_initialized) return true;
        try
        {
            var b = Kernel.CreateBuilder();

            if (!string.IsNullOrEmpty(_config.Endpoint))
                b.AddOpenAIChatCompletion(_config.ModelId, new Uri(_config.Endpoint), _config.ApiKey);
            else if (!string.IsNullOrEmpty(_config.ApiKey))
                b.AddOpenAIChatCompletion(_config.ModelId, _config.ApiKey);
            else
            {
                _log.LogWarning("ChatBot: No API key or endpoint configured. AI chat disabled.");
                return false;
            }

            b.Plugins.AddFromType<DateTimePlugin>();
            b.Plugins.AddFromType<MathPlugin>();
            b.Plugins.AddFromObject(new TavilySearchPlugin(_cfg, _hf), "TavilySearch");
            b.Plugins.AddFromObject(new DatabaseQueryPlugin(_sp), "DatabaseQuery");
            _kernel = b.Build();
            _chat = _kernel.GetRequiredService<IChatCompletionService>();
            _initialized = true;
            _log.LogInformation("ChatBot initialized with model {Model} via {Provider}", _config.ModelId, _config.ModelProvider);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ChatBot initialization failed");
            return false;
        }
    }

    public async Task<string> ChatAsync(int sessionId, string userMsg, string userId,
        List<string>? imgUrls = null, List<string>? attUrls = null)
    {
        // Simpan pesan user dulu (pakai scope terpisah, simpan segera)
        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SmartDriveDbContext>();
            db.ChatBotMessages.Add(new ChatBotMessage
            {
                SessionId = sessionId, Role = "user", Content = userMsg,
                ImageUrls = imgUrls?.Count > 0 ? JsonSerializer.Serialize(imgUrls) : null,
                AttachmentUrls = attUrls?.Count > 0 ? JsonSerializer.Serialize(attUrls) : null,
                SentAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Fallback: jika AI tidak tersedia, balas dengan pesan bantuan
        if (!_initialized && !Initialize())
        {
            var fallback = GenerateFallbackResponse(userMsg);
            await SaveAssistantMessage(sessionId, fallback);
            return fallback;
        }

        if (_chat == null || _kernel == null)
        {
            var fallback = "⚠️ Maaf, layanan AI sedang tidak tersedia. Coba lagi nanti atau hubungi admin.\n\nSementara itu, coba tanya:\n- **Jadwal**: /student/book\n- **Materi**: /student/theory\n- **Lokasi**: /locations";
            await SaveAssistantMessage(sessionId, fallback);
            return fallback;
        }

        try
        {
            // Bangun chat history (hanya pesan yang sudah tersimpan)
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SmartDriveDbContext>();

            var history = new ChatHistory(_config.SystemPrompt);

            // Ambil pesan yang SUDAH tersimpan (message barusan sudah di-save di atas)
            var prev = await db.ChatBotMessages
                .Where(m => m.SessionId == sessionId)
                .OrderBy(m => m.SentAt)
                .Take(30)
                .ToListAsync();

            foreach (var m in prev)
            {
                if (m.Role == "user")
                {
                    var c = m.Content;
                    if (!string.IsNullOrEmpty(m.ImageUrls))
                    { try { var u = JsonSerializer.Deserialize<List<string>>(m.ImageUrls); if (u?.Count > 0) c += "\n[Gambar: " + string.Join(", ", u) + "]"; } catch { } }
                    if (!string.IsNullOrEmpty(m.AttachmentUrls))
                    { try { var u = JsonSerializer.Deserialize<List<string>>(m.AttachmentUrls); if (u?.Count > 0) c += "\n[Dokumen: " + string.Join(", ", u) + "]"; } catch { } }
                    history.AddUserMessage(c);
                }
                else if (m.Role == "assistant")
                    history.AddAssistantMessage(m.Content);
            }

            // Inject context user
            var ctx = await GetDbInfo(db, userId);
            if (!string.IsNullOrEmpty(ctx))
                history.AddSystemMessage(ctx);

            var st = new OpenAIPromptExecutionSettings
            {
                Temperature = _config.Temperature,
                MaxTokens = _config.MaxTokens,
                TopP = _config.TopP,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            var resp = await _chat.GetChatMessageContentAsync(history, st, _kernel);
            var reply = resp.Content ?? "Maaf, saya tidak bisa menjawab saat ini.";

            await SaveAssistantMessage(sessionId, reply);
            return reply;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Chat error for session {SessionId}", sessionId);
            var fallback = $"⚠️ Terjadi kesalahan: {ex.Message}\n\nSilakan coba lagi atau hubungi admin.";
            await SaveAssistantMessage(sessionId, fallback);
            return fallback;
        }
    }

    /// <summary>
    /// Fallback response ketika AI tidak tersedia — tetap helpful!
    /// </summary>
    private string GenerateFallbackResponse(string msg)
    {
        var lower = msg.ToLower();
        if (lower.Contains("kendaraan") || lower.Contains("mobil"))
            return "🚗 Untuk melihat daftar kendaraan latihan, buka menu **Admin → Kendaraan** atau kunjungi halaman `/admin/vehicles`.\n\nSaat ini layanan AI belum dikonfigurasi. Tambahkan `ChatBot:ApiKey` di `appsettings.json` untuk mengaktifkan Om Bambang AI.";
        if (lower.Contains("lokasi") || lower.Contains("tempat") || lower.Contains("latihan"))
            return "📍 Lihat peta lokasi latihan di halaman **Peta Lokasi** atau kunjungi `/locations`.\n\n💡 Tambahkan API key AI di appsettings untuk mengaktifkan asisten cerdas.";
        if (lower.Contains("jadwal") || lower.Contains("booking"))
            return "📅 Booking jadwal latihan bisa dilakukan di halaman **Siswa → Booking** (`/student/book`).\n\nPastikan API key AI sudah dikonfigurasi untuk bantuan lebih lanjut.";
        if (lower.Contains("materi") || lower.Contains("teori") || lower.Contains("belajar"))
            return "📚 Modul teori tersedia di **Siswa → Materi Teori** (`/student/theory`). Ada 6 modul dari rambu lalu lintas sampai keselamatan berkendara.";
        if (lower.Contains("halo") || lower.Contains("hai") || lower.Contains("bambang"))
            return "👋 Halo! Saya Om Bambang, asisten virtual SmartDrive Academy. Saat ini saya berjalan dalam mode **offline** (API key AI belum dikonfigurasi).\n\nSaya tetap bisa membantu dengan informasi dasar. Silakan tanya tentang kendaraan, lokasi, jadwal, atau materi teori!";
        return "👋 Halo! Saya **Om Bambang**, asisten SmartDrive Academy.\n\n⚠️ Mode offline — API key AI belum dikonfigurasi. Tambahkan di `appsettings.json` → `ChatBot:ApiKey`.\n\nSementara itu, saya bisa bantu dengan info dasar. Coba tanya: **kendaraan**, **lokasi**, **jadwal**, atau **materi**.";
    }

    private async Task SaveAssistantMessage(int sessionId, string content)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartDriveDbContext>();
        db.ChatBotMessages.Add(new ChatBotMessage
        {
            SessionId = sessionId, Role = "assistant", Content = content,
            SentAt = DateTime.UtcNow, ModelUsed = _config.ModelId
        });
        var sess = await db.ChatBotSessions.FindAsync(sessionId);
        if (sess != null) sess.LastActivityAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private async Task<string> GetDbInfo(SmartDriveDbContext db, string uid)
    {
        try
        {
            var u = await db.Users.FindAsync(uid); if (u == null) return "";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[Info] User: {u.FullName}, Role: {u.Role}, UID: {uid}");
            if (u.Role == UserRole.Student) { var sp = await db.StudentProfiles.FirstOrDefaultAsync(x => x.UserId == uid); if (sp != null) sb.AppendLine($"Level: {sp.CurrentLevel}, Jam: {sp.TotalHoursCompleted}, Sesi: {sp.TotalSessionsCompleted}, XP: {sp.ExperiencePoints}"); }
            else if (u.Role == UserRole.Instructor) { var ip = await db.InstructorProfiles.FirstOrDefaultAsync(x => x.UserId == uid); if (ip != null) sb.AppendLine($"Rating: {ip.AverageRating}, Siswa: {ip.TotalStudents}, Id: {ip.Id}"); }
            sb.AppendLine($"Kendaraan: {await db.Vehicles.CountAsync(v => v.Status == VehicleStatus.Available)}");
            return sb.ToString();
        }
        catch { return ""; }
    }

    public async Task<ChatBotSession> CreateSessionAsync(string uid, string t = "New Chat") { using var s = _sp.CreateScope(); var db = s.ServiceProvider.GetRequiredService<SmartDriveDbContext>(); var ses = new ChatBotSession { UserId = uid, Title = t, CreatedAt = DateTime.UtcNow, LastActivityAt = DateTime.UtcNow }; db.ChatBotSessions.Add(ses); await db.SaveChangesAsync(); return ses; }
    public async Task<bool> DeleteSessionAsync(int sid, string uid) { using var s = _sp.CreateScope(); var db = s.ServiceProvider.GetRequiredService<SmartDriveDbContext>(); var ses = await db.ChatBotSessions.FindAsync(sid); if (ses == null || ses.UserId != uid) return false; db.ChatBotMessages.RemoveRange(await db.ChatBotMessages.Where(x => x.SessionId == sid).ToListAsync()); db.ChatBotSessions.Remove(ses); await db.SaveChangesAsync(); return true; }
    public async Task<bool> ResetSessionAsync(int sid, string uid) { using var s = _sp.CreateScope(); var db = s.ServiceProvider.GetRequiredService<SmartDriveDbContext>(); var ses = await db.ChatBotSessions.FindAsync(sid); if (ses == null || ses.UserId != uid) return false; db.ChatBotMessages.RemoveRange(await db.ChatBotMessages.Where(x => x.SessionId == sid).ToListAsync()); await db.SaveChangesAsync(); return true; }
    public async Task<List<ChatBotSession>> GetUserSessionsAsync(string uid) { using var s = _sp.CreateScope(); var db = s.ServiceProvider.GetRequiredService<SmartDriveDbContext>(); return await db.ChatBotSessions.Where(x => x.UserId == uid && x.IsActive).OrderByDescending(x => x.LastActivityAt).ToListAsync(); }
    public async Task<List<ChatBotMessage>> GetSessionMessagesAsync(int sid) { using var s = _sp.CreateScope(); var db = s.ServiceProvider.GetRequiredService<SmartDriveDbContext>(); return await db.ChatBotMessages.Where(x => x.SessionId == sid).OrderBy(x => x.SentAt).ToListAsync(); }
    public void ReloadConfig() { _initialized = false; LoadConfig(); Initialize(); }
}

// === Kernel Function Plugins ===

public class DateTimePlugin
{
    [KernelFunction, Description("Get current date and time in Jakarta/Indonesia WIB UTC+7")]
    public string GetCurrentDateTime() { var n = DateTime.Now; return $"Sekarang {n:dddd, dd MMMM yyyy}, pukul {n:HH:mm:ss} WIB"; }
    [KernelFunction, Description("Get current date")] public string GetCurrentDate() => DateTime.Now.ToString("dd MMMM yyyy");
    [KernelFunction, Description("Get current time")] public string GetCurrentTime() => DateTime.Now.ToString("HH:mm:ss");
    [KernelFunction, Description("Days between two dates yyyy-MM-dd")] public int DaysBetween(string s, string e) { if (DateTime.TryParse(s, out var sd) && DateTime.TryParse(e, out var ed)) return (ed - sd).Days; return 0; }
}

public class MathPlugin
{
    [KernelFunction, Description("Math calculation, e.g. '2+2', '100*5'")] public double Calculate(string expr) { try { return Convert.ToDouble(new System.Data.DataTable().Compute(expr, "")); } catch { return double.NaN; } }
}

public class TavilySearchPlugin
{
    private readonly IConfiguration _cfg; private readonly IHttpClientFactory _hf;
    public TavilySearchPlugin(IConfiguration cfg, IHttpClientFactory hf) { _cfg = cfg; _hf = hf; }

    [KernelFunction, Description("Search the internet using Tavily API for real-time info")]
    public async Task<string> SearchInternet(string query)
    {
        var apiKey = _cfg.GetValue("Tavily:ApiKey", "");
        if (string.IsNullOrEmpty(apiKey)) return "Tavily API key not configured.";
        try
        {
            var client = _hf.CreateClient();
            var body = JsonSerializer.Serialize(new { query, api_key = apiKey, search_depth = "basic", max_results = 5 });
            var resp = await client.PostAsync(_cfg.GetValue("Tavily:BaseUrl", "https://api.tavily.com/search")!, new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
            var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var results = json.RootElement.GetProperty("results");
            var answer = json.RootElement.TryGetProperty("answer", out var a) ? a.GetString() : null;
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(answer)) sb.AppendLine($"📝 {answer}\n");
            sb.AppendLine("🔍 Hasil:");
            int i = 1;
            foreach (var r in results.EnumerateArray()) { var t = r.GetProperty("title").GetString(); var c = r.GetProperty("content").GetString(); if (c?.Length > 300) c = c[..300] + "..."; sb.AppendLine($"{i}. **{t}**\n   {c}\n   🔗 {r.GetProperty("url").GetString()}"); i++; }
            return sb.ToString();
        }
        catch (Exception ex) { return $"Search error: {ex.Message}"; }
    }

    [KernelFunction, Description("Scrape web page content from URL")] public async Task<string> ScrapeWebPage(string url) { try { var c = _hf.CreateClient(); c.DefaultRequestHeaders.Add("User-Agent", "SmartDriveBot/1.0"); var h = await c.GetStringAsync(url); h = System.Text.RegularExpressions.Regex.Replace(h, "<script[^>]*>[^<]*</script>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase); h = System.Text.RegularExpressions.Regex.Replace(h, "<style[^>]*>[^<]*</style>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase); h = System.Text.RegularExpressions.Regex.Replace(h, "<[^>]*>", " "); h = System.Text.RegularExpressions.Regex.Replace(h, @"\s+", " ").Trim(); return h.Length > 3000 ? h[..3000] + "..." : h; } catch (Exception ex) { return ex.Message; } }
    [KernelFunction, Description("Read file content from URL")] public async Task<string> ReadFileFromUrl(string url) { try { return await _hf.CreateClient().GetStringAsync(url); } catch (Exception ex) { return ex.Message; } }
}

public class DatabaseQueryPlugin
{
    private readonly IServiceProvider _sp; public DatabaseQueryPlugin(IServiceProvider sp) { _sp = sp; }
    [KernelFunction, Description("Get available vehicles")] public async Task<string> GetVehicles() { using var s = _sp.CreateScope(); var db = s.ServiceProvider.GetRequiredService<SmartDriveDbContext>(); var v = await db.Vehicles.Where(x => x.Status == VehicleStatus.Available).Select(x => new { x.PlateNumber, x.Brand, x.Model, x.Year, x.Transmission }).ToListAsync(); if (!v.Any()) return "No vehicles."; var sb = new System.Text.StringBuilder(); sb.AppendLine($"🚗 {v.Count} vehicles:"); foreach (var x in v) sb.AppendLine($"- {x.PlateNumber}: {x.Brand} {x.Model} ({x.Year}) {x.Transmission}"); return sb.ToString(); }
    [KernelFunction, Description("Get training locations")] public async Task<string> GetLocations() { using var s = _sp.CreateScope(); var db = s.ServiceProvider.GetRequiredService<SmartDriveDbContext>(); var l = await db.TrainingLocations.Where(x => x.IsActive).Select(x => new { x.Name, x.Address, x.LocationType, x.Description }).ToListAsync(); var sb = new System.Text.StringBuilder(); sb.AppendLine($"📍 {l.Count} locations:"); foreach (var x in l) sb.AppendLine($"- {x.Name} ({x.LocationType}): {x.Address}"); return sb.ToString(); }
    [KernelFunction, Description("Get marketplace products")] public async Task<string> GetProducts() { using var s = _sp.CreateScope(); var db = s.ServiceProvider.GetRequiredService<SmartDriveDbContext>(); var p = await db.MarketplaceProducts.Where(x => x.IsActive).Select(x => new { x.Name, x.Price, x.DurationHours, x.Category, x.Description }).ToListAsync(); var sb = new System.Text.StringBuilder(); sb.AppendLine($"🛒 {p.Count} products:"); foreach (var x in p) sb.AppendLine($"- {x.Name} ({x.Category}) Rp{x.Price:N0} {x.DurationHours}h"); return sb.ToString(); }
    [KernelFunction, Description("Get system statistics")] public async Task<string> GetStats() { using var s = _sp.CreateScope(); var db = s.ServiceProvider.GetRequiredService<SmartDriveDbContext>(); return $"Students:{await db.StudentProfiles.CountAsync()} Instructors:{await db.InstructorProfiles.CountAsync()} Vehicles:{await db.Vehicles.CountAsync()}({await db.Vehicles.CountAsync(x=>x.Status==VehicleStatus.Available)}avail) ActiveBookings:{await db.Bookings.CountAsync(x=>x.Status==BookingStatus.Scheduled)} Done:{await db.Bookings.CountAsync(x=>x.Status==BookingStatus.Completed)}"; }
}
