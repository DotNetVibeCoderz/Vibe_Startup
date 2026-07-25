using Microsoft.EntityFrameworkCore;
using Ngibrid.Data;

namespace Ngibrid.Services;

/// <summary>
/// AI route optimization for courier delivery runs.
///
/// Nearest-neighbour construction followed by 2-opt improvement — the standard heuristic pair for
/// vehicle routing at this scale. It reaches within a few percent of optimal for the 10-40 stop
/// routes a courier actually runs, and finishes in milliseconds, unlike exact TSP solving which is
/// factorial in the number of stops.
/// </summary>
public class RouteOptimizationService
{
    private readonly IConfiguration _config;
    private readonly NgibridDbContext _db;

    public RouteOptimizationService(IConfiguration config, NgibridDbContext db)
    { _config = config; _db = db; }

    /// <summary>
    /// Order the stops into an efficient run starting from <paramref name="start"/>.
    /// The returned list begins with the start point.
    /// </summary>
    public Task<List<RouteStop>> OptimizeRouteAsync((double Lat, double Lng) start,
        List<RouteStop> stops)
    {
        if (stops.Count <= 1) return Task.FromResult(stops.ToList());

        var maxIterations = _config.GetValue("AI:RouteOptimization:MaxIterations", 1000);

        // ─── Nearest neighbour ───
        var route = new List<RouteStop>
        {
            new() { Latitude = start.Lat, Longitude = start.Lng, Label = "Start" }
        };
        var remaining = stops.ToList();

        while (remaining.Count > 0)
        {
            var last = route[^1];
            var nearest = remaining
                .OrderBy(s => HaversineDistance(last.Latitude, last.Longitude, s.Latitude, s.Longitude))
                .First();
            route.Add(nearest);
            remaining.Remove(nearest);
        }

        // ─── 2-opt: reverse segments while that shortens the tour ───
        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var improved = false;

            for (var a = 1; a < route.Count - 1; a++)
            {
                for (var b = a + 1; b < route.Count; b++)
                {
                    // Compare the two edges that would be swapped. The tour is open (no return leg),
                    // so the edge after b only exists when b is not the final stop.
                    var beforeA = route[a - 1];
                    var atA = route[a];
                    var atB = route[b];

                    var current = Distance(beforeA, atA);
                    var candidate = Distance(beforeA, atB);

                    if (b + 1 < route.Count)
                    {
                        var afterB = route[b + 1];
                        current += Distance(atB, afterB);
                        candidate += Distance(atA, afterB);
                    }

                    if (candidate < current - 1e-9)
                    {
                        route.Reverse(a, b - a + 1);
                        improved = true;
                    }
                }
            }

            if (!improved) break;
        }

        for (var i = 0; i < route.Count; i++) route[i].Sequence = i;
        return Task.FromResult(route);
    }

    /// <summary>
    /// Build and persist an optimized schedule for a courier's pending deliveries on a given date.
    /// Returns the ordered stops with cumulative distance and ETA.
    /// </summary>
    public async Task<RoutePlan> PlanCourierRouteAsync(long courierProfileId, DateTime date,
        double startLat = -6.2088, double startLng = 106.8456)
    {
        var schedules = await _db.DeliverySchedules
            .Include(s => s.Order)
            .Where(s => s.CourierProfileId == courierProfileId
                        && s.ScheduledDate.Date == date.Date
                        && s.Status != "COMPLETED")
            .ToListAsync();

        var stops = schedules
            .Where(s => s.Order != null)
            .Select(s =>
            {
                // Province-qualified so "Bandung" resolves to the kota or the kabupaten seat as
                // recorded, not to whichever one happened to be indexed first — the two are ~15 km
                // apart and pairs like Kota/Kabupaten Sorong sit in different provinces entirely.
                var (lat, lng) = CityCoordinates.Resolve(s.Order!.RecipientProvince, s.Order.RecipientCity);
                return new RouteStop
                {
                    OrderId = s.OrderId,
                    ScheduleId = s.Id,
                    Label = s.Order.RecipientName,
                    City = s.Order.RecipientCity ?? "",
                    Province = s.Order.RecipientProvince ?? "",
                    // Seat coordinate plus a deterministic per-order offset (~±1 km). Without it every
                    // stop in the same city lands on the identical pixel, so the route map shows one
                    // marker for ten deliveries and the optimiser sees zero distance between them.
                    Latitude = lat + Jitter(s.OrderId, 7),
                    Longitude = lng + Jitter(s.OrderId, 13)
                };
            })
            .ToList();

        var optimized = await OptimizeRouteAsync((startLat, startLng), stops);

        var plan = new RoutePlan { Stops = optimized };
        var avgSpeed = _config.GetValue("GPS:Simulator:SpeedKmh", 40.0);
        var serviceMinutes = _config.GetValue("AI:RouteOptimization:ServiceMinutesPerStop", 8.0);

        double cumulativeKm = 0;
        var clock = date.Date.AddHours(8); // shift starts 08:00

        for (var i = 1; i < optimized.Count; i++)
        {
            var leg = Distance(optimized[i - 1], optimized[i]);
            cumulativeKm += leg;
            clock = clock.AddHours(leg / Math.Max(avgSpeed, 1)).AddMinutes(serviceMinutes);

            optimized[i].DistanceFromPreviousKm = Math.Round(leg, 2);
            optimized[i].CumulativeDistanceKm = Math.Round(cumulativeKm, 2);
            optimized[i].EstimatedArrival = clock;
        }

        plan.TotalDistanceKm = Math.Round(cumulativeKm, 2);
        plan.EstimatedDurationMinutes = (int)(clock - date.Date.AddHours(8)).TotalMinutes;

        // A naive run visits stops in creation order; report what optimization saved.
        var naiveKm = NaiveDistance((startLat, startLng), stops);
        plan.NaiveDistanceKm = Math.Round(naiveKm, 2);
        plan.SavingsPercent = naiveKm > 0
            ? Math.Round((naiveKm - cumulativeKm) / naiveKm * 100, 1)
            : 0;

        // Persist the sequence so the courier page shows the optimized order.
        foreach (var stop in optimized.Where(s => s.ScheduleId.HasValue))
        {
            var schedule = schedules.First(s => s.Id == stop.ScheduleId);
            schedule.SequenceNumber = stop.Sequence;
            schedule.EstimatedDistanceKm = stop.CumulativeDistanceKm;
            schedule.EstimatedDeliveryTime = stop.EstimatedArrival;
        }
        if (schedules.Count > 0) await _db.SaveChangesAsync();

        return plan;
    }

    private static double NaiveDistance((double Lat, double Lng) start, List<RouteStop> stops)
    {
        if (stops.Count == 0) return 0;
        double total = 0;
        var (lat, lng) = start;
        foreach (var stop in stops)
        {
            total += HaversineDistance(lat, lng, stop.Latitude, stop.Longitude);
            lat = stop.Latitude;
            lng = stop.Longitude;
        }
        return total;
    }

    private static double Distance(RouteStop a, RouteStop b) =>
        HaversineDistance(a.Latitude, a.Longitude, b.Latitude, b.Longitude);

    /// <summary>
    /// Stable pseudo-offset in degrees for spreading same-city stops apart. Derived from the id,
    /// so a stop keeps the same position across reloads instead of jumping around the map.
    /// </summary>
    private static double Jitter(long id, int salt)
    {
        var h = (int)((id * 2654435761L + salt * 40503L) & 0x7FFFFFFF);
        return (h % 2001 - 1000) / 100000.0; // ±0.01° ≈ ±1.1 km
    }

    /// <summary>Great-circle distance in kilometres.</summary>
    public static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    public class RouteStop
    {
        public long? OrderId { get; set; }
        public long? ScheduleId { get; set; }
        public string Label { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Sequence { get; set; }
        public double DistanceFromPreviousKm { get; set; }
        public double CumulativeDistanceKm { get; set; }
        public DateTime? EstimatedArrival { get; set; }
    }

    public class RoutePlan
    {
        public List<RouteStop> Stops { get; set; } = new();
        public double TotalDistanceKm { get; set; }
        public double NaiveDistanceKm { get; set; }
        public double SavingsPercent { get; set; }
        public int EstimatedDurationMinutes { get; set; }
    }
}
