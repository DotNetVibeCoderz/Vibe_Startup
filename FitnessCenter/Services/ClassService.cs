using Microsoft.EntityFrameworkCore;
using FitnessCenter.Data;
using FitnessCenter.Models;

namespace FitnessCenter.Services;

public class ClassService
{
    private readonly AppDbContext _db;
    public ClassService(AppDbContext db) => _db = db;

    public async Task<List<FitnessClass>> GetAllAsync() =>
        await _db.FitnessClasses.Include(c => c.Trainer).Include(c => c.Schedules)
            .Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();

    public async Task<FitnessClass?> GetByIdAsync(int id) =>
        await _db.FitnessClasses.Include(c => c.Trainer).Include(c => c.Schedules)
            .Include(c => c.Bookings).ThenInclude(b => b.User)
            .FirstOrDefaultAsync(c => c.Id == id);

    public async Task<FitnessClass> CreateAsync(FitnessClass c) { _db.FitnessClasses.Add(c); await _db.SaveChangesAsync(); return c; }
    public async Task UpdateAsync(FitnessClass c) { _db.FitnessClasses.Update(c); await _db.SaveChangesAsync(); }
    public async Task DeleteAsync(int id) { var c = await _db.FitnessClasses.FindAsync(id); if (c != null) { c.IsActive = false; await _db.SaveChangesAsync(); } }

    public async Task<List<ClassSchedule>> GetScheduleAsync(DateTime? date = null)
    {
        var query = _db.ClassSchedules.Include(s => s.FitnessClass).ThenInclude(c => c!.Trainer).AsQueryable();
        if (date.HasValue) query = query.Where(s => s.DayOfWeek == date.Value.DayOfWeek);
        return await query.OrderBy(s => s.StartTime).ToListAsync();
    }

    public async Task<ClassBooking?> BookClassAsync(int scheduleId, string userId)
    {
        var schedule = await _db.ClassSchedules.Include(s => s.FitnessClass).FirstOrDefaultAsync(s => s.Id == scheduleId);
        if (schedule == null || schedule.CurrentBookings >= schedule.FitnessClass!.MaxParticipants) return null;

        var exists = await _db.ClassBookings.AnyAsync(b => b.ScheduleId == scheduleId && b.UserId == userId);
        if (exists) return null;

        var booking = new ClassBooking { ScheduleId = scheduleId, UserId = userId };
        _db.ClassBookings.Add(booking);
        schedule.CurrentBookings++;
        await _db.SaveChangesAsync();
        return booking;
    }

    public async Task<List<ClassBooking>> GetUserBookingsAsync(string userId) =>
        await _db.ClassBookings.Include(b => b.Schedule).ThenInclude(s => s!.FitnessClass)
            .Where(b => b.UserId == userId).OrderByDescending(b => b.BookedAt).ToListAsync();
}
