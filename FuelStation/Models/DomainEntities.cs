using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FuelStation.Models;

/// <summary>
/// Base entity with common fields for all models
/// </summary>
public abstract class BaseEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; } = false;

    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    [MaxLength(100)]
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Represents a physical fuel station location
/// </summary>
public class FuelStationLocation : BaseEntity
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<Tank> Tanks { get; set; } = new List<Tank>();
    public ICollection<FuelPump> Pumps { get; set; } = new List<FuelPump>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
}

/// <summary>
/// Fuel product types (Pertalite, Pertamax, Solar, etc.)
/// </summary>
public class FuelProduct : BaseEntity
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(20)]
    public string FuelType { get; set; } = "Gasoline"; // Gasoline, Diesel, Electric, etc.

    [Column(TypeName = "decimal(18,2)")]
    public decimal PricePerLiter { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CostPerLiter { get; set; }

    [MaxLength(10)]
    public string? OctaneRating { get; set; } // 90, 92, 95, etc.

    [MaxLength(20)]
    public string? ColorCode { get; set; } // For UI display

    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<Tank> Tanks { get; set; } = new List<Tank>();
    public ICollection<TransactionDetail> TransactionDetails { get; set; } = new List<TransactionDetail>();
    public ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();
}

/// <summary>
/// Price history for tracking price changes
/// </summary>
public class PriceHistory : BaseEntity
{
    public Guid FuelProductId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OldPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NewPrice { get; set; }

    public DateTime EffectiveDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(FuelProductId))]
    public FuelProduct? FuelProduct { get; set; }
}

/// <summary>
/// Physical tank at a fuel station
/// </summary>
public class Tank : BaseEntity
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? TankNumber { get; set; }

    public Guid FuelStationId { get; set; }
    public Guid FuelProductId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CapacityLiters { get; set; } // Max capacity

    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentVolumeLiters { get; set; } // Current volume

    [Column(TypeName = "decimal(18,2)")]
    public decimal MinThresholdLiters { get; set; } // Low stock warning threshold

    public double? TemperatureCelsius { get; set; }
    public double? PressureBar { get; set; }
    public bool IsLeakDetected { get; set; } = false;
    public bool IsActive { get; set; } = true;

    [MaxLength(200)]
    public string? SensorId { get; set; } // IoT sensor identifier

    public DateTime? LastSensorReading { get; set; }

    // Navigation
    [ForeignKey(nameof(FuelStationId))]
    public FuelStationLocation? FuelStation { get; set; }

    [ForeignKey(nameof(FuelProductId))]
    public FuelProduct? FuelProduct { get; set; }

    public ICollection<TankReading> TankReadings { get; set; } = new List<TankReading>();
}

/// <summary>
/// Historical tank readings from IoT sensors
/// </summary>
public class TankReading : BaseEntity
{
    public Guid TankId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal VolumeLiters { get; set; }

    public double? TemperatureCelsius { get; set; }
    public double? PressureBar { get; set; }
    public bool IsLeakDetected { get; set; } = false;
    public DateTime ReadingTime { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(TankId))]
    public Tank? Tank { get; set; }
}

/// <summary>
/// Fuel pump at a station
/// </summary>
public class FuelPump : BaseEntity
{
    [Required, MaxLength(50)]
    public string PumpNumber { get; set; } = string.Empty;

    public Guid FuelStationId { get; set; }
    public Guid TankId { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDamaged { get; set; } = false;

    [MaxLength(200)]
    public string? StatusNote { get; set; }

    public DateTime? LastMaintenanceDate { get; set; }

    [ForeignKey(nameof(FuelStationId))]
    public FuelStationLocation? FuelStation { get; set; }

    [ForeignKey(nameof(TankId))]
    public Tank? Tank { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}

/// <summary>
/// Customer/Membership entity
/// </summary>
public class Customer : BaseEntity
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? MemberCode { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    public int LoyaltyPoints { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalSpent { get; set; } = 0;

    public int VisitCount { get; set; } = 0;
    public DateTime? MemberSince { get; set; }
    public bool IsActive { get; set; } = true;

    [MaxLength(50)]
    public string? MembershipTier { get; set; } = "Regular"; // Regular, Silver, Gold, Platinum

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}

/// <summary>
/// Employee/Operator entity
/// </summary>
public class Employee : BaseEntity
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? EmployeeCode { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(50)]
    public string Role { get; set; } = "Operator"; // Admin, Supervisor, Operator

    public Guid? FuelStationId { get; set; }
    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(FuelStationId))]
    public FuelStationLocation? FuelStation { get; set; }

    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}

/// <summary>
/// Shift management for operators
/// </summary>
public class Shift : BaseEntity
{
    public Guid EmployeeId { get; set; }
    public Guid FuelStationId { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    [MaxLength(20)]
    public string ShiftType { get; set; } = "Morning"; // Morning, Afternoon, Night

    [MaxLength(500)]
    public string? Notes { get; set; }

    public bool IsOvertime { get; set; } = false;

    [ForeignKey(nameof(EmployeeId))]
    public Employee? Employee { get; set; }

    [ForeignKey(nameof(FuelStationId))]
    public FuelStationLocation? FuelStation { get; set; }
}

/// <summary>
/// Main transaction entity for fuel sales
/// </summary>
public class Transaction : BaseEntity
{
    [Required, MaxLength(50)]
    public string TransactionNumber { get; set; } = string.Empty;

    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    public Guid FuelStationId { get; set; }
    public Guid? PumpId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? EmployeeId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Discount { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Tax { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal GrandTotal { get; set; }

    [MaxLength(50)]
    public string PaymentMethod { get; set; } = "Cash"; // Cash, QRIS, EWallet, DebitCard, CreditCard, BankTransfer

    [MaxLength(100)]
    public string? PaymentReference { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Completed"; // Pending, Completed, Cancelled, Refunded

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Navigation
    [ForeignKey(nameof(FuelStationId))]
    public FuelStationLocation? FuelStation { get; set; }

    [ForeignKey(nameof(PumpId))]
    public FuelPump? Pump { get; set; }

    [ForeignKey(nameof(CustomerId))]
    public Customer? Customer { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public Employee? Employee { get; set; }

    public ICollection<TransactionDetail> TransactionDetails { get; set; } = new List<TransactionDetail>();
}

/// <summary>
/// Detail line items for a transaction
/// </summary>
public class TransactionDetail : BaseEntity
{
    public Guid TransactionId { get; set; }
    public Guid FuelProductId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Liters { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PricePerLiter { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Subtotal { get; set; }

    [ForeignKey(nameof(TransactionId))]
    public Transaction? Transaction { get; set; }

    [ForeignKey(nameof(FuelProductId))]
    public FuelProduct? FuelProduct { get; set; }
}

/// <summary>
/// Non-fuel product for marketplace
/// </summary>
public class NonFuelProduct : BaseEntity
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? SKU { get; set; }

    [MaxLength(50)]
    public string Category { get; set; } = "General"; // Oil, Beverage, Accessories, Snacks, etc.

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Cost { get; set; }

    public int StockQuantity { get; set; } = 0;
    public int MinStockThreshold { get; set; } = 5;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Marketplace order
/// </summary>
public class MarketplaceOrder : BaseEntity
{
    [Required, MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty;

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public Guid? CustomerId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Discount { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal GrandTotal { get; set; }

    [MaxLength(50)]
    public string PaymentMethod { get; set; } = "Cash";

    [MaxLength(20)]
    public string Status { get; set; } = "Completed";

    [ForeignKey(nameof(CustomerId))]
    public Customer? Customer { get; set; }

    public ICollection<MarketplaceOrderItem> Items { get; set; } = new List<MarketplaceOrderItem>();
}

/// <summary>
/// Marketplace order line items
/// </summary>
public class MarketplaceOrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }

    public int Quantity { get; set; } = 1;

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Subtotal { get; set; }

    [ForeignKey(nameof(OrderId))]
    public MarketplaceOrder? Order { get; set; }

    [ForeignKey(nameof(ProductId))]
    public NonFuelProduct? Product { get; set; }
}

/// <summary>
/// Customer feedback & rating
/// </summary>
public class Feedback : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Guid? TransactionId { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; } // 1-5 stars

    [MaxLength(1000)]
    public string? Comment { get; set; }

    [MaxLength(20)]
    public string Category { get; set; } = "General"; // Service, Product, Facility

    public bool IsResolved { get; set; } = false;

    [MaxLength(500)]
    public string? ResponseNote { get; set; }

    [ForeignKey(nameof(CustomerId))]
    public Customer? Customer { get; set; }
}

/// <summary>
/// Push notification entity
/// </summary>
public class Notification : BaseEntity
{
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Type { get; set; } = "Promo"; // Promo, Transaction, System, Alert

    public Guid? CustomerId { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime? SentAt { get; set; }
    public DateTime? ReadAt { get; set; }

    [MaxLength(500)]
    public string? ActionUrl { get; set; }

    [ForeignKey(nameof(CustomerId))]
    public Customer? Customer { get; set; }
}

/// <summary>
/// Audit log for all activities
/// </summary>
public class AuditLog : BaseEntity
{
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty; // Create, Update, Delete, Login, Logout

    [MaxLength(200)]
    public string EntityName { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    [MaxLength(200)]
    public string? UserId { get; set; }

    [MaxLength(200)]
    public string? UserName { get; set; }

    [MaxLength(200)]
    public string? IpAddress { get; set; }

    public string? OldValues { get; set; } // JSON serialized
    public string? NewValues { get; set; } // JSON serialized
}

/// <summary>
/// Emergency alert entity
/// </summary>
public class EmergencyAlert : BaseEntity
{
    [MaxLength(50)]
    public string AlertType { get; set; } = "Warning"; // Warning, Critical, Fire, Leak, Damage

    [MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    public Guid? FuelStationId { get; set; }
    public Guid? TankId { get; set; }
    public Guid? PumpId { get; set; }

    public bool IsResolved { get; set; } = false;
    public DateTime? ResolvedAt { get; set; }

    [MaxLength(500)]
    public string? ResolutionNote { get; set; }
}
