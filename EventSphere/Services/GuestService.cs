using Microsoft.EntityFrameworkCore;
using EventSphere.Data.Context;
using EventSphere.Data.Models;

namespace EventSphere.Services;

public class GuestService
{
    private readonly AppDbContext _db;
    public GuestService(AppDbContext db) => _db = db;

    public async Task<List<EventAttendee>> GetAttendeesForEventAsync(Guid eventId) =>
        await _db.EventAttendees.Where(a => a.EventId == eventId).Include(a => a.User).Include(a => a.Table).ToListAsync();

    public async Task<EventAttendee> InviteAsync(EventAttendee attendee)
    {
        attendee.Id = Guid.NewGuid();
        attendee.InvitedAt = DateTime.UtcNow;
        _db.EventAttendees.Add(attendee);
        await _db.SaveChangesAsync();
        return attendee;
    }

    public async Task<bool> UpdateRsvpAsync(Guid attendeeId, RsvpStatus status, string? notes = null)
    {
        var a = await _db.EventAttendees.FindAsync(attendeeId);
        if (a == null) return false;
        a.RsvpStatus = status;
        a.RsvpDate = DateTime.UtcNow;
        if (notes != null) a.Notes = notes;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AssignSeatAsync(Guid attendeeId, Guid tableId, int seatNumber)
    {
        var a = await _db.EventAttendees.FindAsync(attendeeId);
        if (a == null) return false;
        a.TableId = tableId;
        a.SeatNumber = seatNumber;
        await _db.SaveChangesAsync();
        
        // Update table filled seat count
        var table = await _db.TableArrangements.FindAsync(tableId);
        if (table != null) table.FilledSeats = await _db.EventAttendees.CountAsync(ea => ea.TableId == tableId);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveAttendeeAsync(Guid attendeeId)
    {
        var a = await _db.EventAttendees.FindAsync(attendeeId);
        if (a == null) return false;
        _db.EventAttendees.Remove(a);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetRsvpStatsAsync(Guid eventId, RsvpStatus status) =>
        await _db.EventAttendees.CountAsync(a => a.EventId == eventId && a.RsvpStatus == status);
}
