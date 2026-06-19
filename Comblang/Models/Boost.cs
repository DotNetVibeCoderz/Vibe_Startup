using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Comblang.Models;

/// <summary>
/// Profile boost to increase visibility in search results.
/// </summary>
public class Boost
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    public DateTime EndTime { get; set; }

    [MaxLength(20)]
    public string BoostType { get; set; } = "Standard"; // "Standard", "Super", "Mega"

    public bool IsActive { get; set; } = true;

    public int ImpressionsGained { get; set; } = 0;

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
}
