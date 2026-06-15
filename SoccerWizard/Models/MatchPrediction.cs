using Microsoft.ML.Data;

namespace SoccerWizard.Models;

/// <summary>
/// Model hasil prediksi ML.NET
/// </summary>
public class MatchPrediction
{
    [ColumnName("PredictedLabel")]
    public bool PredictedHomeWin { get; set; }
    
    public float Probability { get; set; }
    public float Score { get; set; }
}

/// <summary>
/// Model prediksi skor (regression)
/// </summary>
public class ScorePrediction
{
    public float PredictedHomeGoals { get; set; }
    public float PredictedAwayGoals { get; set; }
}

/// <summary>
/// Model untuk hasil evaluasi ML
/// </summary>
public class ModelEvaluation
{
    public double Accuracy { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1Score { get; set; }
    public double AUC { get; set; }
    public string ConfusionMatrix { get; set; } = string.Empty;
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    public int TotalSamples { get; set; }
}
