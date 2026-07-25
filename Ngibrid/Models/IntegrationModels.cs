using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ngibrid.Models;

/// <summary>
/// External system connection — marketplace (Tokopedia, Shopee), ERP, or CRM.
/// Credentials are stored per-integration so several accounts of the same platform can coexist.
/// </summary>
public class Integration : BaseEntity
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Platform { get; set; } = "TOKOPEDIA"; // TOKOPEDIA, SHOPEE, ERP, CRM, CUSTOM

    [MaxLength(50)]
    public string IntegrationType { get; set; } = "MARKETPLACE"; // MARKETPLACE, ERP, CRM

    [MaxLength(500)]
    public string? Endpoint { get; set; }

    [MaxLength(500)]
    public string? ApiKey { get; set; }

    [MaxLength(200)]
    public string? ShopId { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>Auto-import new marketplace orders on the sync interval.</summary>
    public bool AutoSync { get; set; } = false;

    public int SyncIntervalMinutes { get; set; } = 30;

    public DateTime? LastSyncAt { get; set; }

    [MaxLength(50)]
    public string LastSyncStatus { get; set; } = "NEVER"; // NEVER, OK, FAILED

    [MaxLength(1000)]
    public string? LastSyncMessage { get; set; }

    public int TotalOrdersImported { get; set; }

    public ICollection<IntegrationSyncLog>? SyncLogs { get; set; }
}

/// <summary>
/// One sync run against an external system.
/// </summary>
public class IntegrationSyncLog : BaseEntity
{
    public long IntegrationId { get; set; }

    [MaxLength(50)]
    public string Direction { get; set; } = "INBOUND"; // INBOUND, OUTBOUND

    [MaxLength(50)]
    public string Status { get; set; } = "OK"; // OK, FAILED, PARTIAL

    public int RecordsProcessed { get; set; }
    public int RecordsFailed { get; set; }

    public int DurationMs { get; set; }

    [MaxLength(2000)]
    public string? Message { get; set; }

    [ForeignKey(nameof(IntegrationId))]
    public Integration? Integration { get; set; }
}

/// <summary>
/// Third-party logistics partner used for handover shipments and cross-border legs.
/// </summary>
public class LogisticsPartner : BaseEntity
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty; // JNE, POS, DHL, FEDEX

    [MaxLength(50)]
    public string PartnerType { get; set; } = "DOMESTIC"; // DOMESTIC, CROSS_BORDER, LAST_MILE, FREIGHT

    /// <summary>JSON array of ISO country codes this partner can deliver to.</summary>
    [MaxLength(1000)]
    public string? CoverageCountries { get; set; }

    /// <summary>JSON array of served Indonesian cities/provinces.</summary>
    [MaxLength(2000)]
    public string? CoverageAreas { get; set; }

    public decimal BaseRatePerKg { get; set; }
    public decimal HandoverFee { get; set; }

    public int EstimatedDaysMin { get; set; } = 2;
    public int EstimatedDaysMax { get; set; } = 5;

    public bool SupportsCod { get; set; }
    public bool SupportsInsurance { get; set; } = true;
    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? ApiEndpoint { get; set; }

    [MaxLength(500)]
    public string? ApiKey { get; set; }

    public double Rating { get; set; } = 4.5;

    public ICollection<PartnerShipment>? Shipments { get; set; }
}

/// <summary>
/// An order leg handed over to a 3PL partner.
/// </summary>
public class PartnerShipment : BaseEntity
{
    public long OrderId { get; set; }
    public long LogisticsPartnerId { get; set; }

    [Required, MaxLength(100)]
    public string HandoverNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? PartnerTrackingNumber { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "HANDED_OVER"; // HANDED_OVER, IN_TRANSIT, DELIVERED, RETURNED, FAILED

    public decimal PartnerCost { get; set; }

    public bool IsCrossBorder { get; set; }

    [MaxLength(2)]
    public string? DestinationCountry { get; set; }

    public DateTime HandoverAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }

    [ForeignKey(nameof(LogisticsPartnerId))]
    public LogisticsPartner? Partner { get; set; }
}

/// <summary>
/// Smart locker station — parcels can be dropped off or collected with a PIN.
/// </summary>
public class SmartLocker : BaseEntity
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty; // LKR-JKT-001

    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "ONLINE"; // ONLINE, OFFLINE, MAINTENANCE

    /// <summary>Battery / mains telemetry reported by the locker controller.</summary>
    public double? BatteryPercent { get; set; }
    public double? TemperatureCelsius { get; set; }
    public DateTime? LastHeartbeat { get; set; }

    public ICollection<LockerCompartment>? Compartments { get; set; }
}

/// <summary>
/// A single door inside a smart locker.
/// </summary>
public class LockerCompartment : BaseEntity
{
    public long SmartLockerId { get; set; }

    [Required, MaxLength(20)]
    public string CompartmentNumber { get; set; } = string.Empty; // A1, A2, B1

    [MaxLength(20)]
    public string Size { get; set; } = "M"; // S, M, L, XL

    [MaxLength(50)]
    public string Status { get; set; } = "EMPTY"; // EMPTY, OCCUPIED, RESERVED, FAULTY

    public long? OrderId { get; set; }

    /// <summary>Collection PIN handed to the recipient.</summary>
    [MaxLength(10)]
    public string? AccessPin { get; set; }

    public DateTime? OccupiedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? CollectedAt { get; set; }

    [ForeignKey(nameof(SmartLockerId))]
    public SmartLocker? Locker { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }
}
