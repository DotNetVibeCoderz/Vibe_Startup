using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace RentalBoil.Models;

/// <summary>
/// Enum untuk role pengguna dalam sistem
/// </summary>
public enum UserRole
{
    Customer = 0,
    Partner = 1,
    Admin = 2
}

/// <summary>
/// Enum untuk status booking
/// </summary>
public enum BookingStatus
{
    Pending = 0,
    Confirmed = 1,
    Rejected = 2,
    Active = 3,
    Completed = 4,
    Cancelled = 5
}

/// <summary>
/// Enum untuk status pembayaran
/// </summary>
public enum PaymentStatus
{
    Unpaid = 0,
    Paid = 1,
    Refunded = 2,
    Failed = 3
}

/// <summary>
/// Enum untuk metode pembayaran
/// </summary>
public enum PaymentMethod
{
    EWallet = 0,
    CreditCard = 1,
    BankTransfer = 2,
    QRIS = 3
}

/// <summary>
/// Enum untuk jenis kendaraan
/// </summary>
public enum VehicleType
{
    Car = 0,
    Motorcycle = 1
}

/// <summary>
/// Enum untuk transmisi kendaraan
/// </summary>
public enum TransmissionType
{
    Manual = 0,
    Automatic = 1
}

/// <summary>
/// Enum untuk jenis bahan bakar
/// </summary>
public enum FuelType
{
    Petrol = 0,
    Diesel = 1,
    Electric = 2,
    Hybrid = 3
}

/// <summary>
/// Enum status kunci digital
/// </summary>
public enum LockStatus
{
    Locked = 0,
    Unlocked = 1
}

/// <summary>
/// Enum status mesin
/// </summary>
public enum EngineStatus
{
    Off = 0,
    On = 1
}

/// <summary>
/// Enum status kendaraan (GPS)
/// </summary>
public enum VehicleMotionStatus
{
    Stopped = 0,
    Moving = 1
}
