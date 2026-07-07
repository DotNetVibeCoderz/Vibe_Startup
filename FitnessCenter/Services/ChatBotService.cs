using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using FitnessCenter.Data;
using FitnessCenter.Models;

namespace FitnessCenter.Services;

/// <summary>
/// ChatBot Service — "Coach Tommy"
/// Menggunakan Microsoft Semantic Kernel dengan multi-AI provider support
/// (OpenAI, Anthropic, Gemini, Ollama) dan berbagai Kernel Functions.
/// </summary>
public class ChatBotService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChatBotService> _logger;

    // Cache Kernel per provider
    private readonly ConcurrentDictionary<string, Kernel> _kernelCache = new();

    public string BotName => _config.GetValue<string>("ChatBot:Name") ?? "Coach Tommy";
    public string WelcomeMessage => _config.GetValue<string>("ChatBot:WelcomeMessage")
        ?? "Halo! Saya Coach Tommy, asisten virtual kebugaran profesional. Ada yang bisa saya bantu hari ini? 💪";

    public ChatBotService(AppDbContext db, IConfiguration config, IHttpClientFactory httpClient,
                          IServiceProvider serviceProvider, ILogger<ChatBotService> logger)
    {
        _db = db;
        _config = config;
        _httpClient = httpClient;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    // ==================== SESSION MANAGEMENT ====================

    public async Task<List<ChatSession>> GetSessionsAsync(string? userId) =>
        await _db.ChatSessions.Include(s => s.Messages)
            .Where(s => s.UserId == userId && s.IsActive)
            .OrderByDescending(s => s.LastActivity).Take(20).ToListAsync();

    public async Task<ChatSession> CreateSessionAsync(string? userId, string title = "New Chat")
    {
        var session = new ChatSession { UserId = userId, Title = title };
        _db.ChatSessions.Add(session);
        await _db.SaveChangesAsync();

        _db.ChatMessages.Add(new ChatMessage
        {
            SessionId = session.Id, Role = "assistant",
            Content = WelcomeMessage, ModelUsed = "system"
        });
        await _db.SaveChangesAsync();
        return session;
    }

    public async Task<ChatSession?> GetSessionAsync(int id) =>
        await _db.ChatSessions.Include(s => s.Messages)
            .OrderBy(m => m.CreatedAt)
            .FirstOrDefaultAsync(s => s.Id == id);

    public async Task ResetSessionAsync(int id)
    {
        var session = await _db.ChatSessions.Include(s => s.Messages).FirstOrDefaultAsync(s => s.Id == id);
        if (session != null)
        {
            _db.ChatMessages.RemoveRange(session.Messages);
            _db.ChatMessages.Add(new ChatMessage
            {
                SessionId = id, Role = "assistant",
                Content = WelcomeMessage, ModelUsed = "system"
            });
            await _db.SaveChangesAsync();
        }
    }

    public async Task DeleteSessionAsync(int id)
    {
        var session = await _db.ChatSessions.Include(s => s.Messages).FirstOrDefaultAsync(s => s.Id == id);
        if (session != null) { _db.ChatSessions.Remove(session); await _db.SaveChangesAsync(); }
    }

    // ==================== MESSAGE HANDLING ====================

    public async Task<ChatMessage> SendMessageAsync(int sessionId, string content,
        string? imageUrl = null, string? documentUrl = null)
    {
        var session = await _db.ChatSessions.FindAsync(sessionId)
            ?? throw new ArgumentException("Session not found");

        var userMsg = new ChatMessage
        {
            SessionId = sessionId, Role = "user", Content = content,
            ImageUrl = imageUrl, DocumentUrl = documentUrl
        };
        _db.ChatMessages.Add(userMsg);
        await _db.SaveChangesAsync();

        var enrichedContent = content;
        if (!string.IsNullOrEmpty(imageUrl))
            enrichedContent = $"[User mengirim gambar: {imageUrl}]\n\n{content}";
        if (!string.IsNullOrEmpty(documentUrl))
            enrichedContent = $"[User mengirim dokumen: {documentUrl}]\n\n{enrichedContent}";

        var (aiContent, modelUsed) = await GenerateResponseAsync(sessionId, enrichedContent);

        var assistantMsg = new ChatMessage
        {
            SessionId = sessionId, Role = "assistant",
            Content = aiContent, ModelUsed = modelUsed
        };
        _db.ChatMessages.Add(assistantMsg);

        session.LastActivity = DateTime.UtcNow;
        if (session.Title == "New Chat")
            session.Title = content.Length > 50 ? content[..50].Trim() + "..." : content.Trim();

        await _db.SaveChangesAsync();
        return assistantMsg;
    }

    // ==================== AI RESPONSE GENERATION ====================

    private async Task<(string content, string model)> GenerateResponseAsync(int sessionId, string userMessage)
    {
        var provider = _config.GetValue<string>("AI:DefaultProvider") ?? "OpenAI";
        var modelName = GetModelName(provider);

        try
        {
            var kernel = GetOrCreateKernel(provider);
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var history = await BuildChatHistoryAsync(sessionId);

            var settings = new OpenAIPromptExecutionSettings
            {
                Temperature = _config.GetValue<double>("ChatBot:Temperature", 0.7),
                MaxTokens = _config.GetValue<int>("ChatBot:MaxTokens", 2048),
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            var response = await chatService.GetChatMessageContentAsync(history, settings, kernel);
            var content = response.Content ?? "Maaf, saya tidak bisa memberikan respon saat ini.";

            _logger.LogInformation("ChatBot response: Provider={Provider}, Model={Model}, Length={Length}",
                provider, modelName, content.Length);

            return (content, modelName);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401"))
        {
            return ($"⚠️ API key untuk {provider} tidak valid. Setup di appsettings.json → AI:Providers:{provider}:ApiKey", modelName);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404"))
        {
            return ($"⚠️ Model '{modelName}' tidak ditemukan untuk {provider}. Periksa di appsettings.json.", modelName);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("API key"))
        {
            return ($"⚠️ {ex.Message}", modelName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ChatBot error: Provider={Provider}", provider);
            return ($"❌ Maaf, terjadi kesalahan: {ex.Message}\n\nSilakan coba lagi.", "error");
        }
    }

    // ==================== KERNEL BUILDING ====================

    private Kernel GetOrCreateKernel(string provider)
    {
        return _kernelCache.GetOrAdd(provider, _ => BuildKernel(provider));
    }

    private Kernel BuildKernel(string provider)
    {
        var builder = Kernel.CreateBuilder();
        RegisterChatService(builder, provider);
        builder.Services.AddSingleton(_httpClient);
        RegisterPlugins(builder);

        var kernel = builder.Build();
        _logger.LogInformation("Semantic Kernel built for: {Provider}", provider);
        return kernel;
    }

    private void RegisterChatService(IKernelBuilder builder, string provider)
    {
        switch (provider.ToLowerInvariant())
        {
            case "openai":
            {
                var apiKey = _config.GetValue<string>("AI:Providers:OpenAI:ApiKey") ?? "";
                var model = GetModelName("OpenAI");
                if (string.IsNullOrEmpty(apiKey))
                    throw new InvalidOperationException("OpenAI API key belum dikonfigurasi di appsettings.json");

                var endpoint = _config.GetValue<string>("AI:Providers:OpenAI:Endpoint");
                if (!string.IsNullOrEmpty(endpoint))
                    builder.AddOpenAIChatCompletion(model, new Uri(endpoint), apiKey);
                else
                    builder.AddOpenAIChatCompletion(model, apiKey);
                break;
            }
            case "gemini":
            {
                var apiKey = _config.GetValue<string>("AI:Providers:Gemini:ApiKey") ?? "";
                var model = GetModelName("Gemini");
                if (string.IsNullOrEmpty(apiKey))
                    throw new InvalidOperationException("Gemini API key belum dikonfigurasi");
                builder.AddOpenAIChatCompletion(model,
                    new Uri("https://generativelanguage.googleapis.com/v1beta/openai/"), apiKey);
                break;
            }
            case "ollama":
            {
                var endpoint = _config.GetValue<string>("AI:Providers:Ollama:Endpoint") ?? "http://localhost:11434";
                var model = GetModelName("Ollama");
                builder.AddOpenAIChatCompletion(model,
                    new Uri($"{endpoint.TrimEnd('/')}/v1"), "ollama");
                break;
            }
            case "anthropic":
            {
                var apiKey = _config.GetValue<string>("AI:Providers:Anthropic:ApiKey") ?? "";
                var model = GetModelName("Anthropic");
                if (string.IsNullOrEmpty(apiKey))
                    throw new InvalidOperationException("Anthropic API key belum dikonfigurasi");
                builder.AddOpenAIChatCompletion(model,
                    new Uri("https://api.anthropic.com/v1/"), apiKey);
                break;
            }
            default:
                throw new InvalidOperationException($"Unknown AI provider: {provider}");
        }
    }

    private void RegisterPlugins(IKernelBuilder builder)
    {
        builder.Plugins.AddFromObject(
            new ChatBot.DatabaseQueryPlugin(_serviceProvider.GetRequiredService<AppDbContext>()), "DatabaseQuery");
        builder.Plugins.AddFromObject(new ChatBot.UtilityPlugin(), "Utility");
        builder.Plugins.AddFromObject(new ChatBot.WebPlugin(_httpClient, _config), "Web");
        _logger.LogInformation("Kernel plugins registered: DatabaseQuery, Utility, Web");
    }

    // ==================== HELPERS ====================

    private string GetModelName(string provider) => provider.ToLowerInvariant() switch
    {
        "openai" => _config.GetValue<string>("AI:Providers:OpenAI:Model") ?? "gpt-4o",
        "anthropic" => _config.GetValue<string>("AI:Providers:Anthropic:Model") ?? "claude-3-5-sonnet-20241022",
        "gemini" => _config.GetValue<string>("AI:Providers:Gemini:Model") ?? "gemini-2.0-flash",
        "ollama" => _config.GetValue<string>("AI:Providers:Ollama:Model") ?? "llama3.2",
        _ => "gpt-4o"
    };

    /// <summary>
    /// Build ChatHistory dari pesan-pesan di session.
    /// PERBAIKAN: .TakeLast() tidak bisa di-translate EF Core.
    /// Gunakan .OrderByDescending().Take().ToListAsync() lalu reverse.
    /// </summary>
    private async Task<ChatHistory> BuildChatHistoryAsync(int sessionId)
    {
        var history = new ChatHistory();

        // System prompt
        var systemPrompt = BuildSystemPrompt();
        history.AddSystemMessage(systemPrompt);

        // Ambil 20 pesan terakhir — EF Core compatible approach
        // Karena .TakeLast() tidak bisa di-translate, kita pakai OrderByDescending + Take
        var messages = await _db.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.CreatedAt)    // paling baru dulu
            .Take(20)                                 // ambil 20
            .ToListAsync();                           // materialize

        // Kembalikan ke urutan kronologis (paling lama → paling baru)
        messages.Reverse();

        foreach (var msg in messages)
        {
            if (msg.Role == "user")
                history.AddUserMessage(msg.Content);
            else if (msg.Role == "assistant")
                history.AddAssistantMessage(msg.Content);
        }

        return history;
    }

    /// <summary>
    /// Build system prompt yang kaya konteks.
    /// </summary>
    private string BuildSystemPrompt()
    {
        var basePrompt = _config.GetValue<string>("ChatBot:SystemPrompt") ??
            "Kamu adalah Coach Tommy, asisten virtual kebugaran profesional dari FitnessCenter.";

        var func = _config.GetSection("ChatBot:Functions");
        var sb = new System.Text.StringBuilder(basePrompt);
        sb.AppendLine("\n\n=== KEMAMPUAN SPESIAL ===");
        sb.AppendLine("Kamu memiliki akses ke fungsi-fungsi berikut. GUNAKAN dengan bijak:");

        if (func.GetValue<bool>("EnableDatabaseQuery", true))
        {
            sb.AppendLine(
                "\n📊 DATABASE (DatabaseQuery plugin):" +
                "\n   - get_member_count — Total member aktif" +
                "\n   - get_member_by_name — Cari member by nama" +
                "\n   - get_member_stats — Statistik member lengkap" +
                "\n   - get_classes_today — Jadwal kelas hari ini" +
                "\n   - get_class_by_type — Cari kelas by tipe" +
                "\n   - get_trainers — Daftar trainer + rating" +
                "\n   - get_trainer_schedule — Jadwal trainer tertentu" +
                "\n   - get_membership_plans — Paket membership + harga" +
                "\n   - get_revenue_today — Pendapatan hari ini" +
                "\n   - get_upcoming_events — Event mendatang" +
                "\n   - get_active_discounts — Promo aktif" +
                "\n   - get_leaderboard — Top 10 leaderboard");
        }

        if (func.GetValue<bool>("EnableWebSearch", true))
        {
            sb.AppendLine(
                "\n🔍 WEB SEARCH (Web plugin):" +
                "\n   - search_internet — Cari apapun di internet via Tavily" +
                "\n   - get_fitness_news — Berita fitness terbaru" +
                "\n   - get_exercise_info — Info teknik latihan spesifik");
        }

        if (func.GetValue<bool>("EnableWebScraping", true))
        {
            sb.AppendLine(
                "\n📄 WEB SCRAPING (Web plugin):" +
                "\n   - scrape_webpage — Baca konten halaman web" +
                "\n   - read_file_from_url — Baca file dari URL");
        }

        if (func.GetValue<bool>("EnableDateTime", true))
        {
            sb.AppendLine(
                "\n🕐 DATE & TIME (Utility plugin):" +
                "\n   - get_current_time — Waktu saat ini (UTC, WIB, WITA, WIT)" +
                "\n   - get_date_info — Info detail tanggal" +
                "\n   - calculate_days_between — Selisih hari antar tanggal");
        }

        if (func.GetValue<bool>("EnableMathCalculation", true))
        {
            sb.AppendLine(
                "\n🧮 MATH & FITNESS (Utility plugin):" +
                "\n   - calculate — Kalkulasi matematika" +
                "\n   - calculate_bmi — Hitung BMI" +
                "\n   - calculate_calories_burned — Estimasi kalori terbakar" +
                "\n   - convert_unit — Konversi satuan (kg↔lbs, cm↔inch, km↔mile, C↔F)");
        }

        sb.AppendLine(
            "\n\n=== ATURAN PENTING ===" +
            "\n1. Jika diminta data spesifik (member, kelas, trainer), WAJIB gunakan fungsi DatabaseQuery." +
            "\n2. Jika ditanya berita/trend terkini, WAJIB gunakan fungsi search_internet." +
            "\n3. Jika ditanya perhitungan (BMI, kalori), gunakan fungsi calculate_bmi / calculate_calories_burned." +
            "\n4. Berikan jawaban yang MEMOTIVASI dan POSITIF. Gunakan emoji!" +
            "\n5. Untuk pertanyaan medis serius, sarankan konsultasi dokter." +
            "\n6. Format jawaban dengan MARKDOWN: **bold**, list, table, code block." +
            "\n7. SELALU jawab dalam Bahasa Indonesia yang santai dan ramah." +
            "\n8. Panggil user dengan 'kamu', 'bro/sis', atau nama jika diketahui.");

        return sb.ToString();
    }

    // ==================== CONFIGURATION INFO ====================

    public ChatBotInfo GetChatBotInfo()
    {
        var provider = _config.GetValue<string>("AI:DefaultProvider") ?? "OpenAI";
        return new ChatBotInfo
        {
            Name = BotName,
            Provider = provider,
            Model = GetModelName(provider),
            Temperature = _config.GetValue<double>("ChatBot:Temperature", 0.7),
            MaxTokens = _config.GetValue<int>("ChatBot:MaxTokens", 2048),
            FunctionsEnabled = new Dictionary<string, bool>
            {
                ["WebSearch"] = _config.GetValue<bool>("ChatBot:Functions:EnableWebSearch", true),
                ["WebScraping"] = _config.GetValue<bool>("ChatBot:Functions:EnableWebScraping", true),
                ["DateTime"] = _config.GetValue<bool>("ChatBot:Functions:EnableDateTime", true),
                ["MathCalculation"] = _config.GetValue<bool>("ChatBot:Functions:EnableMathCalculation", true),
                ["DatabaseQuery"] = _config.GetValue<bool>("ChatBot:Functions:EnableDatabaseQuery", true),
            }
        };
    }
}

/// <summary>
/// Informasi konfigurasi ChatBot.
/// </summary>
public class ChatBotInfo
{
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public int MaxTokens { get; set; }
    public Dictionary<string, bool> FunctionsEnabled { get; set; } = new();
}
