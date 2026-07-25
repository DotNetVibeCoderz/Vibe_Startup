using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ngibrid.Models;

/// <summary>
/// Courier profile - extends user with courier-specific data
/// </summary>
public class CourierProfile : BaseEntity
{
    public long UserId { get; set; }

    [Required, MaxLength(50)]
    public string CourierId { get; set; } = string.Empty; // CID-001

    [MaxLength(50)]
    public string VehicleType { get; set; } = "MOTORCYCLE"; // MOTORCYCLE, CAR, VAN, TRUCK

    [MaxLength(20)]
    public string VehiclePlateNumber { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Status { get; set; } = "AVAILABLE"; // AVAILABLE, ON_DELIVERY, OFFLINE, RESTING

    public double CurrentLatitude { get; set; }
    public double CurrentLongitude { get; set; }
    public DateTime? LastLocationUpdate { get; set; }

    public double Rating { get; set; } = 5.0;
    public int TotalDeliveries { get; set; } = 0;
    public int SuccessfulDeliveries { get; set; } = 0;

    public double MaxLoadKg { get; set; } = 20.0;
    public double CurrentLoadKg { get; set; } = 0;

    [MaxLength(500)]
    public string? ServiceArea { get; set; } // JSON array of area codes

    public bool IsAvailable { get; set; } = true;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    public ICollection<DeliverySchedule>? Schedules { get; set; }
}

/// <summary>
/// Delivery schedule for couriers
/// </summary>
public class DeliverySchedule : BaseEntity
{
    public long CourierProfileId { get; set; }
    public long OrderId { get; set; }

    public DateTime ScheduledDate { get; set; }
    public DateTime? EstimatedDeliveryTime { get; set; }
    public DateTime? ActualDeliveryTime { get; set; }

    public int SequenceNumber { get; set; } // Order in courier's route

    [MaxLength(50)]
    public string Status { get; set; } = "SCHEDULED"; // SCHEDULED, IN_PROGRESS, COMPLETED, FAILED

    public double EstimatedDistanceKm { get; set; }
    public double ActualDistanceKm { get; set; }

    public string? RouteData { get; set; } // JSON with waypoints

    [ForeignKey(nameof(CourierProfileId))]
    public CourierProfile? CourierProfile { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }
}

/// <summary>
/// Pickup request from customer
/// </summary>
public class PickupRequest : BaseEntity
{
    public long CustomerId { get; set; }

    [Required, MaxLength(100)]
    public string RequestNumber { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string PickupAddress { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? PickupProvince { get; set; }

    /// <summary>City as stored in the master table, e.g. "Kota Bandung" / "Kabupaten Bandung".</summary>
    [MaxLength(100)]
    public string? PickupCity { get; set; }

    [MaxLength(10)]
    public string? PickupPostalCode { get; set; }

    public DateTime RequestedPickupDate { get; set; }
    [MaxLength(50)]
    public string? PreferredTimeSlot { get; set; } // MORNING, AFTERNOON, EVENING

    public int EstimatedPackageCount { get; set; }
    public double EstimatedWeightKg { get; set; }

    [MaxLength(500)]
    public string? SpecialInstructions { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "REQUESTED"; // REQUESTED, ASSIGNED, PICKED_UP, CANCELLED

    public long? AssignedCourierId { get; set; }
    public DateTime? ActualPickupTime { get; set; }

    [ForeignKey(nameof(CustomerId))]
    public ApplicationUser? Customer { get; set; }

    [ForeignKey(nameof(AssignedCourierId))]
    public ApplicationUser? AssignedCourier { get; set; }
}

/// <summary>
/// Customer support ticket
/// </summary>
public class SupportTicket : BaseEntity
{
    public long UserId { get; set; }
    public long? OrderId { get; set; }

    [Required, MaxLength(100)]
    public string TicketNumber { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Category { get; set; } = "GENERAL"; // GENERAL, COMPLAINT, LOST_PACKAGE, DAMAGE, REFUND, OTHER

    [Required, MaxLength(200)]
    public string Subject { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Priority { get; set; } = "NORMAL"; // LOW, NORMAL, HIGH, URGENT

    [MaxLength(50)]
    public string Status { get; set; } = "OPEN"; // OPEN, IN_PROGRESS, WAITING_CUSTOMER, RESOLVED, CLOSED

    public DateTime? ResolvedAt { get; set; }
    public long? AssignedToUserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }

    public ICollection<SupportMessage>? Messages { get; set; }
}

/// <summary>
/// Support ticket messages
/// </summary>
public class SupportMessage : BaseEntity
{
    public long SupportTicketId { get; set; }
    public long SenderId { get; set; }

    [Required, MaxLength(5000)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(50)]
    public string SenderType { get; set; } = "CUSTOMER"; // CUSTOMER, AGENT, SYSTEM

    [MaxLength(500)]
    public string? AttachmentUrl { get; set; }

    [ForeignKey(nameof(SupportTicketId))]
    public SupportTicket? Ticket { get; set; }
}
