using Microsoft.EntityFrameworkCore;
using Ngibrid.Data;
using Ngibrid.Models;

namespace Ngibrid.Services;

/// <summary>
/// Real-time tracking service with GPS data management
/// </summary>
public class TrackingService
{
    private readonly NgibridDbContext _db;
    private readonly GpsSimulatorService _gpsSim;

    public TrackingService(NgibridDbContext db, GpsSimulatorService gpsSim)
    {
        _db = db;
        _gpsSim = gpsSim;
    }

    /// <summary>
    /// Add a tracking point
    /// </summary>
    public async Task<ShipmentTracking> AddTrackingPointAsync(long orderId, double lat, double lng, 
        double? speed = null, double? heading = null, string? description = null, long? courierId = null)
    {
        var point = new ShipmentTracking
        {
            OrderId = orderId,
            Latitude = lat,
            Longitude = lng,
            SpeedKmh = speed,
            Heading = heading,
            LocationDescription = description,
            CourierId = courierId,
            EventType = "GPS_UPDATE"
        };
        _db.ShipmentTrackings.Add(point);
        await _db.SaveChangesAsync();
        return point;
    }

    /// <summary>
    /// Get tracking history for an order
    /// </summary>
    public async Task<List<ShipmentTracking>> GetTrackingHistoryAsync(long orderId, int take = 100) =>
        await _db.ShipmentTrackings
            .Where(t => t.OrderId == orderId)
            .OrderByDescending(t => t.Timestamp)
            .Take(take)
            .OrderBy(t => t.Timestamp)
            .ToListAsync();

    /// <summary>
    /// Resolve a tracking number (or order number) to its order id.
    /// Used by the SignalR hub so clients can subscribe with the number printed on the label.
    /// </summary>
    public async Task<long?> ResolveOrderIdAsync(string trackingNumber)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber)) return null;

        // Accept a raw order id too, so callers that already have one need no lookup.
        if (long.TryParse(trackingNumber, out var directId) &&
            await _db.Orders.AnyAsync(o => o.Id == directId))
            return directId;

        var key = trackingNumber.Trim();
        return await _db.Orders
            .Where(o => o.TrackingNumber == key || o.OrderNumber == key)
            .Select(o => (long?)o.Id)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Get latest position for an order
    /// </summary>
    public async Task<ShipmentTracking?> GetLatestPositionAsync(long orderId) =>
        await _db.ShipmentTrackings
            .Where(t => t.OrderId == orderId)
            .OrderByDescending(t => t.Timestamp)
            .FirstOrDefaultAsync();

    /// <summary>
    /// Start simulating tracking for an order
    /// </summary>
    public void StartSimulation(long orderId, double startLat, double startLng, 
        double endLat, double endLng)
    {
        _gpsSim.StartSimulation(orderId, startLat, startLng, endLat, endLng);
    }

    /// <summary>
    /// Stop simulation for an order
    /// </summary>
    public void StopSimulation(long orderId) => _gpsSim.StopSimulation(orderId);

    /// <summary>
    /// Get active simulations
    /// </summary>
    public Dictionary<long, (double Lat, double Lng)> GetActiveSimulations() => _gpsSim.GetActivePositions();
}

/// <summary>
/// Dynamic pricing service
/// </summary>
public class DynamicPricingService
{
    private readonly IConfiguration _config;
    private readonly NgibridDbContext _db;

    public DynamicPricingService(IConfiguration config, NgibridDbContext db)
    {
        _config = config;
        _db = db;
    }

    /// <summary>
    /// Calculate a shipping tariff.
    ///
    /// Tariffs follow the Indonesian courier model: a flat first-kilogram fare that scales with
    /// the distance zone, plus a cheaper rate for each additional kilogram. Multiplying
    /// distance × rate × weight (the previous formula) produced millions of rupiah for an
    /// ordinary intercity parcel, so the zone model is used instead.
    ///
    /// On top of the base tariff sit the dynamic factors the requirement asks for:
    /// service level, peak hour, weekend, and live demand.
    /// </summary>
    public async Task<PriceResult> CalculatePriceAsync(string originCity, string destCity,
        double weightKg, string serviceType = "REG",
        string? originProvince = null, string? destProvince = null)
    {
        var distance = EstimateDistance(originProvince, originCity, destProvince, destCity);
        var chargeableWeight = Math.Max(Math.Ceiling(weightKg), 1); // billed per started kg

        var (firstKgFare, nextKgFare) = GetZoneFare(distance);

        decimal price = firstKgFare + nextKgFare * (decimal)(chargeableWeight - 1);

        // ─── Service level ───
        price *= serviceType.ToUpperInvariant() switch
        {
            "EXP" => 1.8m,
            "SAMEDAY" => 3.0m,
            "ECO" => 0.8m,
            _ => 1.0m
        };

        // ─── Time-based demand ───
        var now = DateTime.UtcNow;
        if (IsPeakHour(now))
            price *= _config.GetValue<decimal>("AI:DynamicPricing:PeakHourMultiplier", 1.3m);
        if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            price *= _config.GetValue<decimal>("AI:DynamicPricing:WeekendMultiplier", 1.2m);

        // ─── Live network load ───
        var today = now.Date;
        var tomorrow = today.AddDays(1);
        var activeOrders = await _db.Orders.CountAsync(o =>
            o.Status == "IN_TRANSIT" && o.CreatedAt >= today && o.CreatedAt < tomorrow);
        if (activeOrders > 100) price *= 1.15m;

        price = Math.Round(price, 0);

        var insuranceRate = _config.GetValue<decimal>("Shipment:InsuranceRate", 0.02m);
        var insuranceFee = Math.Round(price * insuranceRate, 0);

        return new PriceResult
        {
            BasePrice = price,
            InsuranceFee = insuranceFee,
            TotalPrice = price + insuranceFee,
            EstimatedDistanceKm = Math.Round(distance, 0),
            ServiceType = serviceType
        };
    }

    /// <summary>
    /// Distance zones and their first-/next-kilogram fares, mirroring how domestic couriers price.
    /// </summary>
    private (decimal FirstKg, decimal NextKg) GetZoneFare(double distanceKm)
    {
        var baseFare = _config.GetValue<decimal>("AI:DynamicPricing:BaseFare", 9000);
        var perKm = _config.GetValue<decimal>("AI:DynamicPricing:RatePerKm", 22);

        // First kg = flat base + a small distance component; each extra kg costs ~60% of that.
        var firstKg = baseFare + (decimal)distanceKm * perKm;
        return (Math.Round(firstKg, 0), Math.Round(firstKg * 0.6m, 0));
    }

    /// <summary>
    /// Distance between two kota/kabupaten, resolved against the city master table. Passing the
    /// province matters: "Bandung" alone cannot tell the kota from the kabupaten seat, and the
    /// two produce different tariffs.
    /// </summary>
    private static double EstimateDistance(string? originProvince, string origin,
        string? destProvince, string dest)
    {
        if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(dest))
            return 25;

        var from = CityCoordinates.Resolve(originProvince, origin);
        var to = CityCoordinates.Resolve(destProvince, dest);
        var straight = RouteOptimizationService.HaversineDistance(from.Lat, from.Lng, to.Lat, to.Lng);

        // Same area: both sides resolve to the same seat, so the great-circle distance is zero.
        // Bill it as an average cross-town run.
        if (straight < 1) return 12;

        // An overland leg runs longer than the straight line — 1.3 is the usual detour factor here.
        // Past ~600 km the leg is flown or shipped, and those track much closer to the great circle,
        // so keeping 1.3 would have overcharged every Jakarta–Papua parcel by a third.
        var factor = straight <= 600 ? 1.3 : 1.12;
        return Math.Max(Math.Round(straight * factor, 0), 10);
    }

    private static bool IsPeakHour(DateTime time)
    {
        var hour = time.Hour;
        return hour is >= 7 and <= 9 or >= 17 and <= 19;
    }
}

public class PriceResult
{
    public decimal BasePrice { get; set; }
    public decimal InsuranceFee { get; set; }
    public decimal TotalPrice { get; set; }
    public double EstimatedDistanceKm { get; set; }
    public string ServiceType { get; set; } = "REG";
}
