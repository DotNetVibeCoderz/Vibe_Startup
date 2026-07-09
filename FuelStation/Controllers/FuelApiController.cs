using FuelStation.Data;
using FuelStation.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FuelStation.Controllers;

/// <summary>
/// REST API for external integrations (IoT sensors, pumps, third-party systems)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class FuelApiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<FuelApiController> _logger;

    public FuelApiController(AppDbContext db, ILogger<FuelApiController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get all fuel stations
    /// </summary>
    [HttpGet("stations")]
    public async Task<IActionResult> GetStations()
    {
        var stations = await _db.FuelStations
            .Where(s => s.IsActive)
            .Select(s => new { s.Id, s.Name, s.Code, s.Address, s.Phone })
            .ToListAsync();
        return Ok(stations);
    }

    /// <summary>
    /// Get all fuel products
    /// </summary>
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts()
    {
        var products = await _db.FuelProducts
            .Where(p => p.IsActive)
            .Select(p => new { p.Id, p.Name, p.Code, p.PricePerLiter, p.FuelType })
            .ToListAsync();
        return Ok(products);
    }

    /// <summary>
    /// Get real-time tank readings
    /// </summary>
    [HttpGet("tanks/{stationId}")]
    public async Task<IActionResult> GetTankReadings(Guid stationId)
    {
        var tanks = await _db.Tanks
            .Where(t => t.FuelStationId == stationId && t.IsActive)
            .Include(t => t.FuelProduct)
            .Select(t => new
            {
                t.Id, t.Name, t.TankNumber,
                t.CapacityLiters, t.CurrentVolumeLiters, t.MinThresholdLiters,
                FillPercentage = Math.Round((double)(t.CurrentVolumeLiters / t.CapacityLiters * 100), 1),
                t.TemperatureCelsius, t.PressureBar,
                t.IsLeakDetected, t.LastSensorReading,
                ProductName = t.FuelProduct != null ? t.FuelProduct.Name : null
            })
            .ToListAsync();

        return Ok(tanks);
    }

    /// <summary>
    /// Update tank sensor reading (for IoT integration)
    /// </summary>
    [HttpPost("tanks/{tankId}/reading")]
    public async Task<IActionResult> UpdateTankReading(Guid tankId, [FromBody] TankReadingDto dto)
    {
        var tank = await _db.Tanks.FindAsync(tankId);
        if (tank == null) return NotFound("Tank not found");

        // Update current volume
        tank.CurrentVolumeLiters = dto.VolumeLiters;
        tank.TemperatureCelsius = dto.TemperatureCelsius;
        tank.PressureBar = dto.PressureBar;
        tank.IsLeakDetected = dto.IsLeakDetected;
        tank.LastSensorReading = DateTime.UtcNow;

        // Record reading history
        _db.TankReadings.Add(new TankReading
        {
            Id = Guid.NewGuid(),
            TankId = tankId,
            VolumeLiters = dto.VolumeLiters,
            TemperatureCelsius = dto.TemperatureCelsius,
            PressureBar = dto.PressureBar,
            IsLeakDetected = dto.IsLeakDetected,
            ReadingTime = DateTime.UtcNow
        });

        // Check for alerts
        if (dto.IsLeakDetected)
        {
            _db.EmergencyAlerts.Add(new EmergencyAlert
            {
                Id = Guid.NewGuid(),
                AlertType = "Leak",
                Message = $"Kebocoran terdeteksi pada tangki {tank.Name}!",
                TankId = tankId,
                FuelStationId = tank.FuelStationId,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (tank.CurrentVolumeLiters <= tank.MinThresholdLiters)
        {
            _db.EmergencyAlerts.Add(new EmergencyAlert
            {
                Id = Guid.NewGuid(),
                AlertType = "Warning",
                Message = $"Stok menipis pada tangki {tank.Name}! Tersisa {tank.CurrentVolumeLiters}L",
                TankId = tankId,
                FuelStationId = tank.FuelStationId,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Tank {TankId} reading updated: {Volume}L", tankId, dto.VolumeLiters);

        return Ok(new { success = true, tank.CurrentVolumeLiters });
    }

    /// <summary>
    /// Create a new transaction (for automated systems/pump integration)
    /// </summary>
    [HttpPost("transactions")]
    public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionDto dto)
    {
        var product = await _db.FuelProducts.FindAsync(dto.FuelProductId);
        if (product == null) return NotFound("Product not found");

        var station = await _db.FuelStations.FindAsync(dto.FuelStationId);
        if (station == null) return NotFound("Station not found");

        var txNumber = $"TRX-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}".Substring(0, 25);
        var subtotal = dto.Liters * product.PricePerLiter;
        var discount = dto.CustomerId.HasValue ? Math.Round(subtotal * 0.02m, 2) : 0;
        var total = subtotal - discount;

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionNumber = txNumber,
            TransactionDate = DateTime.UtcNow,
            FuelStationId = dto.FuelStationId,
            CustomerId = dto.CustomerId,
            TotalAmount = subtotal,
            Discount = discount,
            GrandTotal = total,
            PaymentMethod = dto.PaymentMethod ?? "Cash",
            Status = "Completed",
            PaymentReference = dto.PaymentReference
        };

        transaction.TransactionDetails.Add(new TransactionDetail
        {
            Id = Guid.NewGuid(),
            TransactionId = transaction.Id,
            FuelProductId = product.Id,
            Liters = dto.Liters,
            PricePerLiter = product.PricePerLiter,
            Subtotal = subtotal
        });

        // Update tank volume
        var tank = await _db.Tanks
            .FirstOrDefaultAsync(t => t.FuelProductId == dto.FuelProductId && t.FuelStationId == dto.FuelStationId);
        if (tank != null)
        {
            tank.CurrentVolumeLiters = Math.Max(0, tank.CurrentVolumeLiters - dto.Liters);
        }

        _db.Transactions.Add(transaction);
        await _db.SaveChangesAsync();

        return Ok(new { transaction.Id, transaction.TransactionNumber, transaction.GrandTotal });
    }

    /// <summary>
    /// Get daily transaction summary
    /// </summary>
    [HttpGet("summary/daily")]
    public async Task<IActionResult> GetDailySummary([FromQuery] DateTime? date)
    {
        var targetDate = date?.Date ?? DateTime.UtcNow.Date;
        var nextDay = targetDate.AddDays(1);

        var summary = await _db.Transactions
            .Where(t => t.TransactionDate >= targetDate && t.TransactionDate < nextDay && t.Status == "Completed")
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalTransactions = g.Count(),
                TotalRevenue = g.Sum(t => t.GrandTotal),
                TotalLiters = g.Sum(t => t.TransactionDetails.Sum(d => d.Liters)),
                AverageTransaction = g.Average(t => t.GrandTotal)
            })
            .FirstOrDefaultAsync();

        return Ok(summary ?? new { TotalTransactions = 0, TotalRevenue = 0m, TotalLiters = 0m, AverageTransaction = 0m });
    }

    /// <summary>
    /// Get emergency alerts
    /// </summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> GetAlerts([FromQuery] bool unresolvedOnly = true)
    {
        var query = _db.EmergencyAlerts.AsQueryable();
        if (unresolvedOnly)
            query = query.Where(a => !a.IsResolved);

        var alerts = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .ToListAsync();

        return Ok(alerts);
    }
}

// DTOs
public class TankReadingDto
{
    public decimal VolumeLiters { get; set; }
    public double? TemperatureCelsius { get; set; }
    public double? PressureBar { get; set; }
    public bool IsLeakDetected { get; set; }
}

public class CreateTransactionDto
{
    public Guid FuelStationId { get; set; }
    public Guid FuelProductId { get; set; }
    public decimal Liters { get; set; }
    public Guid? CustomerId { get; set; }
    public string? PaymentMethod { get; set; }
    public string? PaymentReference { get; set; }
}
