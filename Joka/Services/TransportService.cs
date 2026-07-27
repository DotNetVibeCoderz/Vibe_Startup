// Local transport search and booking (F-5).
//
// Fares are computed in one place so the search list, the chatbot and the
// booking record can never disagree about the price - the class of bug that
// already bit this project once when the chatbot invented a number.
using Microsoft.EntityFrameworkCore;
using Joka.Data;
using Joka.Models.Transport;

namespace Joka.Services;

/// <param name="Fare">Price for the trip as asked for, already rounded to rupiah.</param>
public record TransportQuote(TransportOption Option, decimal Fare, int EstimatedMinutes);

public record TransportBookingResult(bool Success, string Message, TransportBooking? Booking = null);

public class TransportService
{
    private readonly AppDbContext _db;

    public TransportService(AppDbContext db) => _db = db;

    public const string RideHailing = "RideHailing";
    public const string AirportTransfer = "AirportTransfer";

    /// <summary>
    /// Ride-hailing: base + per-km, never below the minimum fare.
    /// Airport transfer: the flat route price, distance ignored.
    /// </summary>
    public static decimal FareFor(TransportOption option, double distanceKm)
    {
        if (option.PricingMode == "Flat")
            return Math.Round(option.BasePrice, 0, MidpointRounding.AwayFromZero);

        if (distanceKm <= 0) distanceKm = 1;

        var fare = option.BasePrice + option.PricePerKm * (decimal)distanceKm;

        if (fare < option.MinimumFare) fare = option.MinimumFare;

        // Rounded to the nearest 500 rupiah, like every ride-hailing app here.
        return Math.Round(fare / 500m, 0, MidpointRounding.AwayFromZero) * 500m;
    }

    /// <summary>Rough travel time: flat routes carry their own estimate, per-km rides get 3 min/km plus pickup.</summary>
    private static int MinutesFor(TransportOption option, double distanceKm) =>
        option.PricingMode == "Flat"
            ? option.EstimatedMinutes
            : Math.Max(option.EstimatedMinutes, (int)Math.Ceiling(distanceKm * 3) + 5);

    public async Task<List<TransportQuote>> SearchAsync(
        string? city, string? serviceType, string? vehicleType, double distanceKm, string? airportCode)
    {
        var query = _db.TransportOptions.AsNoTracking()
            .Include(o => o.Provider)
            .Where(o => o.IsActive);

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(o => o.City == city);

        if (!string.IsNullOrWhiteSpace(serviceType))
            query = query.Where(o => o.ServiceType == serviceType);

        if (!string.IsNullOrWhiteSpace(vehicleType))
            query = query.Where(o => o.VehicleType == vehicleType);

        if (!string.IsNullOrWhiteSpace(airportCode))
            query = query.Where(o => o.AirportCode == airportCode);

        var options = await query.ToListAsync();

        // Priced in memory: the fare depends on the requested distance, which
        // SQL knows nothing about.
        return options
            .Select(o => new TransportQuote(o, FareFor(o, distanceKm), MinutesFor(o, distanceKm)))
            .OrderBy(q => q.Fare)
            .ToList();
    }

    public Task<List<string>> CitiesAsync() =>
        _db.TransportOptions.AsNoTracking()
            .Where(o => o.IsActive)
            .Select(o => o.City).Distinct().OrderBy(c => c)
            .ToListAsync();

    public async Task<TransportBookingResult> BookAsync(
        Guid? userId, Guid optionId,
        string pickup, string dropoff, DateTime pickupTime,
        double distanceKm, int passengers,
        string? flightNumber, string? email, string? phone, string? notes)
    {
        var option = await _db.TransportOptions.AsNoTracking()
            .Include(o => o.Provider)
            .FirstOrDefaultAsync(o => o.Id == optionId);

        if (option is null || !option.IsActive)
            return new(false, "Layanan ini sedang tidak tersedia.");

        if (string.IsNullOrWhiteSpace(pickup) || string.IsNullOrWhiteSpace(dropoff))
            return new(false, "Isi alamat penjemputan dan tujuan.");

        if (passengers < 1 || passengers > option.Capacity)
            return new(false, $"{option.Name} maksimal {option.Capacity} penumpang.");

        if (pickupTime < DateTime.UtcNow.AddMinutes(-5))
            return new(false, "Waktu penjemputan sudah lewat.");

        var booking = new TransportBooking
        {
            UserId = userId ?? Guid.Empty,
            TransportOptionId = option.Id,
            BookingCode = $"JKA-TR-{DateTime.UtcNow:yyMMdd}-{Random.Shared.Next(1000, 9999)}",
            Status = "Confirmed",
            PickupAddress = pickup.Trim(),
            DropoffAddress = dropoff.Trim(),
            PickupTime = pickupTime,
            DistanceKm = option.PricingMode == "Flat" ? 0 : distanceKm,
            PassengerCount = passengers,
            FlightNumber = string.IsNullOrWhiteSpace(flightNumber) ? null : flightNumber.Trim().ToUpperInvariant(),
            TotalPrice = FareFor(option, distanceKm),
            ContactEmail = email,
            ContactPhone = phone,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        };

        booking.QrCodeData = booking.BookingCode;

        _db.TransportBookings.Add(booking);
        await _db.SaveChangesAsync();

        return new(true, $"Pemesanan {booking.BookingCode} dikonfirmasi.", booking);
    }

    public Task<TransportBooking?> GetByCodeAsync(string code) =>
        _db.TransportBookings.AsNoTracking()
            .Include(b => b.Option!).ThenInclude(o => o.Provider)
            .FirstOrDefaultAsync(b => b.BookingCode == code);
}
