namespace BlazorViz.Services.Ai;

/// <summary>Bound from the "Ai" section of appsettings.json (persona, model, temperature, providers…).</summary>
public sealed class AiOptions
{
    public const string Section = "Ai";

    /// <summary>OpenAI | Anthropic | Gemini | Ollama</summary>
    public string Provider { get; set; } = "Ollama";
    public string AssistantName { get; set; } = "Data Wizard";
    public string SystemPrompt { get; set; } =
        "You are Data Wizard, a friendly analytics assistant inside BlazorViz. " +
        "You help users explore their datasets and dashboards. Prefer using the available tools " +
        "to query real data instead of guessing. You can also BUILD dashboards for the user: first call " +
        "list_datasets and get_dataset_schema to learn the exact column names, then create_dashboard, then " +
        "add_panel for each visualization (KPIs first, then charts) and add_filter for interactivity, and " +
        "finish by giving the user the /dashboards/{id} link. " +
        "Answer with well-formatted markdown; use tables for tabular results.";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2048;

    public Dictionary<string, AiProviderOptions> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OpenAI"] = new() { Model = "gpt-4o-mini" },
        ["Anthropic"] = new() { Model = "claude-sonnet-5", Endpoint = "https://api.anthropic.com/v1" },
        ["Gemini"] = new() { Model = "gemini-2.0-flash" },
        ["Ollama"] = new() { Model = "llama3.1", Endpoint = "http://localhost:11434" }
    };

    public EmbeddingOptions Embeddings { get; set; } = new();
    public RagOptions Rag { get; set; } = new();
    public TavilyOptions Tavily { get; set; } = new();

    public AiProviderOptions Active =>
        Providers.TryGetValue(Provider, out var p) ? p : new AiProviderOptions();
}

public sealed class AiProviderOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "";
    /// <summary>Custom/OpenAI-compatible endpoint (Anthropic compat, Azure gateway, Ollama, LM Studio…).</summary>
    public string? Endpoint { get; set; }
}

public sealed class EmbeddingOptions
{
    /// <summary>Local | OpenAI | Ollama — Local is an offline hashing embedder (no API key needed).</summary>
    public string Provider { get; set; } = "Local";
    public string Model { get; set; } = "text-embedding-3-small";
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public int Dimensions { get; set; } = 384;
}

/// <summary>Tavily web-search API used by the Data Wizard's search_internet tool (https://tavily.com).</summary>
public sealed class TavilyOptions
{
    public string? ApiKey { get; set; }
    public string Endpoint { get; set; } = "https://api.tavily.com/search";
    /// <summary>basic | advanced</summary>
    public string SearchDepth { get; set; } = "basic";
    public int MaxResults { get; set; } = 5;
}

public sealed class RagOptions
{
    /// <summary>InMemory | Qdrant | Chroma | AzureAISearch</summary>
    public string VectorStore { get; set; } = "InMemory";
    public string Collection { get; set; } = "blazorviz-docs";
    public int TopK { get; set; } = 4;
    public int ChunkSize { get; set; } = 1200;
    public int ChunkOverlap { get; set; } = 200;
    public string? QdrantEndpoint { get; set; } = "http://localhost:6333";
    public string? QdrantApiKey { get; set; }
    public string? ChromaEndpoint { get; set; } = "http://localhost:8000";
    public string? AzureSearchEndpoint { get; set; }
    public string? AzureSearchApiKey { get; set; }
}
