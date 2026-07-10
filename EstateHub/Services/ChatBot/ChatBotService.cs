using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using EstateHub.Data;
using EstateHub.Models;

// Resolve ambiguity between EstateHub.Models.ChatHistory and SK ChatHistory
using SKChatHistory = Microsoft.SemanticKernel.ChatCompletion.ChatHistory;
using DbChatHistory = EstateHub.Models.ChatHistory;

namespace EstateHub.Services.ChatBot;

/// <summary>
/// AI ChatBot "Tante Rita" - fully powered by Semantic Kernel
/// Supports: OpenAI, Azure OpenAI, Ollama with configurable settings
/// </summary>
public class ChatBotService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppDbContext _db;
    private readonly EstateHubKernelFunctions _kernelFunctions;
    private readonly string _provider;
    private readonly string _systemPrompt;
    private Kernel? _kernel;

    public ChatBotService(
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        AppDbContext db,
        EstateHubKernelFunctions kernelFunctions)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _db = db;
        _kernelFunctions = kernelFunctions;
        _provider = config.GetValue<string>("ChatBot:Provider") ?? "OpenAI";
        _systemPrompt = config.GetValue<string>("ChatBot:SystemPrompt")
            ?? "Kamu adalah Tante Rita, asisten EstateHub yang ramah dan profesional.";
    }

    private Kernel GetKernel()
    {
        if (_kernel != null) return _kernel;
        var builder = Kernel.CreateBuilder();

        switch (_provider.ToLower())
        {
            case "openai":
                var key = _config.GetValue<string>("ChatBot:Models:OpenAI:ApiKey") ?? "";
                var model = _config.GetValue<string>("ChatBot:Models:OpenAI:ModelId") ?? "gpt-4o";
                if (!string.IsNullOrEmpty(key))
                    builder.AddOpenAIChatCompletion(modelId: model, apiKey: key);
                break;
            case "azureopenai":
                var azKey = _config.GetValue<string>("ChatBot:Models:AzureOpenAI:ApiKey") ?? "";
                var azEp = _config.GetValue<string>("ChatBot:Models:AzureOpenAI:Endpoint") ?? "";
                var azModel = _config.GetValue<string>("ChatBot:Models:AzureOpenAI:ModelId") ?? "gpt-4o";
                if (!string.IsNullOrEmpty(azKey))
                    builder.AddAzureOpenAIChatCompletion(deploymentName: azModel, endpoint: azEp, apiKey: azKey);
                break;
            case "ollama":
                var olEp = _config.GetValue<string>("ChatBot:Models:Ollama:Endpoint") ?? "http://localhost:11434";
                var olModel = _config.GetValue<string>("ChatBot:Models:Ollama:ModelId") ?? "llama3.2";
                builder.AddOpenAIChatCompletion(modelId: olModel, endpoint: new Uri($"{olEp}/v1"), apiKey: "ollama");
                break;
        }

        builder.Plugins.AddFromObject(_kernelFunctions, "EstateHub");
        builder.Plugins.AddFromType<TimePlugin>("Time");
        _kernel = builder.Build();
        return _kernel;
    }

    public async Task<ChatBotResponse> SendMessageAsync(string message, int? sessionId, string? imageUrl = null)
    {
        ChatSession session;
        if (sessionId.HasValue)
            session = await _db.ChatSessions.Include(s => s.Messages).FirstOrDefaultAsync(s => s.Id == sessionId.Value) ?? CreateNewSession();
        else
        {
            session = CreateNewSession();
            _db.ChatSessions.Add(session);
            await _db.SaveChangesAsync();
        }

        var userMsg = new DbChatHistory
        {
            SessionId = session.Id, Role = "user", Content = message,
            ContentType = string.IsNullOrEmpty(imageUrl) ? "text" : "image",
            AttachmentUrl = imageUrl, CreatedAt = DateTime.UtcNow
        };
        _db.ChatHistories.Add(userMsg);
        await _db.SaveChangesAsync();

        var skHistory = BuildSKChatHistory(session, message, imageUrl);
        string aiResponse;

        try
        {
            var kernel = GetKernel();
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var settings = new OpenAIPromptExecutionSettings
            {
                Temperature = _config.GetValue<double>("ChatBot:Temperature", 0.7),
                MaxTokens = _config.GetValue<int>("ChatBot:MaxTokens", 4096),
                TopP = _config.GetValue<double>("ChatBot:TopP", 0.95),
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };
            var result = await chatService.GetChatMessageContentAsync(skHistory, settings, kernel);
            aiResponse = result.Content ?? "Maaf, Tante Rita tidak bisa merespon saat ini.";
        }
        catch
        {
            aiResponse = await GetFallbackResponseAsync(message);
        }

        var assistantMsg = new DbChatHistory
        {
            SessionId = session.Id, Role = "assistant", Content = aiResponse,
            ContentType = "text", CreatedAt = DateTime.UtcNow
        };
        _db.ChatHistories.Add(assistantMsg);
        session.LastMessageAt = DateTime.UtcNow;
        if (session.Messages.Count <= 2 && session.Title == "New Chat")
            session.Title = message.Length > 50 ? message[..50] + "..." : message;
        await _db.SaveChangesAsync();

        return new ChatBotResponse { SessionId = session.Id, Message = aiResponse, SessionTitle = session.Title };
    }

    private SKChatHistory BuildSKChatHistory(ChatSession session, string newMessage, string? imageUrl)
    {
        var history = new SKChatHistory();
        history.AddSystemMessage(_systemPrompt);
        var dbCtx = GetDatabaseContextAsync().GetAwaiter().GetResult();
        if (!string.IsNullOrEmpty(dbCtx)) history.AddSystemMessage(dbCtx);

        foreach (var msg in session.Messages.OrderBy(m => m.CreatedAt))
        {
            if (msg.Role == "user")
            {
                if (msg.ContentType == "image" && !string.IsNullOrEmpty(msg.AttachmentUrl))
                    history.AddUserMessage([new ImageContent(new Uri(msg.AttachmentUrl)), new TextContent(msg.Content)]);
                else
                    history.AddUserMessage(msg.Content);
            }
            else if (msg.Role == "assistant")
                history.AddAssistantMessage(msg.Content);
        }

        if (!string.IsNullOrEmpty(imageUrl))
            history.AddUserMessage([new ImageContent(new Uri(imageUrl)), new TextContent(newMessage)]);
        else
            history.AddUserMessage(newMessage);

        return history;
    }

    private async Task<string> GetDatabaseContextAsync()
    {
        try
        {
            var pc = await _db.Properties.CountAsync();
            var cities = await _db.Properties.Where(p => p.City != null).Select(p => p.City!).Distinct().Take(10).ToListAsync();
            return $"[DB] Properti: {pc}. Kota: {string.Join(", ", cities)}.";
        }
        catch { return ""; }
    }

    private ChatSession CreateNewSession() => new() { Title = "New Chat", UserId = "buyer-001", CreatedAt = DateTime.UtcNow };

    public async Task<List<ChatSession>> GetSessionsAsync(string userId) =>
        await _db.ChatSessions.Where(s => s.UserId == userId).OrderByDescending(s => s.LastMessageAt).ToListAsync();

    public async Task<List<DbChatHistory>> GetSessionHistoryAsync(int sessionId) =>
        await _db.ChatHistories.Where(h => h.SessionId == sessionId).OrderBy(h => h.CreatedAt).ToListAsync();

    public async Task DeleteSessionAsync(int sessionId)
    {
        var s = await _db.ChatSessions.FindAsync(sessionId);
        if (s != null) { _db.ChatSessions.Remove(s); await _db.SaveChangesAsync(); }
    }

    public async Task ResetSessionAsync(int sessionId)
    {
        var msgs = await _db.ChatHistories.Where(h => h.SessionId == sessionId).ToListAsync();
        _db.ChatHistories.RemoveRange(msgs);
        await _db.SaveChangesAsync();
    }

    private async Task<string> GetFallbackResponseAsync(string message)
    {
        var msg = message.ToLower();

        if (msg.Contains("kpr") || msg.Contains("cicilan"))
            return "💡 **Tante Rita Offline** - Untuk simulasi KPR, gunakan **KPR Simulator** di menu aplikasi ya! Kamu bisa hitung cicilan, lihat jadwal amortisasi, dan cek kelayakan kredit. 💰";

        if (msg.Contains("rekomendasi") || msg.Contains("cari"))
        {
            var props = await _db.Properties.Where(p => p.IsVerified && p.Status == "Available").Take(5).ToListAsync();
            if (props.Any())
            {
                var lines = string.Join("\n", props.Select(p => $"- **{p.Title}** | {p.City} | Rp {p.Price:N0}"));
                return $"💡 **Rekomendasi:**\n{lines}\n\nKlik properti untuk detail! 🏠";
            }
            return "💡 Belum ada properti tersedia.";
        }

        if (msg.Contains("pajak") || msg.Contains("pph") || msg.Contains("bphtb"))
            return "💡 **Info Pajak:** PPh Penjual 2.5%, BPHTB Pembeli 5%, PPN 11%, Notaris ~1%.";

        return "💡 Hai! Tante Rita di sini. Kamu bisa cari properti, simulasi KPR, atau tanya soal pajak & legal properti. Ada yang bisa Tante bantu? 😊";
    }
}

/// <summary>
/// Time kernel functions plugin
/// </summary>
public class TimePlugin
{
    [KernelFunction("get_time")]
    [Description("Mendapatkan waktu dan tanggal saat ini")]
    public string GetTime()
    {
        var now = DateTime.Now;
        return $"🕐 {now:dddd, dd MMMM yyyy HH:mm:ss} WIB";
    }

    [KernelFunction("get_day_of_week")]
    [Description("Mendapatkan nama hari dari tanggal")]
    public string GetDayOfWeek([Description("Tanggal format yyyy-MM-dd")] string dateStr)
    {
        if (DateTime.TryParse(dateStr, out var date))
            return $"📅 {date:dddd, dd MMMM yyyy}";
        return "Format tanggal tidak valid.";
    }
}

public class ChatBotResponse
{
    public int SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string SessionTitle { get; set; } = string.Empty;
}
