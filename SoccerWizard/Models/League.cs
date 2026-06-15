using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SoccerWizard.Models;

/// <summary>
/// Model untuk liga/kompetisi
/// </summary>
public class League
{
    [Key]
    public int Id { get; set; }
    
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string Country { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string LogoUrl { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string Season { get; set; } = string.Empty;
    
    public int TotalTeams { get; set; }
    public int TotalRounds { get; set; }
    
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;
    
    public ICollection<Team> Teams { get; set; } = new List<Team>();
    public ICollection<Match> Matches { get; set; } = new List<Match>();
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
