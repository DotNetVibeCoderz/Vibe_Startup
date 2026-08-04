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
    BankTransfer = 4,

    /// <summary>Indonesia's unified QR standard — the most common cashless method locally.</summary>
    Qris = 5,

    /// <summary>Bank virtual account number generated per transaction.</summary>
    VirtualAccount = 6
}

/// <summary>
/// Lifecycle of a payment.
///
/// <see cref="Pending"/> and <see cref="Failed"/> existed from the start but were never
/// assigned — every payment was created as <see cref="Completed"/> because no money actually
/// moved. With a real provider in the loop the whole path matters.
/// </summary>
public enum PaymentStatus
{
    /// <summary>Created, not yet handed to a provider.</summary>
    Pending = 1,

    Completed = 2,
    Failed = 3,
    Refunded = 4,

    /// <summary>Provider is holding the charge — QR shown, VA issued, waiting for the payer.</summary>
    AwaitingPayment = 5,

    /// <summary>The payer never completed it before the provider's deadline.</summary>
    Expired = 6
}

/// <summary>
/// Which e-wallet a charge is routed to. Only meaningful when the method is
/// <see cref="PaymentMethod.EWallet"/>; QRIS reaches all of them through one QR.
/// </summary>
public enum EWalletChannel
{
    Unspecified = 0,
    GoPay = 1,
    Ovo = 2,
    Dana = 3,
    ShopeePay = 4,
    LinkAja = 5
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

/// <summary>
/// Documents a driver must submit before being allowed to take orders.
/// </summary>
public enum DocumentType
{
    DriverLicense = 1,
    VehicleRegistration = 2,
    IdentityCard = 3,
    Insurance = 4,
    VehiclePhoto = 5
}

/// <summary>
/// Review state of a submitted driver document.
/// </summary>
public enum DocumentStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

/// <summary>
/// Who ended a trip before it completed.
/// </summary>
public enum CancelledByParty
{
    Rider = 1,
    Driver = 2,
    System = 3
}
