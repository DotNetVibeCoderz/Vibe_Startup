using Microsoft.EntityFrameworkCore;
using Bioskop.Data;
using Bioskop.Services;

namespace Bioskop.Api;

public static class MinimalApi
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1");

        api.MapGet("/movies", async (ApplicationDbContext db, string? search, string? genre) =>
        {
            var query = db.Movies.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(m => m.Title.Contains(search));
            if (!string.IsNullOrWhiteSpace(genre)) query = query.Where(m => m.Genre != null && m.Genre.Contains(genre));
            return Results.Ok(await query.OrderBy(m => m.Title).ToListAsync());
        });

        api.MapGet("/movies/{id}", async (ApplicationDbContext db, int id) =>
        {
            var movie = await db.Movies.Include(m => m.Ratings).FirstOrDefaultAsync(m => m.Id == id);
            return movie is not null ? Results.Ok(movie) : Results.NotFound();
        });

        api.MapGet("/showtimes", async (ApplicationDbContext db, int? movieId, DateTime? date) =>
        {
            var query = db.Showtimes.Include(s => s.Movie).Include(s => s.Studio).AsQueryable();
            if (movieId.HasValue) query = query.Where(s => s.MovieId == movieId.Value);
            if (date.HasValue) query = query.Where(s => s.StartTime.Date == date.Value.Date);
            return Results.Ok(await query.OrderBy(s => s.StartTime).ToListAsync());
        });

        api.MapGet("/studios/{studioId}/seats", async (ApplicationDbContext db, int studioId) =>
        {
            var seats = await db.Seats.Where(s => s.StudioId == studioId).OrderBy(s => s.RowLabel).ThenBy(s => s.ColumnNumber).ToListAsync();
            return Results.Ok(seats);
        });

        api.MapGet("/showtimes/{showtimeId}/seat-status", async (SeatService seatService, int showtimeId) =>
        {
            var status = await seatService.GetSeatStatusForShowtimeAsync(showtimeId);
            return Results.Ok(status);
        });

        api.MapGet("/snacks", async (ApplicationDbContext db, string? category) =>
        {
            var query = db.Snacks.Where(s => s.IsAvailable).AsQueryable();
            if (!string.IsNullOrEmpty(category)) query = query.Where(s => s.Category == category);
            return Results.Ok(await query.ToListAsync());
        });

        api.MapPost("/orders", async (OrderService orderService, CreateOrderRequest req) =>
        {
            var snacks = req.Snacks?.Select(s => (s.SnackId, s.Quantity)).ToList();
            var (order, error) = await orderService.CreateOrderAsync(req.UserId, req.ShowtimeId, req.SeatIds, snacks);
            if (order == null) return Results.BadRequest(new { error });
            return Results.Created($"/api/v1/orders/{order.Id}", order);
        });

        api.MapGet("/orders/{id}", async (OrderService orderService, int id) =>
        {
            var order = await orderService.GetByIdAsync(id);
            return order is not null ? Results.Ok(order) : Results.NotFound();
        });

        api.MapGet("/tickets/{qrCode}", async (TicketService ticketService, string qrCode) =>
        {
            var ticket = await ticketService.GetTicketByQrAsync(qrCode);
            return ticket is not null ? Results.Ok(ticket) : Results.NotFound();
        });

        api.MapPost("/tickets/validate", async (TicketService ticketService, ValidateTicketRequest req) =>
        {
            var (ticket, message) = await ticketService.ValidateTicketByQrAsync(req.QrCode);
            return Results.Ok(new { valid = ticket != null, message, ticket });
        });

        api.MapGet("/stats", async (ApplicationDbContext db) =>
        {
            var totalMovies = await db.Movies.CountAsync();
            var totalOrders = await db.Orders.CountAsync();
            var totalRevenue = await db.Orders.Where(o => o.Status == "Paid").SumAsync(o => o.GrandTotal);
            var totalUsers = await db.Users.CountAsync();
            return Results.Ok(new { totalMovies, totalOrders, totalRevenue, totalUsers });
        });
    }
}

public record CreateOrderRequest(string UserId, int ShowtimeId, List<int> SeatIds, List<SnackItemRequest>? Snacks);
public record SnackItemRequest(int SnackId, int Quantity);
public record ValidateTicketRequest(string QrCode);
