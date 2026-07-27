// Bus & shuttle models. Shuttles are the same booking flow as buses, so they
// share these tables and are told apart by BusService.ServiceType.
using Joka.Models.Common;

namespace Joka.Models.Buses;

public class BusTerminal : BaseEntity
{
    public string Code { get; set; } = string.Empty;   // PLG, LBB, KPB
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Perusahaan Otobus (PO) or shuttle operator.</summary>
public class BusOperator : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public int Rating { get; set; } = 4;
    public bool IsActive { get; set; } = true;
}

public class BusService : BaseEntity
{
    /// <summary>Owning partner. Drives what a Merchant account may see and edit.</summary>
    public Guid? MerchantId { get; set; }

    public Guid BusOperatorId { get; set; }
    public BusOperator? Operator { get; set; }

    public string BusNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Bus or Shuttle - drives which label and seat layout is shown.</summary>
    public string ServiceType { get; set; } = "Bus";

    /// <summary>Ekonomi, Bisnis, Eksekutif, Sleeper.</summary>
    public string Class { get; set; } = "Ekonomi";

    /// <summary>e.g. "2-2" for a coach, "2-1" for an executive, "1-1" for a sleeper.</summary>
    public string SeatLayout { get; set; } = "2-2";

    public int TotalSeats { get; set; }
    public bool HasWifi { get; set; }
    public bool HasAirConditioning { get; set; } = true;
    public bool HasToilet { get; set; }
    public bool HasRecliningSeat { get; set; }
    public bool HasEntertainment { get; set; }

    /// <summary>Shuttles pick up at your address rather than a terminal.</summary>
    public bool HasDoorToDoor { get; set; }

    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
}

public class BusSchedule : BaseEntity
{
    public Guid BusServiceId { get; set; }
    public BusService? BusService { get; set; }

    public Guid DepartureTerminalId { get; set; }
    public BusTerminal? DepartureTerminal { get; set; }

    public Guid ArrivalTerminalId { get; set; }
    public BusTerminal? ArrivalTerminal { get; set; }

    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public int DurationMinutes { get; set; }

    public decimal BasePrice { get; set; }
    public string Currency { get; set; } = "IDR";

    public int AvailableSeats { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<BusBooking> Bookings { get; set; } = new List<BusBooking>();
}

public class BusBooking : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid BusScheduleId { get; set; }
    public BusSchedule? BusSchedule { get; set; }

    public string BookingCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int PassengerCount { get; set; } = 1;

    public decimal TotalPrice { get; set; }
    public string Currency { get; set; } = "IDR";

    public string? PassengerNames { get; set; }
    public string? SelectedSeats { get; set; }

    /// <summary>Pickup address for door-to-door shuttle bookings.</summary>
    public string? PickupAddress { get; set; }

    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public DateTime BookingDate { get; set; } = DateTime.UtcNow;
    public string? ETicketUrl { get; set; }
    public string? QrCodeData { get; set; }
}
