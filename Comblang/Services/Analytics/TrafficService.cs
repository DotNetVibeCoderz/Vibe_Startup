using Comblang.Data;
using Comblang.Models;
using Microsoft.EntityFrameworkCore;

namespace Comblang.Services.Analytics;

/// <summary>
/// Tracks page visits and provides analytics data such as daily visit
/// counts, top pages, and match-related insights.
/// </summary>
public class TrafficService
{
    private readonly AppDbContext _db;

    public TrafficService(AppDbContext db)
    {
        _db = db;
    }

    // -------------------------------------------------------
    //  Visit logging
    // -------------------------------------------------------

    /// <summary>
    /// Logs a page visit for analytics purposes.
    /// </summary>
    public async Task LogVisitAsync(
        string? pageUrl,
        string? ipAddress,
        string? userAgent,
        string? sessionId,
        Guid? userId)
    {
        var log = new TrafficLog
        {
            PageUrl = pageUrl,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            SessionId = sessionId,
            UserId = userId,
            Timestamp = DateTime.UtcNow
        };

        _db.TrafficLogs.Add(log);
        await _db.SaveChangesAsync();
    }

    // -------------------------------------------------------
    //  Daily visits
    // -------------------------------------------------------

    /// <summary>
    /// Returns daily visit counts for the last N days as a dictionary
    /// keyed by "yyyy-MM-dd".
    /// </summary>
    public async Task<Dictionary<string, int>> GetDailyVisitsAsync(int days = 30)
    {
        var startDate = DateTime.UtcNow.Date.AddDays(-days + 1);

        var logs = await _db.TrafficLogs
            .Where(t => t.Timestamp >= startDate)
            .ToListAsync();

        return logs
            .GroupBy(t => t.Timestamp.Date)
            .OrderBy(g => g.Key)
            .ToDictionary(
                g => g.Key.ToString("yyyy-MM-dd"),
                g => g.Count());
    }

    // -------------------------------------------------------
    //  Top pages
    // -------------------------------------------------------

    /// <summary>
    /// Returns the most-visited pages with their hit counts.
    /// </summary>
    public async Task<Dictionary<string, int>> GetTopPagesAsync(int count = 10)
    {
        return await _db.TrafficLogs
            .GroupBy(t => t.PageUrl ?? "/")
            .OrderByDescending(g => g.Count())
            .Take(count)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }

    // -------------------------------------------------------
    //  Match insights
    // -------------------------------------------------------

    /// <summary>
    /// Returns match-related insights for a specific user (profile views,
    /// likes received, and total active matches).
    /// </summary>
    public async Task<MatchInsights> GetMatchInsightsAsync(Guid userId)
    {
        var profileViews = await _db.AuditLogs
            .CountAsync(a => a.EntityId == userId && a.Action == "ProfileView");

        var likesReceived = await _db.Swipes
            .CountAsync(s =>
                s.TargetId == userId &&
                (s.SwipeType == "Like" || s.SwipeType == "SuperLike"));

        var totalMatches = await _db.Matches
            .CountAsync(m =>
                (m.UserId1 == userId || m.UserId2 == userId) && m.IsActive);

        return new MatchInsights
        {
            ProfileViews = profileViews,
            LikesReceived = likesReceived,
            TotalMatches = totalMatches
        };
    }
}

/// <summary>
/// Lightweight DTO for match-related analytics.
/// </summary>
public class MatchInsights
{
    public int ProfileViews { get; set; }
    public int LikesReceived { get; set; }
    public int TotalMatches { get; set; }
}
