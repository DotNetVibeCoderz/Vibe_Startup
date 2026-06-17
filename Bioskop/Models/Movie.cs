using System.ComponentModel.DataAnnotations;

namespace Bioskop.Models;

/// <summary>
/// Data film yang tayang di bioskop
/// </summary>
public class Movie
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Genre { get; set; } // Action, Comedy, Horror, dll

    public int DurationMinutes { get; set; } // Durasi film dalam menit

    [MaxLength(500)]
    public string? PosterUrl { get; set; }

    [MaxLength(500)]
    public string? TrailerUrl { get; set; }

    [MaxLength(50)]
    public string? AgeRating { get; set; } // SU, R13, R17, D17

    public DateTime ReleaseDate { get; set; }
    public DateTime? EndDate { get; set; } // Sampai kapan tayang
    public bool IsNowPlaying { get; set; } = true;
    public decimal BasePrice { get; set; } = 50000; // Harga dasar tiket

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
    public ICollection<MovieRating> Ratings { get; set; } = new List<MovieRating>();
}
