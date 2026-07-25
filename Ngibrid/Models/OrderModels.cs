using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ngibrid.Models;

/// <summary>
/// Order entity - represents a customer shipment order
/// </summary>
public class Order : BaseEntity
{
    [Required, MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty; // e.g., NGB-20250101-0001

    public long CustomerId { get; set; }

    [MaxLength(50)]
    public string? ExternalOrderId { get; set; } // From marketplace

    // Sender info
    [Required, MaxLength(200)]
    public string SenderName { get; set; } = string.Empty;
    [Required, MaxLength(20)]
    public string SenderPhone { get; set; } = string.Empty;
    [Required, MaxLength(500)]
    public string SenderAddress { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? SenderProvince { get; set; }
    /// <summary>City as stored in the master table, e.g. "Kota Bandung" / "Kabupaten Bandung".</summary>
    [MaxLength(100)]
    public string? SenderCity { get; set; }
    [MaxLength(10)]
    public string? SenderPostalCode { get; set; }

    // Recipient info
    [Required, MaxLength(200)]
    public string RecipientName { get; set; } = string.Empty;
    [Required, MaxLength(20)]
    public string RecipientPhone { get; set; } = string.Empty;
    [Required, MaxLength(500)]
    public string RecipientAddress { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? RecipientProvince { get; set; }
    [MaxLength(100)]
    public string? RecipientCity { get; set; }
    [MaxLength(10)]
    public string? RecipientPostalCode { get; set; }

    // Package details
    [Required, MaxLength(200)]
    public string PackageDescription { get; set; } = string.Empty;
    public double WeightKg { get; set; }
    public double LengthCm { get; set; }
    public double WidthCm { get; set; }
    public double HeightCm { get; set; }
    public double VolumetricWeight { get; set; }

    // Shipment
    [MaxLength(50)]
    public string ServiceType { get; set; } = "REG"; // REG=Regular, EXP=Express, SAMEDAY, ECO
    [MaxLength(50)]
    public string? TrackingNumber { get; set; }

    // Status
    [MaxLength(50)]
    public string Status { get; set; } = "CREATED"; // CREATED, PICKED_UP, IN_TRANSIT, AT_WAREHOUSE, OUT_FOR_DELIVERY, DELIVERED, FAILED, RETURNED, CANCELLED

    // Financial
    public decimal BasePrice { get; set; }
    public decimal InsuranceFee { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    [MaxLength(10)]
    public string Currency { get; set; } = "IDR";

    // Insurance
    public bool HasInsurance { get; set; }
    public decimal? DeclaredValue { get; set; }

    // Dates
    public DateTime? PickupDate { get; set; }
    public DateTime? EstimatedDeliveryDate { get; set; }
    public DateTime? ActualDeliveryDate { get; set; }

    // Eco
    public double? CarbonEmissionGram { get; set; }
    public bool IsEcoDelivery { get; set; }

    // Barcode/QR
    [MaxLength(500)]
    public string? BarcodeData { get; set; }
    [MaxLength(500)]
    public string? QrCodeUrl { get; set; }

    // Relationships
    [ForeignKey(nameof(CustomerId))]
    public ApplicationUser? Customer { get; set; }

    public long? AssignedCourierId { get; set; }
    [ForeignKey(nameof(AssignedCourierId))]
    public ApplicationUser? AssignedCourier { get; set; }

    public ICollection<OrderStatusHistory>? StatusHistory { get; set; }
    public ICollection<ShipmentTracking>? Trackings { get; set; }
    public Payment? Payment { get; set; }
    public Invoice? Invoice { get; set; }
    public InsuranceClaim? InsuranceClaim { get; set; }
}

/// <summary>
/// Order status history for audit trail
/// </summary>
public class OrderStatusHistory : BaseEntity
{
    public long OrderId { get; set; }

    [Required, MaxLength(50)]
    public string Status { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    [MaxLength(256)]
    public string? UpdatedByUserId { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }
}

/// <summary>
/// Real-time GPS tracking data points
/// </summary>
public class ShipmentTracking : BaseEntity
{
    public long OrderId { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Altitude { get; set; }
    public double? SpeedKmh { get; set; }
    public double? Heading { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [MaxLength(200)]
    public string? LocationDescription { get; set; }

    [MaxLength(50)]
    public string? EventType { get; set; } // GPS_UPDATE, STATUS_CHANGE, DELIVERY_ATTEMPT

    public long? CourierId { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }
}
