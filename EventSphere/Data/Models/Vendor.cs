using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventSphere.Data.Models;

/// <summary>
/// Vendor/catering/dekorasi/fotografer dll
/// </summary>
public class Vendor
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(2000)]
    public string? Description { get; set; }
    
    [Required, MaxLength(100)]
    public string Category { get; set; } = string.Empty; // Catering, Dekorasi, Fotografi, Musik, Venue, dll
    
    [MaxLength(500)]
    public string? Address { get; set; }
    
    [MaxLength(100)]
    public string? City { get; set; }
    
    [Phone, MaxLength(30)]
    public string? Phone { get; set; }
    
    [EmailAddress, MaxLength(200)]
    public string? Email { get; set; }
    
    [MaxLength(500)]
    public string? Website { get; set; }
    
    [MaxLength(500)]
    public string? PortfolioUrl { get; set; }
    
    public decimal Rating { get; set; } // 1-5
    public int ReviewCount { get; set; }
    
    [MaxLength(50)]
    public string? PriceRange { get; set; } // Budget, Standard, Premium, Luxury
    
    public bool IsVerified { get; set; }
    public bool IsActive { get; set; } = true;
    
    [MaxLength(500)]
    public string? LogoUrl { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigasi
    public ICollection<VendorContract> Contracts { get; set; } = new List<VendorContract>();
    public ICollection<VendorReview> Reviews { get; set; } = new List<VendorReview>();
    public ICollection<VendorPortfolio> Portfolios { get; set; } = new List<VendorPortfolio>();
}

/// <summary>
/// Kontrak vendor untuk event tertentu
/// </summary>
public class VendorContract
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid EventId { get; set; }
    
    [Required]
    public Guid VendorId { get; set; }
    
    [MaxLength(200)]
    public string? ContractTitle { get; set; }
    
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; }
    
    public ContractStatus Status { get; set; } = ContractStatus.Pending;
    
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? SignedDate { get; set; }
    
    [MaxLength(2000)]
    public string? Terms { get; set; }
    
    [MaxLength(500)]
    public string? ContractFileUrl { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }
    
    [ForeignKey(nameof(VendorId))]
    public Vendor? Vendor { get; set; }
    
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}

public enum ContractStatus
{
    Pending,
    Sent,
    Signed,
    Active,
    Completed,
    Cancelled,
    Disputed
}

/// <summary>
/// Invoice/pembayaran vendor
/// </summary>
public class Invoice
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid ContractId { get; set; }
    
    [Required, MaxLength(100)]
    public string InvoiceNumber { get; set; } = string.Empty;
    
    public decimal Amount { get; set; }
    public decimal Tax { get; set; }
    public decimal TotalAmount { get; set; }
    
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;
    
    public DateTime? DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    
    [MaxLength(1000)]
    public string? Description { get; set; }
    
    [MaxLength(500)]
    public string? InvoiceFileUrl { get; set; }
    
    public string? PaidById { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [ForeignKey(nameof(ContractId))]
    public VendorContract? Contract { get; set; }
    
    [ForeignKey(nameof(PaidById))]
    public ApplicationUser? PaidBy { get; set; }
}

public enum InvoiceStatus
{
    Pending,
    Sent,
    Paid,
    Overdue,
    Cancelled
}

/// <summary>
/// Review vendor
/// </summary>
public class VendorReview
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid VendorId { get; set; }
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    public int Rating { get; set; } // 1-5
    
    [MaxLength(2000)]
    public string? Comment { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [ForeignKey(nameof(VendorId))]
    public Vendor? Vendor { get; set; }
    
    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
}

/// <summary>
/// Portfolio foto vendor
/// </summary>
public class VendorPortfolio
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid VendorId { get; set; }
    
    [Required, MaxLength(500)]
    public string ImageUrl { get; set; } = string.Empty;
    
    [MaxLength(200)]
    public string? Caption { get; set; }
    
    public int SortOrder { get; set; }
    
    [ForeignKey(nameof(VendorId))]
    public Vendor? Vendor { get; set; }
}
