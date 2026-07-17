using System.ComponentModel.DataAnnotations;

namespace WashUp.Models;

/// <summary>
/// Customer review and rating for completed orders
/// </summary>
public class Review
{
    public int Id { get; set; }
    
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    
    [Range(1, 5)]
    public int Rating { get; set; } // 1-5 stars
    
    [MaxLength(2000)]
    public string? Comment { get; set; }
    
    [MaxLength(500)]
    public string? AdminResponse { get; set; }
    
    public DateTime? AdminResponseAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsVisible { get; set; } = true;
}

/// <summary>
/// Customer complaint management
/// </summary>
public class Complaint
{
    public int Id { get; set; }
    
    [Required, MaxLength(50)]
    public string ComplaintNumber { get; set; } = string.Empty; // CMP-{date}-{seq}
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
    
    [Required, MaxLength(50)]
    public string Category { get; set; } = string.Empty; // Kualitas, Keterlambatan, Kehilangan, Kerusakan, Lainnya
    
    [Required, MaxLength(200)]
    public string Subject { get; set; } = string.Empty;
    
    [Required, MaxLength(5000)]
    public string Description { get; set; } = string.Empty;
    
    [MaxLength(30)]
    public string Status { get; set; } = "Diterima"; // Diterima, Diproses, Selesai, Ditolak
    
    [MaxLength(5000)]
    public string? Resolution { get; set; }
    
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedByUserId { get; set; }
    
    [MaxLength(50)]
    public string Priority { get; set; } = "Normal"; // Low, Normal, High, Urgent
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    public ICollection<ComplaintAttachment> Attachments { get; set; } = new List<ComplaintAttachment>();
}

/// <summary>
/// Attachment for complaint evidence
/// </summary>
public class ComplaintAttachment
{
    public int Id { get; set; }
    public int ComplaintId { get; set; }
    public Complaint? Complaint { get; set; }
    
    [MaxLength(500)]
    public string FileUrl { get; set; } = string.Empty;
    
    [MaxLength(200)]
    public string? FileName { get; set; }
    
    [MaxLength(50)]
    public string? FileType { get; set; } // Image, Document, Video
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Loyalty membership subscription
/// </summary>
public class Subscription
{
    public int Id { get; set; }
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    
    [MaxLength(30)]
    public string Tier { get; set; } = "Regular"; // Regular, Silver, Gold, Platinum
    
    [MaxLength(50)]
    public string? PackageType { get; set; } // Bulanan, Kiloan, Express
    
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    
    public int? MaxOrdersPerMonth { get; set; }
    public double? MaxWeightPerOrder { get; set; }
    public double DiscountPercentage { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<LoyaltyPointTransaction> PointTransactions { get; set; } = new List<LoyaltyPointTransaction>();
}

/// <summary>
/// Loyalty point earning and redemption
/// </summary>
public class LoyaltyPointTransaction
{
    public int Id { get; set; }
    public int SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }
    
    [MaxLength(20)]
    public string TransactionType { get; set; } = string.Empty; // Earn, Redeem, Expire
    
    public int Points { get; set; }
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    public int? OrderId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
