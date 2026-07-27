// Aggregations behind the D3 charts on the admin and merchant dashboards.
// Everything here reads real rows - there is no synthetic demo series.
using Microsoft.EntityFrameworkCore;
using Joka.Data;

namespace Joka.Services;

/// <summary>One plotted value. Matches Chart.razor's Point record.</summary>
public record ChartPoint(string Label, decimal Value);

public class AnalyticsService
{
    private readonly AppDbContext _db;

    public AnalyticsService(AppDbContext db) => _db = db;

    // ------------------------------------------------------------------
    // Platform-wide (Admin console)
    // ------------------------------------------------------------------

    /// <summary>
    /// Paid volume per day for the last <paramref name="days"/> days. Days with
    /// no transaction are emitted as zero so the line has no gaps - otherwise
    /// D3 would draw a straight segment across a quiet week and imply activity.
    /// </summary>
    public async Task<List<ChartPoint>> RevenueByDayAsync(int days = 14)
    {
        var since = DateTime.UtcNow.Date.AddDays(-(days - 1));

        var rows = await _db.PaymentTransactions.AsNoTracking()
            .Where(t => t.Status == "Completed" && t.PaidAt != null && t.PaidAt >= since)
            .Select(t => new { Day = t.PaidAt!.Value.Date, t.FinalAmount })
            .ToListAsync();

        var byDay = rows.GroupBy(r => r.Day).ToDictionary(g => g.Key, g => g.Sum(x => x.FinalAmount));

        return Enumerable.Range(0, days)
            .Select(i => since.AddDays(i))
            .Select(d => new ChartPoint(d.ToString("dd/MM"), byDay.GetValueOrDefault(d)))
            .ToList();
    }

    /// <summary>Count of transactions per payment method.</summary>
    public async Task<List<ChartPoint>> TransactionsByMethodAsync()
    {
        var rows = await _db.PaymentTransactions.AsNoTracking()
            .GroupBy(t => t.PaymentMethod)
            .Select(g => new { Method = g.Key, Count = g.Count() })
            .ToListAsync();

        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Method))
            .OrderByDescending(r => r.Count)
            .Select(r => new ChartPoint(MethodLabel(r.Method), r.Count))
            .ToList();
    }

    /// <summary>Transactions per status - the health of the payment funnel.</summary>
    public async Task<List<ChartPoint>> TransactionsByStatusAsync()
    {
        var rows = await _db.PaymentTransactions.AsNoTracking()
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        return rows.OrderByDescending(r => r.Count)
            .Select(r => new ChartPoint(r.Status, r.Count))
            .ToList();
    }

    /// <summary>Booking counts per product line, across all six booking tables.</summary>
    public async Task<List<ChartPoint>> BookingsByProductAsync()
    {
        var points = new List<ChartPoint>
        {
            new("Pesawat", await _db.FlightBookings.CountAsync()),
            new("Kereta", await _db.TrainBookings.CountAsync()),
            new("Bus & Shuttle", await _db.BusBookings.CountAsync()),
            new("Hotel", await _db.HotelBookings.CountAsync()),
            new("Rental mobil", await _db.CarRentalBookings.CountAsync()),
            new("Aktivitas", await _db.ActivityBookings.CountAsync()),
            new("Paket travel", await _db.TravelPackageBookings.CountAsync())
        };

        // Product lines nobody has booked yet only add noise to the chart.
        return points.Where(p => p.Value > 0).OrderByDescending(p => p.Value).ToList();
    }

    /// <summary>New user registrations per month, last 6 months.</summary>
    public async Task<List<ChartPoint>> UserGrowthAsync(int months = 6)
    {
        var since = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-(months - 1));

        var rows = await _db.Users.AsNoTracking()
            .Where(u => u.CreatedAt >= since)
            .Select(u => u.CreatedAt)
            .ToListAsync();

        var byMonth = rows
            .GroupBy(d => new DateTime(d.Year, d.Month, 1))
            .ToDictionary(g => g.Key, g => (decimal)g.Count());

        return Enumerable.Range(0, months)
            .Select(i => since.AddMonths(i))
            .Select(m => new ChartPoint(m.ToString("MMM"), byMonth.GetValueOrDefault(m)))
            .ToList();
    }

    // ------------------------------------------------------------------
    // Merchant-scoped
    // ------------------------------------------------------------------

    /// <summary>Settlement net amount per period for one partner.</summary>
    public async Task<List<ChartPoint>> MerchantSettlementTrendAsync(Guid merchantId, int periods = 8)
    {
        var rows = await _db.MerchantSettlements.AsNoTracking()
            .Where(s => s.MerchantId == merchantId)
            .OrderByDescending(s => s.PeriodEnd)
            .Take(periods)
            .Select(s => new { s.PeriodEnd, s.NetAmount })
            .ToListAsync();

        return rows.OrderBy(r => r.PeriodEnd)
            .Select(r => new ChartPoint(r.PeriodEnd.ToString("MMM yy"), r.NetAmount))
            .ToList();
    }

    /// <summary>
    /// How many bookings each of this partner's product lines produced.
    /// Ownership is resolved through the same MerchantId joins the portal uses,
    /// so a partner never sees another partner's numbers.
    /// </summary>
    public async Task<List<ChartPoint>> MerchantBookingsByProductAsync(Guid merchantId)
    {
        var hotel = await _db.HotelBookings.AsNoTracking()
            .CountAsync(b => b.Room!.Hotel!.MerchantId == merchantId);

        var bus = await _db.BusBookings.AsNoTracking()
            .CountAsync(b => b.BusSchedule!.BusService!.MerchantId == merchantId);

        var flight = await _db.FlightBookings.AsNoTracking()
            .CountAsync(b => b.Flight!.MerchantId == merchantId);

        var activity = await _db.ActivityBookings.AsNoTracking()
            .CountAsync(b => b.Activity!.MerchantId == merchantId);

        var points = new List<ChartPoint>
        {
            new("Hotel", hotel), new("Bus & Shuttle", bus),
            new("Pesawat", flight), new("Aktivitas", activity)
        };

        return points.Where(p => p.Value > 0).OrderByDescending(p => p.Value).ToList();
    }

    /// <summary>Room occupancy per property: booked share of total rooms.</summary>
    public async Task<List<ChartPoint>> MerchantRoomOccupancyAsync(Guid merchantId)
    {
        var rooms = await _db.Rooms.AsNoTracking()
            .Include(r => r.Hotel)
            .Where(r => r.Hotel!.MerchantId == merchantId && r.TotalRooms > 0)
            .Select(r => new { Hotel = r.Hotel!.Name, r.TotalRooms, r.AvailableRooms })
            .ToListAsync();

        return rooms
            .GroupBy(r => r.Hotel)
            .Select(g =>
            {
                var total = g.Sum(r => r.TotalRooms);
                var taken = total - g.Sum(r => r.AvailableRooms);
                return new ChartPoint(g.Key, total == 0 ? 0 : Math.Round(taken * 100m / total, 1));
            })
            .OrderByDescending(p => p.Value)
            .ToList();
    }

    /// <summary>Revenue this partner generated per day, from paid bookings.</summary>
    public async Task<List<ChartPoint>> MerchantRevenueByDayAsync(Guid merchantId, int days = 14)
    {
        var since = DateTime.UtcNow.Date.AddDays(-(days - 1));

        var hotel = await _db.HotelBookings.AsNoTracking()
            .Where(b => b.Room!.Hotel!.MerchantId == merchantId && b.BookingDate >= since)
            .Select(b => new { b.BookingDate, b.TotalPrice }).ToListAsync();

        var bus = await _db.BusBookings.AsNoTracking()
            .Where(b => b.BusSchedule!.BusService!.MerchantId == merchantId && b.BookingDate >= since)
            .Select(b => new { b.BookingDate, b.TotalPrice }).ToListAsync();

        var byDay = hotel.Concat(bus)
            .GroupBy(b => b.BookingDate.Date)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.TotalPrice));

        return Enumerable.Range(0, days)
            .Select(i => since.AddDays(i))
            .Select(d => new ChartPoint(d.ToString("dd/MM"), byDay.GetValueOrDefault(d)))
            .ToList();
    }

    private static string MethodLabel(string method) => method switch
    {
        "BankTransfer" => "Transfer bank",
        "EWallet" => "E-wallet",
        "CreditCard" => "Kartu kredit",
        "PayLater" => "PayLater",
        _ => method
    };
}
