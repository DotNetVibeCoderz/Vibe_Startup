// ChatBot Service using Semantic Kernel
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Joka.Models.Chat;
using Joka.Data;

namespace Joka.Services.Chat;

public class ChatBotService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ChatBotService> _logger;
    private readonly AppDbContext _db;
    private Kernel? _kernel;
    private IChatCompletionService? _chatService;
    private ChatHistory? _chatHistory;
    private string _currentProvider = "OpenAI";
    private string _modelId = "gpt-4o-mini";

    public ChatBotService(
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<ChatBotService> logger,
        AppDbContext db)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _db = db;
    }

    public async Task InitializeAsync(string? provider = null, string? model = null)
    {
        _currentProvider = provider ?? _config["ChatBot:Provider"] ?? "OpenAI";
        var builder = Kernel.CreateBuilder();

        // Build the plugins here rather than with AddFromType<T>(): the kernel has
        // its own service container, which does not know about IHttpClientFactory
        // or AppDbContext, so type-based registration cannot construct them.
        builder.Plugins.AddFromObject(new ChatKernelFunctions(_httpClientFactory, _config), "utilitas");
        builder.Plugins.AddFromObject(new JokaDataFunctions(_db), "joka");

        // Each provider gets its own connector. Routing Anthropic and Gemini
        // through AddOpenAIChatCompletion used to "work" only against an
        // OpenAI-compatible proxy; against the real endpoints it fails, and
        // function calling never worked at all because the tool payloads differ.
        switch (_currentProvider)
        {
            case "Anthropic":
                var anthropicKey = _config["ChatBot:Providers:Anthropic:ApiKey"];
                var anthropicModel = model ?? _config["ChatBot:Providers:Anthropic:Model"] ?? "claude-sonnet-4-5";

                if (string.IsNullOrWhiteSpace(anthropicKey))
                    throw new InvalidOperationException(
                        "ChatBot:Providers:Anthropic:ApiKey belum diisi. Isi lewat user-secrets atau environment variable.");

                // Anthropic.SDK's Messages endpoint implements IChatClient, and SK
                // adapts any IChatClient into an IChatCompletionService - including
                // tool calling, which is what makes the Joka functions reachable.
                var anthropicClient = new Anthropic.SDK.AnthropicClient(anthropicKey);

                builder.Services.AddKeyedSingleton<IChatCompletionService>(
                    serviceKey: null,
                    (sp, _) =>
                    {
                        var pipeline = new Microsoft.Extensions.AI.ChatClientBuilder(anthropicClient.Messages);

                        // Called statically rather than fluently: importing the
                        // Microsoft.Extensions.AI namespace here would collide with
                        // Joka.Models.Chat.ChatResponse used below.
                        pipeline = Microsoft.Extensions.AI.FunctionInvokingChatClientBuilderExtensions
                            .UseFunctionInvocation(pipeline, null, null);

                        return pipeline.Build().AsChatCompletionService(sp);
                    });

                _modelId = anthropicModel;
                break;

            case "Gemini":
                var geminiKey = _config["ChatBot:Providers:Gemini:ApiKey"];
                var geminiModel = model ?? _config["ChatBot:Providers:Gemini:Model"] ?? "gemini-2.0-flash";

                if (string.IsNullOrWhiteSpace(geminiKey))
                    throw new InvalidOperationException(
                        "ChatBot:Providers:Gemini:ApiKey belum diisi. Isi lewat user-secrets atau environment variable.");

                builder.AddGoogleAIGeminiChatCompletion(geminiModel, geminiKey);
                _modelId = geminiModel;
                break;

            case "Ollama":
                // Ollama genuinely serves an OpenAI-compatible API, but it lives
                // under /v1 - pointing at the bare host 404s on every request.
                var ollamaEndpoint = _config["ChatBot:Providers:Ollama:Endpoint"] ?? "http://localhost:11434";
                var ollamaModel = model ?? _config["ChatBot:Providers:Ollama:Model"] ?? "llama3.2";

                if (!ollamaEndpoint.TrimEnd('/').EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                    ollamaEndpoint = ollamaEndpoint.TrimEnd('/') + "/v1";

                builder.AddOpenAIChatCompletion(ollamaModel, new Uri(ollamaEndpoint), apiKey: "ollama");
                _modelId = ollamaModel;
                break;

            case "OpenAI":
            default:
                var openAiKey = _config["ChatBot:Providers:OpenAI:ApiKey"];
                var openAiModel = model ?? _config["ChatBot:Providers:OpenAI:Model"] ?? "gpt-4o-mini";

                if (string.IsNullOrWhiteSpace(openAiKey))
                    throw new InvalidOperationException(
                        "ChatBot:Providers:OpenAI:ApiKey belum diisi. Isi lewat user-secrets atau environment variable.");

                builder.AddOpenAIChatCompletion(openAiModel, openAiKey);
                _modelId = openAiModel;
                break;
        }

        _kernel = builder.Build();
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();

        var systemPrompt = _config["ChatBot:SystemPrompt"] ?? "Kamu adalah Mas Bolang, asisten virtual Joka.";
        _chatHistory = new ChatHistory(systemPrompt);
    }

    /// <summary>
    /// Execution settings differ per connector, and getting this wrong is silent:
    /// the functions register fine but the model is never told they exist.
    /// OpenAI uses ToolCallBehavior, Gemini uses GeminiToolCallBehavior, and the
    /// IChatClient-backed Anthropic path uses FunctionChoiceBehavior.
    /// </summary>
    private PromptExecutionSettings BuildSettings()
    {
        // InvariantCulture is required: appsettings always writes "0.7", but the
        // app runs under id-ID where "." is a thousands separator, so a
        // culture-sensitive parse turns 0.7 into 7 and the API rejects it.
        var temperature = double.TryParse(_config["ChatBot:Temperature"],
            NumberStyles.Float, CultureInfo.InvariantCulture, out var t) ? t : 0.7;

        var maxTokens = int.TryParse(_config["ChatBot:MaxTokens"],
            NumberStyles.Integer, CultureInfo.InvariantCulture, out var mt) ? mt : 4096;

        return _currentProvider switch
        {
            "Gemini" => new GeminiPromptExecutionSettings
            {
                ModelId = _modelId,
                Temperature = temperature,
                MaxTokens = maxTokens,
                ToolCallBehavior = GeminiToolCallBehavior.AutoInvokeKernelFunctions
            },

            "Anthropic" => new PromptExecutionSettings
            {
                ModelId = _modelId,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                ExtensionData = new Dictionary<string, object>
                {
                    ["temperature"] = temperature,
                    ["max_tokens"] = maxTokens
                }
            },

            // OpenAI and Ollama both speak the OpenAI protocol.
            _ => new OpenAIPromptExecutionSettings
            {
                ModelId = _modelId,
                Temperature = temperature,
                MaxTokens = maxTokens,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            }
        };
    }

    public async Task<ChatResponse> SendMessageAsync(string message, List<string>? attachmentUrls = null)
    {
        try
        {
            if (_chatHistory == null || _chatService == null)
                await InitializeAsync();
        }
        catch (Exception ex)
        {
            // A missing key now fails loudly inside InitializeAsync instead of
            // sending "sk-placeholder" and getting an opaque 401 back. Surface it
            // as a message rather than a 500 on the chat API.
            _logger.LogWarning(ex, "Provider {Provider} tidak bisa diinisialisasi", _currentProvider);

            return new ChatResponse
            {
                Message = $"Provider {_currentProvider} belum siap: {ex.Message}",
                TokenCount = 0,
                Timestamp = DateTime.UtcNow
            };
        }

        var userMessage = new StringBuilder(message);
        if (attachmentUrls?.Any() == true)
        {
            userMessage.AppendLine("\n\n[Lampiran:]");
            foreach (var url in attachmentUrls)
            {
                userMessage.AppendLine($"- {url}");
            }
        }

        _chatHistory!.AddUserMessage(userMessage.ToString());

        var settings = BuildSettings();

        try
        {
            var response = await _chatService!.GetChatMessageContentAsync(_chatHistory, settings, _kernel);
            _chatHistory.AddAssistantMessage(response.Content ?? "Maaf, saya tidak bisa memberikan respons saat ini.");

            return new ChatResponse
            {
                Message = response.Content ?? "Maaf, terjadi kesalahan.",
                TokenCount = 0,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chat error with provider {Provider}", _currentProvider);
            return new ChatResponse
            {
                Message = $"Maaf, layanan AI sedang tidak tersedia. Silakan coba lagi nanti atau ganti provider. (Error: {ex.Message})",
                TokenCount = 0,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    public void ResetSession()
    {
        _chatHistory = null;
    }

    public ChatBotConfig GetConfig()
    {
        return _config.GetSection("ChatBot").Get<ChatBotConfig>() ?? new ChatBotConfig();
    }
}

/// <summary>
/// Kernel Functions available for Mas Bolang chatbot.
/// Provides internet search, date/time, calculations, and database queries.
/// </summary>
public class ChatKernelFunctions
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public ChatKernelFunctions(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    [KernelFunction("search_internet")]
    [Description("Search the internet using Tavily for latest information")]
    public async Task<string> SearchInternet(
        [Description("Search query")] string query,
        [Description("Max results (default 5)")] int maxResults = 5)
    {
        var tavilyKey = _config["ChatBot:Tavily:ApiKey"];
        if (string.IsNullOrEmpty(tavilyKey))
            return "Maaf, layanan pencarian internet belum dikonfigurasi. Silakan set ChatBot:Tavily:ApiKey di appsettings.json.";

        var client = _httpClientFactory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.tavily.com/search")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    query,
                    max_results = maxResults,
                    search_depth = "basic",
                    include_answer = true
                }),
                Encoding.UTF8, "application/json")
        };

        // The key has to actually travel with the request - reading it from config
        // and then not sending it makes every search fail with 401.
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tavilyKey);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return $"Pencarian internet gagal (HTTP {(int)response.StatusCode}). {Truncate(body, 200)}";

        return SummariseTavily(body);
    }

    /// <summary>
    /// Tavily returns a large payload with scores and raw content. Feeding all of
    /// it back costs tokens and buries the answer, so keep the answer plus the
    /// title/url/snippet of each result.
    /// </summary>
    private static string SummariseTavily(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var text = new StringBuilder();

            if (root.TryGetProperty("answer", out var answer) && answer.ValueKind == JsonValueKind.String)
                text.AppendLine($"Ringkasan: {answer.GetString()}\n");

            if (root.TryGetProperty("results", out var results))
            {
                foreach (var r in results.EnumerateArray())
                {
                    var title = r.TryGetProperty("title", out var t) ? t.GetString() : "(tanpa judul)";
                    var url = r.TryGetProperty("url", out var u) ? u.GetString() : "";
                    var snippet = r.TryGetProperty("content", out var c) ? c.GetString() : "";

                    text.AppendLine($"- **{title}** ({url})");
                    text.AppendLine($"  {Truncate(snippet, 400)}");
                }
            }

            var output = text.ToString();
            return string.IsNullOrWhiteSpace(output) ? "Pencarian tidak mengembalikan hasil." : output;
        }
        catch
        {
            return Truncate(json, 4000);
        }
    }

    private static string Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? string.Empty
        : value.Length <= max ? value
        : value[..max] + "...";

    [KernelFunction("scrape_webpage")]
    [Description("Scrape content from a webpage URL")]
    public async Task<string> ScrapeWebpage(
        [Description("URL to scrape")] string url)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetStringAsync(url);
            return response.Length > 10000 ? response[..10000] + "..." : response;
        }
        catch (Exception ex)
        {
            return $"Gagal mengambil konten: {ex.Message}";
        }
    }

    [KernelFunction("get_current_time")]
    [Description("Get current date and time")]
    public string GetCurrentTime(
        [Description("Timezone (default: Asia/Jakarta)")] string timezone = "Asia/Jakarta")
    {
        try
        {
            var now = DateTime.UtcNow;
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(now, tz);
            return $"Waktu saat ini: {localTime:dddd, dd MMMM yyyy HH:mm:ss} ({timezone})";
        }
        catch
        {
            return $"Waktu UTC: {DateTime.UtcNow:dddd, dd MMMM yyyy HH:mm:ss}";
        }
    }

    [KernelFunction("calculate_math")]
    [Description("Perform mathematical calculations")]
    public string CalculateMath(
        [Description("Mathematical expression")] string expression)
    {
        try
        {
            var result = new System.Data.DataTable().Compute(expression, null);
            return $"Hasil: {expression} = {result}";
        }
        catch (Exception ex)
        {
            return $"Gagal menghitung: {ex.Message}";
        }
    }

    [KernelFunction("read_file_from_url")]
    [Description("Read and extract text content from a file URL")]
    public async Task<string> ReadFileFromUrl(
        [Description("File URL")] string url)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var bytes = await client.GetByteArrayAsync(url);
            var text = Encoding.UTF8.GetString(bytes);
            return text.Length > 5000 ? text[..5000] + "\n... (dipotong)" : text;
        }
        catch (Exception ex)
        {
            return $"Gagal membaca file: {ex.Message}";
        }
    }

    [KernelFunction("get_date_info")]
    [Description("Get detailed date information")]
    public string GetDateInfo(
        [Description("Date in yyyy-MM-dd format, or 'today'")] string dateInput = "today")
    {
        var date = dateInput.Equals("today", StringComparison.OrdinalIgnoreCase)
            ? DateTime.Now
            : DateTime.Parse(dateInput);

        return $"Tanggal: {date:dd MMMM yyyy}, Hari: {date:dddd}, Minggu ke-{System.Globalization.CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday)}, Kuartal: {(date.Month + 2) / 3}";
    }

    [KernelFunction("convert_currency")]
    [Description("Convert currency amounts (simulated rates)")]
    public string ConvertCurrency(
        [Description("Amount")] decimal amount,
        [Description("From currency (e.g., IDR)")] string fromCurrency,
        [Description("To currency (e.g., USD)")] string toCurrency)
    {
        var rates = new Dictionary<string, decimal>
        {
            ["IDR"] = 1, ["USD"] = 15500m, ["SGD"] = 11500m,
            ["MYR"] = 3300m, ["AUD"] = 10200m, ["EUR"] = 16800m,
            ["JPY"] = 105m, ["GBP"] = 19600m
        };

        if (rates.TryGetValue(fromCurrency.ToUpper(), out var fromRate) &&
            rates.TryGetValue(toCurrency.ToUpper(), out var toRate))
        {
            var converted = amount * fromRate / toRate;
            return $"{amount:N2} {fromCurrency.ToUpper()} = {converted:N2} {toCurrency.ToUpper()} (kurs simulasi)";
        }
        return "Mata uang tidak didukung.";
    }
}
