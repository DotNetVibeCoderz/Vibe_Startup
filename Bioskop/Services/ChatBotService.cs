using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Bioskop.Data;
using Bioskop.Models;
using System.Text;
using System.Text.Json;
using System.ComponentModel;
using System.Data;

namespace Bioskop.Services;

public class ChatBotService
{
    private readonly IConfiguration _config;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ChatBotService> _logger;

    public ChatBotService(IConfiguration config, ApplicationDbContext context, ILogger<ChatBotService> logger)
    {
        _config = config;
        _context = context;
        _logger = logger;
    }

    public async Task<ChatSession> GetOrCreateSessionAsync(string userId, int? sessionId = null)
    {
        if (sessionId.HasValue)
        {
            var existing = await _context.ChatSessions
                .Include(cs => cs.Messages.OrderBy(m => m.CreatedAt))
                .FirstOrDefaultAsync(cs => cs.Id == sessionId && cs.UserId == userId);
            if (existing != null) { existing.LastActivityAt = DateTime.UtcNow; await _context.SaveChangesAsync(); return existing; }
        }
        var session = new ChatSession { UserId = userId, Title = "Chat Baru", LastActivityAt = DateTime.UtcNow };
        _context.ChatSessions.Add(session);
        await _context.SaveChangesAsync();
        return session;
    }

    public async Task<List<ChatSession>> GetUserSessionsAsync(string userId)
    {
        return await _context.ChatSessions
            .Where(cs => cs.UserId == userId && cs.IsActive)
            .OrderByDescending(cs => cs.LastActivityAt).ToListAsync();
    }

    public async Task<bool> ResetSessionAsync(int sessionId, string userId)
    {
        var session = await _context.ChatSessions.FirstOrDefaultAsync(cs => cs.Id == sessionId && cs.UserId == userId);
        if (session == null) return false;
        var messages = await _context.ChatMessages.Where(m => m.ChatSessionId == sessionId).ToListAsync();
        _context.ChatMessages.RemoveRange(messages);
        session.Title = "Chat Baru"; session.LastActivityAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<(string response, ChatMessage userMsg, ChatMessage assistantMsg)> SendMessageAsync(
        int sessionId, string userId, string message, string? imageUrl = null, string? documentUrl = null)
    {
        var session = await _context.ChatSessions
            .Include(cs => cs.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(cs => cs.Id == sessionId && cs.UserId == userId);
        if (session == null) throw new InvalidOperationException("Session tidak ditemukan");

        var metadata = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(imageUrl)) metadata["imageUrl"] = imageUrl;
        if (!string.IsNullOrEmpty(documentUrl)) metadata["documentUrl"] = documentUrl;

        var userMessage = new ChatMessage
        {
            ChatSessionId = sessionId, Role = "user",
            Content = BuildUserMessage(message, imageUrl, documentUrl),
            Metadata = JsonSerializer.Serialize(metadata)
        };
        _context.ChatMessages.Add(userMessage);
        await _context.SaveChangesAsync();

        if (session.Title == "Chat Baru") session.Title = message.Length > 50 ? message[..50] + "..." : message;

        var chatHistory = BuildChatHistory(session);
        var response = await GetChatCompletionAsync(chatHistory);

        var assistantMessage = new ChatMessage { ChatSessionId = sessionId, Role = "assistant", Content = response };
        _context.ChatMessages.Add(assistantMessage);
        session.LastActivityAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return (response, userMessage, assistantMessage);
    }

    private string BuildUserMessage(string message, string? imageUrl, string? documentUrl)
    {
        var sb = new StringBuilder(message);
        if (!string.IsNullOrEmpty(imageUrl)) sb.Append($"\n\n[Gambar: {imageUrl}]");
        if (!string.IsNullOrEmpty(documentUrl)) sb.Append($"\n\n[Dokumen: {documentUrl}]");
        return sb.ToString();
    }

    private ChatHistory BuildChatHistory(ChatSession session)
    {
        var systemPrompt = _config.GetValue<string>("ChatBot:SystemPrompt") ??
            "Kamu adalah Si Bobby Movie Maniac, asisten bioskop yang ramah dan antusias!";
        var history = new ChatHistory(systemPrompt);
        var moviesContext = GetMoviesContext();
        if (!string.IsNullOrEmpty(moviesContext)) history.AddSystemMessage(moviesContext);
        var recentMessages = session.Messages.OrderByDescending(m => m.CreatedAt).Take(20).Reverse();
        foreach (var msg in recentMessages)
        {
            if (msg.Role == "user")
            {
                if (TryGetImageUrl(msg, out var imageUrl) && Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
                {
                    // Tambahkan teks + lampiran gambar sebagai ImageContent
                    var items = new ChatMessageContentItemCollection
                    {
                        new TextContent(msg.Content),
                        new ImageContent(uri)
                    };
                    history.AddUserMessage(items);
                }
                else
                {
                    history.AddUserMessage(msg.Content);
                }
            }
            else if (msg.Role == "assistant")
            {
                history.AddAssistantMessage(msg.Content);
            }
        }
        return history;
    }

    private bool TryGetImageUrl(ChatMessage msg, out string? imageUrl)
    {
        imageUrl = null;
        if (string.IsNullOrWhiteSpace(msg.Metadata)) return false;
        try
        {
            var meta = JsonSerializer.Deserialize<Dictionary<string, object>>(msg.Metadata);
            if (meta != null && meta.TryGetValue("imageUrl", out var value))
            {
                imageUrl = value?.ToString();
                return !string.IsNullOrWhiteSpace(imageUrl);
            }
        }
        catch { }
        return false;
    }

    private string GetMoviesContext()
    {
        try
        {
            var nowPlaying = _context.Movies.Where(m => m.IsNowPlaying)
                .Select(m => new { m.Title, m.Genre, m.DurationMinutes, m.AgeRating, m.BasePrice }).ToList();
            if (!nowPlaying.Any()) return "";
            var sb = new StringBuilder();
            sb.AppendLine("=== FILM SEDANG TAYANG ===");
            foreach (var m in nowPlaying)
                sb.AppendLine($"- {m.Title} | {m.Genre} | {m.DurationMinutes}mnt | {m.AgeRating} | Rp{m.BasePrice:N0}");
            return sb.ToString();
        }
        catch { return ""; }
    }

    private async Task<string> GetChatCompletionAsync(ChatHistory chatHistory)
    {
        var model = _config.GetValue<string>("ChatBot:DefaultModel") ?? "OpenAI";
        try
        {
            var kernel = BuildKernel(model);
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var settings = new OpenAIPromptExecutionSettings
            {
                Temperature = _config.GetValue<double>("ChatBot:Temperature", 0.7),
                MaxTokens = _config.GetValue<int>("ChatBot:MaxTokens", 2048),
                // Auto invoke kernel functions (tools)
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: true)
            };
            var result = await chatService.GetChatMessageContentAsync(chatHistory, settings, kernel);
            return result?.Content ?? "Maaf, aku bingung... 😅";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat completion from {Model}", model);
            return $"Maaf, ada gangguan teknis... Server {model} lagi ngambek. Coba lagi ya! 🙏";
        }
    }

    private Kernel BuildKernel(string model)
    {
        var builder = Kernel.CreateBuilder();
        switch (model)
        {
            case "OpenAI":
                var openAiKey = _config.GetValue<string>("ChatBot:Models:OpenAI:ApiKey");
                var openAiModel = _config.GetValue<string>("ChatBot:Models:OpenAI:ModelId") ?? "gpt-4o";
                if (!string.IsNullOrEmpty(openAiKey)) builder.AddOpenAIChatCompletion(openAiModel, openAiKey);
                break;
            case "Ollama":
                var ollamaEndpoint = _config.GetValue<string>("ChatBot:Models:Ollama:Endpoint") ?? "http://localhost:11434";
                var ollamaModel = _config.GetValue<string>("ChatBot:Models:Ollama:ModelId") ?? "llama3.2";
                builder.AddOpenAIChatCompletion(ollamaModel, new Uri($"{ollamaEndpoint}/v1/"), "ollama");
                break;
        }
        RegisterKernelFunctions(builder);
        return builder.Build();
    }

    private void RegisterKernelFunctions(IKernelBuilder builder)
    {
        builder.Plugins.AddFromObject(new MovieDbFunctions(_context), "MovieDB");
        builder.Plugins.AddFromObject(new ShowtimeDbFunctions(_context), "ShowtimeDB");
        builder.Plugins.AddFromObject(new SnackDbFunctions(_context), "SnackDB");
        builder.Plugins.AddFromObject(new WebToolsFunctions(_config), "WebTools");
        builder.Plugins.AddFromObject(new CommonFunctions(), "CommonTools");
    }
}

public class MovieDbFunctions
{
    private readonly ApplicationDbContext _context;
    public MovieDbFunctions(ApplicationDbContext context) => _context = context;

    [KernelFunction("get_now_playing_movies")]
    [Description("Get all currently playing movies with their details")]
    public string GetNowPlayingMovies()
    {
        var movies = _context.Movies.Where(m => m.IsNowPlaying)
            .Select(m => $"{m.Title} ({m.Genre}) - Rp{m.BasePrice:N0}").ToList();
        return string.Join("\n", movies);
    }
}

public class ShowtimeDbFunctions
{
    private readonly ApplicationDbContext _context;
    public ShowtimeDbFunctions(ApplicationDbContext context) => _context = context;

    [KernelFunction("get_movie_showtimes")]
    [Description("Get available showtimes for a movie by its title")]
    public string GetMovieShowtimes(string movieTitle)
    {
        var showtimes = _context.Showtimes.Include(s => s.Movie).Include(s => s.Studio)
            .Where(s => s.Movie!.Title.Contains(movieTitle) && s.IsActive && s.StartTime > DateTime.UtcNow)
            .OrderBy(s => s.StartTime).Take(10)
            .Select(s => $"{s.StartTime:dd/MM HH:mm} - {s.Studio!.Name} ({s.ShowType}) - Rp{s.Price:N0}").ToList();
        return showtimes.Any() ? string.Join("\n", showtimes) : "Tidak ada jadwal";
    }
}

public class SnackDbFunctions
{
    private readonly ApplicationDbContext _context;
    public SnackDbFunctions(ApplicationDbContext context) => _context = context;

    [KernelFunction("get_available_snacks")]
    [Description("Get all available snacks and beverages")]
    public string GetAvailableSnacks()
    {
        var snacks = _context.Snacks.Where(s => s.IsAvailable)
            .Select(s => $"{s.Name} ({s.Category}) - Rp{s.Price:N0}").ToList();
        return string.Join("\n", snacks);
    }
}

public class WebToolsFunctions
{
    private readonly IConfiguration _config;
    public WebToolsFunctions(IConfiguration config) => _config = config;

    [KernelFunction("search_internet")]
    [Description("Search the internet using Tavily search engine")]
    public async Task<string> SearchInternet(string query)
    {
        var apiKey = _config.GetValue<string>("ChatBot:Tools:TavilyApiKey");
        if (string.IsNullOrEmpty(apiKey)) return "Tavily API key not configured";
        try
        {
            using var client = new HttpClient();
            var content = new StringContent(JsonSerializer.Serialize(new { api_key = apiKey, query, search_depth = "basic" }), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://api.tavily.com/search", content);
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex) { return $"Search error: {ex.Message}"; }
    }

    [KernelFunction("scrape_page")]
    [Description("Scrape and extract text content from a URL")]
    public async Task<string> ScrapePage(string url)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var html = await client.GetStringAsync(url);
            var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
            return text.Length > 5000 ? text[..5000] + "..." : text;
        }
        catch (Exception ex) { return $"Scrape error: {ex.Message}"; }
    }
}

public class CommonFunctions
{
    private TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        // Default Jakarta, fallback jika time zone tidak ada
        var tz = timeZoneId ?? "Asia/Jakarta";
        try { return TimeZoneInfo.FindSystemTimeZoneById(tz); }
        catch
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); }
            catch { return TimeZoneInfo.Local; }
        }
    }

    [KernelFunction("get_current_datetime")]
    [Description("Get current date time in a specific timezone (default Asia/Jakarta)")]
    public string GetCurrentDateTime(string? timeZoneId = null)
    {
        var tz = ResolveTimeZone(timeZoneId);
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        return now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    [KernelFunction("get_current_date")]
    [Description("Get current date in a specific timezone (default Asia/Jakarta)")]
    public string GetCurrentDate(string? timeZoneId = null)
    {
        var tz = ResolveTimeZone(timeZoneId);
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        return now.ToString("yyyy-MM-dd");
    }

    [KernelFunction("get_current_time")]
    [Description("Get current time in a specific timezone (default Asia/Jakarta)")]
    public string GetCurrentTime(string? timeZoneId = null)
    {
        var tz = ResolveTimeZone(timeZoneId);
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        return now.ToString("HH:mm:ss");
    }

    [KernelFunction("add_days")]
    [Description("Add days to a date (format: yyyy-MM-dd) and return new date")]
    public string AddDays(string date, int days)
    {
        if (!DateTime.TryParse(date, out var dt)) return "Invalid date";
        return dt.AddDays(days).ToString("yyyy-MM-dd");
    }

    [KernelFunction("days_between")]
    [Description("Calculate days between two dates (format: yyyy-MM-dd)")]
    public string DaysBetween(string startDate, string endDate)
    {
        if (!DateTime.TryParse(startDate, out var s) || !DateTime.TryParse(endDate, out var e))
            return "Invalid date";
        return (e.Date - s.Date).TotalDays.ToString("0");
    }

    [KernelFunction("calculate_math")]
    [Description("Calculate a math expression (example: (10+2)*3)")]
    public string CalculateMath(string expression)
    {
        try
        {
            var result = new DataTable().Compute(expression, null);
            return Convert.ToString(result) ?? "0";
        }
        catch { return "Invalid expression"; }
    }
}
