using HolySafar.Data;
using Microsoft.EntityFrameworkCore;

namespace HolySafar.Services;

/// <summary>
/// Simulator GPS untuk mensimulasikan pergerakan jamaah di sekitar Masjidil Haram
/// </summary>
public class GpsSimulatorService : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private Timer? _timer;
    private bool _isRunning;

    // Masjidil Haram center
    private const double CenterLat = 21.4225;
    private const double CenterLng = 39.8262;
    private static readonly Random _rng = new();

    public bool IsRunning => _isRunning;

    public GpsSimulatorService(IServiceScopeFactory scopeFactory, IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _config = config;
    }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;
        var interval = _config.GetValue<int>("AppSettings:SimulatorIntervalMs", 2000);
        _timer = new Timer(SimulateTick, null, 0, interval);
    }

    public void Stop()
    {
        _isRunning = false;
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private async void SimulateTick(object? state)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Ambil jamaah yang statusnya InTransit atau Arrived atau InHotel
            var jamaahList = await db.Jamaah
                .Where(j => j.StatusKeberangkatan == Models.DepartureStatus.InTransit ||
                            j.StatusKeberangkatan == Models.DepartureStatus.Arrived ||
                            j.StatusKeberangkatan == Models.DepartureStatus.InHotel)
                .ToListAsync();

            foreach (var j in jamaahList)
            {
                // Gerakkan secara random di sekitar masjidil haram (radius ~500m = ~0.005 derajat)
                var offsetLat = (_rng.NextDouble() - 0.5) * 0.01;
                var offsetLng = (_rng.NextDouble() - 0.5) * 0.01;

                j.Latitude = Math.Round(CenterLat + offsetLat, 6);
                j.Longitude = Math.Round(CenterLng + offsetLng, 6);
                j.LastLocationUpdate = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
        }
        catch
        {
            // Ignore errors during simulation
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
