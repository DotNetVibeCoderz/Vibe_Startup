using Microsoft.EntityFrameworkCore;
using EventSphere.Data.Context;
using EventSphere.Data.Models;

namespace EventSphere.Services;

/// <summary>
/// Manajemen event: CRUD, status, dan query
/// </summary>
public class EventService
{
    private readonly AppDbContext _db;
    
    public EventService(AppDbContext db) => _db = db;

    public async Task<List<Event>> GetAllAsync(string? userId = null, string? role = null)
    {
        var query = _db.Events
            .Include(e => e.CreatedBy)
            .Include(e => e.Organizer)
            .Include(e => e.Client)
            .AsQueryable();

        if (role == "Client" && userId != null)
            query = query.Where(e => e.ClientId == userId);
        else if (role == "Organizer" && userId != null)
            query = query.Where(e => e.OrganizerId == userId || e.CreatedById == userId);

        return await query.OrderByDescending(e => e.EventDate).ToListAsync();
    }

    public async Task<Event?> GetByIdAsync(Guid id)
    {
        return await _db.Events
            .Include(e => e.CreatedBy)
            .Include(e => e.Organizer)
            .Include(e => e.Client)
            .Include(e => e.Attendees).ThenInclude(a => a.User)
            .Include(e => e.VendorContracts).ThenInclude(vc => vc.Vendor)
            .Include(e => e.BudgetItems)
            .Include(e => e.TaskItems)
            .Include(e => e.TableArrangements)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<Event> CreateAsync(Event evt)
    {
        evt.Id = Guid.NewGuid();
        evt.CreatedAt = DateTime.UtcNow;
        evt.UpdatedAt = DateTime.UtcNow;
        _db.Events.Add(evt);
        await _db.SaveChangesAsync();
        return evt;
    }

    public async Task<Event?> UpdateAsync(Event evt)
    {
        var existing = await _db.Events.FindAsync(evt.Id);
        if (existing == null) return null;

        existing.Name = evt.Name;
        existing.Description = evt.Description;
        existing.EventDate = evt.EventDate;
        existing.EndDate = evt.EndDate;
        existing.Location = evt.Location;
        existing.Theme = evt.Theme;
        existing.PrimaryColor = evt.PrimaryColor;
        existing.SecondaryColor = evt.SecondaryColor;
        existing.Status = evt.Status;
        existing.BudgetTotal = evt.BudgetTotal;
        existing.ExpectedGuests = evt.ExpectedGuests;
        existing.EventType = evt.EventType;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var evt = await _db.Events.FindAsync(id);
        if (evt == null) return false;
        _db.Events.Remove(evt);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task UpdateBudgetAsync(Guid eventId)
    {
        var spent = await _db.BudgetItems
            .Where(b => b.EventId == eventId)
            .SumAsync(b => b.ActualCost);
        
        var evt = await _db.Events.FindAsync(eventId);
        if (evt != null)
        {
            evt.BudgetSpent = spent;
            evt.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task UpdateGuestCountAsync(Guid eventId)
    {
        var confirmed = await _db.EventAttendees
            .CountAsync(a => a.EventId == eventId && a.RsvpStatus == RsvpStatus.Accepted);
        
        var evt = await _db.Events.FindAsync(eventId);
        if (evt != null)
        {
            evt.ConfirmedGuests = confirmed;
            evt.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<List<Event>> GetUpcomingEventsAsync(int days = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(days);
        return await _db.Events
            .Where(e => e.EventDate >= DateTime.UtcNow && e.EventDate <= cutoff && e.Status != EventStatus.Cancelled)
            .OrderBy(e => e.EventDate)
            .ToListAsync();
    }
}
