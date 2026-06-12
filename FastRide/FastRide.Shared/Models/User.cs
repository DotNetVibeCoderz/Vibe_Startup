namespace FastRide.Shared.Models;

/// <summary>Represents a user (Rider or Driver).</summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Rider;
    public bool IsVerified { get; set; } = false;

    /// <summary>Public URL to profile photo on configured storage provider.</summary>
    public string? PhotoUrl { get; set; }

    /// <summary>Legacy base64 fallback for previously stored photos.</summary>
    public string? ProfilePhotoBase64 { get; set; }

    /// <summary>MIME type hint for the photo.</summary>
    public string? ProfilePhotoMimeType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public DriverProfile? DriverProfile { get; set; }
    public ICollection<Order> RiderOrders { get; set; } = new List<Order>();
}

public class DriverProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string LicenseNumber { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public string VehiclePlate { get; set; } = string.Empty;
    public DriverStatus Status { get; set; } = DriverStatus.Offline;
    public double Rating { get; set; } = 5.0;
    public int TotalTrips { get; set; }
    public decimal TotalEarnings { get; set; }
    public double CurrentLatitude { get; set; }
    public double CurrentLongitude { get; set; }
    public User User { get; set; } = null!;
}
