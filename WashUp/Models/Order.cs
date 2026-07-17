using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WashUp.Models;

/// <summary>
/// Main order entity for laundry services
/// </summary>
public class Order
{
    public int Id { get; set; }
    
    [Required, MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty; // Auto-generated: WO-{date}-{seq}
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
    
    // Service type
    [Required, MaxLength(30)]
    public string ServiceType { get; set; } = string.Empty; // CuciKering, Setrika, Express, Kiloan, CuciLipat
    
    // Order details
    public double WeightKg { get; set; }
    public int? ItemCount { get; set; }
    
    [MaxLength(1000)]
    public string? ItemDescription { get; set; }
    
    [MaxLength(200)]
    public string? DetergentPreference { get; set; }
    
    [MaxLength(200)]
    public string? FragrancePreference { get; set; }
    
    // Pricing
    [Column(TypeName = "decimal(18,2)")]
    public decimal PricePerKg { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Subtotal { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Discount { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }
    
    // Status tracking
    [MaxLength(30)]
    public string Status { get; set; } = "Diterima"; // Diterima, Dicuci, Disetrika, Selesai, Dikirim, Selesai
    
    public DateTime? ReceivedAt { get; set; }
    public DateTime? WashedAt { get; set; }
    public DateTime? IronedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    
    // Estimated completion
    public DateTime EstimatedCompletion { get; set; }
    
    // Pickup & Delivery
    public bool IsPickupRequested { get; set; }
    public bool IsDeliveryRequested { get; set; }
    public string? PickupAddress { get; set; }
    public string? DeliveryAddress { get; set; }
    public int? CourierId { get; set; }
    public CourierAssignment? CourierAssignment { get; set; }
    
    // Payment
    [MaxLength(30)]
    public string PaymentStatus { get; set; } = "BelumBayar"; // BelumBayar, Lunas, DP
    
    [MaxLength(30)]
    public string? PaymentMethod { get; set; } // OVO, GoPay, Dana, QRIS, Transfer, Cash
    
    // Audit
    [MaxLength(500)]
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation
    public Invoice? Invoice { get; set; }
    public ICollection<OrderStatusLog> StatusLogs { get; set; } = new List<OrderStatusLog>();
    public Review? Review { get; set; }
}
