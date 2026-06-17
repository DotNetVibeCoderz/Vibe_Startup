using Microsoft.EntityFrameworkCore;
using Bioskop.Data;
using Bioskop.Models;

namespace Bioskop.Services;

/// <summary>
/// Service untuk mengelola pemesanan tiket
/// </summary>
public class OrderService
{
    private readonly ApplicationDbContext _context;
    private readonly SeatService _seatService;
    private readonly AuditService _audit;

    public OrderService(ApplicationDbContext context, SeatService seatService, AuditService audit)
    {
        _context = context;
        _seatService = seatService;
        _audit = audit;
    }

    public async Task<(Order? order, string? error)> CreateOrderAsync(
        string userId, int showtimeId, List<int> seatIds, List<(int snackId, int qty)>? snacks = null)
    {
        // Validate showtime exists
        var showtime = await _context.Showtimes
            .Include(s => s.Movie)
            .Include(s => s.Studio)
            .FirstOrDefaultAsync(s => s.Id == showtimeId);

        if (showtime == null) return (null, "Showtime tidak ditemukan");
        if (showtime.StartTime <= DateTime.UtcNow) return (null, "Showtime sudah dimulai");

        // Validate seats
        var seats = await _context.Seats.Where(s => seatIds.Contains(s.Id)).ToListAsync();
        if (seats.Count != seatIds.Count) return (null, "Beberapa kursi tidak valid");

        // Check seat availability
        var canHold = await _seatService.HoldSeatsAsync(showtimeId, seatIds, userId);
        if (!canHold) return (null, "Beberapa kursi sudah dipesan");

        // Calculate prices
        decimal subtotal = 0;
        foreach (var seat in seats)
        {
            subtotal += showtime.Price * seat.PriceMultiplier;
        }

        decimal snackTotal = 0;
        var orderSnacks = new List<OrderSnack>();

        if (snacks != null && snacks.Any())
        {
            foreach (var (snackId, qty) in snacks)
            {
                var snack = await _context.Snacks.FindAsync(snackId);
                if (snack == null || !snack.IsAvailable) continue;

                var orderSnack = new OrderSnack
                {
                    SnackId = snackId,
                    Quantity = qty,
                    UnitPrice = snack.Price,
                    Subtotal = snack.Price * qty
                };
                orderSnacks.Add(orderSnack);
                snackTotal += orderSnack.Subtotal;
            }
        }

        var taxRate = 0.11m; // 11% PPN
        decimal taxAmount = (subtotal + snackTotal) * taxRate;
        decimal grandTotal = subtotal + snackTotal + taxAmount;

        // Create order
        var order = new Order
        {
            OrderNumber = GenerateOrderNumber(),
            UserId = userId,
            ShowtimeId = showtimeId,
            Subtotal = subtotal,
            SnackTotal = snackTotal,
            TaxAmount = taxAmount,
            GrandTotal = grandTotal,
            Status = "Pending",
            OrderSnacks = orderSnacks
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        // Create tickets
        foreach (var seat in seats)
        {
            var ticket = new Ticket
            {
                TicketNumber = GenerateTicketNumber(),
                OrderId = order.Id,
                SeatId = seat.Id,
                ShowtimeId = showtimeId,
                Price = showtime.Price * seat.PriceMultiplier,
                QrCode = Guid.NewGuid().ToString("N"),
                Status = "Active"
            };
            _context.Tickets.Add(ticket);
        }

        await _context.SaveChangesAsync();
        await _seatService.InvalidateSeatCacheAsync(showtimeId);
        await _audit.LogAsync("Create", "Order", order.Id.ToString(), null,
            System.Text.Json.JsonSerializer.Serialize(new { order.OrderNumber, order.GrandTotal }));

        return (order, null);
    }

    public async Task<Order?> GetByIdAsync(int id)
    {
        return await _context.Orders
            .Include(o => o.Showtime!).ThenInclude(s => s.Movie)
            .Include(o => o.Showtime!).ThenInclude(s => s.Studio)
            .Include(o => o.Tickets).ThenInclude(t => t.Seat)
            .Include(o => o.OrderSnacks).ThenInclude(os => os.Snack)
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<List<Order>> GetByUserAsync(string userId)
    {
        return await _context.Orders
            .Include(o => o.Showtime!).ThenInclude(s => s.Movie)
            .Include(o => o.Showtime!).ThenInclude(s => s.Studio)
            .Include(o => o.Tickets).ThenInclude(t => t.Seat)
            .Include(o => o.Payment)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
    }

    public async Task<bool> CancelOrderAsync(int orderId, string userId)
    {
        var order = await _context.Orders
            .Include(o => o.Tickets)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null || order.UserId != userId) return false;
        if (order.Status == "Paid" || order.Status == "Cancelled") return false;

        order.Status = "Cancelled";
        order.CancelledAt = DateTime.UtcNow;

        foreach (var ticket in order.Tickets)
            ticket.Status = "Cancelled";

        await _context.SaveChangesAsync();
        await _seatService.InvalidateSeatCacheAsync(order.ShowtimeId);
        await _audit.LogAsync("Cancel", "Order", order.Id.ToString(), null, null);
        return true;
    }

    public async Task<List<Order>> GetAllOrdersAsync(string? status = null)
    {
        var query = _context.Orders
            .Include(o => o.Showtime!).ThenInclude(s => s.Movie)
            .Include(o => o.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(o => o.Status == status);

        return await query.OrderByDescending(o => o.OrderDate).ToListAsync();
    }

    public async Task<decimal> GetTotalRevenueAsync(DateTime? from = null, DateTime? to = null)
    {
        var query = _context.Orders.Where(o => o.Status == "Paid");

        if (from.HasValue)
            query = query.Where(o => o.OrderDate >= from.Value);
        if (to.HasValue)
            query = query.Where(o => o.OrderDate <= to.Value);

        return await query.SumAsync(o => o.GrandTotal);
    }

    public async Task<int> GetTotalOrdersAsync(DateTime? from = null, DateTime? to = null)
    {
        var query = _context.Orders.AsQueryable();

        if (from.HasValue)
            query = query.Where(o => o.OrderDate >= from.Value);
        if (to.HasValue)
            query = query.Where(o => o.OrderDate <= to.Value);

        return await query.CountAsync();
    }

    private string GenerateOrderNumber()
    {
        return $"BSK-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(10000, 99999)}";
    }

    private string GenerateTicketNumber()
    {
        return $"TKT-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }
}
