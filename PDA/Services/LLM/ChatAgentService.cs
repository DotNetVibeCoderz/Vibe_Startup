using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PDA.Data;
using PDA.Models;
using PDA.Services.Database;
using PDA.Services.Storage;

namespace PDA.Services.LLM;

public class ChatAgentService
{
    private readonly AppDbContext _db;
    private readonly SemanticKernelFactory _skFactory;
    private readonly SchemaExtractionService _schemaService;
    private readonly AuditLogService _auditLog;
    private readonly PdaMonitoringService _monitoring;
    private readonly IConfiguration _configuration;
    private readonly IStorageService _storage;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ChatAgentService> _logger;

    public ChatAgentService(
        AppDbContext db, SemanticKernelFactory skFactory,
        SchemaExtractionService schemaService, AuditLogService auditLog,
        PdaMonitoringService monitoring, IConfiguration configuration,
        IStorageService storage, IHttpClientFactory httpClientFactory,
        ILogger<ChatAgentService> logger)
    {
        _db = db; _skFactory = skFactory; _schemaService = schemaService;
        _auditLog = auditLog; _monitoring = monitoring;
        _configuration = configuration; _storage = storage;
        _httpClientFactory = httpClientFactory; _logger = logger;
    }

    public async Task<ChatMessage> ProcessMessageAsync(ChatRequest request)
    {
        var startTime = DateTime.UtcNow;
        var session = await _db.ChatSessions
            .Include(s => s.Messages.OrderBy(m => m.Timestamp))
            .Include(s => s.DatabaseConnection)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId);
        if (session == null) throw new InvalidOperationException("Chat session not found.");

        _logger.LogInformation("📩 ProcessMessage: Sess={Id}, Db={DbName}, Att={AttCount}",
            session.Id, session.DatabaseConnection?.Name, request.Attachments?.Count ?? 0);

        string? attJson = request.Attachments is { Count: > 0 } ? JsonSerializer.Serialize(request.Attachments) : null;
        var userMsg = new ChatMessage { ChatSessionId = session.Id, Role = "user", Content = request.Message, Attachments = attJson, Timestamp = DateTime.UtcNow };
        _db.ChatMessages.Add(userMsg); await _db.SaveChangesAsync();

        var config = new LlmConfig { Provider = session.ModelProvider, Model = session.ModelName, Temperature = session.Temperature, MaxTokens = session.MaxTokens };
        var kernel = _skFactory.CreateKernel(config, session.DatabaseConnection);
        var chatHistory = new ChatHistory();
        var systemPrompt = await BuildSystemPromptAsync(session);
        chatHistory.AddSystemMessage(systemPrompt);

        foreach (var msg in session.Messages.OrderBy(m => m.Timestamp).TakeLast(20))
        { if (msg.Role == "user") chatHistory.AddUserMessage(msg.Content); else if (msg.Role == "assistant") chatHistory.AddAssistantMessage(msg.Content); }

        await AddUserMessageWithImagesAsync(chatHistory, request.Message, request.Attachments);

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var settings = new PromptExecutionSettings { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() };

        string finalContent = ""; string? dashboardHtml = null; int promptTokens = 0, completionTokens = 0;
        try
        {
            var result = await chatService.GetChatMessageContentAsync(chatHistory, settings, kernel);
            finalContent = result.Content ?? "";
            foreach (var item in chatHistory)
                if (item.Role == AuthorRole.Tool && item.Content != null && item.Content.Contains("dashboard-container"))
                    dashboardHtml = item.Content;
            promptTokens = systemPrompt.Length / 3 + request.Message.Length / 3;
            completionTokens = finalContent.Length / 3;
        }
        catch (Exception ex) { _logger.LogError(ex, "SK failed"); finalContent = $"❌ Error: {ex.Message}"; }

        var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
        var assistantMsg = new ChatMessage { ChatSessionId = session.Id, Role = "assistant", Content = finalContent, DashboardHtml = dashboardHtml, PromptTokens = promptTokens, CompletionTokens = completionTokens, TotalTokens = promptTokens + completionTokens, ResponseTimeMs = responseTime, Timestamp = DateTime.UtcNow };
        _db.ChatMessages.Add(assistantMsg);

        session.UpdatedAt = DateTime.UtcNow;
        if (session.Messages.Count <= 2) session.Title = request.Message.Length > 50 ? request.Message[..50] + "..." : request.Message;
        await _db.SaveChangesAsync();

        await _auditLog.LogAsync("Chat", "MessageProcessed", $"Chat: {session.Title}", durationMs: responseTime);
        _monitoring.RecordChatMessage(session.ModelProvider, promptTokens + completionTokens);
        return assistantMsg;
    }

    private async Task AddUserMessageWithImagesAsync(ChatHistory chatHistory, string message, List<MessageAttachment>? attachments)
    {
        var collection = new ChatMessageContentItemCollection();
        if (!string.IsNullOrWhiteSpace(message)) collection.Add(new TextContent(message));

        if (attachments is { Count: > 0 })
        {
            foreach (var att in attachments)
            {
                if (att.IsImage)
                {
                    try
                    {
                        var imageBytes = await DownloadImageBytesAsync(att);
                        if (imageBytes is { Length: > 0 })
                        { collection.Add(new ImageContent(new ReadOnlyMemory<byte>(imageBytes), att.ContentType)); continue; }
                    }
                    catch (Exception ex) { _logger.LogWarning(ex, "Img load fail {File}", att.FileName); }
                    collection.Add(new TextContent($"\n[📎 Gambar: {att.FileName} - gagal]"));
                }
                else { collection.Add(new TextContent($"\n[📎 File: {att.FileName}]")); }
            }
        }
        if (collection.Count > 0) chatHistory.Add(new ChatMessageContent(AuthorRole.User, collection));
        else chatHistory.AddUserMessage("(empty)");
    }

    private async Task<byte[]?> DownloadImageBytesAsync(MessageAttachment att)
    {
        if (string.IsNullOrWhiteSpace(att.FileUrl)) return null;
        var stream = await _storage.DownloadAsync(att.FileUrl);
        if (stream != null) { using var ms = new MemoryStream(); await stream.CopyToAsync(ms); return ms.ToArray(); }
        if (att.FileUrl.StartsWith("http")) { var c = _httpClientFactory.CreateClient("DefaultClient"); return await c.GetByteArrayAsync(att.FileUrl); }
        return null;
    }

    public async Task<ChatSession> CreateSessionAsync(string userId, int? databaseConnectionId = null)
    {
        DatabaseConnection? dbConn = null;
        if (databaseConnectionId.HasValue) dbConn = await _db.DatabaseConnections.FirstOrDefaultAsync(d => d.Id == databaseConnectionId.Value && d.UserId == userId);
        dbConn ??= await _db.DatabaseConnections.Where(d => d.UserId == userId && d.IsActive).OrderBy(d => d.CreatedAt).FirstOrDefaultAsync();
        dbConn ??= await _db.DatabaseConnections.FirstOrDefaultAsync();
        var session = new ChatSession { Title = "New Chat", UserId = userId, DatabaseConnectionId = dbConn?.Id, DatabaseConnection = dbConn, ModelProvider = _configuration["LLM:DefaultProvider"] ?? "OpenAI", ModelName = _configuration["LLM:DefaultModel"] ?? "gpt-4o", Temperature = double.TryParse(_configuration["LLM:DefaultTemperature"], out var t) ? t : 0.3, MaxTokens = int.TryParse(_configuration["LLM:DefaultMaxTokens"], out var mt) ? mt : 4096, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _db.ChatSessions.Add(session); await _db.SaveChangesAsync();
        var w = await GenerateWelcomeMessage(session); _db.ChatMessages.Add(w); await _db.SaveChangesAsync();
        return session;
    }

    public async Task ResetSessionAsync(int sid) { var m = await _db.ChatMessages.Where(x => x.ChatSessionId == sid).ToListAsync(); _db.ChatMessages.RemoveRange(m); await _db.SaveChangesAsync(); }
    public async Task DeleteSessionAsync(int sid) { var s = await _db.ChatSessions.Include(x => x.Messages).FirstOrDefaultAsync(x => x.Id == sid); if (s == null) return; _db.ChatMessages.RemoveRange(s.Messages); _db.ChatSessions.Remove(s); await _db.SaveChangesAsync(); }

    public async Task<List<string>> GenerateSamplePromptsAsync(int dbId)
    {
        var c = await _db.DatabaseConnections.FindAsync(dbId); if (c == null) return new();
        var sc = await _schemaService.ExtractSchemaAsync(c.DatabaseType, c.ConnectionString, c.FilePath);
        var p = new List<string>();
        if (sc.Tables.Count > 0)
        { var tn = sc.Tables.Select(t => t.Name).ToList(); p.Add($"Ringkasan {tn[0]}"); if (tn.Count > 1) p.Add($"Hubungan {tn[0]} & {tn[1]}?"); p.Add("Total record?"); p.Add("Statistik deskriptif"); p.Add("Dashboard ringkasan"); p.Add("Analisis anomali"); }
        return p;
    }

    private async Task<string> BuildSystemPromptAsync(ChatSession session)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 🧠 PDA - Personal Data Analyst");
        sb.AppendLine("You are an expert data analyst AI. Analyze data, read images, use tools.");
        sb.AppendLine("Tools: queryToDatabase, getQueryStat, searchInternet, quickSearch, searchKnowledgeBase, createDashboard, readDataFromUrl");
        if (session.DatabaseConnection != null)
        {
            try { var sc = await _schemaService.ExtractSchemaAsync(session.DatabaseConnection.DatabaseType, session.DatabaseConnection.ConnectionString, session.DatabaseConnection.FilePath); sb.AppendLine(_schemaService.GenerateSchemaPrompt(sc, session.DatabaseConnection.Name)); }
            catch { sb.AppendLine($"📊 DB: {session.DatabaseConnection.Name}"); }
        }
        sb.AppendLine("Rules: 1)If image→analyze it 2)Use queryToDatabase 3)Markdown tables 4)Respond in user's language");
        sb.AppendLine($"Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");
        return sb.ToString();
    }

    private async Task<ChatMessage> GenerateWelcomeMessage(ChatSession session)
    {
        var sb = new StringBuilder(); sb.AppendLine("👋 **PDA!** 🚀");
        if (session.DatabaseConnection != null) sb.AppendLine($"📊 DB: **{session.DatabaseConnection.Name}** ({session.DatabaseConnection.DatabaseType})");
        sb.AppendLine($"🤖 {session.ModelName} | {session.ModelProvider}"); sb.AppendLine("📎 Bisa attach gambar!");
        return new ChatMessage { ChatSessionId = session.Id, Role = "assistant", Content = sb.ToString(), Timestamp = DateTime.UtcNow };
    }
}
