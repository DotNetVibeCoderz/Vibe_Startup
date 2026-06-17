using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Bioskop.Data;
using Bioskop.Models;

namespace Bioskop.Services;

/// <summary>
/// Service untuk mengelola kursi dan pemilihan kursi
/// </summary>
public class SeatService
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public SeatService(ApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<List<Seat>> GetByStudioAsync(int studioId)
    {
        return await _context.Seats
            .Where(s => s.StudioId == studioId && s.IsActive)
            .OrderBy(s => s.RowLabel)
            .ThenBy(s => s.ColumnNumber)
            .ToListAsync();
    }

    /// <summary>
    /// Mendapatkan status kursi untuk showtime tertentu (apakah sudah dipesan)
    /// </summary>
    public async Task<Dictionary<int, string>> GetSeatStatusForShowtimeAsync(int showtimeId)
    {
        var cacheKey = $"seat_status_{showtimeId}";
        if (_cache.TryGetValue(cacheKey, out Dictionary<int, string>? cached) && cached != null)
            return cached;

        var showtime = await _context.Showtimes
            .Include(s => s.Studio)
            .FirstOrDefaultAsync(s => s.Id == showtimeId);

        if (showtime == null) return new();

        // Get all seats for the studio
        var seats = await _context.Seats
            .Where(s => s.StudioId == showtime.StudioId && s.IsActive)
            .ToListAsync();

        // Get booked seats for this showtime
        var bookedSeatIds = await _context.Tickets
            .Where(t => t.ShowtimeId == showtimeId && t.Status == "Active")
            .Join(_context.Orders,
                t => t.OrderId,
                o => o.Id,
                (t, o) => new { t.SeatId, o.Status })
            .Where(x => x.Status != "Cancelled")
            .Select(x => x.SeatId)
            .ToListAsync();

        var result = new Dictionary<int, string>();
        foreach (var seat in seats)
        {
            if (seat.Status == "Maintenance")
                result[seat.Id] = "maintenance";
            else if (bookedSeatIds.Contains(seat.Id))
                result[seat.Id] = "occupied";
            else
                result[seat.Id] = "available";
        }

        // Cache for 30 seconds
        _cache.Set(cacheKey, result, TimeSpan.FromSeconds(30));
        return result;
    }

    /// <summary>
    /// Book seats secara temporary (hold) untuk mencegah double booking
    /// </summary>
    public async Task<bool> HoldSeatsAsync(int showtimeId, List<int> seatIds, string userId)
    {
        // Check if any of the seats is already booked
        var bookedSeatIds = await _context.Tickets
            .Where(t => t.ShowtimeId == showtimeId && t.Status == "Active" && seatIds.Contains(t.SeatId))
            .Join(_context.Orders,
                t => t.OrderId,
                o => o.Id,
                (t, o) => new { t.SeatId, o.Status })
            .Where(x => x.Status != "Cancelled")
            .Select(x => x.SeatId)
            .ToListAsync();

        if (bookedSeatIds.Any())
            return false; // Some seats are already booked

        // Invalidate cache
        _cache.Remove($"seat_status_{showtimeId}");
        return true;
    }

    public async Task InvalidateSeatCacheAsync(int showtimeId)
    {
        _cache.Remove($"seat_status_{showtimeId}");
    }
}
