using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SoccerWizard.Models;

/// <summary>
/// Model untuk pertandingan sepak bola
/// </summary>
public class Match
{
    [Key]
    public int Id { get; set; }
    
    public int HomeTeamId { get; set; }
    public Team HomeTeam { get; set; } = null!;
    
    public int AwayTeamId { get; set; }
    public Team AwayTeam { get; set; } = null!;
    
    public int? LeagueId { get; set; }
    public League? League { get; set; }
    
    public DateTime MatchDate { get; set; }
    
    [MaxLength(50)]
    public string Status { get; set; } = "SCHEDULED"; // SCHEDULED, LIVE, FINISHED, POSTPONED, CANCELLED
    
    [MaxLength(100)]
    public string Venue { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string Referee { get; set; } = string.Empty;
    
    // Skor
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }
    public int? HomeHalfTimeScore { get; set; }
    public int? AwayHalfTimeScore { get; set; }
    
    // Statistik Pertandingan
    public int? HomePossession { get; set; }
    public int? AwayPossession { get; set; }
    public int? HomeShots { get; set; }
    public int? AwayShots { get; set; }
    public int? HomeShotsOnTarget { get; set; }
    public int? AwayShotsOnTarget { get; set; }
    public int? HomeCorners { get; set; }
    public int? AwayCorners { get; set; }
    public int? HomeFouls { get; set; }
    public int? AwayFouls { get; set; }
    public int? HomeYellowCards { get; set; }
    public int? AwayYellowCards { get; set; }
    public int? HomeRedCards { get; set; }
    public int? AwayRedCards { get; set; }
    
    // Expected Goals
    public double? HomeXG { get; set; }
    public double? AwayXG { get; set; }
    
    // Cuaca
    [MaxLength(50)]
    public string Weather { get; set; } = string.Empty;
    
    public double? Temperature { get; set; }
    public double? Humidity { get; set; }
    
    // Formasi
    [MaxLength(20)]
    public string HomeFormation { get; set; } = string.Empty;
    
    [MaxLength(20)]
    public string AwayFormation { get; set; } = string.Empty;
    
    // Head to Head summary (JSON string atau computed)
    public int HomeWinsH2H { get; set; }
    public int AwayWinsH2H { get; set; }
    public int DrawsH2H { get; set; }
    
    // Informasi tambahan
    [MaxLength(100)]
    public string Round { get; set; } = string.Empty;
    
    [MaxLength(2000)]
    public string MatchSummary { get; set; } = string.Empty;
    
    public ICollection<MatchStatistic> Statistics { get; set; } = new List<MatchStatistic>();
    public ICollection<Prediction> Predictions { get; set; } = new List<Prediction>();
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
