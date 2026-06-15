using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SoccerWizard.Models;

/// <summary>
/// Model head-to-head antara dua tim
/// </summary>
public class HeadToHead
{
    [Key]
    public int Id { get; set; }
    
    public int Team1Id { get; set; }
    public Team Team1 { get; set; } = null!;
    
    public int Team2Id { get; set; }
    public Team Team2 { get; set; } = null!;
    
    public int TotalMatches { get; set; }
    public int Team1Wins { get; set; }
    public int Team2Wins { get; set; }
    public int Draws { get; set; }
    public int Team1Goals { get; set; }
    public int Team2Goals { get; set; }
    
    [MaxLength(4000)]
    public string RecentMatches { get; set; } = "[]"; // JSON array of recent match results
    
    public DateTime LastMatchDate { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
