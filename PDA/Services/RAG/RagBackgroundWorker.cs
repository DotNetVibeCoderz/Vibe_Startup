namespace PDA.Services.RAG;

/// <summary>
/// Background worker that periodically scans the KnowledgeBase folder
/// and indexes documents into the vector store.
/// </summary>
public class RagBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RagBackgroundWorker> _logger;

    public RagBackgroundWorker(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<RagBackgroundWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _configuration.GetValue<bool>("RAG:Enabled");
        if (!enabled)
        {
            _logger.LogInformation("RAG indexing is disabled in configuration.");
            return;
        }

        var intervalMinutes = _configuration.GetValue<int>("RAG:ScanIntervalMinutes");
        if (intervalMinutes <= 0) intervalMinutes = 30;

        _logger.LogInformation("RAG Background Worker started. Scan interval: {Interval} minutes.", intervalMinutes);

        // Initial delay to let the app start
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("RAG: Starting knowledge base scan...");
                
                using var scope = _serviceProvider.CreateScope();
                var indexingService = scope.ServiceProvider.GetRequiredService<RagIndexingService>();
                
                await indexingService.ScanAndIndexAsync(stoppingToken);
                
                _logger.LogInformation("RAG: Knowledge base scan completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RAG: Error during knowledge base scan.");
            }

            // Wait for next scan
            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }
    }
}
