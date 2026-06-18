using System.Text;
using LandLord.Data;
using LandLord.Models;
using LandLord.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

using LlmFunctionResult = LandLord.Models.FunctionResult;

namespace LandLord.Services;

public class SkChatService : IChatService, IDisposable
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _cfg;
    private readonly ILogger<SkChatService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HttpClient _http;
    private readonly IStorageService _storage;

    private Kernel? _kernel;
    private ChatHistory? _chatHistory;
    private string _provider = "", _model = "";
    private readonly string _botName, _systemPrompt;
    private readonly int _maxHistory;

    public SkChatService(AppDbContext context, IConfiguration cfg,
        ILogger<SkChatService> logger, IServiceScopeFactory scopeFactory,
        HttpClient http, IStorageService storage)
    {
        _context = context; _cfg = cfg; _logger = logger;
        _scopeFactory = scopeFactory; _http = http; _storage = storage;
        _botName = cfg.GetValue<string>("ChatBot:Name") ?? "Frengky Ganteng";
        _systemPrompt = cfg.GetValue<string>("ChatBot:SystemPrompt") ?? GetDefaultPrompt();
        _maxHistory = cfg.GetValue<int>("ChatBot:MaxHistory", 20);
        InitKernel();
    }

    private void InitKernel()
    {
        var b = Kernel.CreateBuilder();
        b.Services.AddSingleton(_http);
        b.Services.AddSingleton(_scopeFactory);
        b.Services.AddSingleton<IConfiguration>(_cfg);
        b.Services.AddSingleton<IStorageService>(_storage);

        b.Services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Warning);
        });

        // ✅ InternetPlugin & CommonPlugin — pakai AddFromType (DI dari SK container)
        b.Plugins.AddFromType<InternetPlugin>("Internet");
        b.Plugins.AddFromType<CommonPlugin>("Common");

        // ✅ DatabasePlugin — pakai AddFromObject (bypass SK DI container)
        //    SK container tidak kenal AppDbContext (terdaftar di ASP.NET Core DI).
        //    IServiceScopeFactory yang di-register di atas memang dari ASP.NET Core,
        //    tapi SK membungkusnya dan scope yang dibuat tetap pakai SK service provider.
        //    Dengan AddFromObject, instance dibuat pakai ASP.NET Core DI langsung.
        var dbPlugin = new DatabasePlugin(_scopeFactory);
        b.Plugins.AddFromObject(dbPlugin, "Database");

        _logger.LogInformation("✅ SK Plugins: Internet + Database + Common registered");

        _provider = _cfg.GetValue<string>("LLMProvider:Provider") ?? "OpenAI";
        _model = _cfg.GetValue<string>("LLMProvider:Model") ?? "gpt-4o";
        var key = _cfg.GetValue<string>("LLMProvider:ApiKey") ?? "";
        var ep = _cfg.GetValue<string>("LLMProvider:Endpoint") ?? "";

        try
        {
            switch (_provider.ToLowerInvariant())
            {
                case "openai":
                    if (!string.IsNullOrEmpty(key))
                    {
                        if (!string.IsNullOrEmpty(ep)) b.AddOpenAIChatCompletion(_model, new Uri(ep), key);
                        else b.AddOpenAIChatCompletion(_model, key);
                        _logger.LogInformation("✅ SK: OpenAI/{Model}", _model);
                    }
                    else _logger.LogWarning("⚠️ OpenAI API key not set");
                    break;
                case "ollama":
                    var ollamaUrl = !string.IsNullOrEmpty(ep) ? ep : "http://localhost:11434";
                    b.AddOpenAIChatCompletion(_model, new Uri($"{ollamaUrl.TrimEnd('/')}/v1"), "ollama");
                    _logger.LogInformation("✅ SK: Ollama/{Model} @ {Url}", _model, ollamaUrl);
                    break;
                case "anthropic":
                    if (!string.IsNullOrEmpty(key)) TryConfigureAnthropic(b, _model, key);
                    break;
                case "gemini":
                    if (!string.IsNullOrEmpty(key)) TryConfigureGemini(b, _model, key);
                    break;
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "LLM init failed"); }

        _kernel = b.Build();
    }

    private void TryConfigureAnthropic(IKernelBuilder b, string model, string key)
    {
        try
        {
            var t = Type.GetType("Microsoft.SemanticKernel.AnthropicServiceCollectionExtensions, Microsoft.SemanticKernel.Connectors.Anthropic");
            if (t != null)
            {
                var m = t.GetMethod("AddAnthropicChatCompletion", new[] { typeof(IKernelBuilder), typeof(string), typeof(string) });
                m?.Invoke(null, new object[] { b, model, key });
                _logger.LogInformation("✅ SK: Anthropic/{Model}", model);
            }
        }
        catch { }
    }

    private void TryConfigureGemini(IKernelBuilder b, string model, string key)
    {
        try
        {
            var t = Type.GetType("Microsoft.SemanticKernel.GoogleServiceCollectionExtensions, Microsoft.SemanticKernel.Connectors.Google");
            if (t != null)
            {
                var m = t.GetMethod("AddGoogleAIGeminiChatCompletion", new[] { typeof(IKernelBuilder), typeof(string), typeof(string) });
                m?.Invoke(null, new object[] { b, model, key });
                _logger.LogInformation("✅ SK: Gemini/{Model}", model);
            }
        }
        catch { }
    }

    // ================================================================
    // SESSION MANAGEMENT
    // ================================================================

    public async Task<List<ChatSession>> GetSessionsAsync(string? userId)
        => await _context.ChatSessions.Where(s => s.UserId == userId && s.IsActive).OrderByDescending(s => s.LastUpdatedAt).ToListAsync();

    public async Task<ChatSession> CreateSessionAsync(string? userId, string title = "Chat Baru")
    {
        var s = new ChatSession { UserId = userId, Title = title, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow, IsActive = true };
        _context.ChatSessions.Add(s); await _context.SaveChangesAsync();
        _chatHistory = new ChatHistory(); _chatHistory.AddSystemMessage(_systemPrompt);
        return s;
    }

    public async Task<ChatSession?> GetSessionAsync(int id)
        => await _context.ChatSessions.Include(s => s.Messages.OrderBy(m => m.SentAt)).FirstOrDefaultAsync(s => s.Id == id);

    public async Task<List<ChatMessage>> GetMessagesAsync(int id)
        => await _context.ChatMessages.Where(m => m.ChatSessionId == id).OrderBy(m => m.SentAt).ToListAsync();

    public async Task<ChatMessage> SendMessageAsync(int sid, string content,
        string? imgUrl = null, string? docUrl = null, string? docName = null)
    {
        var m = new ChatMessage { ChatSessionId = sid, Role = "user", Content = content, ImageUrl = imgUrl, DocumentUrl = docUrl, DocumentName = docName, SentAt = DateTime.UtcNow };
        _context.ChatMessages.Add(m);
        var s = await _context.ChatSessions.FindAsync(sid);
        if (s != null) { s.LastUpdatedAt = DateTime.UtcNow; if (s.Title == "Chat Baru" && !string.IsNullOrWhiteSpace(content)) s.Title = content.Length > 50 ? content[..50] + "..." : content; }
        await _context.SaveChangesAsync(); return m;
    }

    private async Task<ChatMessage> SaveAssistantMessageAsync(int sessionId, string content)
    {
        var msg = new ChatMessage { ChatSessionId = sessionId, Role = "assistant", Content = content, SentAt = DateTime.UtcNow };
        _context.ChatMessages.Add(msg);
        var session = await _context.ChatSessions.FindAsync(sessionId);
        if (session != null) session.LastUpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(); return msg;
    }

    public async Task<bool> DeleteSessionAsync(int id) { var s = await _context.ChatSessions.FindAsync(id); if (s == null) return false; s.IsActive = false; await _context.SaveChangesAsync(); return true; }

    public async Task<bool> ResetSessionAsync(int id)
    {
        var msgs = await _context.ChatMessages.Where(m => m.ChatSessionId == id).ToListAsync();
        _context.ChatMessages.RemoveRange(msgs);
        var s = await _context.ChatSessions.FindAsync(id);
        if (s != null) { s.Title = "Chat Baru"; s.LastUpdatedAt = DateTime.UtcNow; }
        await _context.SaveChangesAsync();
        _chatHistory = new ChatHistory(); _chatHistory.AddSystemMessage(_systemPrompt);
        return true;
    }

    public async Task<string?> UploadAttachmentAsync(Stream fileStream, string fileName, string contentType)
    {
        try { var url = await _storage.UploadAsync(fileName, fileStream, contentType); return await _storage.GetPublicUrlAsync(url); }
        catch (Exception ex) { _logger.LogError(ex, "Upload failed: {FileName}", fileName); return null; }
    }

    // ================================================================
    // AI RESPONSE
    // ================================================================

    public async Task<ChatResponse> GetAIResponseAsync(int sessionId, string userMessage,
        string? imageUrl = null, string? documentUrl = null, string? documentName = null)
    {
        var r = new ChatResponse();
        var lm = userMessage.ToLower().Trim();
        if (IsGreeting(lm) && string.IsNullOrEmpty(imageUrl)) { r.Content = Greeting(); await SaveAssistantMessageAsync(sessionId, r.Content); return r; }
        if (IsHelp(lm) && string.IsNullOrEmpty(imageUrl)) { r.Content = Help(); await SaveAssistantMessageAsync(sessionId, r.Content); return r; }
        await BuildHistoryAsync(sessionId);
        if (_kernel == null) { r.Content = KwResponse(lm); await SaveAssistantMessageAsync(sessionId, r.Content); return r; }

        try
        {
            var cs = _kernel.GetRequiredService<IChatCompletionService>();
            if (!string.IsNullOrEmpty(imageUrl) && Uri.TryCreate(imageUrl, UriKind.Absolute, out var imgUri))
            {
                var chatMsg = new ChatMessageContent(AuthorRole.User, content: null);
                chatMsg.Items.Add(new TextContent(userMessage));
                chatMsg.Items.Add(new ImageContent(imgUri));
                _chatHistory!.Add(chatMsg);
            }
            else
            {
                var msg = userMessage;
                if (!string.IsNullOrEmpty(documentUrl)) msg += $"\n\n📎 [Dokumen: {documentName ?? "Dokumen"}]({documentUrl})";
                _chatHistory!.AddUserMessage(msg);
            }

            var es = new OpenAIPromptExecutionSettings
            {
                Temperature = _cfg.GetValue<double>("ChatBot:Temperature", 0.8),
                MaxTokens = _cfg.GetValue<int>("ChatBot:MaxTokens", 4096),
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            var result = await cs.GetChatMessageContentAsync(_chatHistory!, es, _kernel);
            var content = result.Content ?? "Maaf, aku tidak bisa merespons saat ini. 😅";
            _chatHistory!.AddAssistantMessage(content);
            TrimHistory();
            r.Content = content;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("service"))
        { r.Content = "⚠️ **LLM belum dikonfigurasi.** Tambahkan API key di Settings → LLM Provider."; }
        catch (Exception ex) { _logger.LogError(ex, "SK error"); r.Content = $"❌ Gagal: {ex.Message}\n\nCoba lagi atau ketik **help**."; }

        await SaveAssistantMessageAsync(sessionId, r.Content);
        return r;
    }

    // ================================================================
    // FUNCTIONS
    // ================================================================

    public List<KernelFunctionDefinition> GetAvailableFunctions() => new()
    {
        new() { Name = "Internet.tavily_search", DisplayName = "🌐 Internet Search", Description = "Cari di internet via Tavily API" },
        new() { Name = "Internet.scrape_webpage", DisplayName = "📄 Scrape Web", Description = "Baca konten halaman web" },
        new() { Name = "Internet.read_file_from_url", DisplayName = "📎 Read File URL", Description = "Baca file dari URL" },
        new() { Name = "Database.query_tanah", DisplayName = "🏞️ Cari Tanah", Description = "Query database tanah" },
        new() { Name = "Database.query_bangunan", DisplayName = "🏗️ Cari Bangunan", Description = "Query database bangunan" },
        new() { Name = "Database.get_statistics", DisplayName = "📊 Statistik Properti", Description = "Ringkasan statistik properti" },
        new() { Name = "Common.get_current_time", DisplayName = "🕐 Waktu Saat Ini", Description = "Dapatkan waktu saat ini" },
        new() { Name = "Common.get_current_date", DisplayName = "📅 Tanggal Hari Ini", Description = "Dapatkan tanggal hari ini" },
        new() { Name = "Common.calculate_date_diff", DisplayName = "🔢 Selisih Tanggal", Description = "Hitung selisih dua tanggal" },
        new() { Name = "Common.calculate_math", DisplayName = "🧮 Kalkulator", Description = "Hitung ekspresi matematika" },
        new() { Name = "Common.calculate_percentage", DisplayName = "📊 Persentase", Description = "Hitung persentase" },
        new() { Name = "Common.convert_unit", DisplayName = "⚖️ Konversi Satuan", Description = "Konversi satuan" },
        new() { Name = "Common.generate_random_number", DisplayName = "🎲 Random Number", Description = "Generate angka random" },
        new() { Name = "Common.generate_uuid", DisplayName = "🆔 Generate UUID", Description = "Generate UUID baru" },
        new() { Name = "Common.summarize_text", DisplayName = "📄 Statistik Teks", Description = "Analisis statistik teks" },
    };

    public async Task<LlmFunctionResult> ExecuteFunctionAsync(int sid, string fn, Dictionary<string, object?> p)
    {
        if (_kernel == null) return LlmFunctionResult.Fail(fn, "SK not initialized");
        try
        {
            var parts = fn.Split('.'); if (parts.Length != 2) return LlmFunctionResult.Fail(fn, "Format: Plugin.Function");
            var args = new KernelArguments(); foreach (var kv in p) args[kv.Key] = kv.Value;
            var res = await _kernel.InvokeAsync(parts[0], parts[1], args);
            return LlmFunctionResult.Ok(fn, res.GetValue<string>() ?? "");
        }
        catch (Exception ex) { return LlmFunctionResult.Fail(fn, ex.Message); }
    }

    public bool ShouldSearchInternet(string msg)
    {
        if (string.IsNullOrWhiteSpace(msg)) return false;
        var l = msg.ToLower();
        foreach (var t in new[] { "cari di internet", "search", "googling", "berita", "info terkini", "tren", "harga pasar", "news", "update", "latest", "what is", "apa itu", "regulasi", "peraturan", "uu", "undang-undang", "kebijakan", "pajak terbaru", "aturan" })
            if (l.Contains(t)) return true;
        if (l.Contains("http://") || l.Contains("https://")) return true;
        return (l.Contains("harga") || l.Contains("nilai")) && (l.Contains("sekarang") || l.Contains("saat ini") || l.Contains("terkini") || l.Contains("terbaru"));
    }

    private async Task BuildHistoryAsync(int sid)
    {
        _chatHistory = new ChatHistory(); _chatHistory.AddSystemMessage(_systemPrompt);
        var msgs = await _context.ChatMessages.Where(m => m.ChatSessionId == sid).OrderBy(m => m.SentAt).Take(_maxHistory).ToListAsync();
        foreach (var m in msgs)
            if (m.Role == "user") _chatHistory.AddUserMessage(m.Content);
            else if (m.Role == "assistant") _chatHistory.AddAssistantMessage(m.Content);
    }

    private void TrimHistory()
    {
        if (_chatHistory == null) return;
        while (_chatHistory.Count > _maxHistory * 2 + 1)
        {
            var idx = _chatHistory.Select((m, i) => new { m.Role, i }).FirstOrDefault(x => x.Role != AuthorRole.System)?.i ?? -1;
            if (idx > 0) _chatHistory.RemoveAt(idx); else break;
        }
    }

    private static string GetDefaultPrompt() =>
        "Kamu adalah Frengky Ganteng, asisten virtual LandLord. " +
        "Gunakan Internet.tavily_search untuk mencari di internet. " +
        "Gunakan Database.query_tanah / Database.query_bangunan untuk data properti. " +
        "Gunakan Common.* untuk kalkulator, waktu, konversi satuan. " +
        "Jawab dalam bahasa Indonesia santai & profesional dengan emoji.";

    private bool IsGreeting(string m) => m == "hai" || m == "halo" || m == "hello" || m == "hi" || m.Contains("perkenalkan");
    private bool IsHelp(string m) => m.Contains("help") || m.Contains("bantuan") || m.Contains("fitur") || m == "?";

    private string Greeting() =>
        $"👋 **{_botName}** — AI LandLord (SK: {_provider}/{_model})!\n\n" +
        "🌐 Internet Search • 🏞️ DB Tanah • 🏗️ DB Bangunan\n📄 Baca URL • 📊 Statistik • 🧮 Kalkulator\n📎 Upload Gambar!\n\nSilakan tanya! 😊";

    private string Help() =>
        $"🛟 **Bantuan {_botName}** (SK: {_provider}/{_model})\n\n" +
        "### 🌐 Internet\n\"cari di internet [topik]\" | \"baca [url]\"\n\n" +
        "### 🏞️ Database\n\"cari tanah [keyword]\" | \"cari bangunan [keyword]\" | \"statistik\"\n\n" +
        "### 📎 Upload Gambar\nKlik 📎 → pilih gambar → tanya \"deskripsikan ini\"\n\n" +
        "### 🧮 Math\n\"hitung 100 * 0.5 + 50\" | \"10 km ke meter\" | \"30 C ke F\"";

    private string KwResponse(string m)
    {
        if (m.Contains("total tanah")) return $"🏞️ **{_context.Tanah.Count():N0} bidang tanah**.";
        if (m.Contains("total bangunan")) return $"🏗️ **{_context.Bangunan.Count():N0} unit bangunan**.";
        return $"🤔 Coba: 🌐 \"cari di internet {m}\" | 🏞️ \"cari tanah [kw]\" | 🛟 \"help\"";
    }

    public void Dispose() { _kernel = null; _chatHistory = null; GC.SuppressFinalize(this); }
}
