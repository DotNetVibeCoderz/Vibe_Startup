using System.Collections.Concurrent;
using System.Diagnostics;
using FuelStation.Data;
using FuelStation.Models;
using Microsoft.EntityFrameworkCore;

namespace FuelStation.Services;

/// <summary>
/// Background simulator that generates automated transactions, simulates vehicles,
/// and performs stress testing for the fuel station system.
/// </summary>
public class SimulatorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SimulatorService> _logger;
    private readonly IConfiguration _config;
    private readonly ConcurrentDictionary<string, SimulatedVehicle> _vehicles = new();
    private readonly ConcurrentBag<SimulationLog> _logs = new();
    private readonly ConcurrentBag<StressTestResult> _stressResults = new();
    private bool _isRunning;
    private int _totalTransactions;
    private int _transactionsSinceLast;
    private DateTime _lastMetricTime = DateTime.UtcNow;

    // Metrics
    public bool IsRunning => _isRunning;
    public int TotalTransactions => _totalTransactions;
    public IReadOnlyCollection<SimulatedVehicle> Vehicles => _vehicles.Values.ToList().AsReadOnly();
    public IReadOnlyCollection<SimulationLog> Logs => _logs.ToList().AsReadOnly();
    public IReadOnlyCollection<StressTestResult> StressResults => _stressResults.ToList().AsReadOnly();
    public double AverageResponseTimeMs { get; private set; }
    public int TransactionsPerSecond { get; private set; }

    public SimulatorService(IServiceScopeFactory scopeFactory, ILogger<SimulatorService> logger, IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _config = config;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _config.GetValue<bool>("Simulator:Enabled", false);
        _isRunning = enabled;

        if (enabled)
        {
            _logger.LogInformation("🚗 Fuel Station Simulator started (auto-enabled)");
        }
        else
        {
            _logger.LogInformation("Simulator is disabled by default, waiting for manual start...");
        }

        var interval = _config.GetValue<int>("Simulator:IntervalMs", 5000);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_isRunning)
            {
                await Task.Delay(1000, stoppingToken);
                continue;
            }

            try
            {
                await SimulateVehicleArrival();
                await ProcessVehicles();
                UpdateThroughputMetric();
                await Task.Delay(interval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Simulator error");
            }
        }
    }

    /// <summary>
    /// Simulate a vehicle arriving at the station
    /// </summary>
    private async Task SimulateVehicleArrival()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var stations = await db.FuelStations.Where(s => s.IsActive).ToListAsync();
        var products = await db.FuelProducts.Where(p => p.IsActive).ToListAsync();
        var rng = new Random();

        if (!stations.Any() || !products.Any()) return;

        var station = stations[rng.Next(stations.Count)];
        var product = products[rng.Next(products.Count)];
        var liters = Math.Round((decimal)(rng.NextDouble() * 30 + 5), 2);

        var vehicle = new SimulatedVehicle
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            LicensePlate = GeneratePlate(rng),
            FuelType = product.Code,
            RequestedLiters = liters,
            ArrivalTime = DateTime.UtcNow,
            Status = "Arrived",
            StationName = station.Name
        };

        _vehicles[vehicle.Id] = vehicle;
        _logs.Add(new SimulationLog
        {
            Time = DateTime.UtcNow,
            Message = $"🚗 {vehicle.LicensePlate} arrived at {station.Name}, requesting {liters}L {product.Name}",
            Type = "Arrival"
        });

        _logger.LogInformation("Vehicle {Plate} arrived", vehicle.LicensePlate);
    }

    /// <summary>
    /// Process vehicles through fueling stages
    /// </summary>
    private async Task ProcessVehicles()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var vehicle in _vehicles.Values.Where(v => v.Status == "Arrived"))
        {
            vehicle.Status = "Fueling";
            _logs.Add(new SimulationLog
            {
                Time = DateTime.UtcNow,
                Message = $"⛽ {vehicle.LicensePlate} started fueling",
                Type = "Fueling"
            });

            // Simulate fueling time (2-5 seconds)
            var fuelingTime = new Random().Next(2000, 5000);
            await Task.Delay(fuelingTime);

            // Create actual transaction
            await CreateSimulatedTransaction(db, vehicle);

            vehicle.Status = "Completed";
            vehicle.CompletionTime = DateTime.UtcNow;

            _logs.Add(new SimulationLog
            {
                Time = DateTime.UtcNow,
                Message = $"✅ {vehicle.LicensePlate} completed fueling and left",
                Type = "Completed"
            });

            Interlocked.Increment(ref _totalTransactions);
            Interlocked.Increment(ref _transactionsSinceLast);
        }

        // Clean up completed vehicles older than 5 minutes
        var toRemove = _vehicles.Values
            .Where(v => v.Status == "Completed" && v.CompletionTime.HasValue
                && (DateTime.UtcNow - v.CompletionTime.Value).TotalMinutes > 5)
            .Select(v => v.Id)
            .ToList();

        foreach (var id in toRemove)
            _vehicles.TryRemove(id, out _);
    }

    /// <summary>
    /// Create a transaction in the database from simulated data
    /// </summary>
    private async Task CreateSimulatedTransaction(AppDbContext db, SimulatedVehicle vehicle)
    {
        var station = await db.FuelStations.FirstOrDefaultAsync(s => s.Name == vehicle.StationName);
        var product = await db.FuelProducts.FirstOrDefaultAsync(p => p.Code == vehicle.FuelType);

        if (station == null || product == null) return;

        var rng = new Random();
        var paymentMethods = new[] { "Cash", "QRIS", "EWallet", "DebitCard", "BankTransfer" };
        var subtotal = vehicle.RequestedLiters * product.PricePerLiter;
        var discount = rng.Next(5) == 0 ? Math.Round(subtotal * 0.05m, 2) : 0;

        var tx = new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionNumber = $"SIM-{DateTime.UtcNow:yyyyMMddHHmmss}-{vehicle.Id}",
            TransactionDate = DateTime.UtcNow,
            FuelStationId = station.Id,
            TotalAmount = subtotal,
            Discount = discount,
            GrandTotal = subtotal - discount,
            PaymentMethod = paymentMethods[rng.Next(paymentMethods.Length)],
            Status = "Completed",
            Notes = $"[SIMULATED] Vehicle: {vehicle.LicensePlate}"
        };

        tx.TransactionDetails.Add(new TransactionDetail
        {
            Id = Guid.NewGuid(),
            TransactionId = tx.Id,
            FuelProductId = product.Id,
            Liters = vehicle.RequestedLiters,
            PricePerLiter = product.PricePerLiter,
            Subtotal = subtotal
        });

        db.Transactions.Add(tx);

        // Update tank
        var tank = await db.Tanks
            .FirstOrDefaultAsync(t => t.FuelProductId == product.Id && t.FuelStationId == station.Id);
        if (tank != null)
        {
            tank.CurrentVolumeLiters = Math.Max(0, tank.CurrentVolumeLiters - vehicle.RequestedLiters);
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Run a stress test with parallel transactions
    /// </summary>
    public async Task<StressTestResult> RunStressTest(int concurrentOrders, int totalOrders)
    {
        var sw = Stopwatch.StartNew();
        var completed = 0;
        var failed = 0;
        var responseTimes = new ConcurrentBag<double>();
        var rng = new Random();

        var tasks = new List<Task>();
        var semaphore = new SemaphoreSlim(concurrentOrders);

        for (int i = 0; i < totalOrders; i++)
        {
            await semaphore.WaitAsync();
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var orderSw = Stopwatch.StartNew();
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var stations = await db.FuelStations.Where(s => s.IsActive).ToListAsync();
                    var products = await db.FuelProducts.Where(p => p.IsActive).ToListAsync();

                    if (stations.Any() && products.Any())
                    {
                        var tx = new Transaction
                        {
                            Id = Guid.NewGuid(),
                            TransactionNumber = $"STRESS-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}".Substring(0, 25),
                            TransactionDate = DateTime.UtcNow,
                            FuelStationId = stations[rng.Next(stations.Count)].Id,
                            TotalAmount = 50000m,
                            GrandTotal = 50000m,
                            PaymentMethod = "Cash",
                            Status = "Completed"
                        };

                        tx.TransactionDetails.Add(new TransactionDetail
                        {
                            Id = Guid.NewGuid(),
                            TransactionId = tx.Id,
                            FuelProductId = products[rng.Next(products.Count)].Id,
                            Liters = (decimal)(rng.NextDouble() * 30 + 5),
                            PricePerLiter = 10000m,
                            Subtotal = 50000m
                        });

                        db.Transactions.Add(tx);
                        await db.SaveChangesAsync();
                    }

                    orderSw.Stop();
                    responseTimes.Add(orderSw.Elapsed.TotalMilliseconds);
                    Interlocked.Increment(ref completed);
                }
                catch
                {
                    Interlocked.Increment(ref failed);
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        var result = new StressTestResult
        {
            TestTime = DateTime.UtcNow,
            TotalOrders = totalOrders,
            ConcurrentOrders = concurrentOrders,
            Completed = completed,
            Failed = failed,
            TotalDurationMs = sw.Elapsed.TotalMilliseconds,
            AverageResponseMs = responseTimes.Any() ? responseTimes.Average() : 0,
            MinResponseMs = responseTimes.Any() ? responseTimes.Min() : 0,
            MaxResponseMs = responseTimes.Any() ? responseTimes.Max() : 0,
            OrdersPerSecond = sw.Elapsed.TotalSeconds > 0 ? completed / sw.Elapsed.TotalSeconds : 0
        };

        _stressResults.Add(result);
        AverageResponseTimeMs = result.AverageResponseMs;
        TransactionsPerSecond = (int)result.OrdersPerSecond;

        _logger.LogInformation("Stress test completed: {Completed}/{Total} in {Duration}ms", completed, totalOrders, sw.Elapsed.TotalMilliseconds);

        return result;
    }

    private static string GeneratePlate(Random rng)
    {
        var letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        return $"B {rng.Next(1000, 9999)} {letters[rng.Next(26)]}{letters[rng.Next(26)]}{letters[rng.Next(26)]}";
    }

    private void UpdateThroughputMetric()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastMetricTime).TotalSeconds;
        if (elapsed < 1) return;

        var count = Interlocked.Exchange(ref _transactionsSinceLast, 0);
        TransactionsPerSecond = (int)Math.Round(count / elapsed);
        _lastMetricTime = now;
    }

    public void Start() { _isRunning = true; }
    public void Stop() { _isRunning = false; }
    public void ClearLogs() { _logs.Clear(); }
    public void ClearVehicles() { _vehicles.Clear(); }
}

/// <summary>
/// Simulated vehicle model
/// </summary>
public class SimulatedVehicle
{
    public string Id { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
    public string FuelType { get; set; } = string.Empty;
    public decimal RequestedLiters { get; set; }
    public DateTime ArrivalTime { get; set; }
    public DateTime? CompletionTime { get; set; }
    public string Status { get; set; } = "Arrived"; // Arrived, Fueling, Completed
    public string StationName { get; set; } = string.Empty;
    public string? AssignedPump { get; set; }
    public string? OperatorName { get; set; }
}

/// <summary>
/// Log entry for simulation events
/// </summary>
public class SimulationLog
{
    public DateTime Time { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = "Info";
}

/// <summary>
/// Stress test benchmark result
/// </summary>
public class StressTestResult
{
    public DateTime TestTime { get; set; }
    public int TotalOrders { get; set; }
    public int ConcurrentOrders { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public double TotalDurationMs { get; set; }
    public double AverageResponseMs { get; set; }
    public double MinResponseMs { get; set; }
    public double MaxResponseMs { get; set; }
    public double OrdersPerSecond { get; set; }
}
