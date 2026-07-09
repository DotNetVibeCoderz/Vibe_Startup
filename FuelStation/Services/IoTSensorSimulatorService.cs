using System.Collections.Concurrent;
using System.Diagnostics;
using FuelStation.Data;
using FuelStation.Models;
using Microsoft.EntityFrameworkCore;

namespace FuelStation.Services;

/// <summary>
/// Background service that simulates IoT sensor readings for fuel station tanks.
/// Generates periodic volume, temperature, pressure readings and detects leaks.
/// </summary>
public class IoTSensorSimulatorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IoTSensorSimulatorService> _logger;
    private readonly IConfiguration _config;

    private bool _isRunning;
    private int _totalReadings;
    private DateTime? _lastReadingTime;
    private readonly ConcurrentBag<double> _recentReadingTimestamps = new();

    // ───── Public Properties ─────

    /// <summary>Whether the simulator is currently running and generating readings.</summary>
    public bool IsRunning => _isRunning;

    /// <summary>Total number of sensor readings generated since start.</summary>
    public int TotalReadings => _totalReadings;

    /// <summary>Timestamp of the most recent sensor reading.</summary>
    public DateTime? LastReadingTime => _lastReadingTime;

    /// <summary>Calculated readings per minute based on recent activity.</summary>
    public double ReadingsPerMinute
    {
        get
        {
            // Purge timestamps older than 60 seconds
            var cutoff = DateTime.UtcNow.AddSeconds(-60);
            while (_recentReadingTimestamps.TryPeek(out var ts) && ts < cutoff.Ticks)
                _recentReadingTimestamps.TryTake(out _);
            return _recentReadingTimestamps.Count;
        }
    }

    // ───── Constructor ─────

    public IoTSensorSimulatorService(
        IServiceScopeFactory scopeFactory,
        ILogger<IoTSensorSimulatorService> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _config = config;
    }

    // ───── BackgroundService Loop ─────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _config.GetValue<bool>("IoTSimulator:Enabled", false);
        if (!enabled)
        {
            _logger.LogInformation("🛑 IoT Sensor Simulator is disabled in configuration (IoTSimulator:Enabled)");
            return;
        }

        var intervalMs = _config.GetValue<int>("IoTSimulator:IntervalMs", 10_000);

        _logger.LogInformation("🛰️ IoT Sensor Simulator started — interval: {Interval}ms", intervalMs);
        _isRunning = true;

        while (!stoppingToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                await GenerateSensorReadings(stoppingToken);
                await Task.Delay(intervalMs, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IoT Sensor Simulator encountered an error");
            }
        }

        _isRunning = false;
        _logger.LogInformation("🛑 IoT Sensor Simulator stopped. Total readings: {Total}", _totalReadings);
    }

    // ───── Core Reading Generation ─────

    /// <summary>
    /// Generates a sensor reading for every active tank in the database.
    /// </summary>
    private async Task GenerateSensorReadings(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tanks = await db.Tanks
            .Include(t => t.FuelProduct)
            .Include(t => t.FuelStation)
            .Where(t => t.IsActive)
            .ToListAsync(ct);

        if (tanks.Count == 0)
        {
            _logger.LogDebug("No active tanks found — skipping sensor cycle");
            return;
        }

        var rng = new Random();
        var now = DateTime.UtcNow;
        var readingsToAdd = new List<TankReading>();
        var alertsToAdd = new List<EmergencyAlert>();

        foreach (var tank in tanks)
        {
            // ── Volume: slight decrease to simulate consumption ──
            var consumptionPercent = rng.NextDouble() * 0.005; // 0% – 0.5% of capacity
            var consumed = (decimal)((double)tank.CapacityLiters * consumptionPercent);
            // Occasionally consumption can be slightly higher (rush hour simulation)
            if (rng.Next(10) == 0)
                consumed *= 3m;
            var newVolume = Math.Max(0, tank.CurrentVolumeLiters - consumed);

            // ── Temperature: 25–35 °C ──
            var temperature = Math.Round(25.0 + rng.NextDouble() * 10.0, 2);

            // ── Pressure: 0.95–1.05 bar ──
            var pressure = Math.Round(0.95 + rng.NextDouble() * 0.10, 3);

            // ── Leak detection: 1% chance ──
            var isLeakDetected = rng.Next(100) == 0;
            if (tank.IsLeakDetected)
                isLeakDetected = true; // persist existing leak

            // ── Build reading ──
            var reading = new TankReading
            {
                Id = Guid.NewGuid(),
                TankId = tank.Id,
                VolumeLiters = newVolume,
                TemperatureCelsius = temperature,
                PressureBar = pressure,
                IsLeakDetected = isLeakDetected,
                ReadingTime = now,
                CreatedAt = now
            };
            readingsToAdd.Add(reading);

            // ── Update tank live properties ──
            tank.CurrentVolumeLiters = newVolume;
            tank.TemperatureCelsius = temperature;
            tank.PressureBar = pressure;
            tank.IsLeakDetected = isLeakDetected;
            tank.LastSensorReading = now;
            tank.UpdatedAt = now;

            // ── Emergency alerts ──
            if (isLeakDetected && !tank.IsLeakDetected) // newly detected leak
            {
                alertsToAdd.Add(new EmergencyAlert
                {
                    Id = Guid.NewGuid(),
                    AlertType = "Leak",
                    Message = $"⚠️ Kebocoran terdeteksi pada tangki {tank.Name} ({tank.FuelProduct?.Name ?? "N/A"}) di {tank.FuelStation?.Name ?? "N/A"}! Volume saat ini: {newVolume:N0} L",
                    TankId = tank.Id,
                    FuelStationId = tank.FuelStationId,
                    IsResolved = false,
                    CreatedAt = now
                });
                _logger.LogWarning("⚠️ LEAK detected on tank {TankName} ({TankId}) — volume={Volume}L", tank.Name, tank.Id, newVolume);
            }

            if (newVolume <= tank.MinThresholdLiters)
            {
                alertsToAdd.Add(new EmergencyAlert
                {
                    Id = Guid.NewGuid(),
                    AlertType = "Critical",
                    Message = $"🔴 Stok kritis tangki {tank.Name} ({tank.FuelProduct?.Name ?? "N/A"}): {newVolume:N0} L (threshold: {tank.MinThresholdLiters:N0} L)",
                    TankId = tank.Id,
                    FuelStationId = tank.FuelStationId,
                    IsResolved = false,
                    CreatedAt = now
                });
                _logger.LogWarning("🔴 CRITICAL stock on tank {TankName} — {Volume}L below threshold {Threshold}L",
                    tank.Name, newVolume, tank.MinThresholdLiters);
            }
        }

        // ── Persist to database ──
        db.TankReadings.AddRange(readingsToAdd);

        if (alertsToAdd.Count > 0)
            db.EmergencyAlerts.AddRange(alertsToAdd);

        await db.SaveChangesAsync(ct);

        // ── Update metrics ──
        _totalReadings += readingsToAdd.Count;
        _lastReadingTime = now;
        foreach (var _ in readingsToAdd)
            _recentReadingTimestamps.Add(now.Ticks);

        // ── Audit log ──
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = "IoTReading",
            EntityName = "Tank",
            UserName = "IoTSensorSimulator",
            NewValues = $"Generated {readingsToAdd.Count} readings, {alertsToAdd.Count} alerts",
            CreatedAt = now
        });
        await db.SaveChangesAsync(ct);

        _logger.LogDebug("📡 IoT cycle complete: {Readings} readings, {Alerts} alerts @ {Time}",
            readingsToAdd.Count, alertsToAdd.Count, now.ToString("HH:mm:ss"));
    }

    // ───── Public API Methods ─────

    /// <summary>Start the simulator manually.</summary>
    public void Start()
    {
        if (_isRunning)
        {
            _logger.LogInformation("IoT Sensor Simulator is already running");
            return;
        }
        _isRunning = true;
        _logger.LogInformation("▶️ IoT Sensor Simulator started manually");
    }

    /// <summary>Stop the simulator manually.</summary>
    public void Stop()
    {
        if (!_isRunning)
        {
            _logger.LogInformation("IoT Sensor Simulator is already stopped");
            return;
        }
        _isRunning = false;
        _logger.LogInformation("⏸️ IoT Sensor Simulator stopped manually");
    }

    /// <summary>
    /// Retrieves the most recent sensor readings from the database.
    /// </summary>
    /// <param name="count">Number of readings to retrieve (default 50).</param>
    public async Task<List<TankReading>> GetLatestReadings(int count = 50)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.TankReadings
            .Include(r => r.Tank)
                .ThenInclude(t => t!.FuelProduct)
            .OrderByDescending(r => r.ReadingTime)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>
    /// Manually triggers a leak event on the specified tank.
    /// </summary>
    /// <param name="tankId">The ID of the tank to flag as leaking.</param>
    public async Task GenerateLeakEvent(Guid tankId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tank = await db.Tanks
            .Include(t => t.FuelProduct)
            .Include(t => t.FuelStation)
            .FirstOrDefaultAsync(t => t.Id == tankId);

        if (tank == null)
        {
            _logger.LogWarning("GenerateLeakEvent: Tank {TankId} not found", tankId);
            return;
        }

        var now = DateTime.UtcNow;

        tank.IsLeakDetected = true;
        tank.LastSensorReading = now;
        tank.UpdatedAt = now;

        var alert = new EmergencyAlert
        {
            Id = Guid.NewGuid(),
            AlertType = "Leak",
            Message = $"⚠️ [MANUAL] Kebocoran disimulasikan pada tangki {tank.Name} ({tank.FuelProduct?.Name ?? "N/A"}) di {tank.FuelStation?.Name ?? "N/A"}. Volume: {tank.CurrentVolumeLiters:N0} L",
            TankId = tank.Id,
            FuelStationId = tank.FuelStationId,
            IsResolved = false,
            CreatedAt = now
        };

        db.EmergencyAlerts.Add(alert);

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = "ManualLeakEvent",
            EntityName = "Tank",
            EntityId = tankId,
            UserName = "IoTSensorSimulator",
            NewValues = $"Manual leak triggered for {tank.Name}",
            CreatedAt = now
        });

        await db.SaveChangesAsync();

        _logger.LogWarning("⚠️ Manual leak event generated for tank {TankName} ({TankId})", tank.Name, tankId);
    }

    /// <summary>
    /// Resets all active tanks to their full capacity and clears leak flags.
    /// </summary>
    public async Task ResetTankLevels()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tanks = await db.Tanks
            .Where(t => t.IsActive)
            .ToListAsync();

        var now = DateTime.UtcNow;

        foreach (var tank in tanks)
        {
            tank.CurrentVolumeLiters = tank.CapacityLiters;
            tank.IsLeakDetected = false;
            tank.TemperatureCelsius = null;
            tank.PressureBar = null;
            tank.LastSensorReading = null;
            tank.UpdatedAt = now;
        }

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = "ResetTankLevels",
            EntityName = "Tank",
            UserName = "IoTSensorSimulator",
            NewValues = $"Reset {tanks.Count} tanks to full capacity",
            CreatedAt = now
        });

        await db.SaveChangesAsync();

        _logger.LogInformation("🔄 Reset {Count} tanks to full capacity", tanks.Count);
    }
}
