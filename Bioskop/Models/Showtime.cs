using System.ComponentModel.DataAnnotations;

namespace Bioskop.Models;

/// <summary>
/// Jadwal tayang film di studio tertentu
/// </summary>
public class Showtime
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public int StudioId { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public decimal Price { get; set; } // Harga dasar tiket untuk jadwal ini

    [MaxLength(50)]
    public string? ShowType { get; set; } // Regular, 3D, IMAX, 4DX

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Movie? Movie { get; set; }
    public Studio? Studio { get; set; }
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
