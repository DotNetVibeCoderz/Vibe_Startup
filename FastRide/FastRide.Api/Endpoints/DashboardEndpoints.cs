using FastRide.Api.Security;
using FastRide.Api.Services;
using FastRide.Data;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FastRide.Api.Endpoints;

/// <summary>
/// Analytics behind the admin dashboard.
///
/// Everything here is admin-only and cached briefly: the dashboard polls, and recomputing
/// the same aggregates every few seconds is wasted database work.
/// </summary>
public static class DashboardEndpoints
{
    private static readonly TimeSpan OverviewCacheTtl = TimeSpan.FromSeconds(10);

    /// <summary>Platform's cut of each fare, used by the financial report.</summary>
    private const decimal CommissionRate = 0.20m;

    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization(Policies.AdminOnly);

        group.MapGet("/overview", Overview).WithSummary("Everything the dashboard needs in one call");
        group.MapGet("/stats", Stats);
        group.MapGet("/orders-by-status", OrdersByStatus);
        group.MapGet("/orders-by-hour", OrdersByHour);
        group.MapGet("/revenue-series", RevenueSeries);
        group.MapGet("/top-drivers", TopDrivers);
        group.MapGet("/financial-summary", FinancialSummary);
        group.MapGet("/financial-summary/export.csv", ExportFinancialSummary);

        return api;
    }

    private static async Task<IResult> Overview(FastRideDbContext db, ICacheService cache, CancellationToken ct)
    {
        var overview = await cache.GetOrCreateAsync(CacheKeys.DashboardOverview, OverviewCacheTtl, async token =>
        {
            var stats = await ComputeStatsAsync(db, token);
            var byStatus = await ComputeByStatusAsync(db, token);
            var hourly = await ComputeHourlyAsync(db, DateTime.UtcNow.Date, token);
            var revenue = await ComputeRevenueSeriesAsync(db, 30, token);
            var drivers = await ComputeTopDriversAsync(db, 8, token);
            var categories = await ComputeCategoriesAsync(db, token);
            var methods = await ComputePaymentMethodsAsync(db, DateTime.UtcNow.Date.AddDays(-30), DateTime.UtcNow, token);

            return new DashboardOverviewResponse(stats, byStatus, hourly, revenue, drivers, categories, methods);
        }, ct);

        return Results.Ok(overview);
    }

    private static async Task<IResult> Stats(FastRideDbContext db, ICacheService cache, CancellationToken ct) =>
        Results.Ok(await cache.GetOrCreateAsync(
            CacheKeys.DashboardStats, OverviewCacheTtl, token => ComputeStatsAsync(db, token), ct));

    private static async Task<IResult> OrdersByStatus(FastRideDbContext db, CancellationToken ct) =>
        Results.Ok(await ComputeByStatusAsync(db, ct));

    private static async Task<IResult> OrdersByHour(DateTime? date, FastRideDbContext db, CancellationToken ct) =>
        Results.Ok(await ComputeHourlyAsync(db, (date ?? DateTime.UtcNow).Date, ct));

    private static async Task<IResult> RevenueSeries(int? days, FastRideDbContext db, CancellationToken ct) =>
        Results.Ok(await ComputeRevenueSeriesAsync(db, Math.Clamp(days ?? 30, 1, 365), ct));

    private static async Task<IResult> TopDrivers(int? limit, FastRideDbContext db, CancellationToken ct) =>
        Results.Ok(await ComputeTopDriversAsync(db, Math.Clamp(limit ?? 10, 1, 50), ct));

    private static async Task<IResult> FinancialSummary(
        DateTime? from, DateTime? to, FastRideDbContext db, CancellationToken ct) =>
        Results.Ok(await ComputeFinancialSummaryAsync(db, from, to, ct));

    private static async Task<IResult> ExportFinancialSummary(
        DateTime? from, DateTime? to, FastRideDbContext db, CancellationToken ct)
    {
        var summary = await ComputeFinancialSummaryAsync(db, from, to, ct);

        var csv = CsvExporter.Build(summary.Series,
            ("Tanggal", r => r.Date),
            ("Order", r => r.Orders),
            ("Order selesai", r => r.CompletedOrders),
            ("Pendapatan", r => r.Revenue));

        return Results.File(csv, "text/csv", $"fastride-keuangan-{summary.From:yyyyMMdd}-{summary.To:yyyyMMdd}.csv");
    }

    // ───────────────────────── computations ─────────────────────────

    private static async Task<DashboardStatsResponse> ComputeStatsAsync(FastRideDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Today's order mix in a single grouped query rather than five separate counts.
        var todayBuckets = await db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= today)
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Revenue = g.Sum(o => o.FinalFare) })
            .ToListAsync(ct);

        var ordersToday = todayBuckets.Sum(b => b.Count);
        var completedToday = todayBuckets.Where(b => b.Status == OrderStatus.Completed).Sum(b => b.Count);
        var cancelledToday = todayBuckets.Where(b => b.Status is OrderStatus.Cancelled or OrderStatus.Expired).Sum(b => b.Count);
        var pending = todayBuckets.Where(b => b.Status == OrderStatus.Requested).Sum(b => b.Count);
        var revenueToday = todayBuckets.Where(b => b.Status == OrderStatus.Completed).Sum(b => b.Revenue);

        var driverBuckets = await db.DriverProfiles
            .AsNoTracking()
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var revenueMonth = await db.Orders
            .AsNoTracking()
            .Where(o => o.Status == OrderStatus.Completed && o.CompletedAt >= monthStart)
            .SumAsync(o => (decimal?)o.FinalFare, ct) ?? 0m;

        var averageRating = await db.DriverProfiles
            .AsNoTracking()
            .Where(p => p.RatingCount > 0)
            .AverageAsync(p => (double?)p.Rating, ct) ?? 5.0;

        var totalRiders = await db.Users.AsNoTracking().CountAsync(u => u.Role == UserRole.Rider, ct);
        var totalDrivers = driverBuckets.Sum(b => b.Count);

        // "Active riders" = riders who booked something in the last 30 days.
        var activeRiders = await db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= now.AddDays(-30))
            .Select(o => o.RiderId)
            .Distinct()
            .CountAsync(ct);

        return new DashboardStatsResponse(
            ordersToday,
            completedToday,
            pending,
            driverBuckets.Where(b => b.Status is DriverStatus.Online or DriverStatus.OnTrip).Sum(b => b.Count),
            driverBuckets.Where(b => b.Status == DriverStatus.Online).Sum(b => b.Count),
            activeRiders,
            totalRiders,
            totalDrivers,
            revenueToday,
            revenueMonth,
            completedToday == 0 ? 0m : Math.Round(revenueToday / completedToday, 0),
            Math.Round(averageRating, 2),
            ordersToday == 0 ? 0 : Math.Round(completedToday * 100.0 / ordersToday, 1),
            ordersToday == 0 ? 0 : Math.Round(cancelledToday * 100.0 / ordersToday, 1),
            now);
    }

    private static async Task<List<OrderStatusCount>> ComputeByStatusAsync(FastRideDbContext db, CancellationToken ct)
    {
        // Aggregates are projected into an anonymous type first: EF cannot translate a
        // constructor call inside a GroupBy result selector.
        var counts = await db.Orders
            .AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // Report every status, including the ones with no orders, so the chart legend is stable.
        return Enum.GetValues<OrderStatus>()
            .Select(status => new OrderStatusCount(status, counts.FirstOrDefault(c => c.Status == status)?.Count ?? 0))
            .ToList();
    }

    /// <summary>
    /// One grouped query for the whole day. The previous version issued 24 synchronous
    /// COUNT queries inside a loop — the single slowest endpoint in the API.
    /// </summary>
    private static async Task<List<HourlyStats>> ComputeHourlyAsync(FastRideDbContext db, DateTime day, CancellationToken ct)
    {
        var nextDay = day.AddDays(1);

        var buckets = await db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= day && o.CreatedAt < nextDay)
            .GroupBy(o => o.CreatedAt.Hour)
            .Select(g => new
            {
                Hour = g.Key,
                Count = g.Count(),
                Revenue = g.Sum(o => o.Status == OrderStatus.Completed ? o.FinalFare : 0m)
            })
            .ToListAsync(ct);

        return Enumerable.Range(0, 24)
            .Select(hour =>
            {
                var bucket = buckets.FirstOrDefault(b => b.Hour == hour);
                return new HourlyStats(hour, bucket?.Count ?? 0, bucket?.Revenue ?? 0m);
            })
            .ToList();
    }

    private static async Task<List<RevenuePoint>> ComputeRevenueSeriesAsync(FastRideDbContext db, int days, CancellationToken ct)
    {
        var start = DateTime.UtcNow.Date.AddDays(-(days - 1));

        var rows = await db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= start)
            .Select(o => new { o.CreatedAt, o.Status, o.FinalFare })
            .ToListAsync(ct);

        // Grouped in memory: DATE() translation differs across the four supported providers,
        // and a month of orders is a small set.
        var grouped = rows
            .GroupBy(o => o.CreatedAt.Date)
            .ToDictionary(g => g.Key, g => new
            {
                Orders = g.Count(),
                Completed = g.Count(o => o.Status == OrderStatus.Completed),
                Revenue = g.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.FinalFare)
            });

        return Enumerable.Range(0, days)
            .Select(offset =>
            {
                var date = start.AddDays(offset);
                if (grouped.TryGetValue(date, out var bucket))
                    return new RevenuePoint(date, bucket.Revenue, bucket.Orders, bucket.Completed);

                return new RevenuePoint(date, 0m, 0, 0);
            })
            .ToList();
    }

    private static async Task<List<TopDriverItem>> ComputeTopDriversAsync(FastRideDbContext db, int limit, CancellationToken ct)
    {
        var monthStart = DateTime.UtcNow.Date.AddDays(-30);

        var leaders = await db.Orders
            .AsNoTracking()
            .Where(o => o.Status == OrderStatus.Completed && o.DriverId != null && o.CompletedAt >= monthStart)
            .GroupBy(o => o.DriverId!.Value)
            .Select(g => new { DriverId = g.Key, Trips = g.Count(), Earnings = g.Sum(o => o.FinalFare) })
            .OrderByDescending(g => g.Earnings)
            .Take(limit)
            .ToListAsync(ct);

        if (leaders.Count == 0) return [];

        var driverIds = leaders.Select(l => l.DriverId).ToList();
        var profiles = await db.Users
            .AsNoTracking()
            .Where(u => driverIds.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.PhotoUrl,
                Rating = u.DriverProfile!.Rating,
                u.DriverProfile.VehicleType
            })
            .ToListAsync(ct);

        return leaders
            .Select(leader =>
            {
                var profile = profiles.FirstOrDefault(p => p.Id == leader.DriverId);
                return new TopDriverItem(
                    leader.DriverId,
                    profile?.FullName ?? "Driver",
                    profile?.PhotoUrl,
                    leader.Trips,
                    leader.Earnings,
                    profile?.Rating ?? 0,
                    profile?.VehicleType ?? "-");
            })
            .ToList();
    }

    private static async Task<List<CategoryBreakdownItem>> ComputeCategoriesAsync(FastRideDbContext db, CancellationToken ct)
    {
        var monthStart = DateTime.UtcNow.Date.AddDays(-30);

        var buckets = await db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= monthStart)
            .GroupBy(o => o.VehicleCategory)
            .Select(g => new
            {
                Category = g.Key,
                Orders = g.Count(),
                Revenue = g.Sum(o => o.Status == OrderStatus.Completed ? o.FinalFare : 0m)
            })
            .ToListAsync(ct);

        var totalOrders = buckets.Sum(b => b.Orders);

        return buckets
            .OrderByDescending(b => b.Orders)
            .Select(b => new CategoryBreakdownItem(
                b.Category, b.Orders, b.Revenue,
                totalOrders == 0 ? 0 : Math.Round(b.Orders * 100.0 / totalOrders, 1)))
            .ToList();
    }

    private static async Task<List<PaymentMethodBreakdownItem>> ComputePaymentMethodsAsync(
        FastRideDbContext db, DateTime from, DateTime to, CancellationToken ct)
    {
        var buckets = await db.Payments
            .AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Completed && p.CreatedAt >= from && p.CreatedAt <= to)
            .GroupBy(p => p.Method)
            .Select(g => new { Method = g.Key, Count = g.Count(), Amount = g.Sum(p => p.Amount) })
            .ToListAsync(ct);

        return buckets
            .OrderByDescending(b => b.Amount)
            .Select(b => new PaymentMethodBreakdownItem(b.Method, b.Count, b.Amount))
            .ToList();
    }

    private static async Task<FinancialSummaryResponse> ComputeFinancialSummaryAsync(
        FastRideDbContext db, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var end = (to ?? DateTime.UtcNow).Date.AddDays(1).AddTicks(-1);
        var start = (from ?? end.Date.AddDays(-29)).Date;

        var orders = await db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= start && o.CreatedAt <= end)
            .Select(o => new { o.CreatedAt, o.Status, o.FinalFare, o.EstimatedFare, o.DiscountAmount })
            .ToListAsync(ct);

        var completed = orders.Where(o => o.Status == OrderStatus.Completed).ToList();
        var gross = completed.Sum(o => o.EstimatedFare);
        var discounts = completed.Sum(o => o.DiscountAmount);
        var net = completed.Sum(o => o.FinalFare);
        var commission = Math.Round(net * CommissionRate, 0);

        var series = orders
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new RevenuePoint(
                g.Key,
                g.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.FinalFare),
                g.Count(),
                g.Count(o => o.Status == OrderStatus.Completed)))
            .OrderBy(p => p.Date)
            .ToList();

        var methods = await ComputePaymentMethodsAsync(db, start, end, ct);

        return new FinancialSummaryResponse(
            start, end,
            gross, discounts, net,
            net - commission, commission,
            completed.Count,
            orders.Count(o => o.Status is OrderStatus.Cancelled or OrderStatus.Expired),
            completed.Count == 0 ? 0m : Math.Round(net / completed.Count, 0),
            series,
            methods);
    }
}
