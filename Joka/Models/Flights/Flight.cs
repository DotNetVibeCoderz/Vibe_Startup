// Flight-related models: airports, airlines, flights, bookings
using Joka.Models.Common;

namespace Joka.Models.Flights;

public class Airport : BaseEntity
{
    public string Code { get; set; } = string.Empty; // IATA code: CGK, DPS, SUB
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Airline : BaseEntity
{
    public string Code { get; set; } = string.Empty; // IATA: GA, QZ, JT
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? Country { get; set; }
    public bool IsActive { get; set; } = true;
    public int Rating { get; set; } // 1-5
}

public class Flight : BaseEntity
{
    /// <summary>Owning partner. Drives what a Merchant account may see and edit.</summary>
    public Guid? MerchantId { get; set; }

    public string FlightNumber { get; set; } = string.Empty; // GA-123
    public Guid AirlineId { get; set; }
    public Airline? Airline { get; set; }
    public Guid DepartureAirportId { get; set; }
    public Airport? DepartureAirport { get; set; }
    public Guid ArrivalAirportId { get; set; }
    public Airport? ArrivalAirport { get; set; }
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public int DurationMinutes { get; set; }
    public decimal BasePrice { get; set; }
    public string Currency { get; set; } = "IDR";
    public int TotalSeats { get; set; }
    public int AvailableSeats { get; set; }
    public string? CabinClass { get; set; } // Economy, Premium Economy, Business, First
    public int StopCount { get; set; } // 0 = direct
    public string? StopAirports { get; set; } // Comma-separated
    public bool HasMeal { get; set; }
    public int BaggageAllowanceKg { get; set; } = 20;
    public bool IsRefundable { get; set; }
    public bool IsActive { get; set; } = true;
    public string? ImageUrl { get; set; }

    public ICollection<FlightBooking> Bookings { get; set; } = new List<FlightBooking>();
}

public class FlightBooking : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid FlightId { get; set; }
    public Flight? Flight { get; set; }
    public string BookingCode { get; set; } = string.Empty; // Unique booking ref
    public string Status { get; set; } = "Pending"; // Pending, Confirmed, Cancelled, Completed
    public int PassengerCount { get; set; } = 1;
    public decimal TotalPrice { get; set; }
    public string Currency { get; set; } = "IDR";
    public string? PassengerNames { get; set; } // JSON array
    public string? SelectedSeats { get; set; } // JSON array
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public DateTime BookingDate { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public bool IsInsured { get; set; }
    public string? ETicketUrl { get; set; }
    public string? QrCodeData { get; set; }
}
