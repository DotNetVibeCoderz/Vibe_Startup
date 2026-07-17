using System.ComponentModel.DataAnnotations;

namespace WashUp.Models;

/// <summary>
/// Notification for users (order updates, promos, announcements)
/// </summary>
public class Notification
{
    public int Id { get; set; }
    
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    
    [Required, MaxLength(50)]
    public string Type { get; set; } = string.Empty; // OrderUpdate, Promo, Announcement, System
    
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(2000)]
    public string? Message { get; set; }
    
    [MaxLength(500)]
    public string? ActionUrl { get; set; }
    
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [MaxLength(20)]
    public string Priority { get; set; } = "Normal"; // Low, Normal, High
}

/// <summary>
/// Public marketplace listing for laundry services
/// </summary>
public class MarketplaceListing
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }
    
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(2000)]
    public string? Description { get; set; }
    
    [MaxLength(500)]
    public string? ImageUrl { get; set; }
    
    public decimal PricePerKg { get; set; }
    
    [MaxLength(100)]
    public string? ServiceArea { get; set; }
    
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
    
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Tax configuration and report
/// </summary>
public class TaxRecord
{
    public int Id { get; set; }
    
    public int Month { get; set; }
    public int Year { get; set; }
    public int? BranchId { get; set; }
    
    public decimal TotalRevenue { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal PphRate { get; set; } = 0.1m; // 10% PPh
    public decimal PphAmount { get; set; }
    public decimal PpnAmount { get; set; }
    
    [MaxLength(30)]
    public string Status { get; set; } = "Draft"; // Draft, Reported, Paid
    
    public DateTime? ReportedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [MaxLength(500)]
    public string? Notes { get; set; }
}
