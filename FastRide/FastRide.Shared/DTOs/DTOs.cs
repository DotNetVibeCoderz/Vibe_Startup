using FastRide.Shared.Models;

namespace FastRide.Shared.DTOs;

// ══════════════════ AUTH DTOs ══════════════════

public record RegisterRequest(
    string FullName,
    string Email,
    string PhoneNumber,
    string Password,
    UserRole Role = UserRole.Rider
);

public record LoginRequest(string Email, string Password);

public record AuthResponse(
    Guid UserId, string FullName, string Email, string Token,
    UserRole Role, DateTime ExpiresAt,
    string? ProfilePhotoBase64 = null, string? ProfilePhotoMimeType = null
);

public record ResetPasswordRequest(string Email, string ResetCode, string NewPassword);

// ══════════════════ USER / PROFILE DTOs ══════════════════

public record UserProfileResponse(
    Guid Id, string FullName, string Email, string PhoneNumber,
    UserRole Role, bool IsVerified, DateTime CreatedAt,
    DriverProfileResponse? DriverProfile,
    string? ProfilePhotoBase64 = null, string? ProfilePhotoMimeType = null
);

public record DriverProfileResponse(
    string LicenseNumber, string VehicleType, string VehiclePlate,
    DriverStatus Status, double Rating, int TotalTrips,
    decimal TotalEarnings, double CurrentLatitude, double CurrentLongitude
);

/// <summary>
/// Payload for updating user profile (name, phone, photo).
/// </summary>
public record UpdateProfileRequest(
    string? FullName = null,
    string? PhoneNumber = null,
    string? ProfilePhotoBase64 = null,
    string? ProfilePhotoMimeType = null
);

/// <summary>
/// Payload for driver profile update.
/// </summary>
public record UpdateDriverProfileRequest(
    string? LicenseNumber = null,
    string? VehicleType = null,
    string? VehiclePlate = null,
    DriverStatus? Status = null
);

// ══════════════════ ORDER DTOs ══════════════════

public record CreateOrderRequest(
    double PickupLatitude, double PickupLongitude, string PickupAddress,
    double DropoffLatitude, double DropoffLongitude, string DropoffAddress,
    VehicleCategory VehicleCategory = VehicleCategory.Economy,
    PaymentMethod PaymentMethod = PaymentMethod.Cash,
    string? PromoCode = null
);

public record OrderResponse(
    Guid Id, Guid RiderId, string RiderName, Guid? DriverId, string? DriverName,
    string PickupAddress, string DropoffAddress, double DistanceKm,
    int EstimatedDurationMinutes, decimal EstimatedFare, decimal FinalFare,
    VehicleCategory VehicleCategory, OrderStatus Status,
    DateTime CreatedAt, DateTime? CompletedAt, List<TripStopResponse> Stops
);

public record TripStopResponse(int SequenceNumber, double Latitude, double Longitude, string Address, TripStopType StopType);

// ══════════════════ PAYMENT DTOs ══════════════════

public record PaymentRequest(Guid OrderId, PaymentMethod Method, decimal Amount);
public record PaymentResponse(Guid Id, Guid OrderId, decimal Amount, PaymentMethod Method, PaymentStatus Status, DateTime CreatedAt, string? TransactionReference);

// ══════════════════ DRIVER MOBILE DTOs ══════════════════

/// <summary>
/// Driver home screen data — earnings, stats, incoming orders.
/// </summary>
public record DriverHomeResponse(
    Guid DriverId, string FullName, bool IsOnline,
    decimal TodayEarnings, int TodayTrips, double Rating,
    List<IncomingOrderItem> IncomingOrders,
    List<RecentTripItem> RecentTrips
);

public record IncomingOrderItem(
    Guid OrderId, string RiderName, string PickupAddress, string DropoffAddress,
    double DistanceKm, decimal EstimatedFare, int WaitSeconds
);

public record RecentTripItem(
    Guid OrderId, string RiderName, string DropoffAddress,
    decimal Fare, string Status, DateTime CreatedAt
);

/// <summary>
/// Driver earnings summary.
/// </summary>
public record DriverEarningsResponse(
    decimal TodayEarnings, decimal WeekEarnings, decimal MonthEarnings,
    int TodayTrips, int WeekTrips, int MonthTrips,
    decimal BaseFareEarnings, decimal BonusEarnings, decimal TipEarnings,
    List<DailyEarningItem> DailyBreakdown
);

public record DailyEarningItem(DateTime Date, decimal Earnings, int Trips);

// ══════════════════ RIDER MOBILE DTOs ══════════════════

public record RiderHomeResponse(
    Guid RiderId, string FullName,
    int TotalTrips, decimal TotalSpent, double AverageRating,
    List<RecentRiderTripItem> RecentTrips
);

public record RecentRiderTripItem(
    Guid OrderId, string DriverName, string PickupAddress, string DropoffAddress,
    decimal Fare, string Status, DateTime CreatedAt
);

public record BookRideResponse(
    Guid OrderId, string Status, decimal EstimatedFare, decimal FinalFare,
    double DistanceKm, int EstimatedDurationMinutes, string? PromoApplied
);

// ══════════════════ DASHBOARD DTOs ══════════════════

public record DashboardStatsResponse(
    int TotalOrdersToday, int ActiveDrivers, int ActiveRiders,
    decimal TotalRevenueToday, double AverageRating,
    List<OrderStatusCount> OrdersByStatus, List<HourlyStats> HourlyBreakdown
);

public record OrderStatusCount(OrderStatus Status, int Count);
public record HourlyStats(int Hour, int OrderCount, decimal Revenue);
public record DashboardFilter(DateTime? StartDate = null, DateTime? EndDate = null, string? Location = null, VehicleCategory? VehicleCategory = null, OrderStatus? Status = null);

// ══════════════════ NOTIFICATION DTOs ══════════════════

public record NotificationResponse(Guid Id, string Title, string Message, NotificationType Type, bool IsRead, DateTime CreatedAt);

// ══════════════════ REVIEW DTO ══════════════════

public record SubmitReviewRequest(Guid OrderId, Guid ReviewerId, Guid TargetUserId, int Rating, string? Comment);
