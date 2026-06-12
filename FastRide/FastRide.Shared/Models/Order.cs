namespace FastRide.Shared.Models;

/// <summary>
/// Represents a ride-hailing order in the system.
/// </summary>
public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
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
    public VehicleCategory VehicleCategory { get; set; } = VehicleCategory.Economy;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    // Status tracking
    public OrderStatus Status { get; set; } = OrderStatus.Requested;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    // Rating
    public int? RiderRating { get; set; }
    public int? DriverRating { get; set; }
    public string? ReviewComment { get; set; }

    // Navigation
    public User Rider { get; set; } = null!;
    public User? Driver { get; set; }

    // Multi-stop support
    public ICollection<TripStop> Stops { get; set; } = new List<TripStop>();
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

    // Navigation
    public Order Order { get; set; } = null!;
}
