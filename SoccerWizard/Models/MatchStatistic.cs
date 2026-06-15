using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SoccerWizard.Models;

/// <summary>
/// Model statistik detail per pertandingan
/// </summary>
public class MatchStatistic
{
    [Key]
    public int Id { get; set; }
    
    public int MatchId { get; set; }
    public Match Match { get; set; } = null!;
    
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    
    [MaxLength(50)]
    public string StatisticType { get; set; } = string.Empty; // Possession, Shots, Cards, dll
    
    public double Value { get; set; }
    
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;
}
