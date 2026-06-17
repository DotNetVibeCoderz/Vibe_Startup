using System.ComponentModel.DataAnnotations;

namespace Bioskop.Models;

/// <summary>
/// Rating dan review film dari user
/// </summary>
public class MovieRating
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public string UserId { get; set; } = string.Empty;

    [Range(1, 5)]
    public int Rating { get; set; } // 1-5 stars

    [MaxLength(2000)]
    public string? Comment { get; set; }

    /// <summary>
    /// JSON array URL gambar yang dilampirkan dalam review
    /// </summary>
    public string? AttachedImages { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public Movie? Movie { get; set; }
    public ApplicationUser? User { get; set; }
}
