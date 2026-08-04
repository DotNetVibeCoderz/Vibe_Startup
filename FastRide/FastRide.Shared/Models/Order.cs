namespace FastRide.Shared.Models;

/// <summary>
/// Represents a ride-hailing order in the system.
/// </summary>
public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Short human-readable code shown in the apps and support tickets (e.g. FR-8H2K4Q).</summary>
    public string Code { get; set; } = string.Empty;

    public Guid RiderId { get; set; }
    public Guid? DriverId { get; set; }

    // Pickup location
    public double PickupLatitude { get; set; }
    public double PickupLongitude { get; set; }
    public string PickupAddress { get; set; } = string.Empty;

    // Drop-off location
    public double DropoffLatitude { get; set; }
    public double DropoffLongitude { get; set; }
    public string DropoffAddress { get; set; } = string.Empty;

    // Trip details
    public double DistanceKm { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public decimal EstimatedFare { get; set; }
    public decimal FinalFare { get; set; }

    /// <summary>Discount actually granted by the promo, kept so revenue reports can net it out.</summary>
    public decimal DiscountAmount { get; set; }
    public string? PromoCode { get; set; }

    /// <summary>Surge multiplier captured at booking time — fares must not change retroactively.</summary>
    public decimal SurgeMultiplier { get; set; } = 1.0m;

    public VehicleCategory VehicleCategory { get; set; } = VehicleCategory.Economy;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    // Status tracking
    public OrderStatus Status { get; set; } = OrderStatus.Requested;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }
    public DateTime? ArrivedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public CancelledByParty? CancelledBy { get; set; }

    // Live driver position for this trip (mirrors DriverProfile, kept per-order for history)
    public double? DriverLatitude { get; set; }
    public double? DriverLongitude { get; set; }

    // Rating
    public int? RiderRating { get; set; }
    public int? DriverRating { get; set; }
    public string? ReviewComment { get; set; }

    // Navigation
    public User Rider { get; set; } = null!;
    public User? Driver { get; set; }

    // Multi-stop support
    public ICollection<TripStop> Stops { get; set; } = new List<TripStop>();

    /// <summary>Statuses from which the trip can no longer change hands.</summary>
    public bool IsTerminal => Status is OrderStatus.Completed or OrderStatus.Cancelled or OrderStatus.Expired;
}

/// <summary>
/// Represents an intermediate stop in a multi-stop trip.
/// </summary>
public class TripStop
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public int SequenceNumber { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Address { get; set; } = string.Empty;
    public TripStopType StopType { get; set; } = TripStopType.Waypoint;

    /// <summary>Set when the driver marks the waypoint as reached.</summary>
    public DateTime? ReachedAt { get; set; }

    // Navigation
    public Order Order { get; set; } = null!;
}
