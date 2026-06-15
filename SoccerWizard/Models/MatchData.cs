namespace SoccerWizard.Models;

/// <summary>
/// ML.NET data model untuk training dan prediksi
/// </summary>
public class MatchData
{
    public float HomeElo { get; set; }
    public float AwayElo { get; set; }
    public float HomeAttackStrength { get; set; }
    public float AwayAttackStrength { get; set; }
    public float HomeDefenseStrength { get; set; }
    public float AwayDefenseStrength { get; set; }
    public float HomeMomentum { get; set; }
    public float AwayMomentum { get; set; }
    public float HomeAvgGoals { get; set; }
    public float AwayAvgGoals { get; set; }
    public float HomeAvgConceded { get; set; }
    public float AwayAvgConceded { get; set; }
    public float HomeWinRate { get; set; }
    public float AwayWinRate { get; set; }
    public float H2HHomeWins { get; set; }
    public float H2HAwayWins { get; set; }
    public float H2HDraws { get; set; }
    public float HomeXG { get; set; }
    public float AwayXG { get; set; }
    public float Temperature { get; set; }
    public float Humidity { get; set; }
    
    // Label - hasil aktual
    public float HomeGoals { get; set; }
    public float AwayGoals { get; set; }
    public bool HomeWin { get; set; }
    public bool IsDraw { get; set; }
}
