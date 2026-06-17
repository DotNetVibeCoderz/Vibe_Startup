using Microsoft.EntityFrameworkCore;
using Bioskop.Data;
using Bioskop.Models;

namespace Bioskop.Services;

/// <summary>
/// Service untuk mengelola jadwal tayang
/// </summary>
public class ShowtimeService
{
    private readonly ApplicationDbContext _context;

    public ShowtimeService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Showtime>> GetByMovieAsync(int movieId)
    {
        return await _context.Showtimes
            .Include(s => s.Studio)
            .Include(s => s.Movie)
            .Where(s => s.MovieId == movieId && s.IsActive && s.StartTime > DateTime.UtcNow)
            .OrderBy(s => s.StartTime)
            .ToListAsync();
    }

    public async Task<List<Showtime>> GetByDateAsync(DateTime date)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        return await _context.Showtimes
            .Include(s => s.Studio)
            .Include(s => s.Movie)
            .Where(s => s.StartTime >= startOfDay && s.StartTime < endOfDay && s.IsActive)
            .OrderBy(s => s.StartTime)
            .ToListAsync();
    }

    public async Task<Showtime?> GetByIdAsync(int id)
    {
        return await _context.Showtimes
            .Include(s => s.Studio)
            .Include(s => s.Movie)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<List<Showtime>> GetByStudioAsync(int studioId, DateTime? date = null)
    {
        var query = _context.Showtimes
            .Include(s => s.Movie)
            .Include(s => s.Studio)
            .Where(s => s.StudioId == studioId && s.IsActive);

        if (date.HasValue)
        {
            var startOfDay = date.Value.Date;
            var endOfDay = startOfDay.AddDays(1);
            query = query.Where(s => s.StartTime >= startOfDay && s.StartTime < endOfDay);
        }

        return await query.OrderBy(s => s.StartTime).ToListAsync();
    }
}
