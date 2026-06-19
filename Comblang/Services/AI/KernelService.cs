using Comblang.Data;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.DependencyInjection;

namespace Comblang.Services.AI;

/// <summary>
/// Manages the Semantic Kernel instance for AI-powered features.
/// Supports OpenAI, Anthropic, Gemini, and Ollama.
/// Registered as SCOPED so it can safely consume AppDbContext.
/// </summary>
public class KernelService
{
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private Kernel? _kernel;
    private IChatCompletionService? _chatService;

    public KernelService(IConfiguration config, IServiceScopeFactory scopeFactory)
    {
        _config = config;
        _scopeFactory = scopeFactory;
    }

    public Kernel BuildKernel(string? modelOverride = null)
    {
        var model = modelOverride ?? _config["SiMakComblang:Model"] ?? "OpenAI";
        var builder = Kernel.CreateBuilder();

        switch (model)
        {
            case "OpenAI": ConfigureOpenAI(builder, "OpenAI"); break;
            case "Anthropic": ConfigureOpenAI(builder, "Anthropic"); break;
            case "Gemini": ConfigureOpenAI(builder, "Gemini"); break;
            case "Ollama": ConfigureOllama(builder); break;
            default: ConfigureOpenAI(builder, "OpenAI"); break;
        }

        var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var functions = new KernelFunctions(db, _config);
        builder.Plugins.AddFromObject(functions, "ComblangFunctions");

        _kernel = builder.Build();
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();
        return _kernel;
    }

    public IChatCompletionService GetChatService(string? modelOverride = null)
    {
        if (_chatService == null || modelOverride != null) BuildKernel(modelOverride);
        return _chatService!;
    }

    public Kernel GetKernel(string? modelOverride = null)
    {
        if (_kernel == null || modelOverride != null) BuildKernel(modelOverride);
        return _kernel!;
    }

    public async Task<string> ChatSimpleAsync(
        List<(string role, string content)> history,
        string? imageUrl = null, string? documentUrl = null, string? modelOverride = null)
    {
        var messages = history.Select(h =>
            new ChatMessageContent(h.role == "user" ? AuthorRole.User : AuthorRole.Assistant, h.content)).ToList();
        return await ChatInternalAsync(messages, imageUrl, documentUrl, modelOverride);
    }

    public async Task<string> ChatAsync(
        List<ChatMessageContent> history,
        string? imageUrl = null, string? documentUrl = null, string? modelOverride = null)
    {
        return await ChatInternalAsync(history, imageUrl, documentUrl, modelOverride);
    }

    private async Task<string> ChatInternalAsync(
        List<ChatMessageContent> history, string? imageUrl, string? documentUrl, string? modelOverride)
    {
        var kernel = GetKernel(modelOverride);
        var chat = GetChatService(modelOverride);

        var systemPrompt = _config["SiMakComblang:SystemPrompt"]
            ?? "Kamu adalah Si Mak Comblang, asisten perjodohan yang ramah dan bijaksana.";
        var temperature = double.TryParse(_config["SiMakComblang:Temperature"], out var t) ? t : 0.7;
        var maxTokens = int.TryParse(_config["SiMakComblang:MaxTokens"], out var mt) ? mt : 2048;

        var chatHistory = new ChatHistory(systemPrompt);
        foreach (var msg in history) chatHistory.Add(msg);

        // 🔑 Image handling — bedakan data URI vs public URL
        if (!string.IsNullOrEmpty(imageUrl))
        {
            ImageContent imageContent;
            if (imageUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                // Data URI → gunakan string constructor (SK internally sets DataUri property)
                imageContent = new ImageContent(imageUrl);
            }
            else
            {
                // Public URL → gunakan Uri constructor
                imageContent = new ImageContent(new Uri(imageUrl));
            }

            chatHistory.AddUserMessage([new TextContent("Lihat gambar ini:"), imageContent]);
        }

        if (!string.IsNullOrEmpty(documentUrl))
            chatHistory.AddUserMessage($"Dokumen terlampir: {documentUrl}");

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = temperature,
            MaxTokens = maxTokens,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        var response = await chat.GetChatMessageContentAsync(chatHistory, settings, kernel);
        return response.Content ?? "Maaf, Si Mak Comblang sedang bingung nih... 😅";
    }

    private void ConfigureOpenAI(IKernelBuilder builder, string modelKey)
    {
        var section = _config.GetSection($"AI:Models:{modelKey}");
        var apiKey = section["ApiKey"] ?? "";
        var modelId = section["ModelId"] ?? "gpt-4o";
        var endpoint = section["Endpoint"];
        if (!string.IsNullOrWhiteSpace(endpoint))
            builder.AddOpenAIChatCompletion(modelId, new Uri(endpoint), apiKey);
        else
            builder.AddOpenAIChatCompletion(modelId, apiKey);
    }

    private void ConfigureOllama(IKernelBuilder builder)
    {
        var section = _config.GetSection("AI:Models:Ollama");
        var modelId = section["ModelId"] ?? "llama3.2";
        var endpoint = section["Endpoint"] ?? "http://localhost:11434";
        builder.AddOpenAIChatCompletion(modelId, new Uri($"{endpoint.TrimEnd('/')}/v1"), "ollama");
    }
}
