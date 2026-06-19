using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Comblang.Models;

/// <summary>
/// Records swipe actions (Like/Skip/SuperLike) between users.
/// </summary>
public class Swipe
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid SwiperId { get; set; }

    [Required]
    public Guid TargetId { get; set; }

    [Required]
    [MaxLength(20)]
    public string SwipeType { get; set; } = "Skip"; // "Like", "Skip", "SuperLike"

    public DateTime SwipedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(SwiperId))]
    public User? Swiper { get; set; }

    [ForeignKey(nameof(TargetId))]
    public User? Target { get; set; }
}
