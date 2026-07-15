namespace AppBender.Core.AI;

/// <summary>Bound from the "AI" configuration section (appsettings.json).</summary>
public class AiOptions
{
    public const string SectionName = "AI";

    public string DefaultProvider { get; set; } = "openai";
    public string AssistantName { get; set; } = "App Guru";
    /// <summary>Persona / system prompt used by App Guru.</summary>
    public string SystemPrompt { get; set; } =
        "You are App Guru, the friendly AI assistant inside AppBender, a low-code platform. " +
        "Help users design forms, workflows, datasets and connectors. Answer in the user's language. " +
        "Use markdown (tables, code blocks, images) when helpful.";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 4000;
    public Dictionary<string, AiProviderOptions> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public AiProviderOptions? GetProvider(string? name)
    {
        name ??= DefaultProvider;
        return Providers.TryGetValue(name, out var p) ? p : null;
    }
}

public class AiProviderOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    /// <summary>OpenAI-compatible base endpoint. Empty = provider default.</summary>
    public string Endpoint { get; set; } = "";
    public string EmbeddingModel { get; set; } = "";
    public string ImageModel { get; set; } = "";
    public string AudioModel { get; set; } = "";

    public string EffectiveEndpoint(string providerKey) =>
        !string.IsNullOrWhiteSpace(Endpoint) ? Endpoint.TrimEnd('/') : providerKey.ToLowerInvariant() switch
        {
            "openai" => "https://api.openai.com/v1",
            "anthropic" => "https://api.anthropic.com/v1",
            "gemini" => "https://generativelanguage.googleapis.com/v1beta/openai",
            "ollama" => "http://localhost:11434/v1",
            _ => "https://api.openai.com/v1"
        };
}
