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
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
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
/// Represents fare/pricing configuration.
/// </summary>
public class FareConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public VehicleCategory VehicleCategory { get; set; } = VehicleCategory.Economy;
    public decimal BaseFare { get; set; } = 5000m; // In IDR
    public decimal CostPerKm { get; set; } = 3000m;
    public decimal CostPerMinute { get; set; } = 500m;
    public decimal MinimumFare { get; set; } = 10000m;
    public decimal SurgeMultiplier { get; set; } = 1.0m;
    public bool IsActive { get; set; } = true;
}
