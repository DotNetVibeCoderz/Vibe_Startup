namespace PDA.Services.LLM;

/// <summary>
/// LLM Provider configuration used across the application
/// </summary>
public class LlmConfig
{
    public string Provider { get; set; } = "OpenAI";
    public string Model { get; set; } = "gpt-4o";
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public double Temperature { get; set; } = 0.3;
    public int MaxTokens { get; set; } = 4096;
}

/// <summary>
/// Simple helper to get available models from configuration
/// </summary>
public class LlmModelHelper
{
    private readonly IConfiguration _configuration;

    public LlmModelHelper(IConfiguration configuration) => _configuration = configuration;

    public List<string> GetAvailableModels(string provider)
    {
        return _configuration.GetSection($"LLM:Providers:{provider}:Models").Get<List<string>>()
            ?? new List<string> { "default" };
    }

    public string? GetApiKey(string provider)
    {
        return _configuration[$"LLM:Providers:{provider}:ApiKey"];
    }

    public string? GetEndpoint(string provider)
    {
        return _configuration[$"LLM:Providers:{provider}:Endpoint"];
    }
}
