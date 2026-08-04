namespace FastRide.Shared.Models;

/// <summary>
/// Represents a notification sent to users.
/// </summary>
public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; } = NotificationType.Info;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }

    /// <summary>Order this notification refers to, so the app can deep-link to the trip.</summary>
    public Guid? OrderId { get; set; }
}

/// <summary>
/// Represents a rating/review given after a trip.
/// </summary>
public class Review
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid ReviewerId { get; set; }
    public Guid TargetUserId { get; set; }
    public int Rating { get; set; } // 1-5 stars
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Order Order { get; set; } = null!;
}

/// <summary>
/// Represents fare/pricing configuration per vehicle category.
/// </summary>
public class FareConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public VehicleCategory VehicleCategory { get; set; } = VehicleCategory.Economy;
    public decimal BaseFare { get; set; } = 5000m;      // IDR
    public decimal CostPerKm { get; set; } = 3000m;
    public decimal CostPerMinute { get; set; } = 500m;
    public decimal MinimumFare { get; set; } = 10000m;
    public decimal SurgeMultiplier { get; set; } = 1.0m;
    public decimal CancellationFee { get; set; } = 5000m;
    public bool IsActive { get; set; } = true;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Quote for a trip. Surge applies to the metered part, then the minimum fare acts as a floor —
    /// the same order the fare breakdown is shown to the rider.
    /// </summary>
    public decimal Quote(double distanceKm, int durationMinutes)
    {
        var metered = BaseFare + (CostPerKm * (decimal)distanceKm) + (CostPerMinute * durationMinutes);
        var surged = metered * (SurgeMultiplier <= 0 ? 1m : SurgeMultiplier);
        return Math.Round(Math.Max(surged, MinimumFare), 0);
    }
}
