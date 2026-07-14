using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace BlazorViz.Services.Ai;

/// <summary>
/// Builds a Semantic Kernel per configured provider.
/// OpenAI and Anthropic both use the OpenAI-compatible connector (Anthropic exposes a
/// compatibility endpoint); Gemini uses the Google connector; Ollama its own connector.
/// </summary>
public sealed class AiKernelFactory(IOptionsMonitor<AiOptions> options, IServiceProvider services)
{
    public AiOptions Options => options.CurrentValue;

    public Kernel CreateKernel(bool withPlugins = true)
    {
        var opts = options.CurrentValue;
        var p = opts.Active;
        var builder = Kernel.CreateBuilder();

        switch (opts.Provider.ToLowerInvariant())
        {
            case "openai":
                if (string.IsNullOrWhiteSpace(p.Endpoint))
                    builder.AddOpenAIChatCompletion(p.Model, p.ApiKey ?? "");
                else
                    builder.AddOpenAIChatCompletion(p.Model, new Uri(p.Endpoint), p.ApiKey);
                break;
            case "anthropic":
                builder.AddOpenAIChatCompletion(p.Model, new Uri(p.Endpoint ?? "https://api.anthropic.com/v1"), p.ApiKey);
                break;
            case "gemini":
                builder.AddGoogleAIGeminiChatCompletion(p.Model, p.ApiKey ?? "");
                break;
            case "ollama":
                builder.AddOllamaChatCompletion(p.Model, new Uri(p.Endpoint ?? "http://localhost:11434"));
                break;
            default:
                throw new InvalidOperationException($"Unknown AI provider '{opts.Provider}'. Use OpenAI, Anthropic, Gemini or Ollama.");
        }

        var kernel = builder.Build();
        if (withPlugins)
        {
            kernel.Plugins.AddFromObject(new MathPlugin(), "math");
            kernel.Plugins.AddFromObject(new DateTimePlugin(), "datetime");
            kernel.Plugins.AddFromObject(ActivatorUtilities.CreateInstance<WebPlugin>(services), "web");
            kernel.Plugins.AddFromObject(ActivatorUtilities.CreateInstance<DataQueryPlugin>(services), "data");
            kernel.Plugins.AddFromObject(ActivatorUtilities.CreateInstance<DashboardBuilderPlugin>(services), "dashboard");
            var plugins = services.GetService<PluginService>();
            plugins?.RegisterInto(kernel);
        }
        return kernel;
    }

    /// <summary>Provider-appropriate execution settings with tool calling enabled.</summary>
    public PromptExecutionSettings CreateSettings()
    {
        var opts = options.CurrentValue;
        return opts.Provider.ToLowerInvariant() switch
        {
            "gemini" => new GeminiPromptExecutionSettings
            {
                Temperature = opts.Temperature,
                MaxTokens = opts.MaxTokens,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            },
            "ollama" => new PromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            },
            _ => new OpenAIPromptExecutionSettings
            {
                Temperature = opts.Temperature,
                MaxTokens = opts.MaxTokens,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            }
        };
    }
}
