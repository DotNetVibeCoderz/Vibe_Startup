// Payment, transaction, promo, and loyalty models
using Joka.Models.Common;

namespace Joka.Models.Payments;

public class PaymentTransaction : BaseEntity
{
    public Guid UserId { get; set; }
    public string BookingType { get; set; } = string.Empty; // Flight, Train, Hotel, CarRental, Activity, Package
    public Guid BookingId { get; set; }
    public string TransactionCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? InsuranceAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public string Currency { get; set; } = "IDR";
    public string PaymentMethod { get; set; } = string.Empty; // BankTransfer, EWallet, CreditCard, QRIS, PayLater
    public string PaymentGateway { get; set; } = "Midtrans";

    /// <summary>
    /// Voucher applied at checkout. Kept so the quota can be consumed at
    /// settlement rather than at checkout - an abandoned payment must not eat
    /// someone else's voucher.
    /// </summary>
    public string? VoucherCode { get; set; }

    public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed, Refunded
    public string? GatewayTransactionId { get; set; }
    public string? PaymentUrl { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? ExpiryAt { get; set; }
    public string? FailureReason { get; set; }
}

public class PromoVoucher : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = "Percentage"; // Percentage, FixedAmount, Cashback
    public decimal Value { get; set; } // 10 = 10% or Rp10.000
    public decimal MinPurchase { get; set; } // Minimum purchase amount
    public decimal MaxDiscount { get; set; } // Maximum discount cap
    public DateTime ValidFrom { get; set; }
    public DateTime ValidUntil { get; set; }
    public int TotalQuota { get; set; }
    public int UsedCount { get; set; }
    public string? ApplicableTo { get; set; } // Flight, Train, Hotel, All
    public bool IsActive { get; set; } = true;
    public string? ImageUrl { get; set; }
}

public class UserVoucher : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid PromoVoucherId { get; set; }
    public PromoVoucher? Voucher { get; set; }
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime ClaimedAt { get; set; } = DateTime.UtcNow;
}

public class LoyaltyTransaction : BaseEntity
{
    public Guid UserId { get; set; }
    public int Points { get; set; } // Positive = earn, negative = redeem
    public string Type { get; set; } = "Earn"; // Earn, Redeem, Expire, Bonus
    public string? Description { get; set; }
    public string? ReferenceType { get; set; } // Booking, Review, Referral, Daily
    public string? ReferenceId { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
}

public class TravelInsurance : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Coverage { get; set; } = "Basic"; // Basic, Standard, Premium
    public decimal Price { get; set; }
    public string Currency { get; set; } = "IDR";
    public string? Benefits { get; set; } // JSON: delay, cancellation, medical, baggage
    public int MaxDays { get; set; } = 30;
    public bool IsActive { get; set; } = true;
}

public class CarRental : BaseEntity
{
    public string Name { get; set; } = string.Empty; // Avanza, Xpander, etc.
    public string Type { get; set; } = "MPV"; // MPV, SUV, Sedan, Hatchback, Luxury
    public string? ImageUrl { get; set; }
    public string? ImageUrls { get; set; } // JSON array - extra angles
    public string? Description { get; set; }
    public int Seats { get; set; } = 5;
    public string? Transmission { get; set; } = "Automatic"; // Automatic, Manual
    public decimal PricePerDay { get; set; }
    public string Currency { get; set; } = "IDR";
    public bool IncludeDriver { get; set; }
    public decimal? DriverPricePerDay { get; set; }
    public int TotalUnits { get; set; } = 5;
    public int AvailableUnits { get; set; } = 5;
    public string? PickupLocations { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CarRentalBooking : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid CarRentalId { get; set; }
    public CarRental? Car { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime PickupDate { get; set; }
    public DateTime ReturnDate { get; set; }
    public int Days { get; set; }
    public bool IncludeDriver { get; set; }
    public string? PickupLocation { get; set; }
    public string? DropoffLocation { get; set; }
    public decimal TotalPrice { get; set; }
    public string Currency { get; set; } = "IDR";
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public DateTime BookingDate { get; set; } = DateTime.UtcNow;
}

public class TravelPackage : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Destination { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "IDR";
    public string? Includes { get; set; } // JSON: flights, hotels, activities
    public string? Itinerary { get; set; } // JSON day-by-day
    public string? ImageUrl { get; set; }
    public string? ImageUrls { get; set; } // JSON array - package gallery

    /// <summary>True when a merchant created this through the approval flow.</summary>
    public bool MerchantOwned { get; set; }
    public int? MaxParticipants { get; set; }
    public int BookedCount { get; set; }
    public bool IsActive { get; set; } = true;
}

public class TravelPackageBooking : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid TravelPackageId { get; set; }
    public TravelPackage? Package { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int Participants { get; set; } = 1;
    public decimal TotalPrice { get; set; }
    public string Currency { get; set; } = "IDR";
    public DateTime TravelDate { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public DateTime BookingDate { get; set; } = DateTime.UtcNow;
}
