using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SoccerWizard.Models;

/// <summary>
/// Model hasil prediksi pertandingan
/// </summary>
public class Prediction
{
    [Key]
    public int Id { get; set; }
    
    public int MatchId { get; set; }
    public Match Match { get; set; } = null!;
    
    [MaxLength(50)]
    public string PredictionType { get; set; } = "ML"; // ML, LLM, POISSON, HYBRID
    
    // Prediksi hasil
    [MaxLength(20)]
    public string PredictedOutcome { get; set; } = string.Empty; // HOME_WIN, DRAW, AWAY_WIN
    
    public double HomeWinProbability { get; set; }
    public double DrawProbability { get; set; }
    public double AwayWinProbability { get; set; }
    
    // Prediksi skor
    public double? PredictedHomeScore { get; set; }
    public double? PredictedAwayScore { get; set; }
    
    // Distribusi skor (JSON)
    [MaxLength(4000)]
    public string ScoreDistribution { get; set; } = "{}"; // JSON string
    
    // Key factors
    [MaxLength(2000)]
    public string KeyFactors { get; set; } = string.Empty;
    
    // LLM explanation
    [MaxLength(4000)]
    public string LLMExplanation { get; set; } = string.Empty;
    
    // Confidence
    public double Confidence { get; set; } // 0-1
    
    // Akurasi (diisi setelah match selesai)
    public bool? IsCorrect { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
