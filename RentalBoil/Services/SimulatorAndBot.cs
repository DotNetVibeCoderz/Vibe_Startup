using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentalBoil.Data;
using RentalBoil.Models;

namespace RentalBoil.Services;

/// <summary>
/// GPS Simulator Service — semua akses database via IServiceScopeFactory (short-lived scope).
/// Fix: tidak menyimpan AppDbContext sebagai field, menghindari ObjectDisposedException
/// saat dipanggil dari background thread (timer, HostedService, dll).
/// </summary>
public class GpsSimulatorService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<GpsSimulatorService> _logger;
    private readonly Random _random = new();

    public GpsSimulatorService(IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory,
        IConfiguration config, ILogger<GpsSimulatorService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Helper: buat scope pendek untuk akses DB. DI-dispose otomatis setelah method selesai.
    /// </summary>
    private async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }

    private async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(db);
    }

    private string UpdateMode => _config.GetValue<string>("GPS:UpdateMode") ?? "DirectDB";
    private string ApiBaseUrl => _config.GetValue<string>("GPS:ApiBaseUrl") ?? "https://localhost:5001";
    private string ApiKey => _config.GetValue<string>("ApiSettings:ApiKey") ?? "rntl-2025-secure-api-key-change-in-production";

    // ═══════════════════════════════════════════
    // GPS SIMULATION
    // ═══════════════════════════════════════════

    public async Task SimulateGpsUpdateAsync(int vehicleId)
    {
        if (UpdateMode == "Api")
            await SimulateGpsUpdateViaApiAsync(vehicleId);
        else
            await SimulateGpsUpdateDirectDbAsync(vehicleId);
    }

    public async Task SimulateBatchGpsUpdateAsync(List<int> vehicleIds)
    {
        if (UpdateMode == "Api")
            await SimulateBatchUpdateViaApiAsync(vehicleIds);
        else
            foreach (var id in vehicleIds) await SimulateGpsUpdateDirectDbAsync(id);
    }

    private async Task SimulateGpsUpdateDirectDbAsync(int vehicleId)
    {
        await WithDbAsync(async db =>
        {
            try
            {
                var vehicle = await db.Vehicles.FindAsync(vehicleId);
                if (vehicle == null || vehicle.MotionStatus != VehicleMotionStatus.Moving) return;

                if (vehicle.Latitude.HasValue && vehicle.Longitude.HasValue)
                {
                    vehicle.Latitude += (_random.NextDouble() - 0.5) * 0.002;
                    vehicle.Longitude += (_random.NextDouble() - 0.5) * 0.002;
                    vehicle.CurrentSpeed = _random.Next(0, 80);
                    vehicle.CurrentHeading = _random.Next(0, 360);
                    vehicle.UpdatedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "GPS DB error vehicle {Id}", vehicleId); }
        });
    }

    private async Task SimulateGpsUpdateViaApiAsync(int vehicleId)
    {
        try
        {
            var vehicle = await WithDbAsync(async db =>
                await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == vehicleId));

            if (vehicle == null || vehicle.MotionStatus != VehicleMotionStatus.Moving) return;

            double lat = (vehicle.Latitude ?? -6.2088) + (_random.NextDouble() - 0.5) * 0.002;
            double lng = (vehicle.Longitude ?? 106.8456) + (_random.NextDouble() - 0.5) * 0.002;

            var body = new
            {
                Latitude = lat, Longitude = lng,
                Speed = (double)_random.Next(0, 80), Heading = (double)_random.Next(0, 360),
                LockStatus = vehicle.LockStatus.ToString(),
                EngineStatus = vehicle.EngineStatus.ToString(),
                MotionStatus = vehicle.MotionStatus.ToString()
            };

            var http = _httpClientFactory.CreateClient("SimulatorClient");
            var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/api/vehicles/{vehicleId}/simulator-update")
            { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
            request.Headers.Add("X-Api-Key", ApiKey);
            var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Simulator API failed vehicle {Id}: {Code}", vehicleId, response.StatusCode);
        }
        catch (HttpRequestException) { await SimulateGpsUpdateDirectDbAsync(vehicleId); }
        catch (Exception ex) { _logger.LogError(ex, "GPS API error vehicle {Id}", vehicleId); }
    }

    private async Task SimulateBatchUpdateViaApiAsync(List<int> vehicleIds)
    {
        try
        {
            var vehicles = await WithDbAsync(async db => await db.Vehicles.AsNoTracking()
                .Where(v => vehicleIds.Contains(v.Id) && v.MotionStatus == VehicleMotionStatus.Moving).ToListAsync());
            if (!vehicles.Any()) return;

            var items = vehicles.Select(v =>
            {
                double lat = (v.Latitude ?? -6.2088) + (_random.NextDouble() - 0.5) * 0.002;
                double lng = (v.Longitude ?? 106.8456) + (_random.NextDouble() - 0.5) * 0.002;
                return new
                {
                    VehicleId = v.Id, Latitude = lat, Longitude = lng,
                    Speed = (double)_random.Next(0, 80), Heading = (double)_random.Next(0, 360),
                    LockStatus = v.LockStatus.ToString(), EngineStatus = v.EngineStatus.ToString(), MotionStatus = v.MotionStatus.ToString()
                };
            }).ToList<object>();

            var http = _httpClientFactory.CreateClient("SimulatorClient");
            var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/api/vehicles/batch/simulator-update")
            { Content = new StringContent(JsonSerializer.Serialize(items), Encoding.UTF8, "application/json") };
            request.Headers.Add("X-Api-Key", ApiKey);
            await http.SendAsync(request);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Batch API failed, falling back to DB"); foreach (var id in vehicleIds) await SimulateGpsUpdateDirectDbAsync(id); }
    }

    // ═══════════════════════════════════════════
    // TRACKING CONTROL
    // ═══════════════════════════════════════════

    public async Task StartTrackingAsync(int vehicleId)
    {
        await WithDbAsync(async db =>
        {
            var v = await db.Vehicles.FindAsync(vehicleId);
            if (v != null) { v.MotionStatus = VehicleMotionStatus.Moving; v.EngineStatus = EngineStatus.On; v.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); }
        });
    }

    public async Task StopTrackingAsync(int vehicleId)
    {
        await WithDbAsync(async db =>
        {
            var v = await db.Vehicles.FindAsync(vehicleId);
            if (v != null) { v.MotionStatus = VehicleMotionStatus.Stopped; v.EngineStatus = EngineStatus.Off; v.CurrentSpeed = 0; v.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); }
        });
    }

    public async Task<LockStatus> ToggleLockAsync(int vehicleId)
    {
        var result = LockStatus.Locked;
        await WithDbAsync(async db =>
        {
            var v = await db.Vehicles.FindAsync(vehicleId);
            if (v != null) { v.LockStatus = v.LockStatus == LockStatus.Locked ? LockStatus.Unlocked : LockStatus.Locked; v.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); result = v.LockStatus; }
        });
        return result;
    }

    public async Task<EngineStatus> ToggleEngineAsync(int vehicleId)
    {
        var result = EngineStatus.Off;
        await WithDbAsync(async db =>
        {
            var v = await db.Vehicles.FindAsync(vehicleId);
            if (v != null) { v.EngineStatus = v.EngineStatus == EngineStatus.Off ? EngineStatus.On : EngineStatus.Off; v.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); result = v.EngineStatus; }
        });
        return result;
    }

    public async Task<object?> GetVehicleGpsStatusAsync(int vehicleId)
    {
        return await WithDbAsync(async db =>
        {
            var v = await db.Vehicles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == vehicleId);
            if (v == null) return null!;
            return (object)new { v.Id, v.Name, v.PlateNumber, v.Latitude, v.Longitude, v.CurrentSpeed, v.CurrentHeading, v.LockStatus, v.EngineStatus, v.MotionStatus, v.Location, v.UpdatedAt, updateMode = UpdateMode };
        });
    }

    public async Task<List<object>> GetActiveVehicleStatusesAsync()
    {
        return await WithDbAsync(async db =>
        {
            var ids = await db.Bookings.Where(b => b.Status == BookingStatus.Active).Select(b => b.VehicleId).ToListAsync();
            var vehicles = await db.Vehicles.AsNoTracking().Where(v => ids.Contains(v.Id))
                .Select(v => new { v.Id, v.Name, v.PlateNumber, v.Latitude, v.Longitude, v.CurrentSpeed, v.CurrentHeading, v.LockStatus, v.EngineStatus, v.MotionStatus, v.Location, v.UpdatedAt })
                .ToListAsync();
            return vehicles.Cast<object>().ToList();
        });
    }
}

// ═══════════════════════════════════════════
// GPS SIMULATOR HOSTED SERVICE
// ═══════════════════════════════════════════

public class GpsSimulatorHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GpsSimulatorHostedService> _logger;
    private readonly IConfiguration _config;

    public GpsSimulatorHostedService(IServiceScopeFactory scopeFactory, ILogger<GpsSimulatorHostedService> logger, IConfiguration config)
    { _scopeFactory = scopeFactory; _logger = logger; _config = config; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue<bool>("GPS:SimulatorEnabled")) { _logger.LogInformation("GPS Simulator disabled"); return; }

        var interval = _config.GetValue<int>("GPS:UpdateIntervalSeconds");
        var mode = _config.GetValue<string>("GPS:UpdateMode") ?? "DirectDB";
        var useBatch = _config.GetValue<bool>("GPS:UseBatchUpdate");
        _logger.LogInformation("GPS Simulator STARTED | Mode: {Mode} | Interval: {Interval}s | Batch: {Batch}", mode, interval, useBatch);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var gpsService = scope.ServiceProvider.GetRequiredService<GpsSimulatorService>();

                var ids = await db.Bookings.Where(b => b.Status == BookingStatus.Active).Select(b => b.VehicleId).ToListAsync(stoppingToken);
                if (ids.Any())
                {
                    if (useBatch && ids.Count > 1) await gpsService.SimulateBatchGpsUpdateAsync(ids);
                    else foreach (var id in ids) await gpsService.SimulateGpsUpdateAsync(id);
                }
            }
            catch (Exception ex) { _logger.LogError(ex, "GPS simulator loop error"); }
            await Task.Delay(TimeSpan.FromSeconds(interval), stoppingToken);
        }
    }
}
