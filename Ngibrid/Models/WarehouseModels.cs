using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ngibrid.Models;

/// <summary>
/// Warehouse entity
/// </summary>
public class Warehouse : BaseEntity
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty; // WH-JKT-01

    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Province { get; set; } = string.Empty;

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public double TotalCapacityM3 { get; set; }
    public double UsedCapacityM3 { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "ACTIVE"; // ACTIVE, MAINTENANCE, INACTIVE

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public int? OperatingHoursStart { get; set; } // 8
    public int? OperatingHoursEnd { get; set; }   // 20

    public ICollection<WarehouseSection>? Sections { get; set; }
    public ICollection<InventoryItem>? InventoryItems { get; set; }
}

/// <summary>
/// Warehouse section/zone
/// </summary>
public class WarehouseSection : BaseEntity
{
    public long WarehouseId { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string SectionType { get; set; } = "GENERAL"; // GENERAL, COLD, HAZMAT, HIGH_VALUE, BULK

    public double CapacityM3 { get; set; }
    public double UsedCapacityM3 { get; set; }

    public int RackCount { get; set; }
    public int ShelfCount { get; set; }

    [ForeignKey(nameof(WarehouseId))]
    public Warehouse? Warehouse { get; set; }
    public ICollection<InventoryItem>? Items { get; set; }
}

/// <summary>
/// Inventory item in warehouse
/// </summary>
public class InventoryItem : BaseEntity
{
    public long? OrderId { get; set; }
    public long WarehouseId { get; set; }
    public long? SectionId { get; set; }

    [Required, MaxLength(100)]
    public string Sku { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public int Quantity { get; set; }

    [MaxLength(50)]
    public string Unit { get; set; } = "PCS"; // PCS, KG, LITER, BOX

    public double WeightKg { get; set; }
    public double VolumeM3 { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "STORED"; // STORED, IN_TRANSIT, OUT, DAMAGED, EXPIRED

    [MaxLength(100)]
    public string? RfidTag { get; set; }

    [MaxLength(100)]
    public string? Barcode { get; set; }

    [MaxLength(200)]
    public string? ShelfLocation { get; set; }

    public DateTime? ExpiryDate { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? ShippedAt { get; set; }

    [MaxLength(50)]
    public string? BatchNumber { get; set; }

    // IoT sensor data
    public double? CurrentTemperature { get; set; }
    public double? CurrentHumidity { get; set; }
    public DateTime? LastSensorReading { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }

    [ForeignKey(nameof(WarehouseId))]
    public Warehouse? Warehouse { get; set; }

    [ForeignKey(nameof(SectionId))]
    public WarehouseSection? Section { get; set; }

    public ICollection<InventoryMovement>? Movements { get; set; }
}

/// <summary>
/// Inventory movement history
/// </summary>
public class InventoryMovement : BaseEntity
{
    public long InventoryItemId { get; set; }

    [MaxLength(50)]
    public string MovementType { get; set; } = "IN"; // IN, OUT, TRANSFER, ADJUSTMENT

    public int Quantity { get; set; }
    public int BalanceAfter { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(InventoryItemId))]
    public InventoryItem? InventoryItem { get; set; }
}
