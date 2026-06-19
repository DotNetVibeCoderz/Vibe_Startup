using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Comblang.Models;

/// <summary>
/// Represents a successful match between two users (mutual likes).
/// </summary>
public class Match
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId1 { get; set; }

    [Required]
    public Guid UserId2 { get; set; }

    public double CompatibilityScore { get; set; }

    public DateTime MatchedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    // Navigation
    [ForeignKey(nameof(UserId1))]
    public User? User1 { get; set; }

    [ForeignKey(nameof(UserId2))]
    public User? User2 { get; set; }
}
