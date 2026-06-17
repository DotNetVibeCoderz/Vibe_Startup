using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bioskop.Models;

/// <summary>
/// Komentar pada postingan Curhat Film
/// </summary>
public class Comment
{
    public int Id { get; set; }
    public int PostId { get; set; }

    [Required, MaxLength(1000)]
    public string Content { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// JSON array URL gambar yang dilampirkan
    /// </summary>
    public string? AttachedImages { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;

    // Navigation
    public Post? Post { get; set; }
    public ApplicationUser? User { get; set; }
}
