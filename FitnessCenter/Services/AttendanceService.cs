using Microsoft.EntityFrameworkCore;
using FitnessCenter.Data;
using FitnessCenter.Models;

namespace FitnessCenter.Services;

public class AttendanceService
{
    private readonly AppDbContext _db;
    private readonly GamificationService _gamification;
    public AttendanceService(AppDbContext db, GamificationService gamification) { _db = db; _gamification = gamification; }

    public async Task<List<Attendance>> GetUserAttendanceAsync(string userId, int days = 30) =>
        await _db.Attendances.Where(a => a.UserId == userId && a.Timestamp >= DateTime.UtcNow.AddDays(-days))
            .OrderByDescending(a => a.Timestamp).ToListAsync();

    public async Task<Attendance> CheckInAsync(string userId, string? qrData = null)
    {
        var attendance = new Attendance
        {
            UserId = userId,
            Type = AttendanceType.CheckIn,
            Timestamp = DateTime.UtcNow,
            QrCodeData = qrData
        };
        _db.Attendances.Add(attendance);
        await _gamification.AddPointsAsync(userId, 10, "Check-in harian");
        await _db.SaveChangesAsync();
        return attendance;
    }

    public async Task<Attendance> CheckOutAsync(string userId)
    {
        var attendance = new Attendance
        {
            UserId = userId,
            Type = AttendanceType.CheckOut,
            Timestamp = DateTime.UtcNow
        };
        _db.Attendances.Add(attendance);
        await _db.SaveChangesAsync();
        return attendance;
    }

    public async Task<int> GetTodayCheckInsAsync() =>
        await _db.Attendances.CountAsync(a => a.Type == AttendanceType.CheckIn && a.Timestamp.Date == DateTime.UtcNow.Date);

    public async Task<int> GetStreakDaysAsync(string userId)
    {
        var dates = await _db.Attendances
            .Where(a => a.UserId == userId && a.Type == AttendanceType.CheckIn)
            .Select(a => a.Timestamp.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync();

        if (!dates.Any() || dates[0] < DateTime.UtcNow.Date.AddDays(-1)) return 0;
        int streak = 1;
        for (int i = 1; i < dates.Count; i++)
        {
            if (dates[i] == dates[i - 1].AddDays(-1)) streak++;
            else break;
        }
        return streak;
    }
}
