using Comblang.Data;
using Comblang.Models;
using Comblang.Services.Location;
using Microsoft.EntityFrameworkCore;

namespace Comblang.Services.Matching;

/// <summary>
/// Core matchmaking engine: handles swipe logic, compatibility scoring,
/// and recommendation generation.
/// </summary>
public class MatchEngine
{
    private readonly AppDbContext _db;

    public MatchEngine(AppDbContext db)
    {
        _db = db;
    }

    // -------------------------------------------------------
    //  Swipe
    // -------------------------------------------------------

    /// <summary>
    /// Records a swipe action (Like / Skip / SuperLike) from one user to another.
    /// If the swipe is mutual (both "Like" or "SuperLike"), a Match is created.
    /// </summary>
    /// <returns>A tuple indicating whether a match occurred and a message.</returns>
    public async Task<(bool isMatch, string message)> SwipeAsync(
        Guid swiperId, Guid targetId, string swipeType)
    {
        // Validate swipe type
        if (swipeType is not ("Like" or "Skip" or "SuperLike"))
            return (false, "Tipe swipe tidak valid.");

        // Prevent self-swipe
        if (swiperId == targetId)
            return (false, "Kamu tidak bisa swipe diri sendiri.");

        // Check for duplicate
        var existing = await _db.Swipes
            .FirstOrDefaultAsync(s => s.SwiperId == swiperId && s.TargetId == targetId);

        if (existing != null)
            return (false, "Kamu sudah melakukan swipe pada user ini.");

        var swipe = new Swipe
        {
            SwiperId = swiperId,
            TargetId = targetId,
            SwipeType = swipeType
        };
        _db.Swipes.Add(swipe);

        bool isMatch = false;

        // Check for mutual like
        if (swipeType is "Like" or "SuperLike")
        {
            var mutual = await _db.Swipes
                .FirstOrDefaultAsync(s =>
                    s.SwiperId == targetId &&
                    s.TargetId == swiperId &&
                    (s.SwipeType == "Like" || s.SwipeType == "SuperLike"));

            if (mutual != null)
            {
                // Guard against duplicate match
                var alreadyMatched = await _db.Matches.AnyAsync(m =>
                    (m.UserId1 == swiperId && m.UserId2 == targetId) ||
                    (m.UserId1 == targetId && m.UserId2 == swiperId));

                if (!alreadyMatched)
                {
                    var score = await CalculateCompatibilityAsync(swiperId, targetId);
                    var match = new Match
                    {
                        UserId1 = swiperId,
                        UserId2 = targetId,
                        CompatibilityScore = score,
                        MatchedAt = DateTime.UtcNow
                    };
                    _db.Matches.Add(match);
                    isMatch = true;
                }
            }
        }

        await _db.SaveChangesAsync();
        return (isMatch, isMatch ? "\ud83c\udf89 Match! Kalian cocok!" : "Swipe tercatat.");
    }

    // -------------------------------------------------------
    //  Recommendations
    // -------------------------------------------------------

    /// <summary>
    /// Returns recommended profiles for a user. Excludes users who have already
    /// been swiped on, blocked, or are banned. Sorts by boosted status and
    /// filters by geographic radius.
    /// </summary>
    public async Task<List<User>> GetRecommendationsAsync(
        Guid userId, int count = 20, double radiusKm = 50)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return [];

        // IDs of users already swiped on
        var swipedIds = await _db.Swipes
            .Where(s => s.SwiperId == userId)
            .Select(s => s.TargetId)
            .ToListAsync();

        // IDs of blocked relationships (both directions)
        var blockedIds = await _db.UserBlocks
            .Where(b => b.BlockerId == userId || b.BlockedUserId == userId)
            .Select(b => b.BlockerId == userId ? b.BlockedUserId : b.BlockerId)
            .ToListAsync();

        var excludeIds = swipedIds
            .Concat(blockedIds)
            .Append(userId)
            .Distinct()
            .ToHashSet();

        // Fetch candidates
        var candidates = await _db.Users
            .Include(u => u.Profile)
            .Include(u => u.InterestTags)
            .Where(u => !excludeIds.Contains(u.Id) && !u.IsBanned)
            .ToListAsync();

        // Boosted user IDs (for prioritisation)
        var boostedIds = await _db.Boosts
            .Where(b => b.IsActive && b.EndTime > DateTime.UtcNow)
            .Select(b => b.UserId)
            .ToHashSetAsync();

        // Filter by location and order
        var filtered = candidates
            .Where(c =>
            {
                var distance = GeoService.CalculateDistance(
                    user.Latitude, user.Longitude,
                    c.Latitude, c.Longitude);
                return distance <= radiusKm;
            })
            .OrderByDescending(c => boostedIds.Contains(c.Id))  // Boosted first
            .ThenBy(c => Guid.NewGuid())                         // Shuffle
            .Take(count)
            .ToList();

        return filtered;
    }

    // -------------------------------------------------------
    //  Compatibility scoring
    // -------------------------------------------------------

    /// <summary>
    /// Calculates a compatibility score (0-100) between two users based on
    /// shared interest tags, geographic proximity, and profile completeness.
    /// </summary>
    public async Task<double> CalculateCompatibilityAsync(Guid userId1, Guid userId2)
    {
        var tags1 = await _db.InterestTags
            .Where(t => t.UserId == userId1)
            .Select(t => t.TagName.ToLower())
            .ToListAsync();

        var tags2 = await _db.InterestTags
            .Where(t => t.UserId == userId2)
            .Select(t => t.TagName.ToLower())
            .ToListAsync();

        // --- Tag similarity (0-70 points) ---
        double tagScore;
        if (tags1.Count == 0 && tags2.Count == 0)
        {
            tagScore = 35.0; // neutral baseline
        }
        else
        {
            var commonCount = tags1.Intersect(tags2).Count();
            var unionCount = tags1.Union(tags2).Count();
            tagScore = unionCount == 0 ? 35.0 : (double)commonCount / unionCount * 70.0;
        }

        // --- Location proximity (0-20 points) ---
        var user1 = await _db.Users.FindAsync(userId1);
        var user2 = await _db.Users.FindAsync(userId2);

        double locationBonus = 0;
        if (user1 != null && user2 != null)
        {
            var distance = GeoService.CalculateDistance(
                user1.Latitude, user1.Longitude,
                user2.Latitude, user2.Longitude);

            // +20 points at 0 km, 0 points at >= 100 km
            locationBonus = Math.Max(0, 20.0 - distance / 5.0);
        }

        // --- Profile completeness (0-10 points) ---
        var profile1 = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId1);
        var profile2 = await _db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId2);

        double completenessBonus = 0;
        if (!string.IsNullOrWhiteSpace(profile1?.Bio)) completenessBonus += 5;
        if (!string.IsNullOrWhiteSpace(profile2?.Bio)) completenessBonus += 5;

        return Math.Min(100.0, Math.Round(tagScore + locationBonus + completenessBonus, 1));
    }
}
