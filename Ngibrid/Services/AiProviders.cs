using System.Text.Json;
using Microsoft.SemanticKernel;

namespace Ngibrid.Services;

/// <summary>
/// A single turn in the provider-agnostic conversation passed to the direct-HTTP providers.
/// </summary>
public class AiTurn
{
    public string Role { get; set; } = "user"; // system, user, assistant
    public string Text { get; set; } = string.Empty;

    /// <summary>Images attached to this turn, as data URIs or absolute URLs.</summary>
    public List<AiImage> Images { get; set; } = new();
}

public class AiImage
{
    public string MediaType { get; set; } = "image/png";

    /// <summary>Base64 payload without the data-URI prefix.</summary>
    public string Base64Data { get; set; } = string.Empty;
}

/// <summary>
/// Anthropic and Gemini have no first-party Semantic Kernel connector, so these clients speak the
/// providers' native HTTP APIs while still executing the same <see cref="Kernel"/> functions.
///
/// Both implement the standard agent loop: send tools → provider asks for a tool → invoke the kernel
/// function → feed the result back → repeat until the model answers in prose. That is what gives
/// Anthropic and Gemini the same capabilities as the OpenAI/Ollama path rather than a text-only fallback.
/// </summary>
public class AnthropicChatClient
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly ILogger _logger;

    public AnthropicChatClient(IHttpClientFactory http, IConfiguration config, ILogger logger)
    { _http = http; _config = config; _logger = logger; }

    public async Task<string> CompleteAsync(Kernel kernel, List<AiTurn> turns, double temperature,
        int maxTokens, int maxToolRounds = 5, CancellationToken ct = default)
    {
        var apiKey = _config["ChatBot:Models:Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return "🔧 API Key Anthropic belum dikonfigurasi. Atur di Settings → Chat Bot AI.";

        var model = _config["ChatBot:Models:Anthropic:Model"] ?? "claude-sonnet-4-5";
        var endpoint = (_config["ChatBot:Models:Anthropic:Endpoint"] ?? "https://api.anthropic.com/v1").TrimEnd('/');

        var client = _http.CreateClient("Default");
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        // Anthropic takes the system prompt as a top-level field, not as a message with role "system".
        var systemPrompt = string.Join("\n\n", turns.Where(t => t.Role == "system").Select(t => t.Text));
        var messages = turns.Where(t => t.Role != "system").Select(ToAnthropicMessage).ToList();

        var tools = BuildTools(kernel);

        for (var round = 0; round <= maxToolRounds; round++)
        {
            var payload = new Dictionary<string, object?>
            {
                ["model"] = model,
                ["max_tokens"] = maxTokens,
                ["temperature"] = temperature,
                ["messages"] = messages
            };
            if (!string.IsNullOrWhiteSpace(systemPrompt)) payload["system"] = systemPrompt;
            if (tools.Count > 0 && round < maxToolRounds) payload["tools"] = tools;

            var response = await client.PostAsJsonAsync($"{endpoint}/messages", payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Anthropic error {Status}: {Body}", response.StatusCode, body);
                return $"❌ Anthropic error {(int)response.StatusCode}: {ExtractApiError(body)}";
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

            var text = new System.Text.StringBuilder();
            var toolUses = new List<(string Id, string Name, JsonElement Input)>();

            if (result.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            {
                foreach (var block in content.EnumerateArray())
                {
                    var type = block.TryGetProperty("type", out var t) ? t.GetString() : null;
                    if (type == "text" && block.TryGetProperty("text", out var txt))
                        text.Append(txt.GetString());
                    else if (type == "tool_use")
                        toolUses.Add((
                            block.GetProperty("id").GetString() ?? "",
                            block.GetProperty("name").GetString() ?? "",
                            block.TryGetProperty("input", out var input) ? input : default));
                }
            }

            if (toolUses.Count == 0)
                return text.Length > 0 ? text.ToString() : "Maaf, saya tidak bisa merespons saat ini.";

            // Echo the assistant's tool_use blocks back verbatim — Anthropic requires the
            // assistant turn to be replayed before the matching tool_result turn.
            messages.Add(new Dictionary<string, object?>
            {
                ["role"] = "assistant",
                ["content"] = content.Clone()
            });

            var toolResults = new List<object>();
            foreach (var (id, name, input) in toolUses)
            {
                var output = await KernelToolInvoker.InvokeAsync(kernel, name, input, _logger, ct);
                toolResults.Add(new Dictionary<string, object?>
                {
                    ["type"] = "tool_result",
                    ["tool_use_id"] = id,
                    ["content"] = output
                });
            }

            messages.Add(new Dictionary<string, object?>
            {
                ["role"] = "user",
                ["content"] = toolResults
            });
        }

        return "Maaf, permintaan ini butuh terlalu banyak langkah. Coba persempit pertanyaannya.";
    }

    private static Dictionary<string, object?> ToAnthropicMessage(AiTurn turn)
    {
        if (turn.Images.Count == 0)
            return new Dictionary<string, object?> { ["role"] = turn.Role, ["content"] = turn.Text };

        var parts = new List<object>();
        foreach (var img in turn.Images)
        {
            parts.Add(new Dictionary<string, object?>
            {
                ["type"] = "image",
                ["source"] = new Dictionary<string, object?>
                {
                    ["type"] = "base64",
                    ["media_type"] = img.MediaType,
                    ["data"] = img.Base64Data
                }
            });
        }
        if (!string.IsNullOrWhiteSpace(turn.Text))
            parts.Add(new Dictionary<string, object?> { ["type"] = "text", ["text"] = turn.Text });

        return new Dictionary<string, object?> { ["role"] = turn.Role, ["content"] = parts };
    }

    private static List<object> BuildTools(Kernel kernel)
    {
        var tools = new List<object>();
        foreach (var function in kernel.Plugins.GetFunctionsMetadata())
        {
            tools.Add(new Dictionary<string, object?>
            {
                ["name"] = function.Name,
                ["description"] = function.Description,
                ["input_schema"] = KernelToolInvoker.BuildJsonSchema(function)
            });
        }
        return tools;
    }

    internal static string ExtractApiError(string body)
    {
        try
        {
            var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("message", out var msg)) return msg.GetString() ?? body;
                return error.ToString();
            }
        }
        catch (JsonException) { /* fall through to raw body */ }
        return body.Length > 300 ? body[..300] : body;
    }
}

/// <summary>
/// Google Gemini client with function calling, using the generateContent REST API.
/// </summary>
public class GeminiChatClient
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly ILogger _logger;

    public GeminiChatClient(IHttpClientFactory http, IConfiguration config, ILogger logger)
    { _http = http; _config = config; _logger = logger; }

    public async Task<string> CompleteAsync(Kernel kernel, List<AiTurn> turns, double temperature,
        int maxTokens, int maxToolRounds = 5, CancellationToken ct = default)
    {
        var apiKey = _config["ChatBot:Models:Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return "🔧 API Key Gemini belum dikonfigurasi. Atur di Settings → Chat Bot AI.";

        var model = _config["ChatBot:Models:Gemini:Model"] ?? "gemini-2.0-flash";
        var endpoint = (_config["ChatBot:Models:Gemini:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta").TrimEnd('/');

        var client = _http.CreateClient("Default");

        var systemPrompt = string.Join("\n\n", turns.Where(t => t.Role == "system").Select(t => t.Text));
        var contents = turns.Where(t => t.Role != "system").Select(ToGeminiContent).ToList();

        var declarations = BuildFunctionDeclarations(kernel);

        for (var round = 0; round <= maxToolRounds; round++)
        {
            var payload = new Dictionary<string, object?>
            {
                ["contents"] = contents,
                ["generationConfig"] = new Dictionary<string, object?>
                {
                    ["temperature"] = temperature,
                    ["maxOutputTokens"] = maxTokens
                }
            };

            if (!string.IsNullOrWhiteSpace(systemPrompt))
                payload["systemInstruction"] = new Dictionary<string, object?>
                {
                    ["parts"] = new[] { new Dictionary<string, object?> { ["text"] = systemPrompt } }
                };

            if (declarations.Count > 0 && round < maxToolRounds)
                payload["tools"] = new[]
                {
                    new Dictionary<string, object?> { ["functionDeclarations"] = declarations }
                };

            var response = await client.PostAsJsonAsync(
                $"{endpoint}/models/{model}:generateContent?key={apiKey}", payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Gemini error {Status}: {Body}", response.StatusCode, body);
                return $"❌ Gemini error {(int)response.StatusCode}: {AnthropicChatClient.ExtractApiError(body)}";
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

            if (!result.TryGetProperty("candidates", out var candidates) ||
                candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
                return "Maaf, tidak ada respons dari Gemini.";

            var candidate = candidates[0];
            if (!candidate.TryGetProperty("content", out var contentNode) ||
                !contentNode.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array)
                return "Maaf, tidak ada respons dari Gemini.";

            var text = new System.Text.StringBuilder();
            var calls = new List<(string Name, JsonElement Args)>();

            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var t)) text.Append(t.GetString());
                if (part.TryGetProperty("functionCall", out var fc))
                    calls.Add((
                        fc.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                        fc.TryGetProperty("args", out var a) ? a : default));
            }

            if (calls.Count == 0)
                return text.Length > 0 ? text.ToString() : "Maaf, saya tidak bisa merespons saat ini.";

            contents.Add(new Dictionary<string, object?>
            {
                ["role"] = "model",
                ["parts"] = contentNode.TryGetProperty("parts", out var replay) ? replay.Clone() : parts.Clone()
            });

            var responseParts = new List<object>();
            foreach (var (name, args) in calls)
            {
                var output = await KernelToolInvoker.InvokeAsync(kernel, name, args, _logger, ct);
                responseParts.Add(new Dictionary<string, object?>
                {
                    ["functionResponse"] = new Dictionary<string, object?>
                    {
                        ["name"] = name,
                        // Gemini requires the result to be a JSON object, not a bare string.
                        ["response"] = new Dictionary<string, object?> { ["result"] = output }
                    }
                });
            }

            contents.Add(new Dictionary<string, object?> { ["role"] = "user", ["parts"] = responseParts });
        }

        return "Maaf, permintaan ini butuh terlalu banyak langkah. Coba persempit pertanyaannya.";
    }

    private static Dictionary<string, object?> ToGeminiContent(AiTurn turn)
    {
        var parts = new List<object>();

        foreach (var img in turn.Images)
        {
            parts.Add(new Dictionary<string, object?>
            {
                ["inline_data"] = new Dictionary<string, object?>
                {
                    ["mime_type"] = img.MediaType,
                    ["data"] = img.Base64Data
                }
            });
        }

        if (!string.IsNullOrWhiteSpace(turn.Text) || parts.Count == 0)
            parts.Add(new Dictionary<string, object?> { ["text"] = turn.Text });

        return new Dictionary<string, object?>
        {
            // Gemini names the assistant role "model".
            ["role"] = turn.Role == "assistant" ? "model" : "user",
            ["parts"] = parts
        };
    }

    private static List<object> BuildFunctionDeclarations(Kernel kernel)
    {
        var declarations = new List<object>();
        foreach (var function in kernel.Plugins.GetFunctionsMetadata())
        {
            var schema = KernelToolInvoker.BuildJsonSchema(function);
            var declaration = new Dictionary<string, object?>
            {
                ["name"] = function.Name,
                ["description"] = function.Description
            };

            // Gemini rejects an empty parameters object, so omit it for no-arg functions.
            if (schema.TryGetValue("properties", out var props) &&
                props is Dictionary<string, object?> { Count: > 0 })
                declaration["parameters"] = schema;

            declarations.Add(declaration);
        }
        return declarations;
    }
}

/// <summary>
/// Shared bridge between a provider's tool-call payload and a Semantic Kernel function:
/// builds the JSON schema advertised to the model, and invokes the function with the
/// arguments the model chose.
/// </summary>
internal static class KernelToolInvoker
{
    public static Dictionary<string, object?> BuildJsonSchema(KernelFunctionMetadata function)
    {
        var properties = new Dictionary<string, object?>();
        var required = new List<string>();

        foreach (var parameter in function.Parameters)
        {
            properties[parameter.Name] = new Dictionary<string, object?>
            {
                ["type"] = MapJsonType(parameter.ParameterType),
                ["description"] = parameter.Description ?? parameter.Name
            };

            if (parameter.IsRequired) required.Add(parameter.Name);
        }

        var schema = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = properties
        };
        if (required.Count > 0) schema["required"] = required;
        return schema;
    }

    private static string MapJsonType(Type? type)
    {
        if (type == null) return "string";
        var t = Nullable.GetUnderlyingType(type) ?? type;

        if (t == typeof(bool)) return "boolean";
        if (t == typeof(int) || t == typeof(long) || t == typeof(short)) return "integer";
        if (t == typeof(double) || t == typeof(float) || t == typeof(decimal)) return "number";
        return "string";
    }

    /// <summary>
    /// Invoke a kernel function by name. Failures are returned to the model as text so it can
    /// explain or retry, rather than aborting the whole conversation.
    /// </summary>
    public static async Task<string> InvokeAsync(Kernel kernel, string functionName, JsonElement arguments,
        ILogger logger, CancellationToken ct)
    {
        try
        {
            var metadata = kernel.Plugins.GetFunctionsMetadata()
                .FirstOrDefault(f => f.Name.Equals(functionName, StringComparison.OrdinalIgnoreCase));
            if (metadata == null) return $"Fungsi '{functionName}' tidak tersedia.";

            var function = kernel.Plugins
                .SelectMany(p => p)
                .First(f => f.Name.Equals(functionName, StringComparison.OrdinalIgnoreCase));

            var kernelArgs = new KernelArguments();
            if (arguments.ValueKind == JsonValueKind.Object)
            {
                foreach (var parameter in metadata.Parameters)
                {
                    if (!arguments.TryGetProperty(parameter.Name, out var value)) continue;
                    kernelArgs[parameter.Name] = ConvertArgument(value, parameter.ParameterType);
                }
            }

            var result = await function.InvokeAsync(kernel, kernelArgs, ct);
            return result.GetValue<object>()?.ToString() ?? "(tidak ada hasil)";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Kernel function {Function} failed", functionName);
            return $"Fungsi '{functionName}' gagal dijalankan: {ex.Message}";
        }
    }

    private static object? ConvertArgument(JsonElement value, Type? targetType)
    {
        var t = targetType == null ? typeof(string) : Nullable.GetUnderlyingType(targetType) ?? targetType;

        try
        {
            if (t == typeof(bool))
                return value.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String => bool.TryParse(value.GetString(), out var b) && b,
                    _ => false
                };

            if (t == typeof(int) || t == typeof(long) || t == typeof(short))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var i))
                    return Convert.ChangeType(i, t);
                if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var si))
                    return Convert.ChangeType(si, t);
                return Convert.ChangeType(0, t);
            }

            if (t == typeof(double) || t == typeof(float) || t == typeof(decimal))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var d))
                    return Convert.ChangeType(d, t);
                if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var sd))
                    return Convert.ChangeType(sd, t);
                return Convert.ChangeType(0, t);
            }

            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
        }
        catch (Exception)
        {
            // A malformed argument shouldn't kill the call; let the function apply its default.
            return null;
        }
    }
}
