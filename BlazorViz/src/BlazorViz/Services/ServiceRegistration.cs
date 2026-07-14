using BlazorViz.Services.Ai;
using BlazorViz.Services.Connectors;
using BlazorViz.Services.Rag;
using BlazorViz.Services.Scripting;
using BlazorViz.Services.Storage;
using Microsoft.Extensions.Options;

namespace BlazorViz.Services;

public static class ServiceRegistration
{
    public static IServiceCollection AddBlazorVizServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddMemoryCache();
        services.AddHttpClient("connector", c => c.Timeout = TimeSpan.FromSeconds(60));

        // connectors
        services.AddSingleton<IDataConnector, SqliteConnector>();
        services.AddSingleton<IDataConnector, SqlServerConnector>();
        services.AddSingleton<IDataConnector, PostgresConnector>();
        services.AddSingleton<IDataConnector, MySqlDbConnector>();
        services.AddSingleton<IDataConnector, OracleConnector>();
        services.AddSingleton<IDataConnector, ExcelConnector>();
        services.AddSingleton<IDataConnector, CsvConnector>();
        services.AddSingleton<IDataConnector, RestConnector>();
        services.AddSingleton<IDataConnector, GraphQLConnector>();
        services.AddSingleton<DataConnectorFactory>();

        // etl & scripting
        services.AddSingleton<EtlService>();
        services.AddSingleton<IScriptRunner, CSharpScriptRunner>();
        services.AddSingleton<IScriptRunner, JsScriptRunner>();
        services.AddSingleton<IScriptRunner, PythonScriptRunner>();
        services.AddSingleton<ScriptRunnerFactory>();

        // data / viz / export
        services.AddSingleton<DatasetService>();
        services.AddSingleton<ChartBuilder>();
        services.AddSingleton<RecommendationService>();
        services.AddSingleton<ExportService>();
        services.AddSingleton<DashboardService>();
        services.AddSingleton<MlService>();

        // telemetry
        services.AddSingleton<AuditService>();
        services.AddSingleton<UsageService>();
        services.AddSingleton<PerfMonitor>();

        // AI
        services.Configure<AiOptions>(config.GetSection(AiOptions.Section));
        services.AddSingleton<AiKernelFactory>();
        services.AddSingleton<ChatService>();
        services.AddSingleton<DashboardAiService>();
        services.AddSingleton<PluginService>();

        // RAG: embedder + vector index chosen from config
        services.AddSingleton<IEmbedder>(sp =>
        {
            var opts = sp.GetRequiredService<IOptionsMonitor<AiOptions>>().CurrentValue.Embeddings;
            var http = sp.GetRequiredService<IHttpClientFactory>();
            return opts.Provider.ToLowerInvariant() switch
            {
                "openai" => new OpenAIEmbedder(http, opts),
                "ollama" => new OllamaEmbedder(http, opts),
                _ => new LocalHashEmbedder(opts.Dimensions)
            };
        });
        services.AddSingleton<IVectorIndex>(sp =>
        {
            var opts = sp.GetRequiredService<IOptionsMonitor<AiOptions>>().CurrentValue.Rag;
            return opts.VectorStore.ToLowerInvariant() switch
            {
                "qdrant" => ActivatorUtilities.CreateInstance<QdrantVectorIndex>(sp),
                "chroma" => ActivatorUtilities.CreateInstance<ChromaVectorIndex>(sp),
                "azureaisearch" => ActivatorUtilities.CreateInstance<AzureAISearchVectorIndex>(sp),
                _ => ActivatorUtilities.CreateInstance<InMemoryVectorIndex>(sp)
            };
        });
        services.AddSingleton<RagService>();

        // storage
        services.Configure<StorageOptions>(config.GetSection(StorageOptions.Section));
        services.AddSingleton<IFileStorage>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            var env = sp.GetRequiredService<IWebHostEnvironment>();
            return opts.Provider.ToLowerInvariant() switch
            {
                "azureblob" => new AzureBlobStorage(opts),
                "s3" or "minio" => new S3Storage(opts),
                _ => new FileSystemStorage(Path.IsPathRooted(opts.Root)
                    ? opts.Root
                    : Path.Combine(env.ContentRootPath, opts.Root))
            };
        });

        return services;
    }
}
