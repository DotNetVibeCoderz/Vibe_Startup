using System.ComponentModel;
using System.Globalization;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using WashUp.Data;
using WashUp.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace WashUp.Services;

/// <summary>
/// Chatbot "Mbok Inem" berbasis Semantic Kernel.
/// Semua provider (OpenAI, Anthropic, Gemini, Ollama) diakses lewat
/// endpoint OpenAI-compatible masing-masing.
/// </summary>
public class ChatBotService
{
    private readonly IConfiguration _config;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private Kernel? _kernel;
    private IChatCompletionService? _chatService;
    private string _currentProvider = "";
    private string _currentModel = "";

    public ChatBotService(IConfiguration config, IServiceProvider serviceProvider, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
    }

    public void Initialize(string? provider = null, string? model = null)
    {
        provider ??= _config["AI:Provider"] ?? "OpenAI";
        model ??= GetDefaultModel(provider);

        if (_currentProvider == provider && _currentModel == model && _kernel != null)
            return;

        var builder = Kernel.CreateBuilder();
        var configured = false;

        switch (provider)
        {
            case "OpenAI":
                var apiKey = _config["AI:OpenAI:ApiKey"];
                if (!string.IsNullOrEmpty(apiKey))
                {
                    builder.AddOpenAIChatCompletion(model!, apiKey);
                    configured = true;
                }
                break;
            case "Anthropic":
                var antKey = _config["AI:Anthropic:ApiKey"];
                if (!string.IsNullOrEmpty(antKey))
                {
                    builder.AddOpenAIChatCompletion(model!, new Uri("https://api.anthropic.com/v1/"), antKey);
                    configured = true;
                }
                break;
            case "Gemini":
                var geminiKey = _config["AI:Gemini:ApiKey"];
                if (!string.IsNullOrEmpty(geminiKey))
                {
                    builder.AddOpenAIChatCompletion(model!, new Uri("https://generativelanguage.googleapis.com/v1beta/openai/"), geminiKey);
                    configured = true;
                }
                break;
            case "Ollama":
                var endpoint = _config["AI:Ollama:Endpoint"] ?? "http://localhost:11434";
                builder.AddOpenAIChatCompletion(model!, new Uri(endpoint.TrimEnd('/') + "/v1/"), "ollama");
                configured = true;
                break;
        }

        if (!configured)
        {
            _kernel = null;
            _chatService = null;
            _currentProvider = provider;
            _currentModel = model!;
            return;
        }

        // KernelFunctions butuh dependency dari app container, bukan kernel container
        builder.Plugins.AddFromObject(new KernelFunctions(_httpClientFactory, _serviceProvider), "washup");
        _kernel = builder.Build();
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();
        _currentProvider = provider;
        _currentModel = model!;
    }

    /// <param name="history">Riwayat percakapan SEBELUM pesan saat ini (pesan terakhir jangan disertakan).</param>
    public async Task<string> GetResponseAsync(string userMessage, List<ChatMessage> history,
        string? imageUrl = null, string? documentUrl = null, string? documentName = null, string? provider = null)
    {
        Initialize(provider);
        if (_kernel == null || _chatService == null)
            return "Maaf Cah Bagus/Cah Ayu, Mbok Inem lagi ngantuk (API key AI belum dikonfigurasi). Isi `AI:" + (provider ?? _config["AI:Provider"] ?? "OpenAI") + ":ApiKey` di appsettings.json ya! 😴";

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = ParseDouble(_config["ChatBot:Temperature"], 0.8),
            MaxTokens = (int)ParseDouble(_config["ChatBot:MaxTokens"], 2000),
            TopP = ParseDouble(_config["ChatBot:TopP"], 0.9),
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var systemPrompt = _config["ChatBot:SystemPrompt"] ?? "Kamu adalah Mbok Inem, asisten virtual laundry yang ramah.";
        var chatHistory = new ChatHistory(systemPrompt);

        foreach (var msg in history.TakeLast(20))
        {
            if (msg.Role == "user") chatHistory.AddUserMessage(msg.Content);
            else if (msg.Role == "assistant") chatHistory.AddAssistantMessage(msg.Content);
        }

        var finalMessage = userMessage;
        if (!string.IsNullOrEmpty(documentUrl))
            finalMessage = "[Dokumen terlampir: " + (documentName ?? "Dokumen") + " - " + documentUrl + "]\n\n" + userMessage;

        if (!string.IsNullOrEmpty(imageUrl))
        {
            var content = new ChatMessageContentItemCollection
            {
                new TextContent(finalMessage),
                new ImageContent(new Uri(imageUrl, UriKind.RelativeOrAbsolute))
            };
            chatHistory.Add(new ChatMessageContent(AuthorRole.User, content));
        }
        else
        {
            chatHistory.AddUserMessage(finalMessage);
        }

        try
        {
            var response = await _chatService.GetChatMessageContentAsync(chatHistory, settings, _kernel);
            return response.Content ?? "Mbok Inem bingung, coba tanya lagi ya...";
        }
        catch
        {
            return GenerateFallbackResponse(userMessage);
        }
    }

    private static double ParseDouble(string? value, double fallback) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : fallback;

    private string GenerateFallbackResponse(string message)
    {
        var msg = message.ToLower();
        var prefix = "⚠️ *Koneksi AI sedang bermasalah, ini jawaban singkat dari Mbok Inem:*\n\n";
        if (msg.Contains("harga")) return prefix + "🍃 Harga WashUp: Cuci Kering Rp 8.000/kg | Setrika Rp 6.000/kg | Express Rp 12.000/kg | Kiloan Rp 7.000/kg. Gratis antar-jemput min. 5kg!";
        if (msg.Contains("status") || msg.Contains("order")) return prefix + "Cek status order di menu **Order** atau berikan nomor order (WO-YYYYMMDD-XXXX).";
        if (msg.Contains("promo") || msg.Contains("diskon")) return prefix + "🎉 Promo: Diskon 20% member baru (NEW20) | Gratis 1kg order ke-5 | Paket Bulanan Rp 150.000/bulan";
        if (msg.Contains("cabang")) return prefix + "📍 Cabang: Jakarta (Sudirman 123), Bandung (Dago 45), Surabaya (Tunjungan 78). Buka Senin-Sabtu 07-21.";
        return prefix + "👋 Halo! Aku Mbok Inem, asisten WashUp. Tanyakan harga, status order, promo, lokasi, atau tips laundry ya!";
    }

    private string GetDefaultModel(string provider) => provider switch
    {
        "OpenAI" => _config["AI:OpenAI:Model"] ?? "gpt-4o",
        "Anthropic" => _config["AI:Anthropic:Model"] ?? "claude-sonnet-4-5",
        "Gemini" => _config["AI:Gemini:Model"] ?? "gemini-2.0-flash",
        "Ollama" => _config["AI:Ollama:Model"] ?? "llama3.2",
        _ => "gpt-4o"
    };
}

public class KernelFunctions
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceProvider _serviceProvider;

    public KernelFunctions(IHttpClientFactory httpClientFactory, IServiceProvider serviceProvider)
    {
        _httpClientFactory = httpClientFactory;
        _serviceProvider = serviceProvider;
    }

    [KernelFunction, Description("Search the internet using Tavily")]
    public async Task<string> SearchInternet([Description("Search query")] string query)
    {
        try
        {
            var config = _serviceProvider.GetRequiredService<IConfiguration>();
            var apiKey = config["Tavily:ApiKey"];
            if (string.IsNullOrEmpty(apiKey)) return "Tavily API key not configured.";
            var client = _httpClientFactory.CreateClient();
            var content = new StringContent(JsonSerializer.Serialize(new { query, api_key = apiKey, search_depth = "basic" }), System.Text.Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://api.tavily.com/search", content);
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex) { return "Search error: " + ex.Message; }
    }

    [KernelFunction, Description("Scrape text content from a web page URL")]
    public async Task<string> ScrapeUrl([Description("URL")] string url)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", "WashUp-Bot/1.0");
            var html = await client.GetStringAsync(url);
            var text = System.Text.RegularExpressions.Regex.Replace(html, "<script[^>]*>[\\s\\S]*?</script>|<style[^>]*>[\\s\\S]*?</style>", " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
            return text.Length > 5000 ? text[..5000] + "..." : text;
        }
        catch (Exception ex) { return "Scrape error: " + ex.Message; }
    }

    [KernelFunction, Description("Get current date and time in WIB (Jakarta)")]
    public string GetDateTime([Description("Format: full, date, time, day")] string format = "full")
    {
        var now = DateTime.UtcNow.AddHours(7);
        return format.ToLower() switch { "date" => now.ToString("dd MMMM yyyy"), "time" => now.ToString("HH:mm:ss"), "day" => now.ToString("dddd"), _ => now.ToString("dddd, dd MMMM yyyy HH:mm:ss") + " WIB" };
    }

    [KernelFunction, Description("Calculate math expression, e.g. (5*8000)*0.9")]
    public string Calculate([Description("Expression")] string expression)
    {
        try { return expression + " = " + new System.Data.DataTable().Compute(expression, null); }
        catch (Exception ex) { return "Error: " + ex.Message; }
    }

    [KernelFunction, Description("Query laundry orders from database by order number or customer name")]
    public async Task<string> QueryOrders([Description("Order number or customer name")] string query)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var orders = await db.Orders.AsNoTracking().Include(o => o.User)
                .Where(o => o.OrderNumber.Contains(query) || (o.User != null && o.User.FullName.Contains(query)))
                .OrderByDescending(o => o.CreatedAt).Take(5).ToListAsync();
            if (!orders.Any()) return "Tidak ditemukan.";
            return string.Join("\n", orders.Select(o => o.OrderNumber + " | " + (o.User?.FullName ?? "") + " | " + o.ServiceType + " | " + o.Status + " | Rp" + o.TotalAmount.ToString("N0")));
        }
        catch (Exception ex) { return "DB error: " + ex.Message; }
    }

    [KernelFunction, Description("Query customers by name or phone")]
    public async Task<string> QueryCustomers([Description("Name or phone")] string query)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var c = await db.Users.AsNoTracking()
                .Where(u => u.FullName.Contains(query) || (u.PhoneNumber != null && u.PhoneNumber.Contains(query)))
                .Take(5).ToListAsync();
            if (!c.Any()) return "Tidak ditemukan.";
            return string.Join("\n", c.Select(x => x.FullName + " | " + x.PhoneNumber + " | " + x.MembershipTier));
        }
        catch (Exception ex) { return "DB error: " + ex.Message; }
    }

    [KernelFunction, Description("Get business summary: total orders, revenue, expense this month")]
    public async Task<string> GetBusinessSummary()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var thisMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            var totalOrders = await db.Orders.CountAsync();
            var monthOrders = await db.Orders.CountAsync(o => o.CreatedAt >= thisMonth);
            var income = await db.FinancialTransactions.Where(t => t.TransactionType == "Income" && t.TransactionDate >= thisMonth).SumAsync(t => t.Amount);
            var expense = await db.FinancialTransactions.Where(t => t.TransactionType == "Expense" && t.TransactionDate >= thisMonth).SumAsync(t => t.Amount);
            return $"Total order: {totalOrders} (bulan ini: {monthOrders}) | Pemasukan bulan ini: Rp{income:N0} | Pengeluaran: Rp{expense:N0} | Profit: Rp{income - expense:N0}";
        }
        catch (Exception ex) { return "DB error: " + ex.Message; }
    }

    [KernelFunction, Description("Read text file content from URL")]
    public async Task<string> ReadFileFromUrl([Description("File URL")] string fileUrl)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var c = await client.GetStringAsync(fileUrl);
            return c.Length > 5000 ? c[..5000] + "..." : c;
        }
        catch (Exception ex) { return "Error: " + ex.Message; }
    }

    [KernelFunction, Description("Get laundry service pricing")]
    public string GetPricing() => "Cuci Kering: Rp 8.000/kg | Setrika: Rp 6.000/kg | Express: Rp 12.000/kg | Kiloan: Rp 7.000/kg | Cuci Lipat: Rp 8.000/kg";

    [KernelFunction, Description("Get branch locations")]
    public async Task<string> GetBranches()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var b = await db.Branches.AsNoTracking().Where(x => x.IsActive).ToListAsync();
            return string.Join("\n", b.Select(x => x.Name + " | " + x.Address));
        }
        catch { return "Error fetching branches."; }
    }
}
