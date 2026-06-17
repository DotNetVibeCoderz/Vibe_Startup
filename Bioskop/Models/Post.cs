using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bioskop.Models;

/// <summary>
/// Postingan di timeline Curhat Film (seperti Twitter)
/// </summary>
public class Post
{
    public int Id { get; set; }

    [Required, MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// JSON array berisi URL media: gambar, video, audio
    /// Format: [{"type":"image","url":"..."},{"type":"video","url":"..."}]
    /// </summary>
    public string? MediaUrls { get; set; }

    /// <summary>
    /// Tipe event jika ini adalah event post (movie night, gathering, dll)
    /// </summary>
    [MaxLength(200)]
    public string? EventTitle { get; set; }

    public DateTime? EventDate { get; set; }

    [MaxLength(500)]
    public string? EventLocation { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;

    // Navigation
    public ApplicationUser? User { get; set; }

    [InverseProperty("Post")]
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();

    [InverseProperty("Post")]
    public ICollection<Like> Likes { get; set; } = new List<Like>();
}
