using Microsoft.EntityFrameworkCore;
using Bioskop.Data;
using Bioskop.Models;

namespace Bioskop.Services;

/// <summary>
/// Service untuk mengelola data film
/// </summary>
public class MovieService
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _audit;

    public MovieService(ApplicationDbContext context, AuditService audit)
    {
        _context = context;
        _audit = audit;
    }

    public async Task<List<Movie>> GetNowPlayingAsync()
    {
        return await _context.Movies
            .Where(m => m.IsNowPlaying)
            .OrderByDescending(m => m.ReleaseDate)
            .ToListAsync();
    }

    public async Task<List<Movie>> GetAllAsync(string? search = null, string? genre = null, string? sortBy = null)
    {
        var query = _context.Movies.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(m => m.Title.Contains(search) || (m.Description != null && m.Description.Contains(search)));

        if (!string.IsNullOrWhiteSpace(genre))
            query = query.Where(m => m.Genre != null && m.Genre.Contains(genre));

        query = sortBy switch
        {
            "title" => query.OrderBy(m => m.Title),
            "title_desc" => query.OrderByDescending(m => m.Title),
            "release" => query.OrderByDescending(m => m.ReleaseDate),
            "release_asc" => query.OrderBy(m => m.ReleaseDate),
            "price" => query.OrderBy(m => m.BasePrice),
            "price_desc" => query.OrderByDescending(m => m.BasePrice),
            _ => query.OrderByDescending(m => m.ReleaseDate)
        };

        return await query.ToListAsync();
    }

    public async Task<Movie?> GetByIdAsync(int id)
    {
        return await _context.Movies
            .Include(m => m.Ratings).ThenInclude(r => r.User)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<Movie> CreateAsync(Movie movie)
    {
        _context.Movies.Add(movie);
        await _context.SaveChangesAsync();
        await _audit.LogAsync("Create", "Movie", movie.Id.ToString(), null, System.Text.Json.JsonSerializer.Serialize(movie));
        return movie;
    }

    public async Task<Movie?> UpdateAsync(Movie movie)
    {
        var existing = await _context.Movies.FindAsync(movie.Id);
        if (existing == null) return null;

        var oldValues = System.Text.Json.JsonSerializer.Serialize(existing);
        existing.Title = movie.Title;
        existing.Description = movie.Description;
        existing.Genre = movie.Genre;
        existing.DurationMinutes = movie.DurationMinutes;
        existing.PosterUrl = movie.PosterUrl;
        existing.TrailerUrl = movie.TrailerUrl;
        existing.AgeRating = movie.AgeRating;
        existing.ReleaseDate = movie.ReleaseDate;
        existing.EndDate = movie.EndDate;
        existing.IsNowPlaying = movie.IsNowPlaying;
        existing.BasePrice = movie.BasePrice;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        await _audit.LogAsync("Update", "Movie", movie.Id.ToString(), oldValues, System.Text.Json.JsonSerializer.Serialize(existing));
        return existing;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var movie = await _context.Movies.FindAsync(id);
        if (movie == null) return false;

        _context.Movies.Remove(movie);
        await _context.SaveChangesAsync();
        await _audit.LogAsync("Delete", "Movie", id.ToString(), System.Text.Json.JsonSerializer.Serialize(movie), null);
        return true;
    }

    public async Task<MovieRating> AddRatingAsync(int movieId, string userId, int rating, string? comment = null)
    {
        var existing = await _context.MovieRatings
            .FirstOrDefaultAsync(r => r.MovieId == movieId && r.UserId == userId);

        if (existing != null)
        {
            existing.Rating = rating;
            existing.Comment = comment;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var movieRating = new MovieRating
            {
                MovieId = movieId,
                UserId = userId,
                Rating = rating,
                Comment = comment
            };
            _context.MovieRatings.Add(movieRating);
        }

        await _context.SaveChangesAsync();
        return existing ?? await _context.MovieRatings.FirstAsync(r => r.MovieId == movieId && r.UserId == userId);
    }

    public async Task<double> GetAverageRatingAsync(int movieId)
    {
        var ratings = await _context.MovieRatings.Where(r => r.MovieId == movieId).ToListAsync();
        if (!ratings.Any()) return 0;
        return ratings.Average(r => r.Rating);
    }
}
