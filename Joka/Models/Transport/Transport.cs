// Local transport: ride-hailing (ojek/car) and fixed-route airport transfer.
//
// One set of tables covers both, told apart by TransportOption.ServiceType -
// the same trick Buses uses for bus vs shuttle. The difference that matters is
// pricing: ride-hailing is per kilometre, an airport transfer is a flat route
// price, which is why PricingMode exists rather than two entity trees.
using Joka.Models.Common;

namespace Joka.Models.Transport;

public class TransportProvider : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? LogoUrl { get; set; }
    public string? Description { get; set; }
    public double Rating { get; set; }
    public bool IsActive { get; set; } = true;
}

public class TransportOption : BaseEntity
{
    public Guid ProviderId { get; set; }
    public TransportProvider? Provider { get; set; }

    /// <summary>Owning partner, when a merchant runs this fleet.</summary>
    public Guid? MerchantId { get; set; }

    public string Name { get; set; } = string.Empty;          // "Ojek Instan", "Airport Transfer Premium"
    public string? Description { get; set; }

    /// <summary>RideHailing or AirportTransfer.</summary>
    public string ServiceType { get; set; } = "RideHailing";

    public string VehicleType { get; set; } = "Motorcycle";   // Motorcycle, Car, MPV, Premium
    public string City { get; set; } = string.Empty;
    public int Capacity { get; set; } = 1;

    /// <summary>PerKm or Flat. Drives which of the two price fields is used.</summary>
    public string PricingMode { get; set; } = "PerKm";

    public decimal BasePrice { get; set; }
    public decimal PricePerKm { get; set; }

    /// <summary>Lower bound for a per-km fare, so a 300 m trip is not Rp900.</summary>
    public decimal MinimumFare { get; set; }

    /// <summary>Airport this fixed route serves. Null for ride-hailing.</summary>
    public string? AirportCode { get; set; }

    /// <summary>Named endpoint of a fixed route, e.g. "Kuta / Legian".</summary>
    public string? RouteArea { get; set; }

    public int EstimatedMinutes { get; set; }
    public string Currency { get; set; } = "IDR";
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
}

public class TransportBooking : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid TransportOptionId { get; set; }
    public TransportOption? Option { get; set; }

    public string BookingCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";

    public string PickupAddress { get; set; } = string.Empty;
    public string DropoffAddress { get; set; } = string.Empty;
    public DateTime PickupTime { get; set; }

    /// <summary>Zero for a flat-rate airport route, where distance does not price it.</summary>
    public double DistanceKm { get; set; }

    public int PassengerCount { get; set; } = 1;

    /// <summary>Flight the transfer is timed against, when the customer gave one.</summary>
    public string? FlightNumber { get; set; }

    public decimal TotalPrice { get; set; }
    public string Currency { get; set; } = "IDR";

    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Notes { get; set; }

    public DateTime BookingDate { get; set; } = DateTime.UtcNow;
    public string? QrCodeData { get; set; }
}
