// Train-related models
using Joka.Models.Common;

namespace Joka.Models.Trains;

public class TrainStation : BaseEntity
{
    public string Code { get; set; } = string.Empty; // GMR, BD, SBY
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Train : BaseEntity
{
    public string TrainNumber { get; set; } = string.Empty; // Argo Bromo Anggrek
    public string Name { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty; // Ekonomi, Bisnis, Eksekutif, Luxury
    public string? ImageUrl { get; set; }
    public int TotalSeats { get; set; }
    public bool HasWifi { get; set; }
    public bool HasMeal { get; set; }
    public bool HasEntertainment { get; set; }
    public bool IsActive { get; set; } = true;
}

public class TrainSchedule : BaseEntity
{
    public Guid TrainId { get; set; }
    public Train? Train { get; set; }
    public Guid DepartureStationId { get; set; }
    public TrainStation? DepartureStation { get; set; }
    public Guid ArrivalStationId { get; set; }
    public TrainStation? ArrivalStation { get; set; }
    public DateTime DepartureTime { get; set; }
    public DateTime ArrivalTime { get; set; }
    public int DurationMinutes { get; set; }
    public decimal BasePrice { get; set; }
    public string Currency { get; set; } = "IDR";
    public int AvailableSeats { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<TrainBooking> Bookings { get; set; } = new List<TrainBooking>();
}

public class TrainBooking : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid TrainScheduleId { get; set; }
    public TrainSchedule? TrainSchedule { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int PassengerCount { get; set; } = 1;
    public decimal TotalPrice { get; set; }
    public string Currency { get; set; } = "IDR";
    public string? PassengerNames { get; set; }
    public string? SelectedSeats { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public DateTime BookingDate { get; set; } = DateTime.UtcNow;
    public string? ETicketUrl { get; set; }
    public string? QrCodeData { get; set; }
}
