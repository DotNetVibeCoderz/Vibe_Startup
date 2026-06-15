using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SoccerWizard.Models;

/// <summary>
/// Model untuk tim sepak bola
/// </summary>
public class Team
{
    [Key]
    public int Id { get; set; }
    
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string ShortName { get; set; } = string.Empty;
    
    [MaxLength(10)]
    public string Code { get; set; } = string.Empty; // Contoh: "MUN", "LIV"
    
    [MaxLength(500)]
    public string LogoUrl { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string Country { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string Stadium { get; set; } = string.Empty;
    
    public int FoundedYear { get; set; }
    
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;
    
    // Rating & Statistik
    public double EloRating { get; set; } = 1500.0;
    public double AttackStrength { get; set; } = 1.0;
    public double DefenseStrength { get; set; } = 1.0;
    public double MidfieldStrength { get; set; } = 1.0;
    public double Momentum { get; set; } = 0.5;
    
    // Performa
    public int MatchesPlayed { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public double AvgGoalsScored { get; set; }
    public double AvgGoalsConceded { get; set; }
    
    public int? LeagueId { get; set; }
    public League? League { get; set; }
    
    // Navigasi
    public ICollection<Player> Players { get; set; } = new List<Player>();
    public ICollection<Match> HomeMatches { get; set; } = new List<Match>();
    public ICollection<Match> AwayMatches { get; set; } = new List<Match>();
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
