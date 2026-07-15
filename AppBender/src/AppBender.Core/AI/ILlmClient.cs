namespace AppBender.Core.AI;

public record LlmMessage(string Role, string Content, List<string>? ImageUrls = null);

public record LlmResult(string Text, int TokensIn, int TokensOut);

public class LlmRequestOptions
{
    /// <summary>Provider key from AI:Providers config (openai, anthropic, gemini, ollama). Null = default.</summary>
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
    /// <summary>Enable Semantic Kernel function calling (kernel plugins).</summary>
    public bool UseTools { get; set; }
    /// <summary>Force a JSON-only answer.</summary>
    public bool JsonMode { get; set; }
}

/// <summary>Multi-provider LLM access (OpenAI, Anthropic, Gemini, Ollama) built on Semantic Kernel.</summary>
public interface ILlmClient
{
    bool IsConfigured { get; }
    Task<LlmResult> CompleteAsync(IReadOnlyList<LlmMessage> messages, LlmRequestOptions? options = null, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamAsync(IReadOnlyList<LlmMessage> messages, LlmRequestOptions? options = null, CancellationToken ct = default);
    /// <summary>Text embedding; returns empty array when no embedding model is configured.</summary>
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    /// <summary>Generates an image, stores it, and returns its public URL.</summary>
    Task<string> GenerateImageAsync(string prompt, string size = "1024x1024", CancellationToken ct = default);
    /// <summary>Transcribes audio at the given URL/path using a whisper-compatible endpoint.</summary>
    Task<string> TranscribeAudioAsync(string audioPathOrUrl, CancellationToken ct = default);
}

public record WebSearchResult(string Title, string Url, string Snippet);

/// <summary>Internet search (Tavily).</summary>
public interface IWebSearchClient
{
    bool IsConfigured { get; }
    Task<List<WebSearchResult>> SearchAsync(string query, int maxResults = 5, CancellationToken ct = default);
    /// <summary>Fetches a URL and returns readable text content.</summary>
    Task<string> ScrapeAsync(string url, CancellationToken ct = default);
}
