using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartDrive.Models.Enums;

namespace SmartDrive.Models.Entities;

/// <summary>
/// Produk/layanan di marketplace
/// </summary>
public class MarketplaceProduct
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; } // DefensiveDriving, EcoDriving, etc.

    public int DurationHours { get; set; }

    [MaxLength(255)]
    public string? ImagePath { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MarketplaceOrderItem> OrderItems { get; set; } = new List<MarketplaceOrderItem>();
}

/// <summary>
/// Order marketplace
/// </summary>
public class MarketplaceOrder
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    [MaxLength(50)]
    public string? OrderNumber { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public ICollection<MarketplaceOrderItem> OrderItems { get; set; } = new List<MarketplaceOrderItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

/// <summary>
/// Item dalam order marketplace
/// </summary>
public class MarketplaceOrderItem
{
    [Key]
    public int Id { get; set; }

    public int OrderId { get; set; }

    [ForeignKey(nameof(OrderId))]
    public MarketplaceOrder? Order { get; set; }

    public int ProductId { get; set; }

    [ForeignKey(nameof(ProductId))]
    public MarketplaceProduct? Product { get; set; }

    public int Quantity { get; set; } = 1;

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }
}

/// <summary>
/// Konfigurasi sistem yang bisa diubah dari UI
/// </summary>
public class SystemConfig
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string ConfigKey { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? ConfigValue { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// GPS tracking data untuk sesi latihan
/// </summary>
public class GpsTrackingData
{
    [Key]
    public long Id { get; set; }

    public int BookingId { get; set; }

    [ForeignKey(nameof(BookingId))]
    public Booking? Booking { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public double? Speed { get; set; } // km/h

    public double? Heading { get; set; } // degrees

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public bool IsSimulated { get; set; }

    [MaxLength(100)]
    public string? DeviceId { get; set; }
}

/// <summary>
/// Asuransi untuk sesi latihan
/// </summary>
public class InsurancePolicy
{
    [Key]
    public int Id { get; set; }

    [MaxLength(100)]
    public string? PolicyNumber { get; set; }

    [MaxLength(100)]
    public string? ProviderName { get; set; }

    [MaxLength(50)]
    public string? CoverageType { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CoverageAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Premium { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public int? VehicleId { get; set; }

    [ForeignKey(nameof(VehicleId))]
    public Vehicle? Vehicle { get; set; }

    public ICollection<InsuranceClaim> Claims { get; set; } = new List<InsuranceClaim>();
}

/// <summary>
/// Klaim asuransi
/// </summary>
public class InsuranceClaim
{
    [Key]
    public int Id { get; set; }

    public int PolicyId { get; set; }

    [ForeignKey(nameof(PolicyId))]
    public InsurancePolicy? Policy { get; set; }

    public int? BookingId { get; set; }

    [ForeignKey(nameof(BookingId))]
    public Booking? Booking { get; set; }

    public DateTime IncidentDate { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ClaimAmount { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Paid

    public DateTime FiledAt { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAt { get; set; }

    [MaxLength(1000)]
    public string? ResolutionNotes { get; set; }
}
