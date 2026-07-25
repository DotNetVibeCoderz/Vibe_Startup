using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ngibrid.Models;

/// <summary>
/// Tax record per order — PPN and, for international shipments, import duty (bea masuk).
/// Kept separate from Invoice so tax periods can be reported independently of billing.
/// </summary>
public class TaxRecord : BaseEntity
{
    public long OrderId { get; set; }

    [Required, MaxLength(50)]
    public string TaxNumber { get; set; } = string.Empty; // TAX-20250101-0001

    [MaxLength(50)]
    public string TaxType { get; set; } = "PPN"; // PPN, PPH, IMPORT_DUTY, EXPORT_LEVY

    /// <summary>NPWP of the paying party, when supplied.</summary>
    [MaxLength(30)]
    public string? TaxpayerId { get; set; }

    public decimal TaxableAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "IDR";

    /// <summary>Reporting period, normalised to the first day of the month.</summary>
    public DateTime Period { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "RECORDED"; // RECORDED, REPORTED, SETTLED

    public DateTime? ReportedAt { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }
}

/// <summary>
/// Customs declaration for a cross-border shipment (bea cukai).
/// </summary>
public class CustomsDeclaration : BaseEntity
{
    public long OrderId { get; set; }

    [Required, MaxLength(50)]
    public string DeclarationNumber { get; set; } = string.Empty; // CUS-20250101-0001

    [MaxLength(20)]
    public string DeclarationType { get; set; } = "EXPORT"; // EXPORT, IMPORT

    [MaxLength(2)]
    public string OriginCountry { get; set; } = "ID";

    [MaxLength(2)]
    public string DestinationCountry { get; set; } = "SG";

    /// <summary>Harmonized System tariff code.</summary>
    [MaxLength(20)]
    public string? HsCode { get; set; }

    [MaxLength(500)]
    public string GoodsDescription { get; set; } = string.Empty;

    public decimal DeclaredValue { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "USD";

    public decimal DutyAmount { get; set; }
    public decimal VatAmount { get; set; }

    /// <summary>Incoterms 2020 delivery term.</summary>
    [MaxLength(20)]
    public string Incoterm { get; set; } = "DAP"; // EXW, FOB, CIF, DAP, DDP

    [MaxLength(50)]
    public string Status { get; set; } = "DRAFT"; // DRAFT, SUBMITTED, CLEARED, HELD, REJECTED

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ClearedAt { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }

    public ICollection<ComplianceDocument>? Documents { get; set; }
}

/// <summary>
/// Export/import paperwork attached to a customs declaration.
/// </summary>
public class ComplianceDocument : BaseEntity
{
    public long CustomsDeclarationId { get; set; }

    [Required, MaxLength(50)]
    public string DocumentType { get; set; } = "COMMERCIAL_INVOICE";
    // COMMERCIAL_INVOICE, PACKING_LIST, CERTIFICATE_OF_ORIGIN, AWB, EXPORT_PERMIT, IMPORT_PERMIT

    [Required, MaxLength(200)]
    public string DocumentNumber { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? FileUrl { get; set; }

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(50)]
    public string Status { get; set; } = "ISSUED"; // ISSUED, VERIFIED, REJECTED

    [ForeignKey(nameof(CustomsDeclarationId))]
    public CustomsDeclaration? Declaration { get; set; }
}

/// <summary>
/// Loyalty point ledger. Balance on ApplicationUser.LoyaltyPoints is the running total of these rows.
/// </summary>
public class LoyaltyTransaction : BaseEntity
{
    public long UserId { get; set; }

    public long? OrderId { get; set; }

    [MaxLength(50)]
    public string TransactionType { get; set; } = "EARN"; // EARN, REDEEM, EXPIRE, ADJUST

    public int Points { get; set; } // negative for REDEEM/EXPIRE

    public int BalanceAfter { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime? ExpiresAt { get; set; }

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }
}

/// <summary>
/// Demand forecast produced by <see cref="Services.ForecastService"/> for trend analysis and capacity planning.
/// </summary>
public class DemandForecast : BaseEntity
{
    public DateTime ForecastDate { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    public double PredictedOrders { get; set; }
    public double LowerBound { get; set; }
    public double UpperBound { get; set; }

    /// <summary>Multiplier vs. the trailing baseline; > 1 means a demand peak.</summary>
    public double SeasonalIndex { get; set; } = 1.0;

    public bool IsPeakSeason { get; set; }

    [MaxLength(200)]
    public string? Method { get; set; } // HOLT_LINEAR, MOVING_AVERAGE

    public double ConfidencePercent { get; set; }
}
