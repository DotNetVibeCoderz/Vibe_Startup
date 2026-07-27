// Back-office models for the Admin / Operator / Merchant roles.
using Joka.Models.Common;

namespace Joka.Models.Backoffice;

/// <summary>Role names used across authorization policies and seeding.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Operator = "Operator";
    public const string Merchant = "Merchant";
    public const string Customer = "User";

    public static readonly string[] All = { Admin, Operator, Merchant, Customer };

    /// <summary>Roles that can reach any back-office area at all.</summary>
    public const string BackOffice = Admin + "," + Operator + "," + Merchant;
}

/// <summary>A partner company (hotel chain, airline, bus operator, event organiser).</summary>
public class Merchant : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Hotel";     // Hotel, Airline, Transport, Activity, Package
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string ContactEmail { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public string Status { get; set; } = "Active";      // Pending, Active, Suspended
    public decimal CommissionRate { get; set; } = 10m;  // percent
    public double AverageRating { get; set; }
    public int TotalProducts { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Periodic payout from Joka to a merchant, reconciled against transactions.</summary>
public class MerchantSettlement : BaseEntity
{
    public Guid MerchantId { get; set; }
    public Merchant? Merchant { get; set; }

    public string ReferenceNo { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }

    public int TransactionCount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal NetAmount { get; set; }

    public string Status { get; set; } = "Pending";     // Pending, Reconciled, Paid, Disputed
    public string? BankReference { get; set; }
    public DateTime? PaidAt { get; set; }

    /// <summary>Difference between our figure and the bank statement, if any.</summary>
    public decimal VarianceAmount { get; set; }
    public string? Notes { get; set; }
}

/// <summary>A transaction flagged as suspicious by rule-based scoring.</summary>
public class FraudAlert : BaseEntity
{
    public string TransactionCode { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }

    public string Rule { get; set; } = string.Empty;    // which rule fired
    public string Reason { get; set; } = string.Empty;
    public int RiskScore { get; set; }                  // 0-100
    public decimal Amount { get; set; }

    public string Severity { get; set; } = "Medium";    // Low, Medium, High, Critical
    public string Status { get; set; } = "Open";        // Open, Reviewing, Cleared, Confirmed
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
}

/// <summary>Operational problem logged by an operator (technical, fraud, customer).</summary>
public class IncidentReport : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "Technical"; // Technical, Fraud, Customer, Partner
    public string Severity { get; set; } = "Medium";    // Low, Medium, High, Critical
    public string Description { get; set; } = string.Empty;
    public string? RelatedBookingCode { get; set; }

    public string ReportedBy { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";        // Open, InProgress, Resolved, Closed
    public string? AssignedTo { get; set; }
    public string? Resolution { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

/// <summary>Refund or reschedule asked for by a customer, handled by an operator.</summary>
public class RefundRequest : BaseEntity
{
    public string BookingCode { get; set; } = string.Empty;
    public string BookingType { get; set; } = "flight"; // flight, train, bus, hotel
    public Guid? UserId { get; set; }
    public string? CustomerName { get; set; }

    public string RequestType { get; set; } = "Refund"; // Refund, Reschedule
    public string Reason { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime? NewDepartureDate { get; set; }     // for reschedule

    public string Status { get; set; } = "Pending";     // Pending, Approved, Rejected, Completed
    public string? HandledBy { get; set; }
    public DateTime? HandledAt { get; set; }
    public string? HandlingNote { get; set; }
}

/// <summary>
/// Master-data change submitted by a merchant that an admin must approve.
/// The payload holds the proposed values as JSON so the change can be reviewed
/// before it touches the live product tables.
/// </summary>
public class ApprovalRequest : BaseEntity
{
    public Guid MerchantId { get; set; }
    public Merchant? Merchant { get; set; }

    public string EntityType { get; set; } = string.Empty; // Hotel, Room, Flight, BusSchedule...
    public string? EntityId { get; set; }
    public string ChangeType { get; set; } = "Update";     // Create, Update, Delete
    public string Summary { get; set; } = string.Empty;
    public string? PayloadJson { get; set; }

    public string Status { get; set; } = "Pending";        // Pending, Approved, Rejected
    public string RequestedBy { get; set; } = string.Empty;
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
}

/// <summary>An outbound integration with an airline / hotel / transport provider.</summary>
public class ApiIntegration : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Category { get; set; } = "Flight";    // Flight, Hotel, Train, Bus, Payment, Maps
    public string? BaseUrl { get; set; }
    public string Environment { get; set; } = "Sandbox"; // Sandbox, Production
    public bool IsEnabled { get; set; } = true;
    public string Status { get; set; } = "Connected";   // Connected, Degraded, Down, NotConfigured
    public DateTime? LastSyncAt { get; set; }
    public int? LastLatencyMs { get; set; }
    public string? LastError { get; set; }
}

/// <summary>Latest health sample for one system component.</summary>
public class SystemHealthCheck : BaseEntity
{
    public string Component { get; set; } = string.Empty; // Web, Database, Storage, ChatBot...
    public string Status { get; set; } = "Healthy";       // Healthy, Degraded, Down
    public int ResponseTimeMs { get; set; }
    public double UptimePercent { get; set; } = 100;
    public int ErrorCount24h { get; set; }
    public string? Message { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}
