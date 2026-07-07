using Microsoft.EntityFrameworkCore;
using FitnessCenter.Data;
using FitnessCenter.Models;

namespace FitnessCenter.Services;

public class WorkoutService
{
    private readonly AppDbContext _db;
    private readonly GamificationService _gamification;
    public WorkoutService(AppDbContext db, GamificationService gamification) { _db = db; _gamification = gamification; }

    public async Task<List<WorkoutLog>> GetUserLogsAsync(string userId, int days = 30) =>
        await _db.WorkoutLogs.Where(w => w.UserId == userId && w.LoggedAt >= DateTime.UtcNow.AddDays(-days))
            .OrderByDescending(w => w.LoggedAt).ToListAsync();

    public async Task<WorkoutLog> AddLogAsync(WorkoutLog log)
    {
        _db.WorkoutLogs.Add(log);
        await _gamification.AddPointsAsync(log.UserId, 15, "Log latihan");
        await _db.SaveChangesAsync();
        return log;
    }

    public async Task<int> GetTotalWorkoutsAsync(string userId) =>
        await _db.WorkoutLogs.CountAsync(w => w.UserId == userId);

    public async Task<int> GetTotalCaloriesAsync(string userId) =>
        await _db.WorkoutLogs.Where(w => w.UserId == userId).SumAsync(w => w.CaloriesBurned ?? 0);
}
