using Microsoft.EntityFrameworkCore;
using Ngibrid.Data;
using Ngibrid.Models;

namespace Ngibrid.Services;

public class CourierService
{
    private readonly NgibridDbContext _db;
    private readonly AuditService _audit;
    public CourierService(NgibridDbContext db, AuditService audit) { _db = db; _audit = audit; }

    public async Task<List<CourierProfile>> GetAvailableCouriersAsync() =>
        await _db.CourierProfiles.Where(c => c.IsAvailable && c.Status == "AVAILABLE" && !c.IsDeleted)
            .Include(c => c.User).ToListAsync();

    /// <summary>Every courier regardless of availability — used by the courier management page.</summary>
    public async Task<List<CourierProfile>> GetAllCouriersAsync() =>
        await _db.CourierProfiles.Where(c => !c.IsDeleted)
            .Include(c => c.User)
            .OrderBy(c => c.CourierId)
            .ToListAsync();

    public async Task<CourierProfile?> GetCourierAsync(long id) =>
        await _db.CourierProfiles.Include(c => c.User).FirstOrDefaultAsync(c => c.Id == id);

    public async Task UpdateLocationAsync(long courierId, double lat, double lng)
    {
        var courier = await _db.CourierProfiles.FindAsync(courierId);
        if (courier != null) { courier.CurrentLatitude = lat; courier.CurrentLongitude = lng; courier.LastLocationUpdate = DateTime.UtcNow; await _db.SaveChangesAsync(); }
    }

    public async Task UpdateStatusAsync(long courierId, string status)
    {
        var courier = await _db.CourierProfiles.FindAsync(courierId);
        if (courier != null) { courier.Status = status; courier.IsAvailable = status == "AVAILABLE"; await _db.SaveChangesAsync(); }
    }

    public async Task AssignOrderToCourierAsync(long orderId, long courierId, DateTime scheduledDate)
    {
        _db.DeliverySchedules.Add(new DeliverySchedule { CourierProfileId = courierId, OrderId = orderId, ScheduledDate = scheduledDate, Status = "SCHEDULED", EstimatedDistanceKm = 5.0 });
        var order = await _db.Orders.FindAsync(orderId);
        if (order != null) order.AssignedCourierId = courierId;
        var courier = await _db.CourierProfiles.FindAsync(courierId);
        if (courier != null) courier.Status = "ON_DELIVERY";
        await _db.SaveChangesAsync();
    }

    public async Task<List<DeliverySchedule>> GetCourierScheduleAsync(long courierId, DateTime? date = null)
    {
        var query = _db.DeliverySchedules.Where(s => s.CourierProfileId == courierId);
        if (date.HasValue) query = query.Where(s => s.ScheduledDate.Date == date.Value.Date);
        return await query.Include(s => s.Order).OrderBy(s => s.SequenceNumber).ToListAsync();
    }
}

public class PickupService
{
    private readonly NgibridDbContext _db;
    public PickupService(NgibridDbContext db) { _db = db; }

    public async Task<PickupRequest> RequestPickupAsync(PickupRequest request)
    {
        request.RequestNumber = $"PKP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        request.Status = "REQUESTED";
        _db.PickupRequests.Add(request);
        await _db.SaveChangesAsync();
        return request;
    }

    public async Task<List<PickupRequest>> GetPendingPickupsAsync() =>
        await _db.PickupRequests.Where(p => p.Status == "REQUESTED" && !p.IsDeleted)
            .Include(p => p.Customer).OrderBy(p => p.RequestedPickupDate).ToListAsync();

    /// <summary>All pickup requests, newest first — optionally scoped to one customer.</summary>
    public async Task<List<PickupRequest>> GetPickupsAsync(long? customerId = null, int take = 100)
    {
        var query = _db.PickupRequests.Where(p => !p.IsDeleted);
        if (customerId.HasValue) query = query.Where(p => p.CustomerId == customerId);
        return await query.Include(p => p.Customer)
            .OrderByDescending(p => p.CreatedAt).Take(take).ToListAsync();
    }

    public async Task UpdatePickupStatusAsync(long requestId, string status)
    {
        var request = await _db.PickupRequests.FindAsync(requestId);
        if (request == null) return;
        request.Status = status;
        if (status == "PICKED_UP") request.ActualPickupTime = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task AssignPickupAsync(long requestId, long courierId)
    {
        var request = await _db.PickupRequests.FindAsync(requestId);
        if (request != null) { request.AssignedCourierId = courierId; request.Status = "ASSIGNED"; await _db.SaveChangesAsync(); }
    }
}

public class SupportTicketService
{
    private readonly NgibridDbContext _db;
    public SupportTicketService(NgibridDbContext db) { _db = db; }

    public async Task<SupportTicket> CreateTicketAsync(SupportTicket ticket)
    {
        ticket.TicketNumber = $"TKT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync();
        return ticket;
    }

    public async Task<SupportMessage> AddMessageAsync(long ticketId, long senderId, string message, string senderType = "CUSTOMER", string? attachmentUrl = null)
    {
        var msg = new SupportMessage { SupportTicketId = ticketId, SenderId = senderId, Message = message, SenderType = senderType, AttachmentUrl = attachmentUrl };
        _db.SupportMessages.Add(msg);
        await _db.SaveChangesAsync();
        return msg;
    }

    public async Task<SupportTicket?> GetTicketAsync(long id) =>
        await _db.SupportTickets.Include(t => t.Messages).FirstOrDefaultAsync(t => t.Id == id);

    /// <summary>List tickets, newest first. Scope to a user for the customer view, omit for the agent view.</summary>
    public async Task<List<SupportTicket>> GetTicketsAsync(long? userId = null, string? status = null, int take = 100)
    {
        var query = _db.SupportTickets.Where(t => !t.IsDeleted);
        if (userId.HasValue) query = query.Where(t => t.UserId == userId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(t => t.Status == status);
        return await query.OrderByDescending(t => t.CreatedAt).Take(take).ToListAsync();
    }

    public async Task UpdateTicketStatusAsync(long ticketId, string status)
    {
        var ticket = await _db.SupportTickets.FindAsync(ticketId);
        if (ticket == null) return;
        ticket.Status = status;
        if (status is "RESOLVED" or "CLOSED") ticket.ResolvedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}
