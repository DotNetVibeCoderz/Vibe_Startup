using Microsoft.EntityFrameworkCore;
using RentalBoil.Data;
using RentalBoil.Models;

namespace RentalBoil.Services;

public class BookingService
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notificationService;
    private readonly LoyaltyService _loyaltyService;
    private readonly ILogger<BookingService> _logger;

    public BookingService(AppDbContext db, NotificationService notificationService, 
        LoyaltyService loyaltyService, ILogger<BookingService> logger)
    { _db = db; _notificationService = notificationService; _loyaltyService = loyaltyService; _logger = logger; }

    public async Task<Booking?> CreateBookingAsync(Booking booking)
    {
        var vehicle = await _db.Vehicles.FindAsync(booking.VehicleId);
        if (vehicle == null || !vehicle.IsAvailable) return null;

        var todayPrefix = $"RB-{DateTime.UtcNow:yyyyMMdd}-";
        var todayCount = await _db.Bookings.CountAsync(b => b.BookingNumber.StartsWith(todayPrefix));
        booking.BookingNumber = $"{todayPrefix}{(todayCount + 1):D4}";
        booking.BasePrice = vehicle.PricePerDay * booking.DurationDays * vehicle.DynamicPriceMultiplier;
        booking.InsuranceCost = vehicle.InsuranceAvailable ? vehicle.InsuranceCostPerDay * booking.DurationDays : 0;

        if (!string.IsNullOrWhiteSpace(booking.CouponCode))
        {
            var code = booking.CouponCode.ToLowerInvariant();
            var coupon = await _db.Promotions.FirstOrDefaultAsync(p => 
                p.Code.ToLower() == code && p.IsActive && p.StartDate <= DateTime.UtcNow && p.EndDate >= DateTime.UtcNow);
            if (coupon != null && booking.BasePrice >= coupon.MinTransaction)
            { booking.Discount = coupon.DiscountType == "percentage" ? Math.Min(booking.BasePrice * coupon.DiscountValue / 100, coupon.MaxDiscount ?? decimal.MaxValue) : coupon.DiscountValue; coupon.UsageCount++; }
        }

        booking.TotalPrice = booking.BasePrice + booking.InsuranceCost - booking.Discount;
        booking.Status = BookingStatus.Pending;
        booking.CreatedAt = DateTime.UtcNow; booking.UpdatedAt = DateTime.UtcNow;
        _db.Bookings.Add(booking); await _db.SaveChangesAsync();

        _logger.LogInformation("Booking created: {BookingNumber}", booking.BookingNumber);
        // ★ FIX: semua link notifikasi pakai /customer/bookings/{id} (route yang ada)
        await _notificationService.CreateNotificationAsync(vehicle.OwnerId, "Booking Baru", 
            $"Ada booking baru #{booking.BookingNumber} untuk {vehicle.Name}", "info", $"/customer/bookings/{booking.Id}");
        return booking;
    }

    public async Task<List<Booking>> GetCustomerBookingsAsync(string customerId, BookingStatus? status = null)
    { var q = _db.Bookings.Include(b => b.Vehicle).ThenInclude(v => v!.Photos).Where(b => b.CustomerId == customerId); if (status.HasValue) q = q.Where(b => b.Status == status.Value); return await q.OrderByDescending(b => b.CreatedAt).ToListAsync(); }

    public async Task<List<Booking>> GetPartnerBookingsAsync(string partnerId, BookingStatus? status = null)
    { var q = _db.Bookings.Include(b => b.Vehicle).ThenInclude(v => v!.Photos).Include(b => b.Customer).Where(b => b.Vehicle!.OwnerId == partnerId); if (status.HasValue) q = q.Where(b => b.Status == status.Value); return await q.OrderByDescending(b => b.CreatedAt).ToListAsync(); }

    public async Task<List<Booking>> GetAllBookingsAsync(BookingStatus? status = null, int page = 1, int pageSize = 20)
    { var q = _db.Bookings.Include(b => b.Vehicle).ThenInclude(v => v!.Photos).Include(b => b.Customer).AsQueryable(); if (status.HasValue) q = q.Where(b => b.Status == status.Value); return await q.OrderByDescending(b => b.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(); }

    public async Task<Booking?> GetBookingByIdAsync(int id)
        => await _db.Bookings.Include(b => b.Vehicle).ThenInclude(v => v!.Photos).Include(b => b.Vehicle).ThenInclude(v => v!.Owner).Include(b => b.Customer).Include(b => b.Payment).FirstOrDefaultAsync(b => b.Id == id);

    public async Task<bool> AcceptBookingAsync(int bookingId)
    { var b = await _db.Bookings.Include(x => x.Vehicle).FirstOrDefaultAsync(x => x.Id == bookingId); if (b == null || b.Status != BookingStatus.Pending) return false; b.Status = BookingStatus.Confirmed; b.UpdatedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); await _notificationService.CreateNotificationAsync(b.CustomerId, "Booking Dikonfirmasi", $"#{b.BookingNumber} dikonfirmasi!", "success", $"/customer/bookings/{bookingId}"); return true; }

    public async Task<bool> RejectBookingAsync(int bookingId, string reason)
    { var b = await _db.Bookings.Include(x => x.Vehicle).FirstOrDefaultAsync(x => x.Id == bookingId); if (b == null || b.Status != BookingStatus.Pending) return false; b.Status = BookingStatus.Rejected; b.RejectionReason = reason; b.UpdatedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); await _notificationService.CreateNotificationAsync(b.CustomerId, "Booking Ditolak", $"#{b.BookingNumber} ditolak: {reason}", "danger", $"/customer/bookings/{bookingId}"); return true; }

    public async Task<bool> ActivateBookingAsync(int bookingId)
    { var b = await _db.Bookings.Include(x => x.Vehicle).FirstOrDefaultAsync(x => x.Id == bookingId); if (b == null || b.Status != BookingStatus.Confirmed || b.PaymentStatus != PaymentStatus.Paid) return false; b.Status = BookingStatus.Active; b.UpdatedAt = DateTime.UtcNow; if (b.Vehicle != null) { b.Vehicle.IsAvailable = false; b.Vehicle.LockStatus = LockStatus.Unlocked; } await _db.SaveChangesAsync(); return true; }

    public async Task<bool> CompleteBookingAsync(int bookingId)
    { var b = await _db.Bookings.Include(x => x.Vehicle).FirstOrDefaultAsync(x => x.Id == bookingId); if (b == null || b.Status != BookingStatus.Active) return false; b.Status = BookingStatus.Completed; b.UpdatedAt = DateTime.UtcNow; if (b.Vehicle != null) { b.Vehicle.IsAvailable = true; b.Vehicle.RentalCount++; b.Vehicle.LockStatus = LockStatus.Locked; b.Vehicle.EngineStatus = EngineStatus.Off; b.Vehicle.MotionStatus = VehicleMotionStatus.Stopped; } await _loyaltyService.AwardPointsAsync(b.CustomerId, 100, "Booking completed", bookingId); await _db.SaveChangesAsync(); await _notificationService.CreateNotificationAsync(b.CustomerId, "Booking Selesai", $"#{b.BookingNumber} selesai! ⭐ Beri ulasan ya.", "success", $"/customer/bookings/{bookingId}/review"); return true; }

    public async Task<bool> CancelBookingAsync(int bookingId, string? reason = null)
    { var b = await _db.Bookings.Include(x => x.Vehicle).FirstOrDefaultAsync(x => x.Id == bookingId); if (b == null || b.Status == BookingStatus.Completed || b.Status == BookingStatus.Active) return false; b.Status = BookingStatus.Cancelled; b.UpdatedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); return true; }

    public async Task<object> GetPartnerStatsAsync(string partnerId)
    {
        var vehicles = await _db.Vehicles.Where(v => v.OwnerId == partnerId).Select(v => v.Id).ToListAsync();
        var bookings = _db.Bookings.Where(b => vehicles.Contains(b.VehicleId));
        var today = DateTime.UtcNow.Date; var monthStart = new DateTime(today.Year, today.Month, 1);
        return new { TotalBookings = await bookings.CountAsync(), ActiveBookings = await bookings.CountAsync(b => b.Status == BookingStatus.Active), PendingBookings = await bookings.CountAsync(b => b.Status == BookingStatus.Pending), CompletedBookings = await bookings.CountAsync(b => b.Status == BookingStatus.Completed), TotalRevenue = await bookings.Where(b => b.Status == BookingStatus.Completed || b.Status == BookingStatus.Active).SumAsync(b => b.TotalPrice), TodayRevenue = await bookings.Where(b => b.PaidAt.HasValue && b.PaidAt.Value.Date == today).SumAsync(b => b.TotalPrice), MonthRevenue = await bookings.Where(b => b.PaidAt.HasValue && b.PaidAt.Value.Date >= monthStart).SumAsync(b => b.TotalPrice) };
    }

    public async Task<object> GetAdminStatsAsync()
    { var today = DateTime.UtcNow.Date; return new { TotalBookings = await _db.Bookings.CountAsync(), TotalRevenue = await _db.Bookings.Where(b => b.PaymentStatus == PaymentStatus.Paid).SumAsync(b => b.TotalPrice), TodayBookings = await _db.Bookings.CountAsync(b => b.CreatedAt.Date == today), ActiveRentals = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.Active), PendingBookings = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.Pending) }; }
}
