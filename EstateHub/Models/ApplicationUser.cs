using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EstateHub.Models;

/// <summary>
/// User profile extending ASP.NET Identity
/// </summary>
public class ApplicationUser
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(255)]
    public string? AvatarUrl { get; set; }

    /// <summary>Buyer, Tenant, Agent, Owner, Admin</summary>
    [Required, MaxLength(20)]
    public string Role { get; set; } = "Buyer";

    [MaxLength(50)]
    public string? PreferredLocation { get; set; }

    public decimal? MinBudget { get; set; }
    public decimal? MaxBudget { get; set; }

    [MaxLength(20)]
    public string? PreferredType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public ICollection<Property> ListedProperties { get; set; } = new List<Property>();
    public ICollection<WishlistItem> Wishlist { get; set; } = new List<WishlistItem>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<ChatMessage> SentMessages { get; set; } = new List<ChatMessage>();
    public ICollection<ChatMessage> ReceivedMessages { get; set; } = new List<ChatMessage>();
}
