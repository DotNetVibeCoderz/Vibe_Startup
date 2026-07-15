using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using AppBender.Core.Common;
using AppBender.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AppBender.Core.AI;

/// <summary>
/// Multi-provider LLM client built on Semantic Kernel. All providers are accessed through
/// their OpenAI-compatible chat-completions endpoints (OpenAI, Anthropic, Gemini, Ollama),
/// so one connector covers all four, including function calling.
/// </summary>
public class SemanticKernelLlmClient(
    IConfiguration config,
    IHttpClientFactory httpFactory,
    IWebSearchClient webSearch,
    IDataHubService dataHub,
    IStorageService storage,
    IUsageService usage,
    ILogger<SemanticKernelLlmClient> logger) : ILlmClient
{
    private AiOptions Options => config.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();

    public bool IsConfigured
    {
        get
        {
            var provider = Options.GetProvider(null);
            return provider is not null &&
                   (!string.IsNullOrEmpty(provider.ApiKey) ||
                    provider.EffectiveEndpoint(Options.DefaultProvider).Contains("localhost"));
        }
    }

    private (Kernel Kernel, AiProviderOptions Provider, string ProviderKey) BuildKernel(LlmRequestOptions? options, bool withTools)
    {
        var aiOptions = Options;
        var providerKey = options?.Provider ?? aiOptions.DefaultProvider;
        var provider = aiOptions.GetProvider(providerKey)
            ?? throw new InvalidOperationException(
                $"AI provider '{providerKey}' is not configured. Add it under AI:Providers in appsettings.json.");

        var model = options?.Model ?? provider.Model;
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException($"No model configured for AI provider '{providerKey}'.");

        var endpoint = provider.EffectiveEndpoint(providerKey);
        var httpClient = httpFactory.CreateClient("ai");
        httpClient.Timeout = TimeSpan.FromMinutes(5);

        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(
            modelId: model,
            endpoint: new Uri(endpoint),
            apiKey: string.IsNullOrEmpty(provider.ApiKey) ? "not-needed" : provider.ApiKey,
            httpClient: httpClient);
        var kernel = builder.Build();

        if (withTools)
        {
            kernel.Plugins.AddFromObject(new MathPlugin(), "math");
            kernel.Plugins.AddFromObject(new DateTimePlugin(), "datetime");
            kernel.Plugins.AddFromObject(new WebPlugin(webSearch), "web");
            kernel.Plugins.AddFromObject(new DatasetPlugin(dataHub), "datahub");
        }

        return (kernel, provider, providerKey);
    }

    private async Task<ChatHistory> BuildHistoryAsync(IReadOnlyList<LlmMessage> messages, CancellationToken ct)
    {
        var history = new ChatHistory();
        foreach (var message in messages)
        {
            switch (message.Role.ToLowerInvariant())
            {
                case "system":
                    history.AddSystemMessage(message.Content);
                    break;
                case "assistant":
                    history.AddAssistantMessage(message.Content);
                    break;
                default:
                    if (message.ImageUrls is { Count: > 0 })
                    {
                        var items = new ChatMessageContentItemCollection();
                        if (!string.IsNullOrEmpty(message.Content))
                            items.Add(new Microsoft.SemanticKernel.TextContent(message.Content));
                        foreach (var url in message.ImageUrls)
                        {
                            var image = await LoadImageAsync(url, ct);
                            if (image is not null) items.Add(image);
                        }
                        history.Add(new ChatMessageContent(AuthorRole.User, items));
                    }
                    else
                    {
                        history.AddUserMessage(message.Content);
                    }
                    break;
            }
        }
        return history;
    }

    private async Task<Microsoft.SemanticKernel.ImageContent?> LoadImageAsync(string url, CancellationToken ct)
    {
        try
        {
            if (url.StartsWith("/files/", StringComparison.OrdinalIgnoreCase))
            {
                var path = url["/files/".Length..];
                await using var stream = await storage.OpenReadAsync(path);
                if (stream is null) return null;
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct);
                return new Microsoft.SemanticKernel.ImageContent(ms.ToArray(), GuessMime(path));
            }
            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return new Microsoft.SemanticKernel.ImageContent(url);
            if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return new Microsoft.SemanticKernel.ImageContent(new Uri(url));
            // local storage path
            await using var s = await storage.OpenReadAsync(url);
            if (s is null) return null;
            using var buffer = new MemoryStream();
            await s.CopyToAsync(buffer, ct);
            return new Microsoft.SemanticKernel.ImageContent(buffer.ToArray(), GuessMime(url));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load image {Url}", url);
            return null;
        }
    }

    private static string GuessMime(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        _ => "image/jpeg"
    };

    public async Task<LlmResult> CompleteAsync(IReadOnlyList<LlmMessage> messages,
        LlmRequestOptions? options = null, CancellationToken ct = default)
    {
        var (kernel, _, providerKey) = BuildKernel(options, options?.UseTools == true);
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var history = await BuildHistoryAsync(messages, ct);

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = options?.Temperature ?? Options.Temperature,
            MaxTokens = options?.MaxTokens ?? Options.MaxTokens
        };
        if (options?.UseTools == true)
            settings.FunctionChoiceBehavior = FunctionChoiceBehavior.Auto();
        if (options?.JsonMode == true)
            settings.ResponseFormat = "json_object";

        var result = await chat.GetChatMessageContentAsync(history, settings, kernel, ct);
        var text = result.Content ?? "";
        var (tokensIn, tokensOut) = ExtractUsage(result.Metadata, messages, text);
        usage.TrackFireAndForget("llm_tokens_in", tokensIn, providerKey);
        usage.TrackFireAndForget("llm_tokens_out", tokensOut, providerKey);
        return new LlmResult(text, tokensIn, tokensOut);
    }

    public async IAsyncEnumerable<string> StreamAsync(IReadOnlyList<LlmMessage> messages,
        LlmRequestOptions? options = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (kernel, _, providerKey) = BuildKernel(options, options?.UseTools == true);
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var history = await BuildHistoryAsync(messages, ct);

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = options?.Temperature ?? Options.Temperature,
            MaxTokens = options?.MaxTokens ?? Options.MaxTokens
        };
        if (options?.UseTools == true)
            settings.FunctionChoiceBehavior = FunctionChoiceBehavior.Auto();

        var totalChars = 0;
        await foreach (var chunk in chat.GetStreamingChatMessageContentsAsync(history, settings, kernel, ct))
        {
            var piece = chunk.Content;
            if (string.IsNullOrEmpty(piece)) continue;
            totalChars += piece.Length;
            yield return piece;
        }
        // streamed responses rarely expose usage metadata; estimate for the dashboard
        usage.TrackFireAndForget("llm_tokens_in", EstimateTokens(messages.Sum(m => m.Content.Length)), providerKey);
        usage.TrackFireAndForget("llm_tokens_out", EstimateTokens(totalChars), providerKey);
    }

    private static (int In, int Out) ExtractUsage(IReadOnlyDictionary<string, object?>? metadata,
        IReadOnlyList<LlmMessage> messages, string output)
    {
        if (metadata is not null && metadata.TryGetValue("Usage", out var raw) && raw is not null)
        {
            var type = raw.GetType();
            int Read(params string[] names)
            {
                foreach (var name in names)
                {
                    var property = type.GetProperty(name);
                    if (property?.GetValue(raw) is int i) return i;
                    if (property?.GetValue(raw) is long l) return (int)l;
                }
                return 0;
            }
            var tokensIn = Read("InputTokenCount", "PromptTokens", "InputTokens");
            var tokensOut = Read("OutputTokenCount", "CompletionTokens", "OutputTokens");
            if (tokensIn > 0 || tokensOut > 0) return (tokensIn, tokensOut);
        }
        return (EstimateTokens(messages.Sum(m => m.Content.Length)), EstimateTokens(output.Length));
    }

    private static int EstimateTokens(int chars) => Math.Max(1, chars / 4);

    // ------------------------------------------------------------------ embeddings

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var aiOptions = Options;
        // pick the first provider that declares an embedding model
        var candidates = new List<(string Key, AiProviderOptions P)>();
        if (aiOptions.GetProvider(null) is { } dp) candidates.Add((aiOptions.DefaultProvider, dp));
        candidates.AddRange(aiOptions.Providers.Select(kv => (kv.Key, kv.Value)));
        var (key, provider) = candidates.FirstOrDefault(c => !string.IsNullOrEmpty(c.Item2.EmbeddingModel));
        if (provider is null) return [];

        try
        {
            var client = httpFactory.CreateClient("ai");
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"{provider.EffectiveEndpoint(key)}/embeddings");
            if (!string.IsNullOrEmpty(provider.ApiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
            request.Content = new StringContent(JsonUtil.Serialize(new Dictionary<string, object?>
            {
                ["model"] = provider.EmbeddingModel,
                ["input"] = text.Length > 8000 ? text[..8000] : text
            }), Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return [];
            var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var vector = doc.RootElement.GetProperty("data")[0].GetProperty("embedding");
            return vector.EnumerateArray().Select(e => e.GetSingle()).ToArray();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Embedding request failed");
            return [];
        }
    }

    // ------------------------------------------------------------------ image generation

    public async Task<string> GenerateImageAsync(string prompt, string size = "1024x1024", CancellationToken ct = default)
    {
        var aiOptions = Options;
        var candidates = new List<(string Key, AiProviderOptions P)>();
        if (aiOptions.GetProvider(null) is { } dp) candidates.Add((aiOptions.DefaultProvider, dp));
        candidates.AddRange(aiOptions.Providers.Select(kv => (kv.Key, kv.Value)));
        var (key, provider) = candidates.FirstOrDefault(c => !string.IsNullOrEmpty(c.Item2.ImageModel));
        if (provider is null)
            throw new InvalidOperationException("No AI provider has an ImageModel configured.");

        var client = httpFactory.CreateClient("ai");
        client.Timeout = TimeSpan.FromMinutes(5);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"{provider.EffectiveEndpoint(key)}/images/generations");
        if (!string.IsNullOrEmpty(provider.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        request.Content = new StringContent(JsonUtil.Serialize(new Dictionary<string, object?>
        {
            ["model"] = provider.ImageModel,
            ["prompt"] = prompt,
            ["size"] = size,
            ["n"] = 1,
            ["response_format"] = "b64_json"
        }), Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Image generation failed ({(int)response.StatusCode}): {json}");

        var doc = System.Text.Json.JsonDocument.Parse(json);
        var item = doc.RootElement.GetProperty("data")[0];
        byte[] bytes;
        if (item.TryGetProperty("b64_json", out var b64))
            bytes = Convert.FromBase64String(b64.GetString() ?? "");
        else if (item.TryGetProperty("url", out var urlProp))
            bytes = await client.GetByteArrayAsync(urlProp.GetString(), ct);
        else
            throw new InvalidOperationException("Image generation returned no data.");

        var path = $"uploads/ai/{Guid.NewGuid():N}.png";
        using var ms = new MemoryStream(bytes);
        await storage.SaveAsync(path, ms, "image/png");
        usage.TrackFireAndForget("llm_image", 1, key);
        return storage.GetPublicUrl(path);
    }

    // ------------------------------------------------------------------ audio transcription

    public async Task<string> TranscribeAudioAsync(string audioPathOrUrl, CancellationToken ct = default)
    {
        var aiOptions = Options;
        var candidates = new List<(string Key, AiProviderOptions P)>();
        if (aiOptions.GetProvider(null) is { } dp) candidates.Add((aiOptions.DefaultProvider, dp));
        candidates.AddRange(aiOptions.Providers.Select(kv => (kv.Key, kv.Value)));
        var (key, provider) = candidates.FirstOrDefault(c => !string.IsNullOrEmpty(c.Item2.AudioModel));
        if (provider is null)
            throw new InvalidOperationException("No AI provider has an AudioModel configured.");

        var client = httpFactory.CreateClient("ai");
        client.Timeout = TimeSpan.FromMinutes(5);

        byte[] audio;
        string fileName;
        if (audioPathOrUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            audio = await client.GetByteArrayAsync(audioPathOrUrl, ct);
            fileName = Path.GetFileName(new Uri(audioPathOrUrl).LocalPath);
        }
        else
        {
            var path = audioPathOrUrl.StartsWith("/files/", StringComparison.OrdinalIgnoreCase)
                ? audioPathOrUrl["/files/".Length..] : audioPathOrUrl;
            await using var stream = await storage.OpenReadAsync(path)
                ?? throw new FileNotFoundException($"Audio not found: {audioPathOrUrl}");
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            audio = ms.ToArray();
            fileName = Path.GetFileName(path);
        }
        if (string.IsNullOrEmpty(fileName)) fileName = "audio.mp3";

        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"{provider.EffectiveEndpoint(key)}/audio/transcriptions");
        if (!string.IsNullOrEmpty(provider.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(audio), "file", fileName },
            { new StringContent(provider.AudioModel), "model" }
        };
        request.Content = form;

        using var response = await client.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Transcription failed ({(int)response.StatusCode}): {json}");
        var doc = System.Text.Json.JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("text", out var t) ? t.GetString() ?? "" : json;
    }
}
