using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using FuelStation.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace FuelStation.Services;

/// <summary>
/// AI Chat Bot Service - "Bang Jenggo"
/// Supports OpenAI, Anthropic, Gemini, and Ollama models via Semantic Kernel
/// </summary>
public class ChatBotService
{
    private readonly IConfiguration _config;
    private readonly ILogger<ChatBotService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, ChatHistory> _sessions = new();
    private Kernel? _kernel;
    private IChatCompletionService? _chatService;

    public ChatBotService(IConfiguration config, ILogger<ChatBotService> logger, IServiceScopeFactory scopeFactory)
    {
        _config = config;
        _logger = logger;
        _scopeFactory = scopeFactory;
        InitializeKernel();
    }

    /// <summary>
    /// Initialize Semantic Kernel with configured provider
    /// </summary>
    private void InitializeKernel()
    {
        var provider = _config.GetValue<string>("ChatBot:Provider", "OpenAI");
        var model = _config.GetValue<string>("ChatBot:Model", "gpt-4o");
        var apiKey = _config.GetValue<string>("ChatBot:ApiKey", "");
        var endpoint = _config.GetValue<string>("ChatBot:Endpoint", "");

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("ChatBot API key not configured. Chat will not function.");
            return;
        }

        var builder = Kernel.CreateBuilder();

        switch (provider)
        {
            case "OpenAI":
            default:
                builder.AddOpenAIChatCompletion(model, apiKey);
                break;
            case "Ollama":
                // Ollama uses OpenAI-compatible API
                builder.AddOpenAIChatCompletion(model, new Uri(endpoint), apiKey);
                break;
        }

        // Register plugin functions — these are callable by the AI automatically
        builder.Plugins.AddFromObject(new ChatBotFunctions(_scopeFactory, _config), "FuelStation");

        _kernel = builder.Build();
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();

        _logger.LogInformation("ChatBot initialized with provider: {Provider}, model: {Model}", provider, model);
    }

    /// <summary>
    /// Create or retrieve a chat session
    /// </summary>
    public ChatHistory GetOrCreateSession(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var existing))
            return existing;

        var systemPrompt = _config.GetValue<string>("ChatBot:SystemPrompt",
            "Kamu adalah Bang Jenggo, asisten virtual SPBU Mini yang ramah dan informatif. " +
            "Kamu bisa membantu pelanggan dengan informasi produk BBM, harga, promo, " +
            "program loyalitas, dan layanan SPBU lainnya. Gunakan bahasa Indonesia yang santai dan bersahabat.");

        var history = new ChatHistory(systemPrompt);
        _sessions[sessionId] = history;
        return history;
    }

    /// <summary>
    /// Build OpenAIPromptExecutionSettings with auto function invocation enabled.
    /// 🔑 FunctionChoiceBehavior.Auto() memungkinkan AI otomatis memanggil
    /// kernel functions (get_fuel_prices, get_daily_sales, dll) tanpa orchestrator manual.
    /// </summary>
    private OpenAIPromptExecutionSettings CreateExecutionSettings()
    {
        var temperature = _config.GetValue<double>("ChatBot:Temperature", 0.7);

        return new OpenAIPromptExecutionSettings
        {
            Temperature = temperature,
            MaxTokens = 2000,

            // 🔑 Auto-invoke kernel functions
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        };
    }

    /// <summary>
    /// Send a message and get AI response (non-streaming).
    /// </summary>
    public async Task<ChatResponse> SendMessageAsync(string sessionId, string message,
        string? imageUrl = null, string? documentUrl = null)
    {
        if (_chatService == null || _kernel == null)
            return new ChatResponse { Success = false, Text = "⚠️ ChatBot belum dikonfigurasi. Silakan set API key di appsettings.json." };

        try
        {
            var history = GetOrCreateSession(sessionId);

            var builder = new StringBuilder(message);
            if (!string.IsNullOrEmpty(imageUrl))
                builder.Append($"\n[Gambar terlampir: {imageUrl}]");
            if (!string.IsNullOrEmpty(documentUrl))
                builder.Append($"\n[Dokumen terlampir: {documentUrl}]");

            history.AddUserMessage(builder.ToString());

            // 🔑 Settings with FunctionChoiceBehavior.Auto()
            var settings = CreateExecutionSettings();

            var response = await _chatService.GetChatMessageContentAsync(history, settings, _kernel);

            history.AddAssistantMessage(response.Content ?? "Maaf, saya tidak bisa merespon saat ini.");

            return new ChatResponse
            {
                Success = true,
                Text = response.Content ?? string.Empty,
                TokensUsed = response.Metadata?.ContainsKey("Usage") == true ? 0 : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ChatBot error");
            return new ChatResponse { Success = false, Text = $"⚠️ Error: {ex.Message}" };
        }
    }

    /// <summary>
    /// Send a message with streaming response.
    /// </summary>
    public async IAsyncEnumerable<string> SendMessageStreamingAsync(string sessionId, string message,
        string? imageUrl = null, string? documentUrl = null)
    {
        if (_chatService == null || _kernel == null)
        {
            yield return "⚠️ ChatBot belum dikonfigurasi.";
            yield break;
        }

        var history = GetOrCreateSession(sessionId);

        var builder = new StringBuilder(message);
        if (!string.IsNullOrEmpty(imageUrl))
            builder.Append($"\n[Gambar terlampir: {imageUrl}]");
        if (!string.IsNullOrEmpty(documentUrl))
            builder.Append($"\n[Dokumen terlampir: {documentUrl}]");

        history.AddUserMessage(builder.ToString());

        // 🔑 Settings with FunctionChoiceBehavior.Auto()
        var settings = CreateExecutionSettings();

        var fullResponse = new StringBuilder();
        await foreach (var chunk in _chatService.GetStreamingChatMessageContentsAsync(history, settings, _kernel))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                fullResponse.Append(chunk.Content);
                yield return chunk.Content;
            }
        }

        history.AddAssistantMessage(fullResponse.ToString());
    }

    public void ResetSession(string sessionId) => _sessions.TryRemove(sessionId, out _);
    public List<string> GetActiveSessions() => _sessions.Keys.ToList();
}

/// <summary>
/// Response model for chat
/// </summary>
public class ChatResponse
{
    public bool Success { get; set; }
    public string Text { get; set; } = string.Empty;
    public int? TokensUsed { get; set; }
}

/// <summary>
/// Kernel functions accessible by the AI to query database and external services.
/// These are auto-discovered and callable by Bang Jenggo via FunctionChoiceBehavior.Auto().
/// </summary>
public class ChatBotFunctions
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient = new();

    public ChatBotFunctions(IServiceScopeFactory scopeFactory, IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _config = config;
    }

    [KernelFunction("get_current_time")]
    [Description("Get the current date and time in Jakarta (UTC+7)")]
    public string GetCurrentTime()
    {
        var jakartaTime = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "SE Asia Standard Time");
        return jakartaTime.ToString("dddd, dd MMMM yyyy HH:mm:ss WIB");
    }

    [KernelFunction("get_fuel_prices")]
    [Description("Get all current fuel product prices")]
    public async Task<string> GetFuelPrices()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var products = await db.FuelProducts
            .Where(p => p.IsActive)
            .Select(p => new { p.Name, p.PricePerLiter, p.OctaneRating, p.FuelType })
            .ToListAsync();

        return JsonSerializer.Serialize(products);
    }

    [KernelFunction("get_station_info")]
    [Description("Get information about all fuel stations")]
    public async Task<string> GetStationInfo()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var stations = await db.FuelStations
            .Where(s => s.IsActive)
            .Select(s => new { s.Name, s.Address, s.Phone })
            .ToListAsync();

        return JsonSerializer.Serialize(stations);
    }

    [KernelFunction("get_customer_loyalty")]
    [Description("Get loyalty points for a customer by phone number")]
    public async Task<string> GetCustomerLoyalty(string phoneNumber)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var customer = await db.Customers
            .Where(c => c.Phone != null && c.Phone.Contains(phoneNumber))
            .Select(c => new { c.Name, c.MembershipTier, c.LoyaltyPoints, c.TotalSpent, c.VisitCount })
            .FirstOrDefaultAsync();

        return customer != null ? JsonSerializer.Serialize(customer) : "Pelanggan tidak ditemukan.";
    }

    [KernelFunction("get_daily_sales")]
    [Description("Get today's sales summary")]
    public async Task<string> GetDailySales()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var sales = await db.Transactions
            .Where(t => t.TransactionDate >= today && t.TransactionDate < tomorrow && t.Status == "Completed")
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalTransactions = g.Count(),
                TotalRevenue = g.Sum(t => t.GrandTotal),
                TotalLiters = g.Sum(t => t.TransactionDetails.Sum(d => d.Liters))
            })
            .FirstOrDefaultAsync();

        return JsonSerializer.Serialize(sales ?? new { TotalTransactions = 0, TotalRevenue = 0m, TotalLiters = 0m });
    }

    [KernelFunction("get_tank_status")]
    [Description("Get tank status and capacity information")]
    public async Task<string> GetTankStatus()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tanks = await db.Tanks
            .Include(t => t.FuelProduct)
            .Include(t => t.FuelStation)
            .Where(t => t.IsActive)
            .Select(t => new
            {
                t.Name,
                t.TankNumber,
                ProductName = t.FuelProduct != null ? t.FuelProduct.Name : null,
                StationName = t.FuelStation != null ? t.FuelStation.Name : null,
                t.CapacityLiters,
                t.CurrentVolumeLiters,
                FillPercentage = Math.Round((double)(t.CurrentVolumeLiters / t.CapacityLiters * 100), 1),
                t.IsLeakDetected
            })
            .ToListAsync();

        return JsonSerializer.Serialize(tanks);
    }

    [KernelFunction("search_internet")]
    [Description("Search the internet for information using Tavily API")]
    public async Task<string> SearchInternet(string query)
    {
        var apiKey = _config.GetValue<string>("ChatBot:TavilyApiKey", "");
        if (string.IsNullOrEmpty(apiKey))
            return "Tavily API key not configured.";

        try
        {
            var requestBody = JsonSerializer.Serialize(new { api_key = apiKey, query, search_depth = "basic" });
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.tavily.com/search", content);
            var result = await response.Content.ReadAsStringAsync();
            return result;
        }
        catch (Exception ex)
        {
            return $"Search error: {ex.Message}";
        }
    }

    [KernelFunction("math_calculate")]
    [Description("Perform a mathematical calculation")]
    public string MathCalculate(string expression)
    {
        try
        {
            var result = new System.Data.DataTable().Compute(expression, null);
            return $"{expression} = {result}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction("scrape_webpage")]
    [Description("Scrape content from a webpage URL")]
    public async Task<string> ScrapeWebpage(string url)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(url);
            var text = System.Text.RegularExpressions.Regex.Replace(response, "<[^>]+>", " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, "\\s+", " ").Trim();
            return text.Length > 5000 ? text[..5000] + "..." : text;
        }
        catch (Exception ex)
        {
            return $"Scrape error: {ex.Message}";
        }
    }
}
