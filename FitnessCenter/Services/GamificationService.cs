using Microsoft.EntityFrameworkCore;
using FitnessCenter.Data;
using FitnessCenter.Models;

namespace FitnessCenter.Services;

public class GamificationService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    public GamificationService(AppDbContext db, IConfiguration config) { _db = db; _config = config; }

    public async Task AddPointsAsync(string userId, int basePoints, string reason)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;

        var streak = await GetStreakAsync(userId);
        var multiplier = streak >= 7 ? _config.GetValue<double>("Gamification:StreakBonusMultiplier", 2.0) : 1.0;
        user.LoyaltyPoints += (int)(basePoints * multiplier);
        await _db.SaveChangesAsync();

        await CheckBadgesAsync(userId);
    }

    private async Task<int> GetStreakAsync(string userId)
    {
        var dates = await _db.Attendances
            .Where(a => a.UserId == userId && a.Type == AttendanceType.CheckIn)
            .Select(a => a.Timestamp.Date).Distinct().OrderByDescending(d => d).ToListAsync();
        if (!dates.Any() || dates[0] < DateTime.UtcNow.Date.AddDays(-1)) return 0;
        int streak = 1;
        for (int i = 1; i < dates.Count; i++) { if (dates[i] == dates[i - 1].AddDays(-1)) streak++; else break; }
        return streak;
    }

    private async Task CheckBadgesAsync(string userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;

        var totalCheckins = await _db.Attendances.CountAsync(a => a.UserId == userId && a.Type == AttendanceType.CheckIn);
        var totalWorkouts = await _db.WorkoutLogs.CountAsync(w => w.UserId == userId);
        var totalPosts = await _db.ForumPosts.CountAsync(p => p.UserId == userId);
        var totalClasses = await _db.ClassBookings.CountAsync(b => b.UserId == userId && b.IsAttended);

        var badges = _config.GetSection("Gamification:Badges").GetChildren();
        foreach (var badge in badges)
        {
            var name = badge.GetValue<string>("Name");
            var points = badge.GetValue<int>("Points");
            var exists = await _db.Achievements.AnyAsync(a => a.UserId == userId && a.Name == name);
            if (!exists && name != null)
            {
                bool earned = name switch
                {
                    "First Steps" => totalCheckins >= 1,
                    "Dedicated" => totalCheckins >= 10,
                    "Class Master" => totalClasses >= 50,
                    "Iron Body" => totalWorkouts >= 100,
                    "Social Butterfly" => totalPosts >= 50,
                    _ => false
                };
                if (earned)
                {
                    _db.Achievements.Add(new Achievement { UserId = userId, Name = name, Points = points, Category = AchievementCategory.Special, EarnedAt = DateTime.UtcNow });
                    user.LoyaltyPoints += points;
                }
            }
        }
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Get top N leaderboard entries.
    /// Perbaikan: EF Core tidak bisa translate .Select((u, i) => ...), 
    /// jadi materialize dulu ke list baru assign rank di memory.
    /// </summary>
    public async Task<List<LeaderboardEntry>> GetLeaderboardAsync(int top = 20)
    {
        // Step 1: Ambil data dari DB tanpa index (EF Core compatible)
        var topUsers = await _db.Users
            .Where(u => u.IsActive)
            .OrderByDescending(u => u.LoyaltyPoints)
            .Take(top)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.LoyaltyPoints,
                u.ProfilePictureUrl
            })
            .ToListAsync();

        // Step 2: Assign rank di memory (client-side)
        return topUsers
            .Select((u, i) => new LeaderboardEntry
            {
                UserId = u.Id,
                UserName = u.FullName,
                TotalPoints = u.LoyaltyPoints,
                Rank = i + 1,
                ProfilePictureUrl = u.ProfilePictureUrl
            })
            .ToList();
    }

    public async Task<List<Achievement>> GetUserAchievementsAsync(string userId) =>
        await _db.Achievements.Where(a => a.UserId == userId).OrderByDescending(a => a.EarnedAt).ToListAsync();
}
