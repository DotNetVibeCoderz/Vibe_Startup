using Microsoft.EntityFrameworkCore;
using Ngibrid.Data;
using Ngibrid.Models;

namespace Ngibrid.Services;

/// <summary>
/// Demand forecasting and trend analysis.
///
/// Uses Holt's linear exponential smoothing (level + trend) over daily order counts, multiplied by a
/// day-of-week seasonal index derived from the same history. Chosen over a plain moving average
/// because parcel volume has both a drift and a strong weekly shape; chosen over a heavier ML model
/// because it trains in milliseconds on request and needs no extra dependency.
/// </summary>
public class ForecastService
{
    private readonly NgibridDbContext _db;
    private readonly IConfiguration _config;

    public ForecastService(NgibridDbContext db, IConfiguration config)
    { _db = db; _config = config; }

    private const double Alpha = 0.4; // level smoothing
    private const double Beta = 0.2;  // trend smoothing

    /// <summary>
    /// Forecast daily order volume for the next <paramref name="horizonDays"/> days.
    /// Falls back to a flat average when there is too little history to fit a trend.
    /// </summary>
    public async Task<List<DemandForecast>> ForecastDemandAsync(int horizonDays = 14, int historyDays = 90,
        string? city = null)
    {
        var history = await GetDailyVolumeAsync(historyDays, city);
        var forecasts = new List<DemandForecast>();
        if (history.Count == 0) return forecasts;

        var series = history.Select(h => (double)h.Count).ToList();
        var mean = series.Average();

        double level, trend;
        string method;

        if (series.Count < 7)
        {
            level = mean;
            trend = 0;
            method = "MOVING_AVERAGE";
        }
        else
        {
            (level, trend) = FitHoltLinear(series);
            method = "HOLT_LINEAR";
        }

        var seasonal = ComputeWeeklySeasonality(history);
        var residualStdDev = ComputeResidualStdDev(series, mean);
        var peakThreshold = _config.GetValue<double>("AI:Forecast:PeakSeasonThreshold", 1.25);

        for (var h = 1; h <= horizonDays; h++)
        {
            var date = DateTime.UtcNow.Date.AddDays(h);
            var index = seasonal.GetValueOrDefault(date.DayOfWeek, 1.0);
            var point = Math.Max((level + trend * h) * index, 0);

            // Prediction interval widens with the horizon (random-walk error accumulation).
            var margin = 1.96 * residualStdDev * Math.Sqrt(h);

            forecasts.Add(new DemandForecast
            {
                ForecastDate = date,
                City = city,
                PredictedOrders = Math.Round(point, 1),
                LowerBound = Math.Round(Math.Max(point - margin, 0), 1),
                UpperBound = Math.Round(point + margin, 1),
                SeasonalIndex = Math.Round(index, 3),
                IsPeakSeason = index >= peakThreshold,
                Method = method,
                ConfidencePercent = Math.Round(ConfidenceFromSpread(point, margin), 1)
            });
        }

        return forecasts;
    }

    /// <summary>Persist a forecast run so trends can be compared against what actually happened.</summary>
    public async Task<int> SaveForecastAsync(List<DemandForecast> forecasts)
    {
        if (forecasts.Count == 0) return 0;
        var dates = forecasts.Select(f => f.ForecastDate).ToList();
        var city = forecasts[0].City;

        var stale = await _db.DemandForecasts
            .Where(f => dates.Contains(f.ForecastDate) && f.City == city)
            .ToListAsync();
        _db.DemandForecasts.RemoveRange(stale);

        _db.DemandForecasts.AddRange(forecasts);
        await _db.SaveChangesAsync();
        return forecasts.Count;
    }

    /// <summary>
    /// Month-over-month volume and revenue trend, plus the months that ran hot enough to
    /// count as peak season.
    /// </summary>
    public async Task<TrendAnalysis> AnalyzeTrendAsync(int months = 12)
    {
        var since = DateTime.UtcNow.AddMonths(-months);
        var orders = await _db.Orders
            .Where(o => o.CreatedAt >= since && !o.IsDeleted)
            .Select(o => new { o.CreatedAt, o.TotalAmount, o.RecipientCity, o.Status })
            .ToListAsync();

        var monthly = orders
            .GroupBy(o => new DateTime(o.CreatedAt.Year, o.CreatedAt.Month, 1))
            .Select(g => new MonthlyPoint
            {
                Month = g.Key,
                OrderCount = g.Count(),
                Revenue = g.Sum(o => o.TotalAmount)
            })
            .OrderBy(m => m.Month)
            .ToList();

        var analysis = new TrendAnalysis { Monthly = monthly };
        if (monthly.Count == 0) return analysis;

        var avgVolume = monthly.Average(m => m.OrderCount);
        var peakThreshold = _config.GetValue<double>("AI:Forecast:PeakSeasonThreshold", 1.25);

        foreach (var m in monthly)
            m.SeasonalIndex = avgVolume > 0 ? Math.Round(m.OrderCount / avgVolume, 2) : 1;

        analysis.PeakMonths = monthly
            .Where(m => m.SeasonalIndex >= peakThreshold)
            .Select(m => m.Month)
            .ToList();

        if (monthly.Count >= 2)
        {
            var last = monthly[^1];
            var prev = monthly[^2];
            analysis.VolumeGrowthPercent = prev.OrderCount > 0
                ? Math.Round((last.OrderCount - prev.OrderCount) / (double)prev.OrderCount * 100, 1) : 0;
            analysis.RevenueGrowthPercent = prev.Revenue > 0
                ? Math.Round((double)((last.Revenue - prev.Revenue) / prev.Revenue) * 100, 1) : 0;
        }

        analysis.TopCities = orders
            .Where(o => !string.IsNullOrEmpty(o.RecipientCity))
            .GroupBy(o => o.RecipientCity!)
            .Select(g => new CityVolume { City = g.Key, OrderCount = g.Count(), Revenue = g.Sum(o => o.TotalAmount) })
            .OrderByDescending(c => c.OrderCount)
            .Take(10)
            .ToList();

        return analysis;
    }

    /// <summary>
    /// Cost optimisation hints derived from live data — the operational half of "trend analysis".
    /// </summary>
    public async Task<List<CostInsight>> GetCostInsightsAsync()
    {
        var insights = new List<CostInsight>();
        var since = DateTime.UtcNow.AddDays(-30);

        var orders = await _db.Orders.Where(o => o.CreatedAt >= since && !o.IsDeleted).ToListAsync();
        if (orders.Count == 0) return insights;

        var failed = orders.Count(o => o.Status is "FAILED" or "RETURNED");
        if (failed > 0)
        {
            var rate = Math.Round((double)failed / orders.Count * 100, 1);
            insights.Add(new CostInsight
            {
                Title = "Pengiriman gagal / retur",
                Detail = $"{failed} dari {orders.Count} order ({rate}%) gagal atau diretur dalam 30 hari terakhir.",
                Impact = rate > 5 ? "HIGH" : "MEDIUM",
                Recommendation = "Verifikasi nomor telepon penerima saat input order dan aktifkan notifikasi H-1 pengiriman."
            });
        }

        var volumetricHeavy = orders.Count(o => o.VolumetricWeight > o.WeightKg * 1.5 && o.VolumetricWeight > 0);
        if (volumetricHeavy > 0)
        {
            insights.Add(new CostInsight
            {
                Title = "Paket boros volume",
                Detail = $"{volumetricHeavy} paket punya berat volumetrik >1.5× berat aktual.",
                Impact = "MEDIUM",
                Recommendation = "Gunakan Packaging Optimizer untuk menurunkan ukuran box dan biaya berat volumetrik."
            });
        }

        var ecoShare = orders.Count(o => o.IsEcoDelivery);
        insights.Add(new CostInsight
        {
            Title = "Adopsi eco-delivery",
            Detail = $"{ecoShare} dari {orders.Count} order memakai layanan ramah lingkungan.",
            Impact = ecoShare < orders.Count * 0.2 ? "MEDIUM" : "LOW",
            Recommendation = ecoShare < orders.Count * 0.2
                ? "Tawarkan diskon eco-delivery untuk rute padat guna menekan emisi sekaligus biaya."
                : "Pertahankan; emisi per paket sudah tertekan."
        });

        var peakHourOrders = orders.Count(o => o.CreatedAt.Hour is >= 7 and <= 9 or >= 17 and <= 19);
        if (peakHourOrders > orders.Count * 0.4)
        {
            insights.Add(new CostInsight
            {
                Title = "Konsentrasi order di jam sibuk",
                Detail = $"{Math.Round((double)peakHourOrders / orders.Count * 100, 1)}% order masuk pada jam peak.",
                Impact = "MEDIUM",
                Recommendation = "Geser sebagian pickup ke jam lengang dengan insentif tarif untuk menghindari surcharge peak."
            });
        }

        return insights;
    }

    // ─── internals ───

    private async Task<List<DailyPoint>> GetDailyVolumeAsync(int days, string? city)
    {
        var since = DateTime.UtcNow.Date.AddDays(-days);
        var query = _db.Orders.Where(o => o.CreatedAt >= since && !o.IsDeleted);
        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(o => o.RecipientCity == city);

        var raw = await query
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        var byDate = raw.ToDictionary(r => r.Date, r => r.Count);

        // Fill gaps: a day with no orders is a real zero, not a missing sample.
        var series = new List<DailyPoint>();
        for (var d = since; d <= DateTime.UtcNow.Date; d = d.AddDays(1))
            series.Add(new DailyPoint { Date = d, Count = byDate.GetValueOrDefault(d, 0) });

        return series;
    }

    private static (double Level, double Trend) FitHoltLinear(List<double> series)
    {
        var level = series[0];
        var trend = series[1] - series[0];

        for (var i = 1; i < series.Count; i++)
        {
            var prevLevel = level;
            level = Alpha * series[i] + (1 - Alpha) * (level + trend);
            trend = Beta * (level - prevLevel) + (1 - Beta) * trend;
        }

        return (level, trend);
    }

    private static Dictionary<DayOfWeek, double> ComputeWeeklySeasonality(List<DailyPoint> history)
    {
        var overall = history.Average(h => h.Count);
        if (overall <= 0) return new Dictionary<DayOfWeek, double>();

        return history
            .GroupBy(h => h.Date.DayOfWeek)
            .ToDictionary(g => g.Key, g => Math.Clamp(g.Average(h => h.Count) / overall, 0.3, 3.0));
    }

    private static double ComputeResidualStdDev(List<double> series, double mean)
    {
        if (series.Count < 2) return Math.Max(mean * 0.2, 1);
        var variance = series.Sum(v => Math.Pow(v - mean, 2)) / (series.Count - 1);
        return Math.Max(Math.Sqrt(variance), 0.5);
    }

    private static double ConfidenceFromSpread(double point, double margin)
    {
        if (point <= 0) return 50;
        var relative = margin / point;
        return Math.Clamp(100 - relative * 50, 40, 95);
    }

    public class DailyPoint
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }

    public class MonthlyPoint
    {
        public DateTime Month { get; set; }
        public int OrderCount { get; set; }
        public decimal Revenue { get; set; }
        public double SeasonalIndex { get; set; } = 1;
    }

    public class CityVolume
    {
        public string City { get; set; } = string.Empty;
        public int OrderCount { get; set; }
        public decimal Revenue { get; set; }
    }

    public class TrendAnalysis
    {
        public List<MonthlyPoint> Monthly { get; set; } = new();
        public List<DateTime> PeakMonths { get; set; } = new();
        public List<CityVolume> TopCities { get; set; } = new();
        public double VolumeGrowthPercent { get; set; }
        public double RevenueGrowthPercent { get; set; }
    }

    public class CostInsight
    {
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Impact { get; set; } = "LOW"; // LOW, MEDIUM, HIGH
        public string Recommendation { get; set; } = string.Empty;
    }
}
