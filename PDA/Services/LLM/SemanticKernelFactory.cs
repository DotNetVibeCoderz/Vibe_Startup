using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PDA.Models;

namespace PDA.Services.LLM;

/// <summary>
/// Factory for creating Semantic Kernel instances with the correct AI provider.
/// Database connection info is passed in directly (no second DB lookup).
/// </summary>
public class SemanticKernelFactory
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SemanticKernelFactory> _logger;

    public SemanticKernelFactory(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IServiceProvider serviceProvider,
        ILogger<SemanticKernelFactory> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Create a Semantic Kernel with the specified LLM config.
    /// Pass the DatabaseConnection directly — no second DB lookup needed.
    /// </summary>
    public Kernel CreateKernel(LlmConfig config, DatabaseConnection? dbConnection = null)
    {
        var builder = Kernel.CreateBuilder();

        config.ApiKey ??= _configuration[$"LLM:Providers:{config.Provider}:ApiKey"];
        config.Endpoint ??= _configuration[$"LLM:Providers:{config.Provider}:Endpoint"];

        RegisterChatService(builder, config);

        // Pre-configure plugins with the target database
        ConfigureAndRegisterPlugins(builder, dbConnection);

        return builder.Build();
    }

    #region Chat Service

    private void RegisterChatService(IKernelBuilder builder, LlmConfig config)
    {
        switch (config.Provider.ToLower())
        {
            case "openai":
            case "openaicompatible":
                var endpoint = config.Endpoint ?? "https://api.openai.com/v1";
                builder.AddOpenAIChatCompletion(modelId: config.Model, apiKey: config.ApiKey ?? "", endpoint: new Uri(endpoint));
                break;
            case "ollama":
                var ollamaEndpoint = (config.Endpoint ?? "http://localhost:11434").TrimEnd('/');
                builder.AddOpenAIChatCompletion(modelId: config.Model, apiKey: "ollama", endpoint: new Uri($"{ollamaEndpoint}/v1"));
                break;
            case "anthropic":
                var anthroHttp = _httpClientFactory.CreateClient("DefaultClient");
                builder.Services.AddSingleton<IChatCompletionService>(
                    new AnthropicChatCompletionService(config.ApiKey ?? "", config.Model, anthroHttp, _logger));
                break;
            case "gemini":
                var geminiHttp = _httpClientFactory.CreateClient("DefaultClient");
                var geminiEndpoint = config.Endpoint ?? "https://generativelanguage.googleapis.com/v1beta";
                builder.Services.AddSingleton<IChatCompletionService>(
                    new GeminiChatCompletionService(config.ApiKey ?? "", config.Model, geminiEndpoint, geminiHttp, _logger));
                break;
            default:
                _logger.LogWarning("Unknown provider {Provider}, falling back to OpenAI", config.Provider);
                builder.AddOpenAIChatCompletion(modelId: "gpt-4o", apiKey: "", endpoint: new Uri("https://api.openai.com/v1"));
                break;
        }
    }

    #endregion

    #region Plugin Registration

    private void ConfigureAndRegisterPlugins(IKernelBuilder builder, DatabaseConnection? dbConnection)
    {
        var dataAnalysis = _serviceProvider.GetRequiredService<KernelPlugins.DataAnalysisPlugin>();
        var dashboard     = _serviceProvider.GetRequiredService<KernelPlugins.DashboardPlugin>();
        var knowledgeBase = _serviceProvider.GetRequiredService<KernelPlugins.KnowledgeBasePlugin>();
        var webSearch     = _serviceProvider.GetRequiredService<KernelPlugins.WebSearchPlugin>();
        var common        = _serviceProvider.GetRequiredService<KernelPlugins.CommonFunctionsPlugin>();

        if (dbConnection != null)
        {
            dataAnalysis.DatabaseType = dbConnection.DatabaseType;
            dataAnalysis.ConnectionString = dbConnection.ConnectionString;
            dataAnalysis.FilePath = dbConnection.FilePath;
            dataAnalysis.DatabaseName = dbConnection.Name;
            _logger.LogInformation("🔌 DataAnalysisPlugin configured: {DbName} ({DbType}) Conn={Conn}",
                dbConnection.Name, dbConnection.DatabaseType,
                dbConnection.ConnectionString?[..Math.Min(40, dbConnection.ConnectionString?.Length ?? 0)]);
        }
        else
        {
            _logger.LogWarning("⚠️ No DatabaseConnection provided — DataAnalysisPlugin will use defaults");
        }

        builder.Plugins.AddFromObject(dataAnalysis, "data_analysis");
        builder.Plugins.AddFromObject(dashboard, "dashboard");
        builder.Plugins.AddFromObject(knowledgeBase, "knowledge_base");
        builder.Plugins.AddFromObject(webSearch, "web_search");
        builder.Plugins.AddFromObject(common, "common");
    }

    #endregion
}
