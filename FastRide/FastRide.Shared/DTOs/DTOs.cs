using System.ComponentModel.DataAnnotations;
using FastRide.Shared.Models;

namespace FastRide.Shared.DTOs;

// ══════════════════════════════════════════════════════════════════════
// Single source of truth for every request and response on the wire.
// The API, AdminWeb, both MAUI apps and the simulator all bind to these
// types — nothing re-declares its own copy.
//
// Enums travel as strings (JsonStringEnumConverter is registered on both
// ends) but numeric values are still accepted on input.
// ══════════════════════════════════════════════════════════════════════

// ─────────────────────────── AUTH ───────────────────────────

public record RegisterRequest(
    [Required, StringLength(200, MinimumLength = 2)] string FullName,
    [Required, EmailAddress] string Email,
    [Required, Phone] string PhoneNumber,
    [Required, MinLength(8)] string Password,
    UserRole Role = UserRole.Rider,
    string? LicenseNumber = null,
    string? VehicleType = null,
    string? VehiclePlate = null,
    VehicleCategory VehicleCategory = VehicleCategory.Economy);

public record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);

public record AuthResponse(
    Guid UserId, string FullName, string Email, string Token,
    UserRole Role, DateTime ExpiresAt,
    string? PhotoUrl = null, string? ProfilePhotoMimeType = null,
    bool IsVerified = false);

public record ForgotPasswordRequest([Required, EmailAddress] string Email);

/// <summary>
/// In development the reset code is returned by /auth/forgot-password so the flow can be
/// exercised without an SMTP server. Wire up a real mailer before going live.
/// </summary>
public record ForgotPasswordResponse(string Message, string? ResetCode, DateTime ExpiresAt);

public record ResetPasswordRequest(
    [Required, EmailAddress] string Email,
    [Required] string ResetCode,
    [Required, MinLength(8)] string NewPassword);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword);

public record MessageResponse(string Message);

// ────────────────────── USER / PROFILE ──────────────────────

public record UserProfileResponse(
    Guid Id, string FullName, string Email, string PhoneNumber,
    UserRole Role, bool IsVerified, bool IsActive, DateTime CreatedAt,
    string? PhotoUrl, string? ProfilePhotoMimeType,
    DriverProfileResponse? Driver = null,
    RiderStatsResponse? RiderStats = null);

public record DriverProfileResponse(
    string LicenseNumber, string VehicleType, string VehiclePlate,
    VehicleCategory VehicleCategory, DriverStatus Status,
    double Rating, int RatingCount, int TotalTrips, decimal TotalEarnings,
    double CurrentLatitude, double CurrentLongitude,
    bool IsDocumentVerified, DateTime? VerifiedAt);

public record RiderStatsResponse(int TotalTrips, decimal TotalSpent, double AverageRatingGiven);

public record UpdateProfileRequest(
    string? FullName = null,
    string? PhoneNumber = null,
    string? ProfilePhotoBase64 = null,
    string? ProfilePhotoMimeType = null);

public record UpdateDriverProfileRequest(
    string? LicenseNumber = null,
    string? VehicleType = null,
    string? VehiclePlate = null,
    VehicleCategory? VehicleCategory = null);

public record ProfilePhotoResponse(Guid Id, string FullName, string? PhoneNumber, string? PhotoUrl, string? ProfilePhotoMimeType, DateTime? UpdatedAt);

// ────────────────────── DRIVER DOCUMENTS ─────────────────────

public record DriverDocumentResponse(
    Guid Id, DocumentType Type, DocumentStatus Status, string FileUrl,
    string? Notes, DateTime? ExpiresAt, DateTime UploadedAt, DateTime? ReviewedAt);

public record UploadDocumentRequest(
    DocumentType Type,
    [Required] string FileBase64,
    string? MimeType = null,
    DateTime? ExpiresAt = null);

public record ReviewDocumentRequest(DocumentStatus Status, string? Notes = null);

// ─────────────────────────── ORDERS ──────────────────────────

public record TripStopRequest(double Latitude, double Longitude, string Address);

public record CreateOrderRequest(
    Guid RiderId,
    double PickupLatitude, double PickupLongitude, [Required] string PickupAddress,
    double DropoffLatitude, double DropoffLongitude, [Required] string DropoffAddress,
    VehicleCategory VehicleCategory = VehicleCategory.Economy,
    PaymentMethod PaymentMethod = PaymentMethod.Cash,
    string? PromoCode = null,
    List<TripStopRequest>? Stops = null);

/// <summary>Fare preview shown before the rider commits to the booking.</summary>
public record FareQuoteRequest(
    double PickupLatitude, double PickupLongitude,
    double DropoffLatitude, double DropoffLongitude,
    VehicleCategory VehicleCategory = VehicleCategory.Economy,
    string? PromoCode = null,
    List<TripStopRequest>? Stops = null);

public record FareQuoteResponse(
    VehicleCategory VehicleCategory, double DistanceKm, int EstimatedDurationMinutes,
    decimal BaseFare, decimal SurgeMultiplier, decimal EstimatedFare,
    decimal Discount, decimal FinalFare, string? PromoApplied, string? PromoMessage);

public record OrderListItem(
    Guid Id, string Code, Guid RiderId, string RiderName, Guid? DriverId, string? DriverName,
    string PickupAddress, string DropoffAddress, double DistanceKm, int EstimatedDurationMinutes,
    decimal EstimatedFare, decimal FinalFare, decimal DiscountAmount,
    VehicleCategory VehicleCategory, PaymentMethod PaymentMethod, OrderStatus Status,
    DateTime CreatedAt, DateTime? CompletedAt);

public record OrderDetailResponse(
    Guid Id, string Code, OrderStatus Status,
    OrderPartyResponse Rider, OrderPartyResponse? Driver,
    double PickupLatitude, double PickupLongitude, string PickupAddress,
    double DropoffLatitude, double DropoffLongitude, string DropoffAddress,
    double DistanceKm, int EstimatedDurationMinutes,
    decimal EstimatedFare, decimal DiscountAmount, decimal FinalFare,
    decimal SurgeMultiplier, string? PromoCode,
    VehicleCategory VehicleCategory, PaymentMethod PaymentMethod,
    DateTime CreatedAt, DateTime? AcceptedAt, DateTime? ArrivedAt, DateTime? StartedAt,
    DateTime? CompletedAt, DateTime? CancelledAt, string? CancellationReason, CancelledByParty? CancelledBy,
    int? RiderRating, int? DriverRating, string? ReviewComment,
    List<TripStopResponse> Stops,
    PaymentResponse? Payment);

public record OrderPartyResponse(Guid Id, string FullName, string? PhoneNumber, string? PhotoUrl, double? Rating, string? VehicleType, string? VehiclePlate);

public record TripStopResponse(Guid Id, int SequenceNumber, double Latitude, double Longitude, string Address, TripStopType StopType, DateTime? ReachedAt);

public record CreateOrderResponse(
    Guid Id, string Code, OrderStatus Status,
    decimal EstimatedFare, decimal DiscountAmount, decimal FinalFare,
    double DistanceKm, int EstimatedDurationMinutes,
    string? PromoApplied, DateTime CreatedAt);

public record CancelOrderRequest(string? Reason = null);

/// <summary>Live position + status for the rider's tracking screen.</summary>
public record OrderTrackingResponse(
    Guid OrderId, string Code, OrderStatus Status,
    string? DriverName, string? VehicleType, string? VehiclePlate, string? DriverPhotoUrl, double? DriverRating,
    double? DriverLatitude, double? DriverLongitude,
    double PickupLatitude, double PickupLongitude,
    double DropoffLatitude, double DropoffLongitude,
    double? DriverDistanceKm, int? EtaMinutes, DateTime UpdatedAt);

// ────────────────────────── PAYMENTS ─────────────────────────

public record PaymentRequest(
    Guid OrderId,
    PaymentMethod Method,
    decimal Amount = 0,
    EWalletChannel WalletChannel = EWalletChannel.Unspecified);

public record PaymentResponse(
    Guid Id, Guid OrderId, string? OrderCode, decimal Amount, decimal DiscountAmount,
    PaymentMethod Method, PaymentStatus Status,
    DateTime CreatedAt, DateTime? CompletedAt, string? TransactionReference,
    EWalletChannel WalletChannel = EWalletChannel.Unspecified,
    string? ProviderName = null,

    /// <summary>QRIS payload, virtual account number, or redirect URL — whatever the payer needs.</summary>
    string? PaymentPayload = null,

    DateTime? ExpiresAt = null,
    string? FailureReason = null,

    /// <summary>
    /// The QRIS payload rendered as a scannable SVG data URI. Present only for QRIS charges,
    /// so the apps can show a code without carrying a QR encoder.
    /// </summary>
    string? QrImage = null)
{
    public bool IsSettled => Status == PaymentStatus.Completed;
    public bool IsInFlight => Status is PaymentStatus.Pending or PaymentStatus.AwaitingPayment;
    public bool CanRetry => Status is PaymentStatus.Failed or PaymentStatus.Expired;
}

/// <summary>Payment methods a rider can pick right now, given what is switched on.</summary>
public record AvailablePaymentMethodsResponse(List<PaymentMethodOption> Methods);

public record PaymentMethodOption(PaymentMethod Method, string Label, string Icon, bool RequiresApp);

// ───────────────────── payment provider admin ─────────────────────

public record PaymentProviderResponse(
    Guid Id, string Name, string DisplayName, bool IsEnabled, bool IsSandbox,
    List<PaymentMethod> Methods, int Priority,
    string? MerchantId, string? MerchantName, string? MerchantCity,
    int ChargeExpiryMinutes,

    /// <summary>Whether a credential is set — never the credential itself.</summary>
    bool HasServerKey, bool HasClientKey, bool HasWebhookSecret,
    string? BaseUrl, DateTime? UpdatedAt);

public record SavePaymentProviderRequest(
    bool IsEnabled,
    bool IsSandbox,
    List<PaymentMethod> Methods,
    int Priority,
    string? MerchantId = null,
    string? MerchantName = null,
    string? MerchantCity = null,
    int ChargeExpiryMinutes = 15,
    string? BaseUrl = null,

    /// <summary>Leave null to keep the stored credential; send a value to replace it.</summary>
    string? ServerKey = null,
    string? ClientKey = null,
    string? WebhookSecret = null);

// ─────────────────────────── PROMOS ──────────────────────────

public record PromoResponse(
    Guid Id, string Code, string Description, PromoType Type,
    decimal Value, decimal MaxDiscount, decimal MinOrderAmount,
    VehicleCategory? VehicleCategory,
    DateTime ValidFrom, DateTime ValidUntil, bool IsActive,
    int UsageLimit, int UsageCount);

public record SavePromoRequest(
    [Required] string Code, string Description, PromoType Type,
    decimal Value, decimal MaxDiscount = 0, decimal MinOrderAmount = 0,
    VehicleCategory? VehicleCategory = null,
    DateTime? ValidFrom = null, DateTime? ValidUntil = null,
    bool IsActive = true, int UsageLimit = 100);

public record ValidatePromoRequest([Required] string Code, decimal Amount, VehicleCategory? VehicleCategory = null);

public record ValidatePromoResponse(bool Valid, string? Code, string? Description, PromoType? Type, decimal Discount, decimal FinalAmount, string Message);

// ───────────────────────── FARE CONFIG ───────────────────────

public record FareConfigResponse(
    Guid Id, VehicleCategory VehicleCategory,
    decimal BaseFare, decimal CostPerKm, decimal CostPerMinute,
    decimal MinimumFare, decimal SurgeMultiplier, decimal CancellationFee,
    bool IsActive, DateTime? UpdatedAt);

public record UpdateFareConfigRequest(
    decimal BaseFare, decimal CostPerKm, decimal CostPerMinute,
    decimal MinimumFare, decimal SurgeMultiplier, decimal CancellationFee, bool IsActive);

// ─────────────────────────── DRIVERS ─────────────────────────

public record DriverListItem(
    Guid Id, string FullName, string Email, string PhoneNumber, string? PhotoUrl,
    DriverStatus Status, double Rating, int TotalTrips, decimal TotalEarnings,
    string VehicleType, string VehiclePlate, VehicleCategory VehicleCategory,
    double CurrentLatitude, double CurrentLongitude, DateTime? LocationUpdatedAt,
    bool IsDocumentVerified, bool IsActive);

public record NearbyDriverItem(Guid DriverId, string FullName, string VehicleType, string VehiclePlate, double Rating, double Latitude, double Longitude, double Heading, double DistanceKm);

public record UpdateLocationRequest(double Latitude, double Longitude, double Heading = 0);

public record SetDriverStatusRequest(DriverStatus Status);

public record DriverStatusResponse(DriverStatus Status, bool IsOnline);

// ───────────────────────── RIDERS ────────────────────────────

public record RiderListItem(
    Guid Id, string FullName, string Email, string PhoneNumber, string? PhotoUrl,
    bool IsVerified, bool IsActive, DateTime CreatedAt, int TotalTrips, decimal TotalSpent);

// ─────────────────────── MOBILE: RIDER ───────────────────────

public record RiderHomeResponse(
    Guid UserId, string FullName, string? PhotoUrl,
    int TotalTrips, decimal TotalSpent, int UnreadNotifications,
    OrderListItem? ActiveOrder,
    List<RecentTripItem> RecentTrips);

public record RecentTripItem(
    Guid OrderId, string Code, string? DriverName, string PickupAddress, string DropoffAddress,
    decimal Fare, OrderStatus Status, DateTime CreatedAt, int? DriverRating);

// ─────────────────────── MOBILE: DRIVER ──────────────────────

public record DriverHomeResponse(
    Guid DriverId, string FullName, string? PhotoUrl, bool IsOnline, bool IsDocumentVerified,
    decimal TodayEarnings, int TodayTrips, double Rating, int UnreadNotifications,
    OrderDetailResponse? ActiveTrip,
    List<IncomingOrderItem> IncomingOrders,
    List<RecentTripItem> RecentTrips);

public record IncomingOrderItem(
    Guid OrderId, string Code, string RiderName, string PickupAddress, string DropoffAddress,
    double DistanceKm, double PickupDistanceKm, decimal EstimatedFare,
    VehicleCategory VehicleCategory, PaymentMethod PaymentMethod, int WaitSeconds);

public record DriverEarningsResponse(
    decimal TodayEarnings, decimal WeekEarnings, decimal MonthEarnings,
    int TodayTrips, int WeekTrips, int MonthTrips,
    decimal AveragePerTrip, decimal TotalEarnings,
    List<DailyEarningItem> DailyBreakdown);

public record DailyEarningItem(DateTime Date, decimal Earnings, int Trips);

public record AcceptOrderRequest(Guid OrderId);

// ───────────────────────── DASHBOARD ─────────────────────────

public record DashboardStatsResponse(
    int TotalOrdersToday, int TotalTripsToday, int PendingOrders,
    int ActiveDrivers, int OnlineDrivers, int ActiveRiders, int TotalRiders, int TotalDrivers,
    decimal RevenueToday, decimal RevenueMonth, decimal AverageFare,
    double AverageRating, double CompletionRatePercent, double CancellationRatePercent,
    DateTime Timestamp);

public record OrderStatusCount(OrderStatus Status, int Count);

public record HourlyStats(int Hour, int Count, decimal Revenue);

public record RevenuePoint(DateTime Date, decimal Revenue, int Orders, int CompletedOrders);

public record TopDriverItem(Guid DriverId, string FullName, string? PhotoUrl, int Trips, decimal Earnings, double Rating, string VehicleType);

public record CategoryBreakdownItem(VehicleCategory VehicleCategory, int Orders, decimal Revenue, double SharePercent);

public record PaymentMethodBreakdownItem(PaymentMethod Method, int Count, decimal Amount);

/// <summary>Everything the admin dashboard needs in one round-trip.</summary>
public record DashboardOverviewResponse(
    DashboardStatsResponse Stats,
    List<OrderStatusCount> ByStatus,
    List<HourlyStats> Hourly,
    List<RevenuePoint> RevenueSeries,
    List<TopDriverItem> TopDrivers,
    List<CategoryBreakdownItem> Categories,
    List<PaymentMethodBreakdownItem> PaymentMethods);

/// <summary>Server-side filter for the order/report screens.</summary>
public record ReportFilter(
    DateTime? From = null, DateTime? To = null,
    OrderStatus? Status = null, VehicleCategory? VehicleCategory = null,
    PaymentMethod? PaymentMethod = null, string? Search = null);

public record FinancialSummaryResponse(
    DateTime From, DateTime To,
    decimal GrossRevenue, decimal Discounts, decimal NetRevenue,
    decimal DriverEarnings, decimal PlatformCommission,
    int CompletedOrders, int CancelledOrders, decimal AverageOrderValue,
    List<RevenuePoint> Series,
    List<PaymentMethodBreakdownItem> ByPaymentMethod);

// ──────────────────────── NOTIFICATIONS ──────────────────────

public record NotificationResponse(Guid Id, string Title, string Message, NotificationType Type, bool IsRead, DateTime CreatedAt, Guid? OrderId);

public record UnreadCountResponse(int Unread);

// ─────────────────────────── REVIEWS ─────────────────────────

public record SubmitReviewRequest(Guid OrderId, Guid ReviewerId, Guid TargetUserId, [Range(1, 5)] int Rating, string? Comment = null);

public record ReviewResponse(Guid Id, Guid OrderId, string ReviewerName, string? ReviewerPhotoUrl, int Rating, string? Comment, DateTime CreatedAt);

// ──────────────────────── ADMIN / USERS ──────────────────────

public record SetUserActiveRequest(bool IsActive, string? Reason = null);

public record HealthResponse(string Status, DateTime Timestamp, string Version, string Database, string StorageProvider, string Cache);
