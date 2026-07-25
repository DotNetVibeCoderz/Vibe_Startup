using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Ngibrid.Data;
using Ngibrid.Models;

namespace Ngibrid.Services;

/// <summary>
/// AI Chatbot "Mas Supri" — Semantic Kernel orchestration with multi-LLM support.
///
/// All four providers run the same <see cref="Kernel"/> and therefore the same kernel functions:
///   • OpenAI and Ollama go through SK's native connector with automatic function invocation.
///   • Anthropic and Gemini go through <see cref="AnthropicChatClient"/> / <see cref="GeminiChatClient"/>,
///     which implement the tool-call loop against their native APIs (SK ships no connector for them).
///
/// Persona, temperature, max tokens, and model selection all come from appsettings.json and are
/// editable at runtime from the Settings page.
/// </summary>
public class ChatBotService
{
    private readonly IConfiguration _config;
    private readonly NgibridDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ChatBotService> _logger;

    public ChatBotService(IConfiguration config, NgibridDbContext db,
        IHttpClientFactory http, IServiceScopeFactory scopeFactory,
        IWebHostEnvironment env, ILogger<ChatBotService> logger)
    {
        _config = config;
        _db = db;
        _http = http;
        _scopeFactory = scopeFactory;
        _env = env;
        _logger = logger;
    }

    /// <summary>Models the UI can offer.</summary>
    public static readonly string[] SupportedModels = { "OpenAI", "Anthropic", "Gemini", "Ollama" };

    // ═══════════════════════════════════════
    //  SEMANTIC KERNEL BUILD
    // ═══════════════════════════════════════

    /// <summary>
    /// Build a kernel with every plugin registered. A chat completion service is attached only for
    /// providers SK can talk to directly; the others use the kernel purely as a function registry.
    /// </summary>
    private Kernel BuildKernel(string model, long? userId)
    {
        var builder = Kernel.CreateBuilder();

        switch (model.ToLowerInvariant())
        {
            case "openai":
            {
                var apiKey = _config["ChatBot:Models:OpenAI:ApiKey"];
                var modelId = _config["ChatBot:Models:OpenAI:Model"] ?? "gpt-4o";
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    var endpoint = _config["ChatBot:Models:OpenAI:Endpoint"];
                    if (!string.IsNullOrWhiteSpace(endpoint) &&
                        !endpoint.StartsWith("https://api.openai.com", StringComparison.OrdinalIgnoreCase))
                        builder.AddOpenAIChatCompletion(modelId, new Uri(endpoint), apiKey);
                    else
                        builder.AddOpenAIChatCompletion(modelId, apiKey);
                }
                break;
            }

            case "ollama":
            {
                // Ollama exposes an OpenAI-compatible surface at /v1; the API key is ignored but required.
                var endpoint = _config["ChatBot:Models:Ollama:Endpoint"] ?? "http://localhost:11434";
                var modelId = _config["ChatBot:Models:Ollama:Model"] ?? "llama3.2";
                builder.AddOpenAIChatCompletion(modelId, new Uri($"{endpoint.TrimEnd('/')}/v1"), "ollama");
                break;
            }
        }

        RegisterPlugins(builder, userId);
        return builder.Build();
    }

    private void RegisterPlugins(IKernelBuilder builder, long? userId)
    {
        builder.Plugins.AddFromObject(new LogisticsPlugin(_scopeFactory, _config, userId), "Logistics");
        builder.Plugins.AddFromObject(new DateTimePlugin(), "DateTime");
        builder.Plugins.AddFromObject(new MathPlugin(), "Math");
        builder.Plugins.AddFromObject(new InternetPlugin(_http, _config), "Internet");
        builder.Plugins.AddFromObject(new PricingPlugin(_scopeFactory, _config), "Pricing");
        builder.Plugins.AddFromObject(new SupportPlugin(_scopeFactory, userId), "Support");
    }

    /// <summary>Names of every kernel function available to the model — surfaced in the UI.</summary>
    public IReadOnlyList<string> GetAvailableFunctions()
    {
        var builder = Kernel.CreateBuilder();
        RegisterPlugins(builder, null);
        return builder.Build().Plugins.GetFunctionsMetadata()
            .Select(f => f.Name).OrderBy(n => n).ToList();
    }

    // ═══════════════════════════════════════
    //  SEND MESSAGE
    // ═══════════════════════════════════════

    public async Task<ChatMessage> SendMessageAsync(long sessionId, string userMessage,
        string? attachmentsJson = null, CancellationToken ct = default)
    {
        var session = await _db.ChatSessions.FindAsync(new object?[] { sessionId }, ct)
            ?? throw new KeyNotFoundException("Chat session not found");

        var userChatMsg = new ChatMessage
        {
            ChatSessionId = sessionId,
            Role = "user",
            Content = userMessage,
            AttachmentsJson = attachmentsJson
        };
        _db.ChatMessages.Add(userChatMsg);
        await _db.SaveChangesAsync(ct);

        var maxHistory = _config.GetValue("ChatBot:MaxHistoryMessages", 30);
        var history = await _db.ChatMessages
            .Where(m => m.ChatSessionId == sessionId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(maxHistory)
            .ToListAsync(ct);
        history.Reverse();

        var kernel = BuildKernel(session.Model, session.UserId);
        var temperature = session.Temperature;
        var maxTokens = _config.GetValue("ChatBot:MaxTokens", 2000);

        string responseContent;
        try
        {
            responseContent = session.Model.ToLowerInvariant() switch
            {
                "openai" or "ollama" => await SendWithSemanticKernelAsync(kernel, session, history, maxTokens, ct),
                "anthropic" => await new AnthropicChatClient(_http, _config, _logger)
                    .CompleteAsync(kernel, await BuildTurnsAsync(session, history), temperature, maxTokens, ct: ct),
                "gemini" => await new GeminiChatClient(_http, _config, _logger)
                    .CompleteAsync(kernel, await BuildTurnsAsync(session, history), temperature, maxTokens, ct: ct),
                _ => $"🔧 Model '{session.Model}' belum didukung. Pilih salah satu: {string.Join(", ", SupportedModels)}."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat completion failed for session {SessionId} on {Model}", sessionId, session.Model);
            responseContent = $"❌ Gagal menghubungi model {session.Model}: {ex.Message}";
        }

        var assistantMsg = new ChatMessage
        {
            ChatSessionId = sessionId,
            Role = "assistant",
            Content = responseContent,
            ModelUsed = session.Model
        };
        _db.ChatMessages.Add(assistantMsg);

        session.UpdatedAt = DateTime.UtcNow;
        if (IsDefaultTitle(session.Title) && !string.IsNullOrWhiteSpace(userMessage))
            session.Title = userMessage.Length > 40 ? userMessage[..40] + "…" : userMessage;

        await _db.SaveChangesAsync(ct);
        return assistantMsg;
    }

    private static bool IsDefaultTitle(string title) =>
        title.Equals("New Chat", StringComparison.OrdinalIgnoreCase) || title.StartsWith("Chat ", StringComparison.Ordinal);

    /// <summary>
    /// OpenAI / Ollama path: SK invokes kernel functions automatically and loops until the model
    /// produces a final answer.
    /// </summary>
    private async Task<string> SendWithSemanticKernelAsync(Kernel kernel, ChatSession session,
        List<ChatMessage> history, int maxTokens, CancellationToken ct)
    {
        if (!kernel.Services.GetServices<IChatCompletionService>().Any())
            return $"🔧 API Key {session.Model} belum dikonfigurasi. Atur di Settings → Chat Bot AI.";

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(ResolveSystemPrompt(session));

        foreach (var msg in history)
        {
            if (msg.Role == "assistant")
            {
                chatHistory.AddAssistantMessage(msg.Content);
                continue;
            }
            if (msg.Role != "user") continue;

            var images = await LoadImagesAsync(msg.AttachmentsJson, ct);
            var text = ComposeUserText(msg);

            if (images.Count == 0)
            {
                chatHistory.AddUserMessage(text);
            }
            else
            {
                var items = new ChatMessageContentItemCollection { new TextContent(text) };
                foreach (var img in images)
                    items.Add(new ImageContent($"data:{img.MediaType};base64,{img.Base64Data}"));
                chatHistory.AddUserMessage(items);
            }
        }

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = session.Temperature,
            MaxTokens = maxTokens,
            TopP = _config.GetValue("ChatBot:TopP", 0.95),
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var result = await chatService.GetChatMessageContentAsync(chatHistory, settings, kernel, ct);
        return result.Content ?? "Maaf, saya tidak bisa merespons saat ini.";
    }

    /// <summary>Provider-agnostic turn list for the Anthropic/Gemini clients.</summary>
    private async Task<List<AiTurn>> BuildTurnsAsync(ChatSession session, List<ChatMessage> history)
    {
        var turns = new List<AiTurn>
        {
            new() { Role = "system", Text = ResolveSystemPrompt(session) }
        };

        foreach (var msg in history)
        {
            if (msg.Role is not ("user" or "assistant")) continue;

            turns.Add(new AiTurn
            {
                Role = msg.Role,
                Text = msg.Role == "user" ? ComposeUserText(msg) : msg.Content,
                Images = msg.Role == "user" ? await LoadImagesAsync(msg.AttachmentsJson) : new List<AiImage>()
            });
        }

        return turns;
    }

    private string ResolveSystemPrompt(ChatSession session)
    {
        var persona = session.SystemPrompt
            ?? _config["ChatBot:Persona"]
            ?? "Kamu adalah Mas Supri, asisten virtual Ngibrid Logistics.";

        // Tool-use guidance keeps the model from inventing tracking data it could look up instead.
        return $"""
            {persona}

            Panduan menjawab:
            - Gunakan fungsi/tool yang tersedia untuk mengambil data nyata (tracking, tarif, gudang, statistik).
              Jangan pernah mengarang nomor resi, harga, atau status pengiriman.
            - Jawab dalam Bahasa Indonesia yang ramah dan ringkas.
            - Gunakan format Markdown: tabel untuk data terstruktur, `kode` untuk nomor resi, dan
              tautan `[teks](url)` bila merujuk sumber.
            - Bila pengguna melampirkan dokumen, gunakan fungsi read_file_from_url untuk membacanya.
            - Waktu saat ini: {DateTime.UtcNow.AddHours(7):dddd, dd MMMM yyyy HH:mm} WIB.
            """;
    }

    /// <summary>
    /// Fold non-image attachments into the message text as links, so the model knows they exist
    /// and can call read_file_from_url on them.
    /// </summary>
    private static string ComposeUserText(ChatMessage message)
    {
        var attachments = ParseAttachments(message.AttachmentsJson);
        var documents = attachments.Where(a => !a.IsImage).ToList();
        if (documents.Count == 0) return message.Content;

        var lines = documents.Select(d => $"- [{d.FileName}]({d.Url})");
        return $"{message.Content}\n\nLampiran dokumen:\n{string.Join('\n', lines)}";
    }

    public static List<ChatAttachment> ParseAttachments(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<ChatAttachment>();
        try
        {
            return JsonSerializer.Deserialize<List<ChatAttachment>>(json) ?? new List<ChatAttachment>();
        }
        catch (JsonException)
        {
            return new List<ChatAttachment>();
        }
    }

    /// <summary>
    /// Load attached images as base64 so vision models receive the bytes directly. Local uploads are
    /// read from disk because the provider cannot reach a localhost URL.
    /// </summary>
    private async Task<List<AiImage>> LoadImagesAsync(string? attachmentsJson, CancellationToken ct = default)
    {
        var images = new List<AiImage>();
        var attachments = ParseAttachments(attachmentsJson).Where(a => a.IsImage).Take(4).ToList();
        if (attachments.Count == 0) return images;

        foreach (var attachment in attachments)
        {
            try
            {
                byte[] bytes;

                if (attachment.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    attachment.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    var client = _http.CreateClient("Default");
                    bytes = await client.GetByteArrayAsync(attachment.Url, ct);
                }
                else
                {
                    var root = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
                    var relative = attachment.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                    var path = Path.Combine(root, relative);

                    // Guard against a crafted URL escaping wwwroot.
                    var fullPath = Path.GetFullPath(path);
                    if (!fullPath.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase)) continue;
                    if (!File.Exists(fullPath)) continue;

                    bytes = await File.ReadAllBytesAsync(fullPath, ct);
                }

                images.Add(new AiImage
                {
                    MediaType = attachment.ContentType ?? "image/png",
                    Base64Data = Convert.ToBase64String(bytes)
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load attachment {Url}", attachment.Url);
            }
        }

        return images;
    }

    // ═══════════════════════════════════════
    //  SESSION MANAGEMENT
    // ═══════════════════════════════════════

    public async Task<ChatSession> CreateSessionAsync(long userId, string title = "New Chat",
        string? model = null, string? systemPrompt = null)
    {
        var session = new ChatSession
        {
            UserId = userId,
            Title = title,
            Model = model ?? _config["ChatBot:DefaultModel"] ?? "OpenAI",
            SystemPrompt = systemPrompt ?? _config["ChatBot:Persona"],
            Temperature = _config.GetValue("ChatBot:Temperature", 0.7)
        };
        _db.ChatSessions.Add(session);
        await _db.SaveChangesAsync();
        return session;
    }

    /// <summary>Soft-delete keeps the transcript for audit while hiding it from the user.</summary>
    public async Task DeleteSessionAsync(long sessionId)
    {
        var session = await _db.ChatSessions.FindAsync(sessionId);
        if (session == null) return;
        session.IsActive = false;
        session.IsDeleted = true;
        session.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    /// <summary>Clear a session's messages but keep the session and its settings.</summary>
    public async Task ResetSessionAsync(long sessionId)
    {
        var messages = await _db.ChatMessages.Where(m => m.ChatSessionId == sessionId).ToListAsync();
        _db.ChatMessages.RemoveRange(messages);

        var session = await _db.ChatSessions.FindAsync(sessionId);
        if (session != null) session.Title = "New Chat";

        await _db.SaveChangesAsync();
    }

    public async Task UpdateSessionAsync(long sessionId, string? model = null, double? temperature = null,
        string? systemPrompt = null, string? title = null)
    {
        var session = await _db.ChatSessions.FindAsync(sessionId);
        if (session == null) return;

        if (!string.IsNullOrWhiteSpace(model)) session.Model = model;
        if (temperature.HasValue) session.Temperature = Math.Clamp(temperature.Value, 0, 2);
        if (systemPrompt != null) session.SystemPrompt = systemPrompt;
        if (!string.IsNullOrWhiteSpace(title)) session.Title = title;

        await _db.SaveChangesAsync();
    }

    public async Task<List<ChatSession>> GetUserSessionsAsync(long userId) =>
        await _db.ChatSessions
            .Where(s => s.UserId == userId && !s.IsDeleted && s.IsActive)
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .ToListAsync();

    public async Task<ChatSession?> GetSessionAsync(long sessionId) =>
        await _db.ChatSessions.FirstOrDefaultAsync(s => s.Id == sessionId && !s.IsDeleted);

    public async Task<List<ChatMessage>> GetSessionMessagesAsync(long sessionId) =>
        await _db.ChatMessages.Where(m => m.ChatSessionId == sessionId)
            .OrderBy(m => m.CreatedAt).ToListAsync();
}

/// <summary>
/// An uploaded file attached to a chat message. Images are sent to the model as image content;
/// everything else is linked in the message text for read_file_from_url to fetch.
/// </summary>
public class ChatAttachment
{
    public string FileName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }

    public bool IsImage => ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true;

    public string SizeDisplay => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
        _ => $"{SizeBytes / 1024.0 / 1024.0:F1} MB"
    };
}
