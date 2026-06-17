using System.ComponentModel.DataAnnotations.Schema;

namespace Bioskop.Models;

/// <summary>
/// Like pada postingan Curhat Film
/// </summary>
public class Like
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Post? Post { get; set; }
    public ApplicationUser? User { get; set; }
}
