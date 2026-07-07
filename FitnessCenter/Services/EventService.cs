using Microsoft.EntityFrameworkCore;
using FitnessCenter.Data;
using FitnessCenter.Models;

namespace FitnessCenter.Services;

public class EventService
{
    private readonly AppDbContext _db;
    public EventService(AppDbContext db) => _db = db;

    public async Task<List<Event>> GetAllAsync() =>
        await _db.Events.Include(e => e.Registrations).OrderByDescending(e => e.CreatedAt).ToListAsync();

    public async Task<List<Event>> GetPublishedAsync() =>
        await _db.Events.Where(e => e.Status == EventStatus.Published || e.Status == EventStatus.Ongoing)
            .OrderByDescending(e => e.EventDate).ToListAsync();

    public async Task<Event?> GetByIdAsync(int id) =>
        await _db.Events.Include(e => e.Registrations).ThenInclude(r => r.User)
            .Include(e => e.Comments).ThenInclude(c => c.User)
            .FirstOrDefaultAsync(e => e.Id == id);

    public async Task<Event> CreateAsync(Event evt) { _db.Events.Add(evt); await _db.SaveChangesAsync(); return evt; }
    public async Task UpdateAsync(Event evt) { _db.Events.Update(evt); await _db.SaveChangesAsync(); }

    public async Task<EventRegistration?> RegisterAsync(int eventId, string userId)
    {
        var evt = await _db.Events.FindAsync(eventId);
        if (evt == null || evt.CurrentParticipants >= evt.MaxParticipants) return null;
        var exists = await _db.EventRegistrations.AnyAsync(r => r.EventId == eventId && r.UserId == userId);
        if (exists) return null;
        var reg = new EventRegistration { EventId = eventId, UserId = userId };
        _db.EventRegistrations.Add(reg);
        evt.CurrentParticipants++;
        await _db.SaveChangesAsync();
        return reg;
    }

    public async Task<EventComment> AddCommentAsync(EventComment comment) { _db.EventComments.Add(comment); await _db.SaveChangesAsync(); return comment; }
}
