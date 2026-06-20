using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace PDA.Services.LLM;

/// <summary>
/// Custom Google Gemini Chat Completion Service for Semantic Kernel.
/// </summary>
public class GeminiChatCompletionService : IChatCompletionService
{
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string _endpoint;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

    public GeminiChatCompletionService(string apiKey, string model, string endpoint, HttpClient httpClient, ILogger logger)
    {
        _apiKey = apiKey; _model = model; _endpoint = endpoint.TrimEnd('/');
        _httpClient = httpClient; _logger = logger;
    }

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null, CancellationToken cancellationToken = default)
    {
        var url = $"{_endpoint}/models/{_model}:generateContent?key={_apiKey}";

        var systemParts = new List<object>();
        var contents = new List<object>();

        foreach (var msg in chatHistory)
        {
            if (msg.Role == AuthorRole.System)
            { systemParts.Add(new { text = msg.Content ?? "" }); continue; }
            contents.Add(new { role = msg.Role == AuthorRole.Assistant ? "model" : "user", parts = new[] { new { text = msg.Content ?? "" } } });
        }

        var requestBody = new Dictionary<string, object>
        {
            ["contents"] = contents,
            ["generationConfig"] = new { temperature = 0.3, maxOutputTokens = 4096 }
        };
        if (systemParts.Count > 0) requestBody["systemInstruction"] = new { parts = systemParts };

        if (kernel?.Plugins != null)
        {
            var functionDeclarations = new List<object>();
            foreach (var plugin in kernel.Plugins)
            {
                foreach (var function in plugin)
                {
                    var props = new Dictionary<string, object>();
                    foreach (var p in function.Metadata.Parameters)
                        props[p.Name] = new { type = GetGeminiType(p.ParameterType), description = p.Description ?? "" };

                    functionDeclarations.Add(new
                    {
                        name = $"{plugin.Name}_{function.Name}",
                        description = function.Metadata.Description ?? "",
                        parameters = new { type = "object", properties = props }
                    });
                }
            }
            if (functionDeclarations.Count > 0)
                requestBody["tools"] = new[] { new { functionDeclarations } };
        }

        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        var response = await _httpClient.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"), cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini API error {Code}", (int)response.StatusCode);
            return [new ChatMessageContent(AuthorRole.Assistant, $"Error: Gemini API returned {response.StatusCode}")];
        }

        using var doc = JsonDocument.Parse(responseText);
        var candidates = doc.RootElement.GetProperty("candidates");
        var parts = candidates[0].GetProperty("content").GetProperty("parts");

        var textResult = "";
        var functionCalls = new List<FunctionCallContent>();

        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var textEl))
                textResult += textEl.GetString();
            else if (part.TryGetProperty("functionCall", out var funcCall))
            {
                var funcName = funcCall.GetProperty("name").GetString() ?? "";
                var funcArgs = funcCall.GetProperty("args").GetRawText();
                var argsDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(funcArgs) ?? new();
                var kernelArgs = new KernelArguments();
                foreach (var kv in argsDict) kernelArgs[kv.Key] = kv.Value;
                functionCalls.Add(new FunctionCallContent(functionName: funcName, pluginName: null,
                    id: Guid.NewGuid().ToString(), arguments: kernelArgs));
            }
        }

        if (functionCalls.Count > 0)
        {
            var itemCollection = new ChatMessageContentItemCollection();
            foreach (var fc in functionCalls) itemCollection.Add(fc);
            return [new ChatMessageContent(AuthorRole.Assistant, items: itemCollection)];
        }

        return [new ChatMessageContent(AuthorRole.Assistant, textResult)];
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var results = await GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
        foreach (var r in results)
            if (r.Content != null) yield return new StreamingChatMessageContent(AuthorRole.Assistant, r.Content);
    }

    private static string GetGeminiType(Type? t) => t?.Name?.ToUpper() switch
    {
        "INT32" or "INT64" => "INTEGER", "DOUBLE" or "SINGLE" or "DECIMAL" => "NUMBER",
        "BOOLEAN" => "BOOLEAN", _ => "STRING"
    };
}
