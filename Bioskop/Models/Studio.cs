using System.ComponentModel.DataAnnotations;

namespace Bioskop.Models;

/// <summary>
/// Studio bioskop untuk penayangan film
/// </summary>
public class Studio
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty; // Studio 1, Studio VIP, IMAX, dll

    public int TotalRows { get; set; } = 10;
    public int TotalColumns { get; set; } = 15;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<Seat> Seats { get; set; } = new List<Seat>();
    public ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
}
