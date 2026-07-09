using System.ComponentModel.DataAnnotations;

namespace FuelStation.Models;

/// <summary>
/// Raw sales data used for ML training and forecasting.
/// Aggregated from Transactions + TransactionDetails per day.
/// </summary>
public class SalesData
{
    public DateTime Date { get; set; }

    /// <summary>Total revenue (GrandTotal) for the day in IDR.</summary>
    public float Revenue { get; set; }

    /// <summary>Total liters sold for the day.</summary>
    public float Liters { get; set; }

    /// <summary>Number of transactions for the day.</summary>
    public float TransactionCount { get; set; }
}

/// <summary>
/// ML prediction output for a single day's sales.
/// </summary>
public class SalesPrediction
{
    /// <summary>The date this prediction applies to.</summary>
    public DateTime ForecastDate { get; set; }

    /// <summary>Predicted revenue in IDR.</summary>
    public float PredictedRevenue { get; set; }

    /// <summary>Predicted total liters.</summary>
    public float PredictedLiters { get; set; }

    /// <summary>Lower bound of confidence interval.</summary>
    public float ConfidenceLower { get; set; }

    /// <summary>Upper bound of confidence interval.</summary>
    public float ConfidenceUpper { get; set; }
}

/// <summary>
/// Stock depletion prediction for a given tank.
/// </summary>
public class StockPrediction
{
    /// <summary>Tank unique identifier.</summary>
    public Guid TankId { get; set; }

    /// <summary>Human-readable tank name.</summary>
    public string TankName { get; set; } = string.Empty;

    /// <summary>Current volume in liters.</summary>
    public float CurrentLiters { get; set; }

    /// <summary>Estimated daily consumption rate in liters/day.</summary>
    public float DailyConsumptionRate { get; set; }

    /// <summary>Estimated days until tank is empty.</summary>
    public int DaysUntilEmpty { get; set; }

    /// <summary>Suggested date to reorder (before hitting minimum threshold).</summary>
    public DateTime SuggestedReorderDate { get; set; }

    /// <summary>Minimum threshold liters for this tank.</summary>
    public float MinThresholdLiters { get; set; }

    /// <summary>Tank capacity in liters.</summary>
    public float CapacityLiters { get; set; }

    /// <summary>Fuel product name stored in the tank.</summary>
    public string FuelProductName { get; set; } = string.Empty;
}

/// <summary>
/// Result of anomaly detection for a specific date's sales.
/// </summary>
public class AnomalyResult
{
    /// <summary>The date of the sales data point.</summary>
    public DateTime Date { get; set; }

    /// <summary>The actual recorded revenue.</summary>
    public float ActualValue { get; set; }

    /// <summary>The expected (calculated) revenue baseline.</summary>
    public float ExpectedValue { get; set; }

    /// <summary>Percentage deviation from expected value.</summary>
    public float DeviationPercent { get; set; }

    /// <summary>True if this data point is flagged as anomalous.</summary>
    public bool IsAnomaly { get; set; }

    /// <summary>Direction of anomaly: "Spike", "Drop", or "Normal".</summary>
    public string AnomalyType { get; set; } = "Normal";
}

// ========================================
// Internal ML pipeline feature/label types
// ========================================

/// <summary>
/// Features fed into the ML regression pipeline.
/// </summary>
internal class SalesDataFeatures
{
    /// <summary>Day of week (0 = Sunday, 6 = Saturday).</summary>
    public float DayOfWeek { get; set; }

    /// <summary>Day of month (1-31).</summary>
    public float DayOfMonth { get; set; }

    /// <summary>Month number (1-12).</summary>
    public float Month { get; set; }

    /// <summary>Previous day's revenue, used as a lag feature.</summary>
    public float PrevDayRevenue { get; set; }

    /// <summary>Previous day's liters, used as a lag feature.</summary>
    public float PrevDayLiters { get; set; }

    /// <summary>Label: revenue for the current day.</summary>
    public float Revenue { get; set; }

    /// <summary>Label: liters for the current day.</summary>
    public float Liters { get; set; }
}

/// <summary>
/// ML regression prediction result (raw output from pipeline).
/// </summary>
internal class SalesMlPrediction
{
    public float Revenue { get; set; }
    public float Liters { get; set; }
}
