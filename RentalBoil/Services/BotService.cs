#pragma warning disable SKEXP0010
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using RentalBoil.Data;
using RentalBoil.Models;

using SdkChatHistory = Microsoft.SemanticKernel.ChatCompletion.ChatHistory;
using DbChatHistory = RentalBoil.Models.ChatHistory;

namespace RentalBoil.Services;

public class BotService
{
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly VehicleService _vehicleService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BotService> _logger;

    public string CurrentModel => $"{_config.GetValue<string>("AI:Provider")} / {GetModelName()}";

    public BotService(IConfiguration config, IServiceScopeFactory scopeFactory,
        VehicleService vehicleService, IHttpClientFactory httpClientFactory,
        ILogger<BotService> logger)
    {
        _config = config; _scopeFactory = scopeFactory;
        _vehicleService = vehicleService;
        _httpClientFactory = httpClientFactory; _logger = logger;
    }

    private string GetModelName() => _config.GetValue<string>("AI:Provider") switch
    {
        "OpenAI" => _config.GetValue<string>("AI:OpenAI:Model") ?? "gpt-4o-mini",
        "Anthropic" => _config.GetValue<string>("AI:Anthropic:Model") ?? "claude-3-haiku-20240307",
        "Gemini" => _config.GetValue<string>("AI:Gemini:Model") ?? "gemini-2.0-flash",
        "Ollama" => _config.GetValue<string>("AI:Ollama:Model") ?? "llama3.2",
        _ => "unknown"
    };

    public async Task<string> ChatAsync(string userMessage, List<DbChatHistory> history,
        string? imageUrl = null, string? documentUrl = null)
    {
        var provider = _config.GetValue<string>("AI:Provider") ?? "OpenAI";
        try
        {
            return provider switch
            {
                "Anthropic" => await ChatWithAnthropicAsync(userMessage, history, imageUrl, documentUrl),
                "Gemini" => await ChatWithGeminiAsync(userMessage, history, imageUrl, documentUrl),
                "Ollama" => await ChatWithOllamaAsync(userMessage, history, imageUrl, documentUrl),
                _ => await ChatWithOpenAIAsync(userMessage, history, imageUrl, documentUrl)
            };
        }
        catch (Exception ex) { _logger.LogError(ex, "Chat error"); return FallbackResponse(ex.Message); }
    }

    // ═══════════════════════ OPENAI (ImageContent via ChatMessageContent Items) ═══════

    private async Task<string> ChatWithOpenAIAsync(string userMessage, List<DbChatHistory> history,
        string? imageUrl, string? documentUrl)
    {
        var kernel = CreateKernel(); if (kernel == null) return FallbackResponse("Kernel failed");
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var skHistory = BuildSystemHistory();
        AppendHistory(skHistory, history);

        // Build multimodal user message
        var items = new ChatMessageContentItemCollection();
        items.Add(new TextContent(BuildFinalMessage(userMessage, null, documentUrl)));
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            try
            {
                var http = _httpClientFactory.CreateClient();
                var imgBytes = await http.GetByteArrayAsync(imageUrl);
                items.Add(new ImageContent(imgBytes, GetMimeType(imageUrl)));
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to fetch image: {Url}", imageUrl); }
        }

        skHistory.Add(new ChatMessageContent(AuthorRole.User, items));

        var response = await chatService.GetChatMessageContentAsync(skHistory, CreateExecutionSettings(), kernel);
        return response.Content ?? "No response";
    }

    // ═══════════════════════ OLLAMA ═══════════════════════

    private async Task<string> ChatWithOllamaAsync(string userMessage, List<DbChatHistory> history,
        string? imageUrl, string? documentUrl)
    {
        var endpoint = _config.GetValue<string>("AI:Ollama:Endpoint") ?? "http://localhost:11434";
        try
        {
            var kernel = CreateKernel(); if (kernel == null) return FallbackResponse("Ollama kernel failed");
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var skHistory = BuildSystemHistory();
            AppendHistory(skHistory, history);

            var items = new ChatMessageContentItemCollection();
            items.Add(new TextContent(BuildFinalMessage(userMessage, null, documentUrl)));
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                try
                {
                    var http = _httpClientFactory.CreateClient();
                    items.Add(new ImageContent(await http.GetByteArrayAsync(imageUrl), GetMimeType(imageUrl)));
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to fetch image for Ollama"); }
            }
            skHistory.Add(new ChatMessageContent(AuthorRole.User, items));

            var response = await chatService.GetChatMessageContentAsync(skHistory, CreateExecutionSettings(), kernel);
            return response.Content ?? "No response from Ollama";
        }
        catch (HttpRequestException) { return $"⚠️ Ollama server tidak tersedia di {endpoint}."; }
        catch (Exception ex) { return $"⚠️ Ollama error: {ex.Message}"; }
    }

    private Kernel? CreateKernel()
    {
        try
        {
            var provider = _config.GetValue<string>("AI:Provider") ?? "OpenAI";
            var builder = Kernel.CreateBuilder();

            switch (provider)
            {
                case "OpenAI":
                case "Ollama":
                    var model = provider == "Ollama" ? _config.GetValue<string>("AI:Ollama:Model") ?? "llama3.2" : _config.GetValue<string>("AI:OpenAI:Model") ?? "gpt-4o-mini";
                    var endpoint = provider == "Ollama" ? $"{_config.GetValue<string>("AI:Ollama:Endpoint") ?? "http://localhost:11434"}/v1" : null;
                    var apiKey = provider == "Ollama" ? "ollama" : _config.GetValue<string>("AI:OpenAI:ApiKey") ?? "";
                    if (!string.IsNullOrWhiteSpace(apiKey) || provider == "Ollama")
                    {
                        if (endpoint != null) builder.AddOpenAIChatCompletion(modelId: model, endpoint: new Uri(endpoint), apiKey: apiKey);
                        else builder.AddOpenAIChatCompletion(model, apiKey);
                    }
                    else return null;
                    break;
                default: return null;
            }
            builder.Plugins.AddFromObject(new BotKernelFunctions(_config, _scopeFactory), "RentalBoilFunctions");
            return builder.Build();
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to create kernel"); return null; }
    }

    private OpenAIPromptExecutionSettings CreateExecutionSettings() => new()
    {
        Temperature = _config.GetValue<double>("ChatBot:Temperature"),
        MaxTokens = _config.GetValue<int>("ChatBot:MaxTokens"),
        TopP = _config.GetValue<double>("ChatBot:TopP"),
        ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
    };

    // ═══════════════════════ ANTHROPIC ═══════════════════════

    private async Task<string> ChatWithAnthropicAsync(string userMessage, List<DbChatHistory> history,
        string? imageUrl, string? documentUrl)
    {
        var apiKey = _config.GetValue<string>("AI:Anthropic:ApiKey");
        if (string.IsNullOrWhiteSpace(apiKey)) return "⚠️ Anthropic API Key belum dikonfigurasi.";
        var messages = new List<object>();
        foreach (var msg in history.OrderBy(h => h.CreatedAt).TakeLast(10))
            messages.Add(new { role = msg.Role == "user" ? "user" : "assistant", content = msg.Content });

        var contentBlocks = new List<object>();
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            try
            {
                var http = _httpClientFactory.CreateClient();
                var imgBytes = await http.GetByteArrayAsync(imageUrl);
                contentBlocks.Add(new { type = "image", source = new { type = "base64", media_type = GetMimeType(imageUrl), data = Convert.ToBase64String(imgBytes) } });
            }
            catch { }
        }
        contentBlocks.Add(new { type = "text", text = BuildFinalMessage(userMessage, null, documentUrl) });
        messages.Add(new { role = "user", content = contentBlocks });

        var body = new { model = _config.GetValue<string>("AI:Anthropic:Model") ?? "claude-3-haiku-20240307", system = GetSystemPrompt(), messages, max_tokens = _config.GetValue<int>("ChatBot:MaxTokens"), temperature = _config.GetValue<double>("ChatBot:Temperature") };
        var http2 = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages") { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
        request.Headers.Add("x-api-key", apiKey); request.Headers.Add("anthropic-version", "2023-06-01");
        var response = await http2.SendAsync(request); var responseJson = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) return $"⚠️ Anthropic error ({response.StatusCode})";
        using var doc = JsonDocument.Parse(responseJson);
        return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "No response";
    }

    // ═══════════════════════ GEMINI ═══════════════════════

    private async Task<string> ChatWithGeminiAsync(string userMessage, List<DbChatHistory> history,
        string? imageUrl, string? documentUrl)
    {
        var apiKey = _config.GetValue<string>("AI:Gemini:ApiKey");
        if (string.IsNullOrWhiteSpace(apiKey)) return "⚠️ Gemini API Key belum dikonfigurasi.";
        var contents = new List<object> { new { role = "user", parts = new[] { new { text = GetSystemPrompt() } } }, new { role = "model", parts = new[] { new { text = "Baik, siap membantu!" } } } };
        foreach (var msg in history.OrderBy(h => h.CreatedAt).TakeLast(10)) contents.Add(new { role = msg.Role == "user" ? "user" : "model", parts = new[] { new { text = msg.Content } } });

        var parts = new List<object>();
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            try
            {
                var http = _httpClientFactory.CreateClient();
                var imgBytes = await http.GetByteArrayAsync(imageUrl);
                parts.Add(new { inline_data = new { mime_type = GetMimeType(imageUrl), data = Convert.ToBase64String(imgBytes) } });
            }
            catch { }
        }
        parts.Add(new { text = BuildFinalMessage(userMessage, null, documentUrl) });
        contents.Add(new { role = "user", parts });

        var body = new { contents, generationConfig = new { temperature = _config.GetValue<double>("ChatBot:Temperature"), maxOutputTokens = _config.GetValue<int>("ChatBot:MaxTokens"), topP = _config.GetValue<double>("ChatBot:TopP") } };
        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var http3 = _httpClientFactory.CreateClient();
        var resp = await http3.PostAsync($"https://generativelanguage.googleapis.com/v1beta/models/{_config.GetValue<string>("AI:Gemini:Model") ?? "gemini-2.0-flash"}:generateContent?key={apiKey}", new StringContent(json, Encoding.UTF8, "application/json"));
        var respJson = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) return $"⚠️ Gemini error ({resp.StatusCode})";
        using var d = JsonDocument.Parse(respJson);
        return d.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "No response";
    }

    // ═══════════════════════ HELPERS ═══════════════════════

    private SdkChatHistory BuildSystemHistory() { var h = new SdkChatHistory(); h.AddSystemMessage(GetSystemPrompt()); return h; }
    private string GetSystemPrompt() => _config.GetValue<string>("ChatBot:SystemPrompt") ?? "Kamu adalah Bang Tony Brewok, asisten virtual RentalBoil.";
    private void AppendHistory(SdkChatHistory skHistory, List<DbChatHistory> history) { foreach (var msg in history.OrderBy(h => h.CreatedAt).TakeLast(10)) { if (msg.Role == "user") skHistory.AddUserMessage(msg.Content); else if (msg.Role == "assistant") skHistory.AddAssistantMessage(msg.Content); } }
    private string BuildFinalMessage(string msg, string? imgUrl, string? docUrl) { var r = msg; if (!string.IsNullOrWhiteSpace(imgUrl)) r += $"\n\n[Gambar: {imgUrl}]"; if (!string.IsNullOrWhiteSpace(docUrl)) r += $"\n\n[Dokumen: {docUrl}]"; return r; }
    private string FallbackResponse(string error) => $"⚠️ Maaf, Bang Tony Brewok sedang error.\n\nDetail: {error}\n\n📞 CS 0800-1234-5678";

    private static string GetMimeType(string url) => Path.GetExtension(url)?.ToLowerInvariant() switch { ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".gif" => "image/gif", ".webp" => "image/webp", ".bmp" => "image/bmp", _ => "image/jpeg" };

    public async Task<string> GetVehicleInfoForChatAsync(int vehicleId)
    {
        var v = await _vehicleService.GetVehicleByIdAsync(vehicleId);
        return v == null ? "Kendaraan tidak ditemukan." : $"**{v.Name}** ({v.Year})\n{v.Brand} | {v.Transmission} | {v.Capacity} orang\nRp {v.PricePerDay:N0}/hari | ⭐ {v.AverageRating}/5\n📍 {v.Location}";
    }
}
