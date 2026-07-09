using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using FuelStation.Data;
using FuelStation.Models;

namespace FuelStation.Services;

/// <summary>
/// ML.NET-based prediction service for fuel station sales forecasting,
/// stock depletion estimation, and anomaly detection.
/// Falls back to statistical methods (SMA, IQR) when ML training is not feasible.
/// </summary>
public class MLPredictionService
{
    private readonly MLContext _mlContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MLPredictionService> _logger;

    // Cached model and its training date
    private ITransformer? _revenueModel;
    private ITransformer? _litersModel;
    private DateTime _lastTrainingDate = DateTime.MinValue;
    private int _trainingDataCount;
    private static readonly SemaphoreSlim _trainingLock = new(1, 1);

    public MLPredictionService(
        IServiceScopeFactory scopeFactory,
        ILogger<MLPredictionService> logger)
    {
        _mlContext = new MLContext(seed: 42);
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    // ================================================================
    // PUBLIC METHODS
    // ================================================================

    /// <summary>
    /// Predicts next day's sales based on the last 30 days of data.
    /// Uses SdcaRegression ML model; falls back to SMA if training fails.
    /// </summary>
    public async Task<SalesPrediction> PredictNextDaySales()
    {
        var historicalData = await GetHistoricalSalesAsync(30);

        if (historicalData.Count < 3)
        {
            _logger.LogWarning("Insufficient data for prediction. Returning zero forecast.");
            return new SalesPrediction
            {
                ForecastDate = DateTime.Today.AddDays(1),
                PredictedRevenue = 0,
                PredictedLiters = 0,
                ConfidenceLower = 0,
                ConfidenceUpper = 0
            };
        }

        // Try ML-based prediction first
        try
        {
            await EnsureModelTrainedAsync(historicalData);
            var mlResult = PredictWithMl(DateTime.Today.AddDays(1), historicalData);
            if (mlResult != null)
                return mlResult;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ML prediction failed. Falling back to Simple Moving Average.");
        }

        // Fallback to SMA
        return ForecastWithSimpleMovingAverage(historicalData, 1).First();
    }

    /// <summary>
    /// Predicts when a tank will run out of fuel based on average consumption rate.
    /// Uses TankReadings data to calculate depletion.
    /// </summary>
    public async Task<StockPrediction> PredictStockDepletion(Guid tankId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tank = await db.Tanks
            .Include(t => t.FuelProduct)
            .Include(t => t.TankReadings)
            .FirstOrDefaultAsync(t => t.Id == tankId);

        if (tank == null)
        {
            _logger.LogWarning("Tank {TankId} not found. Returning empty prediction.", tankId);
            return new StockPrediction
            {
                TankId = tankId,
                TankName = "Unknown",
                CurrentLiters = 0,
                DailyConsumptionRate = 0,
                DaysUntilEmpty = int.MaxValue,
                SuggestedReorderDate = DateTime.MaxValue,
                MinThresholdLiters = 0,
                CapacityLiters = 0,
                FuelProductName = "Unknown"
            };
        }

        // Calculate daily consumption rate from readings
        var readings = tank.TankReadings
            .OrderBy(r => r.ReadingTime)
            .ToList();

        float dailyConsumptionRate;

        if (readings.Count >= 2)
        {
            var first = readings.First();
            var last = readings.Last();
            var totalDays = (float)(last.ReadingTime - first.ReadingTime).TotalDays;

            if (totalDays > 0.5)
            {
                var consumed = (float)(first.VolumeLiters - last.VolumeLiters);
                dailyConsumptionRate = Math.Max(0, consumed / totalDays);
            }
            else
            {
                dailyConsumptionRate = 0;
            }
        }
        else
        {
            // No readings — estimate from transaction data
            dailyConsumptionRate = await EstimateConsumptionFromTransactionsAsync(db, tankId);
        }

        var currentLiters = (float)tank.CurrentVolumeLiters;
        var minThreshold = (float)tank.MinThresholdLiters;
        var capacity = (float)tank.CapacityLiters;

        int daysUntilEmpty;
        DateTime suggestedReorderDate;

        if (dailyConsumptionRate > 0.001f)
        {
            daysUntilEmpty = (int)Math.Floor(currentLiters / dailyConsumptionRate);

            // Suggest reorder when it hits min threshold (or at least 3 days before empty)
            var litersUntilReorder = currentLiters - minThreshold;
            var daysUntilReorder = (int)Math.Floor(litersUntilReorder / dailyConsumptionRate);
            suggestedReorderDate = DateTime.Today.AddDays(Math.Max(0, daysUntilReorder));
        }
        else
        {
            daysUntilEmpty = int.MaxValue;
            suggestedReorderDate = DateTime.MaxValue;
        }

        return new StockPrediction
        {
            TankId = tankId,
            TankName = tank.Name,
            CurrentLiters = currentLiters,
            DailyConsumptionRate = dailyConsumptionRate,
            DaysUntilEmpty = daysUntilEmpty,
            SuggestedReorderDate = suggestedReorderDate,
            MinThresholdLiters = minThreshold,
            CapacityLiters = capacity,
            FuelProductName = tank.FuelProduct?.Name ?? "Unknown"
        };
    }

    /// <summary>
    /// Forecasts sales for the next N days using ML regression with SMA fallback.
    /// </summary>
    public async Task<List<SalesPrediction>> GetSalesForecast(int days)
    {
        if (days < 1) days = 1;
        if (days > 90) days = 90; // Cap at 90 days

        var historicalData = await GetHistoricalSalesAsync(Math.Max(30, days));

        if (historicalData.Count < 3)
        {
            _logger.LogWarning("Insufficient data for forecast. Returning empty list.");
            return Enumerable.Range(1, days).Select(d => new SalesPrediction
            {
                ForecastDate = DateTime.Today.AddDays(d),
                PredictedRevenue = 0,
                PredictedLiters = 0,
                ConfidenceLower = 0,
                ConfidenceUpper = 0
            }).ToList();
        }

        // Try ML for forecasts
        try
        {
            await EnsureModelTrainedAsync(historicalData);
            var mlForecasts = new List<SalesPrediction>();
            var workingData = new List<SalesData>(historicalData);

            for (int i = 0; i < days; i++)
            {
                var forecastDate = DateTime.Today.AddDays(i + 1);
                var prediction = PredictWithMl(forecastDate, workingData);
                if (prediction != null)
                {
                    mlForecasts.Add(prediction);
                    // Add prediction back as pseudo-data for recursive forecasting
                    workingData.Add(new SalesData
                    {
                        Date = forecastDate,
                        Revenue = prediction.PredictedRevenue,
                        Liters = prediction.PredictedLiters,
                        TransactionCount = workingData.Average(x => x.TransactionCount)
                    });
                }
                else
                {
                    // ML failed mid-way; break to fallback
                    break;
                }
            }

            if (mlForecasts.Count == days)
                return mlForecasts;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ML forecast failed. Falling back to Simple Moving Average.");
        }

        // Fallback: SMA
        return ForecastWithSimpleMovingAverage(historicalData, days);
    }

    /// <summary>
    /// Detects anomalous sales days using the IQR (Interquartile Range) method.
    /// Flags days where revenue deviates significantly from the rolling median.
    /// </summary>
    public async Task<List<AnomalyResult>> DetectAnomalies()
    {
        var historicalData = await GetHistoricalSalesAsync(60);

        if (historicalData.Count < 7)
        {
            _logger.LogWarning("Insufficient data for anomaly detection (need at least 7 days).");
            return new List<AnomalyResult>();
        }

        var results = new List<AnomalyResult>();

        // Use a 7-day rolling window for expected value (median of surrounding days)
        for (int i = 0; i < historicalData.Count; i++)
        {
            var current = historicalData[i];

            // Find surrounding points (±3 days)
            var windowStart = Math.Max(0, i - 3);
            var windowEnd = Math.Min(historicalData.Count - 1, i + 3);
            var window = historicalData
                .Skip(windowStart)
                .Take(windowEnd - windowStart + 1)
                .Where(x => x.Date != current.Date)
                .Select(x => x.Revenue)
                .ToList();

            if (window.Count < 2)
            {
                results.Add(new AnomalyResult
                {
                    Date = current.Date,
                    ActualValue = current.Revenue,
                    ExpectedValue = current.Revenue,
                    DeviationPercent = 0,
                    IsAnomaly = false,
                    AnomalyType = "Normal"
                });
                continue;
            }

            var expectedValue = window.Average();
            float deviationPercent = 0;

            if (expectedValue > 0.01f)
            {
                deviationPercent = ((current.Revenue - expectedValue) / expectedValue) * 100f;
            }

            // Calculate IQR for the window to set thresholds
            var sorted = window.OrderBy(x => x).ToList();
            int q1Idx = sorted.Count / 4;
            int q3Idx = (3 * sorted.Count) / 4;
            float q1 = sorted[q1Idx];
            float q3 = sorted[Math.Min(q3Idx, sorted.Count - 1)];
            float iqr = q3 - q1;
            float lowerBound = q1 - 1.5f * iqr;
            float upperBound = q3 + 1.5f * iqr;

            bool isAnomaly = current.Revenue < lowerBound || current.Revenue > upperBound;
            string anomalyType = "Normal";
            if (isAnomaly)
            {
                anomalyType = current.Revenue > upperBound ? "Spike" : "Drop";
            }

            results.Add(new AnomalyResult
            {
                Date = current.Date,
                ActualValue = current.Revenue,
                ExpectedValue = expectedValue,
                DeviationPercent = (float)Math.Round(deviationPercent, 1),
                IsAnomaly = isAnomaly,
                AnomalyType = anomalyType
            });
        }

        return results;
    }

    // ================================================================
    // PRIVATE: DATA RETRIEVAL
    // ================================================================

    /// <summary>
    /// Retrieves aggregated daily sales from the database.
    /// </summary>
    private async Task<List<SalesData>> GetHistoricalSalesAsync(int days)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow.AddDays(-days);

        var dailySales = await db.Transactions
            .Where(t => t.TransactionDate >= cutoff && t.Status == "Completed" && !t.IsDeleted)
            .GroupBy(t => t.TransactionDate.Date)
            .Select(g => new SalesData
            {
                Date = g.Key,
                Revenue = (float)g.Sum(t => t.GrandTotal),
                Liters = (float)g.Sum(t => t.TransactionDetails.Sum(d => d.Liters)),
                TransactionCount = (float)g.Count()
            })
            .OrderBy(s => s.Date)
            .ToListAsync();

        return dailySales;
    }

    /// <summary>
    /// Estimates daily consumption from transaction data when tank readings are unavailable.
    /// </summary>
    private async Task<float> EstimateConsumptionFromTransactionsAsync(AppDbContext db, Guid tankId)
    {
        var tank = await db.Tanks
            .Include(t => t.FuelProduct)
            .FirstOrDefaultAsync(t => t.Id == tankId);

        if (tank?.FuelProduct == null)
            return 0;

        var productId = tank.FuelProductId;

        // Sum liters sold for this product in last 7 days
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        var totalLiters = await db.TransactionDetails
            .Where(td => td.FuelProductId == productId
                         && td.Transaction!.TransactionDate >= sevenDaysAgo
                         && td.Transaction.Status == "Completed"
                         && !td.Transaction.IsDeleted)
            .SumAsync(td => (decimal?)td.Liters) ?? 0;

        return (float)totalLiters / 7f;
    }

    // ================================================================
    // PRIVATE: ML TRAINING & PREDICTION
    // ================================================================

    /// <summary>
    /// Ensures the ML model is trained with the latest data.
    /// Re-trains only if data has changed significantly.
    /// </summary>
    private async Task EnsureModelTrainedAsync(List<SalesData> historicalData)
    {
        // Skip if model is fresh enough (same data count, trained today)
        if (_revenueModel != null && _litersModel != null
            && _trainingDataCount == historicalData.Count
            && _lastTrainingDate.Date == DateTime.Today)
        {
            return;
        }

        await _trainingLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_revenueModel != null && _litersModel != null
                && _trainingDataCount == historicalData.Count
                && _lastTrainingDate.Date == DateTime.Today)
            {
                return;
            }

            TrainModels(historicalData);
            _trainingDataCount = historicalData.Count;
            _lastTrainingDate = DateTime.Today;
        }
        finally
        {
            _trainingLock.Release();
        }
    }

    /// <summary>
    /// Trains SdcaRegression models for both revenue and liters.
    /// </summary>
    private void TrainModels(List<SalesData> historicalData)
    {
        if (historicalData.Count < 7)
            throw new InvalidOperationException("Need at least 7 data points to train the model.");

        // Build feature data
        var features = BuildFeatures(historicalData);

        // Convert to IDataView
        var dataView = _mlContext.Data.LoadFromEnumerable(features);

        // ---- Revenue Model ----
        var revenuePipeline = _mlContext.Transforms
            .Concatenate("Features",
                nameof(SalesDataFeatures.DayOfWeek),
                nameof(SalesDataFeatures.DayOfMonth),
                nameof(SalesDataFeatures.Month),
                nameof(SalesDataFeatures.PrevDayRevenue),
                nameof(SalesDataFeatures.PrevDayLiters))
            .Append(_mlContext.Regression.Trainers.Sdca(
                labelColumnName: nameof(SalesDataFeatures.Revenue),
                featureColumnName: "Features"));

        _revenueModel = revenuePipeline.Fit(dataView);

        // ---- Liters Model ----
        var litersPipeline = _mlContext.Transforms
            .Concatenate("Features",
                nameof(SalesDataFeatures.DayOfWeek),
                nameof(SalesDataFeatures.DayOfMonth),
                nameof(SalesDataFeatures.Month),
                nameof(SalesDataFeatures.PrevDayRevenue),
                nameof(SalesDataFeatures.PrevDayLiters))
            .Append(_mlContext.Regression.Trainers.Sdca(
                labelColumnName: nameof(SalesDataFeatures.Liters),
                featureColumnName: "Features"));

        _litersModel = litersPipeline.Fit(dataView);

        _logger.LogInformation("ML models trained successfully with {Count} data points.", features.Count);
    }

    /// <summary>
    /// Uses the trained ML model to predict sales for a given date.
    /// </summary>
    private SalesPrediction? PredictWithMl(DateTime forecastDate, List<SalesData> historicalData)
    {
        if (_revenueModel == null || _litersModel == null)
            return null;

        // Build feature vector for the prediction date
        var prevDay = historicalData.Last();

        var input = new SalesDataFeatures
        {
            DayOfWeek = (float)forecastDate.DayOfWeek,
            DayOfMonth = forecastDate.Day,
            Month = forecastDate.Month,
            PrevDayRevenue = prevDay.Revenue,
            PrevDayLiters = prevDay.Liters
        };

        var inputView = _mlContext.Data.LoadFromEnumerable(new[] { input });

        var revenuePrediction = _revenueModel.Transform(inputView);
        var revenueResult = _mlContext.Data
            .CreateEnumerable<SalesMlPrediction>(revenuePrediction, reuseRowObject: false)
            .First();

        var litersPrediction = _litersModel.Transform(inputView);
        var litersResult = _mlContext.Data
            .CreateEnumerable<SalesMlPrediction>(litersPrediction, reuseRowObject: false)
            .First();

        // Calculate confidence interval (±15% for simplicity; ideally use prediction intervals)
        float revenue = Math.Max(0, revenueResult.Revenue);
        float liters = Math.Max(0, litersResult.Liters);
        float marginRevenue = revenue * 0.15f;
        float marginLiters = liters * 0.15f;

        return new SalesPrediction
        {
            ForecastDate = forecastDate,
            PredictedRevenue = revenue,
            PredictedLiters = liters,
            ConfidenceLower = Math.Max(0, revenue - marginRevenue),
            ConfidenceUpper = revenue + marginRevenue
        };
    }

    // ================================================================
    // PRIVATE: FALLBACK — SIMPLE MOVING AVERAGE
    // ================================================================

    /// <summary>
    /// Forecasts using simple moving average of the last 7 days
    /// with a linear regression trend line over the entire dataset.
    /// </summary>
    private List<SalesPrediction> ForecastWithSimpleMovingAverage(
        List<SalesData> historicalData, int daysAhead)
    {
        var forecasts = new List<SalesPrediction>();
        if (historicalData.Count == 0) return forecasts;

        // Use last 7 days for SMA baseline
        var windowSize = Math.Min(7, historicalData.Count);
        var recentWindow = historicalData.Skip(historicalData.Count - windowSize).ToList();

        float avgRevenue = recentWindow.Average(d => d.Revenue);
        float avgLiters = recentWindow.Average(d => d.Liters);

        // Calculate linear trend (simple linear regression over all data)
        float trendRevenue = CalculateTrend(historicalData.Select(d => d.Revenue).ToList());
        float trendLiters = CalculateTrend(historicalData.Select(d => d.Liters).ToList());

        float stdDevRevenue = CalculateStdDev(recentWindow.Select(d => d.Revenue));
        float stdDevLiters = CalculateStdDev(recentWindow.Select(d => d.Liters));

        for (int i = 1; i <= daysAhead; i++)
        {
            float predictedRevenue = Math.Max(0, avgRevenue + trendRevenue * i);
            float predictedLiters = Math.Max(0, avgLiters + trendLiters * i);

            forecasts.Add(new SalesPrediction
            {
                ForecastDate = DateTime.Today.AddDays(i),
                PredictedRevenue = predictedRevenue,
                PredictedLiters = predictedLiters,
                ConfidenceLower = Math.Max(0, predictedRevenue - stdDevRevenue * 2),
                ConfidenceUpper = predictedRevenue + stdDevRevenue * 2
            });
        }

        return forecasts;
    }

    /// <summary>
    /// Calculates per-unit linear trend slope using least squares.
    /// Positive = upward trend, Negative = downward trend.
    /// </summary>
    private static float CalculateTrend(List<float> values)
    {
        if (values.Count < 2) return 0;

        int n = values.Count;
        float sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;

        for (int i = 0; i < n; i++)
        {
            float x = i;
            float y = values[i];
            sumX += x;
            sumY += y;
            sumXY += x * y;
            sumX2 += x * x;
        }

        float denominator = n * sumX2 - sumX * sumX;
        if (Math.Abs(denominator) < 0.0001f) return 0;

        float slope = (n * sumXY - sumX * sumY) / denominator;
        return slope;
    }

    /// <summary>
    /// Calculates population standard deviation.
    /// </summary>
    private static float CalculateStdDev(IEnumerable<float> values)
    {
        var list = values.ToList();
        if (list.Count < 2) return 0;

        float mean = list.Average();
        float sumSquaredDiff = list.Sum(v => (v - mean) * (v - mean));
        return MathF.Sqrt(sumSquaredDiff / list.Count);
    }

    // ================================================================
    // PRIVATE: HELPERS
    // ================================================================

    /// <summary>
    /// Converts raw SalesData into feature vectors for ML training.
    /// Each row includes date-based features and the previous day's values.
    /// </summary>
    private static List<SalesDataFeatures> BuildFeatures(List<SalesData> data)
    {
        var features = new List<SalesDataFeatures>();

        for (int i = 1; i < data.Count; i++)
        {
            var current = data[i];
            var prev = data[i - 1];

            features.Add(new SalesDataFeatures
            {
                DayOfWeek = (float)current.Date.DayOfWeek,
                DayOfMonth = current.Date.Day,
                Month = current.Date.Month,
                PrevDayRevenue = prev.Revenue,
                PrevDayLiters = prev.Liters,
                Revenue = current.Revenue,
                Liters = current.Liters
            });
        }

        return features;
    }
}
