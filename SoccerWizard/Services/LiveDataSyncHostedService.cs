using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SoccerWizard.Services;

/// <summary>
/// Background service untuk menjalankan sync live data secara terjadwal.
/// </summary>
public class LiveDataSyncHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LiveDataSyncSettings _settings;
    private readonly ILogger<LiveDataSyncHostedService> _logger;
    private DateTime _nextRunUtc = DateTime.UtcNow;

    public LiveDataSyncHostedService(IServiceProvider serviceProvider, LiveDataSyncSettings settings, ILogger<LiveDataSyncHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_settings.EnableBackgroundSync)
                {
                    var interval = TimeSpan.FromMinutes(Math.Max(5, _settings.BackgroundIntervalMinutes));
                    if (DateTime.UtcNow >= _nextRunUtc)
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var matchService = scope.ServiceProvider.GetRequiredService<MatchService>();
                        await matchService.SyncLiveDataAsync(_settings);
                        _nextRunUtc = DateTime.UtcNow.Add(interval);
                    }
                }
                else
                {
                    _nextRunUtc = DateTime.UtcNow.AddMinutes(Math.Max(5, _settings.BackgroundIntervalMinutes));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background live data sync failed.");
                _nextRunUtc = DateTime.UtcNow.AddMinutes(Math.Max(5, _settings.BackgroundIntervalMinutes));
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
