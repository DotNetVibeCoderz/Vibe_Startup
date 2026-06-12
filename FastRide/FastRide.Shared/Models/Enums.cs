namespace FastRide.Shared.Models;

/// <summary>
/// Defines user roles in the FastRide platform.
/// </summary>
public enum UserRole
{
    Rider = 1,
    Driver = 2,
    Admin = 3
}

/// <summary>
/// Defines the current status of a driver.
/// </summary>
public enum DriverStatus
{
    Offline = 0,
    Online = 1,
    OnTrip = 2,
    Break = 3
}

/// <summary>
/// Defines the lifecycle status of an order.
/// </summary>
public enum OrderStatus
{
    Requested = 1,
    Accepted = 2,
    DriverArrived = 3,
    Started = 4,
    Completed = 5,
    Cancelled = 6,
    Expired = 7
}

/// <summary>
/// Defines vehicle categories available for riders.
/// </summary>
public enum VehicleCategory
{
    Economy = 1,
    Comfort = 2,
    Premium = 3,
    Bike = 4,
    Electric = 5
}

/// <summary>
/// Defines available payment methods.
/// </summary>
public enum PaymentMethod
{
    Cash = 1,
    EWallet = 2,
    CreditCard = 3,
    BankTransfer = 4
}

/// <summary>
/// Defines the status of a payment transaction.
/// </summary>
public enum PaymentStatus
{
    Pending = 1,
    Completed = 2,
    Failed = 3,
    Refunded = 4
}

/// <summary>
/// Defines the type of promo/discount.
/// </summary>
public enum PromoType
{
    Percentage = 1,
    FixedAmount = 2
}

/// <summary>
/// Defines notification categories.
/// </summary>
public enum NotificationType
{
    Info = 1,
    OrderUpdate = 2,
    Payment = 3,
    Promo = 4,
    System = 5
}

/// <summary>
/// Defines types of trip stops.
/// </summary>
public enum TripStopType
{
    Pickup = 1,
    Waypoint = 2,
    Dropoff = 3
}
