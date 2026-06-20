using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace PDA.Services.LLM;

/// <summary>
/// Custom Anthropic (Claude) Chat Completion Service for Semantic Kernel.
/// </summary>
public class AnthropicChatCompletionService : IChatCompletionService
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

    public AnthropicChatCompletionService(string apiKey, string model, HttpClient httpClient, ILogger logger)
    {
        _apiKey = apiKey; _model = model; _httpClient = httpClient; _logger = logger;
    }

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null, CancellationToken cancellationToken = default)
    {
        var messages = new List<object>();
        string? systemPrompt = null;

        foreach (var msg in chatHistory)
        {
            if (msg.Role == AuthorRole.System) { systemPrompt = msg.Content ?? ""; continue; }
            messages.Add(new { role = msg.Role == AuthorRole.Assistant ? "assistant" : "user", content = msg.Content ?? "" });
        }

        var tools = BuildToolDefinitions(kernel);

        var requestBody = new
        {
            model = _model, max_tokens = 4096, temperature = 0.3,
            system = systemPrompt ?? "", messages,
            tools = tools.Count > 0 ? tools : null
        };

        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Anthropic API error {Code}", (int)response.StatusCode);
            return [new ChatMessageContent(AuthorRole.Assistant, $"Error: Anthropic API returned {response.StatusCode}")];
        }

        using var doc = JsonDocument.Parse(responseText);
        var textContent = "";
        var contentArray = doc.RootElement.GetProperty("content");
        var toolResults = new List<ChatMessageContent>();

        foreach (var block in contentArray.EnumerateArray())
        {
            var type = block.GetProperty("type").GetString();
            if (type == "text")
            {
                textContent += block.GetProperty("text").GetString();
            }
            else if (type == "tool_use")
            {
                var toolName = block.GetProperty("name").GetString() ?? "";
                var toolInput = block.GetProperty("input").GetRawText();
                var toolId = block.GetProperty("id").GetString() ?? "";

                var argsDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(toolInput) ?? new();
                var kernelArgs = new KernelArguments();
                foreach (var kv in argsDict) kernelArgs[kv.Key] = kv.Value;

                var functionCall = new FunctionCallContent(
                    functionName: toolName, pluginName: null, id: toolId, arguments: kernelArgs);

                toolResults.Add(new ChatMessageContent(AuthorRole.Assistant, [functionCall]));
            }
        }

        if (toolResults.Count > 0) return toolResults;
        return [new ChatMessageContent(AuthorRole.Assistant, textContent)];
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var results = await GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
        foreach (var r in results)
            if (r.Content != null) yield return new StreamingChatMessageContent(AuthorRole.Assistant, r.Content);
    }

    private static List<object> BuildToolDefinitions(Kernel? kernel)
    {
        var tools = new List<object>();
        if (kernel?.Plugins == null) return tools;

        foreach (var plugin in kernel.Plugins)
        {
            foreach (var function in plugin)
            {
                var properties = new Dictionary<string, object>();
                foreach (var param in function.Metadata.Parameters)
                    properties[param.Name] = new { type = GetJsonType(param.ParameterType), description = param.Description ?? "" };

                tools.Add(new
                {
                    name = $"{plugin.Name}_{function.Name}",
                    description = function.Metadata.Description ?? "",
                    input_schema = new { type = "object", properties, required = function.Metadata.Parameters.Where(p => p.IsRequired).Select(p => p.Name).ToList() }
                });
            }
        }
        return tools;
    }

    private static string GetJsonType(Type? type) => type switch
    {
        not null when type == typeof(int) || type == typeof(long) => "integer",
        not null when type == typeof(double) || type == typeof(float) || type == typeof(decimal) => "number",
        not null when type == typeof(bool) => "boolean",
        _ => "string"
    };
}
