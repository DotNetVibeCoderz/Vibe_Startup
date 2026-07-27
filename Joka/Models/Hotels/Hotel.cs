// Hotel and accommodation models
using Joka.Models.Common;

namespace Joka.Models.Hotels;

public class Hotel : BaseEntity
{
    /// <summary>Owning partner. Drives what a Merchant account may see and edit.</summary>
    public Guid? MerchantId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = "Hotel"; // Hotel, Villa, Apartment, Resort, Hostel, Guesthouse
    public int StarRating { get; set; } // 1-5
    public string? Address { get; set; }
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = "Indonesia";
    public string? ImageUrl { get; set; }
    public string? ImageUrls { get; set; } // JSON array of image URLs
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Facilities { get; set; } // JSON: ["WiFi","Pool","Spa"]
    public double AverageRating { get; set; } // 1.0-5.0
    public int ReviewCount { get; set; }
    public string? CheckInTime { get; set; } = "14:00";
    public string? CheckOutTime { get; set; } = "12:00";
    public bool IsActive { get; set; } = true;
    public ICollection<Room> Rooms { get; set; } = new List<Room>();
    public ICollection<HotelReview> Reviews { get; set; } = new List<HotelReview>();
}

public class Room : BaseEntity
{
    public Guid HotelId { get; set; }
    public Hotel? Hotel { get; set; }
    public string Name { get; set; } = string.Empty; // Deluxe, Suite, Standard
    public string? Description { get; set; }
    public string Type { get; set; } = "Standard"; // Standard, Deluxe, Suite, Family, Penthouse
    public int Capacity { get; set; } = 2; // Max guests
    public decimal PricePerNight { get; set; }
    public string Currency { get; set; } = "IDR";
    public int TotalRooms { get; set; } = 10;
    public int AvailableRooms { get; set; } = 10;
    public string? ImageUrl { get; set; }
    public string? ImageUrls { get; set; } // JSON array - room gallery
    public string? Amenities { get; set; } // JSON: ["AC","TV","MiniBar"]
    public bool HasBreakfast { get; set; }
    public bool IsRefundable { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class HotelBooking : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid RoomId { get; set; }
    public Room? Room { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int Nights { get; set; }
    public int RoomCount { get; set; } = 1;
    public int GuestCount { get; set; } = 1;
    public decimal TotalPrice { get; set; }
    public string Currency { get; set; } = "IDR";
    public string? GuestNames { get; set; }
    public string? SpecialRequests { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public DateTime BookingDate { get; set; } = DateTime.UtcNow;
    public string? ETicketUrl { get; set; }
    public string? QrCodeData { get; set; }
}

public class HotelReview : BaseEntity
{
    public Guid HotelId { get; set; }
    public Hotel? Hotel { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Denormalised so the moderation queue does not need a join per row.</summary>
    public string? AuthorName { get; set; }

    public int Rating { get; set; } // 1-5
    public string? Title { get; set; }
    public string? Comment { get; set; }
    public string? Pros { get; set; }
    public string? Cons { get; set; }
    public DateTime StayDate { get; set; }

    /// <summary>True when the reviewer actually has a booking for this hotel.</summary>
    public bool IsVerified { get; set; }

    // ---- moderation ----
    /// <summary>Pending, Approved, Rejected. Only Approved reviews are public
    /// and only Approved reviews count toward the hotel's rating.</summary>
    public string Status { get; set; } = "Pending";

    public string? ModeratedBy { get; set; }
    public DateTime? ModeratedAt { get; set; }
    public string? ModerationNote { get; set; }
}

