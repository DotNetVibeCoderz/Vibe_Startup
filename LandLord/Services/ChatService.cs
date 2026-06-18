using System.Text;
using System.Text.RegularExpressions;
using LandLord.Data;
using LandLord.Models;
using Microsoft.EntityFrameworkCore;

namespace LandLord.Services;

/// <summary>
/// Legacy Chat Service — fallback jika SkChatService tidak tersedia
/// </summary>
public class ChatService : IChatService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IKernelFunctionsService _kernelFunctions;
    private readonly ILogger<ChatService> _logger;
    private readonly IStorageService? _storage;
    private readonly string _botName;
    private readonly string _systemPrompt;

    public ChatService(AppDbContext context, IConfiguration configuration,
        IKernelFunctionsService kernelFunctions, ILogger<ChatService> logger,
        IStorageService? storage = null)
    {
        _context = context;
        _configuration = configuration;
        _kernelFunctions = kernelFunctions;
        _logger = logger;
        _storage = storage;
        _botName = configuration.GetValue<string>("ChatBot:Name") ?? "Frengky Ganteng";
        _systemPrompt = configuration.GetValue<string>("ChatBot:SystemPrompt") ?? "";
    }

    public async Task<List<ChatSession>> GetSessionsAsync(string? userId)
        => await _context.ChatSessions.Where(s => s.UserId == userId && s.IsActive)
            .OrderByDescending(s => s.LastUpdatedAt).ToListAsync();

    public async Task<ChatSession> CreateSessionAsync(string? userId, string title = "Chat Baru")
    {
        var session = new ChatSession { UserId = userId, Title = title, CreatedAt = DateTime.UtcNow, LastUpdatedAt = DateTime.UtcNow, IsActive = true };
        _context.ChatSessions.Add(session); await _context.SaveChangesAsync(); return session;
    }

    public async Task<ChatSession?> GetSessionAsync(int sessionId)
        => await _context.ChatSessions.Include(s => s.Messages.OrderBy(m => m.SentAt)).FirstOrDefaultAsync(s => s.Id == sessionId);

    public async Task<List<ChatMessage>> GetMessagesAsync(int sessionId)
        => await _context.ChatMessages.Where(m => m.ChatSessionId == sessionId).OrderBy(m => m.SentAt).ToListAsync();

    public async Task<ChatMessage> SendMessageAsync(int sessionId, string content,
        string? imageUrl = null, string? documentUrl = null, string? documentName = null)
    {
        var message = new ChatMessage { ChatSessionId = sessionId, Role = "user", Content = content, ImageUrl = imageUrl, DocumentUrl = documentUrl, DocumentName = documentName, SentAt = DateTime.UtcNow };
        _context.ChatMessages.Add(message);
        var session = await _context.ChatSessions.FindAsync(sessionId);
        if (session != null) { session.LastUpdatedAt = DateTime.UtcNow; if (session.Title == "Chat Baru" && !string.IsNullOrWhiteSpace(content)) session.Title = content.Length > 50 ? content[..50] + "..." : content; }
        await _context.SaveChangesAsync(); return message;
    }

    public async Task<bool> DeleteSessionAsync(int sessionId)
    { var s = await _context.ChatSessions.FindAsync(sessionId); if (s == null) return false; s.IsActive = false; await _context.SaveChangesAsync(); return true; }

    public async Task<bool> ResetSessionAsync(int sessionId)
    {
        var messages = await _context.ChatMessages.Where(m => m.ChatSessionId == sessionId).ToListAsync();
        _context.ChatMessages.RemoveRange(messages);
        var session = await _context.ChatSessions.FindAsync(sessionId);
        if (session != null) { session.Title = "Chat Baru"; session.LastUpdatedAt = DateTime.UtcNow; }
        await _context.SaveChangesAsync(); return true;
    }

    /// <summary>Upload file dan return URL publik</summary>
    public async Task<string?> UploadAttachmentAsync(Stream fileStream, string fileName, string contentType)
    {
        if (_storage == null) return null;
        try { var url = await _storage.UploadAsync(fileName, fileStream, contentType); return await _storage.GetPublicUrlAsync(url); }
        catch (Exception ex) { _logger.LogError(ex, "Upload failed"); return null; }
    }

    private async Task<ChatMessage> SaveAssistantMessageAsync(int sessionId, string content)
    {
        var msg = new ChatMessage { ChatSessionId = sessionId, Role = "assistant", Content = content, SentAt = DateTime.UtcNow };
        _context.ChatMessages.Add(msg);
        var session = await _context.ChatSessions.FindAsync(sessionId);
        if (session != null) session.LastUpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(); return msg;
    }

    public async Task<ChatResponse> GetAIResponseAsync(int sessionId, string userMessage,
        string? imageUrl = null, string? documentUrl = null, string? documentName = null)
    {
        var response = new ChatResponse();
        var lowerMsg = userMessage.ToLower().Trim();

        if (IsGreeting(lowerMsg)) { response.Content = GetGreetingResponse(); await SaveAssistantMessageAsync(sessionId, response.Content); return response; }
        if (IsHelpRequest(lowerMsg)) { response.Content = GetHelpResponse(); await SaveAssistantMessageAsync(sessionId, response.Content); return response; }

        // Cek image URL
        var effectiveMsg = userMessage;
        if (!string.IsNullOrEmpty(imageUrl))
            effectiveMsg = string.IsNullOrWhiteSpace(userMessage) ? "Deskripsikan gambar ini." : userMessage;
        if (!string.IsNullOrEmpty(documentUrl))
            effectiveMsg += $"\n\n[Dokumen: {documentName ?? "Dokumen"} — {documentUrl}]";

        var enableInternetSearch = _configuration.GetValue<bool>("ChatBot:EnableInternetSearch", true);

        if (enableInternetSearch && _kernelFunctions.ShouldSearchInternet(effectiveMsg))
        {
            response.UsedInternetSearch = true;
            var searchResult = await _kernelFunctions.ExecuteAsync("tavily_search", new Dictionary<string, object?> { ["query"] = effectiveMsg });
            response.Content = searchResult.Success ? searchResult.Result : searchResult.Error ?? "Error";
            response.FunctionCalled = "tavily_search";
            await SaveAssistantMessageAsync(sessionId, response.Content);
            return response;
        }

        var enableDbQuery = _configuration.GetValue<bool>("ChatBot:EnableDatabaseQuery", true);
        if (enableDbQuery)
        {
            if (lowerMsg.Contains("data tanah") || lowerMsg.Contains("cari tanah") || (lowerMsg.Contains("tanah") && lowerMsg.Contains("sertifikat")))
            {
                var keyword = ExtractKeyword(lowerMsg, "tanah");
                var dbResult = await _kernelFunctions.ExecuteAsync("query_tanah_database", new Dictionary<string, object?> { ["keyword"] = keyword });
                response.Content = dbResult.Success ? dbResult.Result : dbResult.Error ?? "Error";
                response.UsedDatabaseQuery = true; response.FunctionCalled = "query_tanah_database";
                await SaveAssistantMessageAsync(sessionId, response.Content);
                return response;
            }
            if (lowerMsg.Contains("data bangunan") || lowerMsg.Contains("cari bangunan") || (lowerMsg.Contains("bangunan") && lowerMsg.Contains("imb")))
            {
                var keyword = ExtractKeyword(lowerMsg, "bangunan");
                var dbResult = await _kernelFunctions.ExecuteAsync("query_bangunan_database", new Dictionary<string, object?> { ["keyword"] = keyword });
                response.Content = dbResult.Success ? dbResult.Result : dbResult.Error ?? "Error";
                response.UsedDatabaseQuery = true; response.FunctionCalled = "query_bangunan_database";
                await SaveAssistantMessageAsync(sessionId, response.Content);
                return response;
            }
        }

        var urls = ExtractUrls(effectiveMsg);
        if (urls.Count > 0)
        {
            var url = urls[0];
            var fn = url.EndsWith(".pdf") || url.EndsWith(".doc") ? "read_file_from_url" : "scrape_webpage";
            var result = await _kernelFunctions.ExecuteAsync(fn, new Dictionary<string, object?> { ["url"] = url });
            response.Content = result.Success ? result.Result : result.Error ?? "Error";
            response.FunctionCalled = fn;
            await SaveAssistantMessageAsync(sessionId, response.Content);
            return response;
        }

        response.Content = GetKeywordResponse(lowerMsg);
        await SaveAssistantMessageAsync(sessionId, response.Content);
        return response;
    }

    public List<KernelFunctionDefinition> GetAvailableFunctions() => _kernelFunctions.GetAvailableFunctions();

    public async Task<FunctionResult> ExecuteFunctionAsync(int sessionId, string functionName, Dictionary<string, object?> parameters)
        => await _kernelFunctions.ExecuteAsync(functionName, parameters);

    public bool ShouldSearchInternet(string userMessage) => _kernelFunctions.ShouldSearchInternet(userMessage);

    private bool IsGreeting(string msg) => msg == "hai" || msg == "halo" || msg == "hello" || msg == "hi" || msg.Contains("perkenalkan");
    private bool IsHelpRequest(string msg) => msg.Contains("help") || msg.Contains("bantuan") || msg.Contains("fitur") || msg == "?";

    private string GetGreetingResponse() =>
        $"👋 **Halo! Aku {_botName}**, asisten virtual LandLord! 🤖\n\n🌐 Internet Search • 🏞️ Cari Tanah • 🏗️ Cari Bangunan\n📄 Baca URL • 🛟 \"help\"\n\nSilakan tanya! 😊";

    private string GetHelpResponse() =>
        $"🛟 **Bantuan {_botName}**\n\n🌐 **Internet Search:** \"cari di internet [topik]\"\n🏞️ **Cari Tanah:** \"cari tanah [keyword]\"\n🏗️ **Cari Bangunan:** \"cari bangunan [keyword]\"\n📄 **Baca URL:** kirim link langsung\n📎 **Upload:** klik 📎 untuk lampirkan file";

    private string GetKeywordResponse(string msg)
    {
        if (msg.Contains("total tanah")) return $"🏞️ **{_context.Tanah.Count():N0} bidang tanah**.";
        if (msg.Contains("total bangunan")) return $"🏗️ **{_context.Bangunan.Count():N0} unit bangunan**.";
        return $"🤔 Coba: 🌐 \"cari di internet {msg}\" | 🏞️ \"cari tanah [kw]\" | 🛟 \"help\"";
    }

    private static List<string> ExtractUrls(string text)
    {
        var urls = new List<string>();
        foreach (Match m in Regex.Matches(text, @"https?://[^\s]+"))
            urls.Add(m.Value.TrimEnd('.', ',', ';', ')', ']', '}'));
        return urls;
    }

    private static string ExtractKeyword(string message, string prefix)
    {
        foreach (var t in new[] { "cari tanah", "data tanah", "tanah", "cari bangunan", "data bangunan", "bangunan", "tentang" })
            message = message.Replace(t, "", StringComparison.OrdinalIgnoreCase);
        return message.Trim();
    }
}
