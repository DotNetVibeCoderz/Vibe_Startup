namespace FastRide.Shared.Models;

/// <summary>Represents a user (Rider, Driver or Admin).</summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Rider;

    /// <summary>Email/phone verified. Drivers additionally need approved documents.</summary>
    public bool IsVerified { get; set; }

    /// <summary>Soft-delete / suspension flag. Suspended users cannot log in.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Public URL to profile photo on configured storage provider.</summary>
    public string? PhotoUrl { get; set; }

    /// <summary>Legacy base64 fallback for previously stored photos.</summary>
    public string? ProfilePhotoBase64 { get; set; }

    /// <summary>MIME type hint for the photo.</summary>
    public string? ProfilePhotoMimeType { get; set; }

    /// <summary>
    /// Incremented on logout / password change. Tokens carrying an older value are rejected,
    /// which is what makes logout and "sign out everywhere" actually terminate a session.
    /// </summary>
    public int SecurityStamp { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

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
    public VehicleCategory VehicleCategory { get; set; } = VehicleCategory.Economy;
    public DriverStatus Status { get; set; } = DriverStatus.Offline;
    public double Rating { get; set; } = 5.0;
    public int RatingCount { get; set; }
    public int TotalTrips { get; set; }
    public decimal TotalEarnings { get; set; }

    public double CurrentLatitude { get; set; }
    public double CurrentLongitude { get; set; }

    /// <summary>Last time the driver pushed a GPS ping. Stale drivers are excluded from matching.</summary>
    public DateTime? LocationUpdatedAt { get; set; }

    /// <summary>Heading in degrees (0-359), used to orient the marker on the map.</summary>
    public double Heading { get; set; }

    /// <summary>All required documents approved by an admin.</summary>
    public bool IsDocumentVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<DriverDocument> Documents { get; set; } = new List<DriverDocument>();
}

/// <summary>A document uploaded by a driver for verification (SIM, STNK, KTP, ...).</summary>
public class DriverDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DriverProfileId { get; set; }
    public DocumentType Type { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

    /// <summary>Public URL on the configured storage provider.</summary>
    public string FileUrl { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }

    public DriverProfile DriverProfile { get; set; } = null!;
}
