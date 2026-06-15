using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SoccerWizard.Models;

/// <summary>
/// Model untuk pemain sepak bola
/// </summary>
public class Player
{
    [Key]
    public int Id { get; set; }
    
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;
    
    [MaxLength(20)]
    public string Position { get; set; } = string.Empty; // GK, DEF, MID, FWD
    
    [MaxLength(10)]
    public string ShirtNumber { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string Nationality { get; set; } = string.Empty;
    
    public DateTime DateOfBirth { get; set; }
    public double Height { get; set; } // cm
    public double Weight { get; set; } // kg
    
    [MaxLength(500)]
    public string PhotoUrl { get; set; } = string.Empty;
    
    // Statistik Musim Ini
    public int Appearances { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public double Rating { get; set; } // 0-10
    public double ExpectedGoals { get; set; } // xG
    public double ExpectedAssists { get; set; } // xA
    public int PassesCompleted { get; set; }
    public int PassesAttempted { get; set; }
    public int ShotsOnTarget { get; set; }
    public int ShotsTotal { get; set; }
    public int Tackles { get; set; }
    public int Interceptions { get; set; }
    
    // Status
    public bool IsInjured { get; set; }
    public string InjuryDescription { get; set; } = string.Empty;
    public bool IsSuspended { get; set; }
    
    public int TeamId { get; set; }
    public Team Team { get; set; } = null!;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
