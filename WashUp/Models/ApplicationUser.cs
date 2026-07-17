using Microsoft.AspNetCore.Identity;

namespace WashUp.Models;

/// <summary>
/// Extended user model with additional laundry-specific properties
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? PhoneNumber2 { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? AvatarUrl { get; set; }
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
    
    // Loyalty points
    public int LoyaltyPoints { get; set; }
    public string? MembershipTier { get; set; } = "Regular"; // Regular, Silver, Gold, Platinum
    
    // Preferences
    public string? PreferredServiceType { get; set; }
    public string? PreferredDetergent { get; set; }
    public string? PreferredFragrance { get; set; }
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Complaint> Complaints { get; set; } = new List<Complaint>();
    public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
