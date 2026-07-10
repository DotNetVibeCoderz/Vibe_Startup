using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EstateHub.Models;

/// <summary>
/// Booking/Schedule for property visits
/// </summary>
public class Booking
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public Property? Property { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    [Required]
    public DateTime ScheduledDate { get; set; }

    [MaxLength(10)]
    public string? TimeSlot { get; set; } // Morning, Afternoon, Evening

    /// <summary>Pending, Confirmed, Completed, Cancelled</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(100)]
    public string? AgentContact { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// User wishlist/favorites
/// </summary>
public class WishlistItem
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    [Required]
    public int PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public Property? Property { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Review & Rating for property or agent
/// </summary>
public class Review
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public Property? Property { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
