using System.Collections.Concurrent;
using Ngibrid.Data;
using Ngibrid.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ngibrid.Services;

/// <summary>
/// GPS Simulator - runs on background thread to simulate real-time courier movement
/// </summary>
public class GpsSimulatorService : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GpsSimulatorService> _logger;
    private readonly ConcurrentDictionary<long, SimulationState> _simulations = new();
    private readonly int _updateIntervalMs;
    private readonly double _speedKmh;

    public GpsSimulatorService(IConfiguration config, IServiceScopeFactory scopeFactory, ILogger<GpsSimulatorService> logger)
    {
        _config = config; _scopeFactory = scopeFactory; _logger = logger;
        _updateIntervalMs = config.GetValue<int>("GPS:Simulator:UpdateIntervalMs", 5000);
        _speedKmh = config.GetValue<double>("GPS:Simulator:SpeedKmh", 40);
    }

    public void StartSimulation(long orderId, double startLat, double startLng, double endLat, double endLng)
    {
        _simulations[orderId] = new SimulationState { OrderId = orderId, StartLat = startLat, StartLng = startLng, EndLat = endLat, EndLng = endLng, CurrentLat = startLat, CurrentLng = startLng, StartedAt = DateTime.UtcNow };
    }

    public void StopSimulation(long orderId) { _simulations.TryRemove(orderId, out _); }

    public Dictionary<long, (double Lat, double Lng)> GetActivePositions()
    {
        return _simulations.ToDictionary(s => s.Key, s => (s.Value.CurrentLat, s.Value.CurrentLng));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue<bool>("GPS:Simulator:Enabled", true)) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await UpdateSimulationsAsync(); } catch (Exception ex) { _logger.LogError(ex, "GPS sim error"); }
            await Task.Delay(_updateIntervalMs, stoppingToken);
        }
    }

    private async Task UpdateSimulationsAsync()
    {
        if (_simulations.IsEmpty) return;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NgibridDbContext>();
        var elapsedHours = _updateIntervalMs / 3600000.0;
        var distancePerTick = _speedKmh * elapsedHours;
        var rng = new Random();

        foreach (var (orderId, state) in _simulations)
        {
            var totalDist = Math.Max(
                RouteOptimizationService.HaversineDistance(state.StartLat, state.StartLng, state.EndLat, state.EndLng),
                0.001);
            var remaining = RouteOptimizationService.HaversineDistance(state.CurrentLat, state.CurrentLng, state.EndLat, state.EndLng);

            if (remaining < 0.1)
            {
                state.CurrentLat = state.EndLat; state.CurrentLng = state.EndLng;
                var order = await db.Orders.FindAsync(orderId);
                if (order != null && order.Status == "IN_TRANSIT") { order.Status = "OUT_FOR_DELIVERY"; }
                StopSimulation(orderId);
                continue;
            }

            var fraction = Math.Min(distancePerTick / totalDist, 0.3);
            state.CurrentLat += (state.EndLat - state.CurrentLat) * fraction;
            state.CurrentLng += (state.EndLng - state.CurrentLng) * fraction;
            state.CurrentLat += (rng.NextDouble() - 0.5) * 0.0001;
            state.CurrentLng += (rng.NextDouble() - 0.5) * 0.0001;

            db.ShipmentTrackings.Add(new ShipmentTracking
            {
                OrderId = orderId,
                Latitude = Math.Round(state.CurrentLat, 6),
                Longitude = Math.Round(state.CurrentLng, 6),
                SpeedKmh = _speedKmh + rng.NextDouble() * 10,
                Heading = rng.NextDouble() * 360,
                EventType = "GPS_UPDATE",
                Timestamp = DateTime.UtcNow
            });

            // The parcel and the courier carrying it are at the same place, so move the courier
            // too — otherwise the fleet map on /courier stays frozen while tracking animates.
            var assignment = await db.Orders
                .Where(o => o.Id == orderId && o.AssignedCourierId != null)
                .Select(o => o.AssignedCourierId!.Value)
                .FirstOrDefaultAsync();
            if (assignment != 0)
            {
                var profile = await db.CourierProfiles.FirstOrDefaultAsync(c => c.UserId == assignment);
                if (profile != null)
                {
                    profile.CurrentLatitude = Math.Round(state.CurrentLat, 6);
                    profile.CurrentLongitude = Math.Round(state.CurrentLng, 6);
                    profile.LastLocationUpdate = DateTime.UtcNow;
                }
            }
        }
        await db.SaveChangesAsync();
    }

    private class SimulationState
    {
        public long OrderId { get; set; }
        public double StartLat { get; set; }
        public double StartLng { get; set; }
        public double EndLat { get; set; }
        public double EndLng { get; set; }
        public double CurrentLat { get; set; }
        public double CurrentLng { get; set; }
        public DateTime StartedAt { get; set; }
    }
}

/// <summary>
/// IoT Sensor Simulator - simulates temperature/humidity sensors
/// </summary>
public class IotSimulatorService : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IotSimulatorService> _logger;
    private readonly int _updateIntervalMs;
    private readonly Random _rng = new();

    public IotSimulatorService(IConfiguration config, IServiceScopeFactory scopeFactory, ILogger<IotSimulatorService> logger)
    {
        _config = config; _scopeFactory = scopeFactory; _logger = logger;
        _updateIntervalMs = config.GetValue<int>("IoT:Simulator:SensorUpdateIntervalMs", 10000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue<bool>("IoT:Simulator:Enabled", true)) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await UpdateSensorsAsync(); } catch (Exception ex) { _logger.LogError(ex, "IoT sim error"); }
            await Task.Delay(_updateIntervalMs, stoppingToken);
        }
    }

    private async Task UpdateSensorsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NgibridDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<NotificationService>();

        var items = await db.InventoryItems.Include(i => i.Section)
            .Where(i => i.Section != null && i.Section.SectionType == "COLD").Take(50).ToListAsync();
        if (items.Count == 0) return;

        var minTemp = _config.GetValue("IoT:Simulator:TemperatureRange:Min", -10.0);
        var maxTemp = _config.GetValue("IoT:Simulator:TemperatureRange:Max", 35.0);
        var alerts = new List<string>();

        foreach (var item in items)
        {
            var isFreezer = item.Section!.Name.Contains("FREEZER", StringComparison.OrdinalIgnoreCase);
            var baseTemp = isFreezer ? -5.0 : 4.0;

            // Occasional excursion so the alerting path is actually exercised.
            var excursion = _rng.NextDouble() < 0.03 ? _rng.NextDouble() * 12 : 0;

            item.CurrentTemperature = Math.Round(
                Math.Clamp(baseTemp + (_rng.NextDouble() - 0.5) * 4 + excursion, minTemp, maxTemp), 1);
            item.CurrentHumidity = Math.Round(
                Math.Clamp(40 + _rng.NextDouble() * 50, _config.GetValue("IoT:Simulator:HumidityRange:Min", 30.0),
                    _config.GetValue("IoT:Simulator:HumidityRange:Max", 90.0)), 1);
            item.LastSensorReading = DateTime.UtcNow;

            var threshold = isFreezer ? 0.0 : 8.0;
            if (item.CurrentTemperature > threshold)
                alerts.Add($"{item.Name} ({item.Sku}) di {item.Section.Name}: {item.CurrentTemperature}°C");
        }

        await db.SaveChangesAsync();

        if (alerts.Count > 0)
        {
            _logger.LogWarning("Cold-chain temperature alert on {Count} item(s)", alerts.Count);

            // Notify warehouse staff so a cold-chain excursion is visible in the app, not just the log.
            var staffIds = await db.Users
                .Where(u => u.UserType == "WarehouseStaff" || u.UserType == "Manager")
                .Select(u => u.Id)
                .ToListAsync();

            foreach (var staffId in staffIds)
            {
                await notifications.SendAsync(staffId, "⚠️ Suhu cold storage melewati batas",
                    string.Join("; ", alerts.Take(3)), "WARNING", "/warehouse");
            }
        }
    }
}

/// <summary>
/// Smart locker telemetry simulator — heartbeats, battery drain, and auto-expiry of uncollected
/// parcels. Runs on its own background thread like the GPS and IoT simulators.
/// </summary>
public class SmartLockerSimulatorService : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SmartLockerSimulatorService> _logger;
    private readonly Random _rng = new();
    private readonly int _updateIntervalMs;

    public SmartLockerSimulatorService(IConfiguration config, IServiceScopeFactory scopeFactory,
        ILogger<SmartLockerSimulatorService> logger)
    {
        _config = config; _scopeFactory = scopeFactory; _logger = logger;
        _updateIntervalMs = config.GetValue("IoT:LockerSimulator:UpdateIntervalMs", 30000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue("IoT:LockerSimulator:Enabled", true)) return;

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "Locker sim error"); }
            await Task.Delay(_updateIntervalMs, stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NgibridDbContext>();

        var lockers = await db.SmartLockers.Where(l => !l.IsDeleted).ToListAsync(ct);
        if (lockers.Count == 0) return;

        foreach (var locker in lockers)
        {
            locker.LastHeartbeat = DateTime.UtcNow;
            locker.TemperatureCelsius = Math.Round(24 + (_rng.NextDouble() - 0.5) * 8, 1);

            var battery = locker.BatteryPercent ?? 100;
            battery -= _rng.NextDouble() * 0.4;
            if (battery < 15) battery = 100; // serviced / recharged
            locker.BatteryPercent = Math.Round(battery, 1);

            // Rare offline blip, recovered on the next tick.
            locker.Status = locker.Status == "MAINTENANCE"
                ? "MAINTENANCE"
                : _rng.NextDouble() < 0.02 ? "OFFLINE" : "ONLINE";
        }

        // Parcels past their hold window go back to the branch.
        var expired = await db.LockerCompartments
            .Where(c => c.Status == "OCCUPIED" && c.ExpiresAt != null && c.ExpiresAt < DateTime.UtcNow)
            .ToListAsync(ct);

        foreach (var compartment in expired)
        {
            compartment.Status = "EMPTY";
            compartment.AccessPin = null;
            compartment.OrderId = null;
            compartment.ExpiresAt = null;
        }

        if (expired.Count > 0)
            _logger.LogInformation("{Count} locker parcel(s) expired and were returned", expired.Count);

        await db.SaveChangesAsync(ct);
    }
}
