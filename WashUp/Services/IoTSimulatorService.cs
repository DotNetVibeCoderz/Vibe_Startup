using WashUp.Data;
using WashUp.Models;
using Microsoft.EntityFrameworkCore;

namespace WashUp.Services;

/// <summary>
/// Background service that simulates IoT device readings.
/// Can be started/stopped via API. Runs on a separate thread.
/// </summary>
public class IoTSimulatorService
{
    private readonly IServiceProvider _serviceProvider;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private readonly object _lock = new();

    public bool IsRunning => _isRunning;

    public IoTSimulatorService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Start the IoT simulator on a background thread
    /// </summary>
    public async Task StartAsync()
    {
        lock (_lock)
        {
            if (_isRunning) return;
            _cts = new CancellationTokenSource();
            _isRunning = true;
        }

        await Task.Run(async () =>
        {
            var random = new Random();
            while (!_cts!.Token.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var devices = await db.IoTDevices.Where(d => d.IsSimulated && d.IsActive).ToListAsync();

                    foreach (var device in devices)
                    {
                        var reading = device.DeviceType switch
                        {
                            "MesinCuci" => new IoTSensorReading
                            {
                                IoTDeviceId = device.Id,
                                Value = random.Next(800, 1500), // RPM
                                Unit = "RPM",
                                SecondaryValue = random.Next(30, 60), // Temperature
                                SecondaryUnit = "Celsius",
                                Status = random.Next(10) < 1 ? "Warning" : "Normal",
                                Timestamp = DateTime.UtcNow
                            },
                            "Listrik" => new IoTSensorReading
                            {
                                IoTDeviceId = device.Id,
                                Value = Math.Round(random.NextDouble() * 3 + 0.5, 2), // kWh
                                Unit = "kWh",
                                SecondaryValue = Math.Round(random.NextDouble() * 220 + 210, 1), // Voltage
                                SecondaryUnit = "Volt",
                                Status = "Normal",
                                Timestamp = DateTime.UtcNow
                            },
                            "Air" => new IoTSensorReading
                            {
                                IoTDeviceId = device.Id,
                                Value = Math.Round(random.NextDouble() * 50 + 10, 1), // Liter
                                Unit = "Liter",
                                SecondaryValue = Math.Round(random.NextDouble() * 3 + 1, 1), // Pressure
                                SecondaryUnit = "Bar",
                                Status = "Normal",
                                Timestamp = DateTime.UtcNow
                            },
                            "SensorSuhu" => new IoTSensorReading
                            {
                                IoTDeviceId = device.Id,
                                Value = Math.Round(random.NextDouble() * 10 + 25, 1), // Celsius
                                Unit = "Celsius",
                                SecondaryValue = Math.Round(random.NextDouble() * 20 + 60, 1), // Humidity
                                SecondaryUnit = "%RH",
                                Status = "Normal",
                                Timestamp = DateTime.UtcNow
                            },
                            _ => null
                        };

                        if (reading != null)
                        {
                            db.IoTSensorReadings.Add(reading);
                            device.LastReadingAt = DateTime.UtcNow;
                            device.Status = device.DeviceType == "MesinCuci" ? "Running" : "Online";
                        }
                    }

                    await db.SaveChangesAsync();

                    // Jaga tabel tetap ringan: buang pembacaan lebih tua dari 1 jam
                    var cutoff = DateTime.UtcNow.AddHours(-1);
                    await db.IoTSensorReadings.Where(r => r.Timestamp < cutoff).ExecuteDeleteAsync();

                    await Task.Delay(5000, _cts.Token); // Read every 5 seconds
                }
                catch (TaskCanceledException) { break; }
                catch { /* Log and continue */ }
            }
        }, _cts.Token);
    }

    /// <summary>
    /// Stop the IoT simulator
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning) return;
            _cts?.Cancel();
            _isRunning = false;
        }
    }

    /// <summary>
    /// Toggle simulator on/off
    /// </summary>
    public async Task ToggleAsync()
    {
        if (_isRunning)
            Stop();
        else
            await StartAsync();
    }
}
