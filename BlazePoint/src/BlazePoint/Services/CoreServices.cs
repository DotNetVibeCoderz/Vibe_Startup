using BlazePoint.Data;
using Markdig;
using Microsoft.EntityFrameworkCore;

namespace BlazePoint.Services;

public class AuditService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public async Task LogAsync(string category, string message, string? userId = null, string userName = "")
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.AuditLogs.Add(new AuditLog { Category = category, Message = message, UserId = userId, UserName = userName });
        await db.SaveChangesAsync();
    }

    public async Task<List<AuditLog>> GetRecentAsync(int count = 100, string? category = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.AuditLogs.AsQueryable();
        if (!string.IsNullOrEmpty(category)) q = q.Where(l => l.Category == category);
        return await q.OrderByDescending(l => l.CreatedAt).Take(count).ToListAsync();
    }
}

public class NotificationService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public event Action? Changed;

    public async Task NotifyAsync(string userId, string title, string message, string link = "")
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.Notifications.Add(new Notification { UserId = userId, Title = title, Message = message, Link = link });
        await db.SaveChangesAsync();
        Changed?.Invoke();
    }

    public async Task NotifyRoleAsync(string role, string title, string message, string link = "")
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var userIds = await (from ur in db.UserRoles
                             join r in db.Roles on ur.RoleId equals r.Id
                             where r.Name == role
                             select ur.UserId).ToListAsync();
        foreach (var id in userIds)
            db.Notifications.Add(new Notification { UserId = id, Title = title, Message = message, Link = link });
        await db.SaveChangesAsync();
        Changed?.Invoke();
    }

    /// <summary>Parse @mentions ("@username" by email prefix or display name word) and notify mentioned users.</summary>
    public async Task NotifyMentionsAsync(string body, string title, string link, string? exceptUserId = null)
    {
        var mentions = System.Text.RegularExpressions.Regex.Matches(body, @"@([\w.\-]+)")
            .Select(m => m.Groups[1].Value.ToLowerInvariant()).Distinct().ToList();
        if (mentions.Count == 0) return;
        await using var db = await dbFactory.CreateDbContextAsync();
        var users = await db.Users.ToListAsync();
        foreach (var user in users)
        {
            if (user.Id == exceptUserId) continue;
            var emailPrefix = (user.Email ?? "").Split('@')[0].ToLowerInvariant();
            var nameParts = user.DisplayName.ToLowerInvariant().Split(' ');
            if (mentions.Any(m => m == emailPrefix || nameParts.Contains(m)))
                db.Notifications.Add(new Notification
                {
                    UserId = user.Id, Title = title,
                    Message = "Anda disebut dalam diskusi.", Link = link
                });
        }
        await db.SaveChangesAsync();
        Changed?.Invoke();
    }

    public async Task<List<Notification>> GetForUserAsync(string userId, bool unreadOnly = false, int count = 30)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.Notifications.Where(n => n.UserId == userId);
        if (unreadOnly) q = q.Where(n => !n.IsRead);
        return await q.OrderByDescending(n => n.CreatedAt).Take(count).ToListAsync();
    }

    public async Task<int> UnreadCountAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task MarkReadAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var n = await db.Notifications.FindAsync(id);
        if (n is null) return;
        n.IsRead = true;
        await db.SaveChangesAsync();
        Changed?.Invoke();
    }

    public async Task MarkAllReadAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Notifications.Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        Changed?.Invoke();
    }
}

public class SettingsService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public async Task<string?> GetAsync(string key)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return (await db.AppSettings.FindAsync(key))?.Value;
    }

    public async Task SetAsync(string key, string value)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var setting = await db.AppSettings.FindAsync(key);
        if (setting is null) db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        else setting.Value = value;
        await db.SaveChangesAsync();
    }

    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.AppSettings.ToDictionaryAsync(s => s.Key, s => s.Value);
    }
}

public class MarkdownService
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseMediaLinks()
        .UseAutoLinks()
        .UseEmojiAndSmiley()
        .Build();

    public string ToHtml(string? markdown)
        => string.IsNullOrWhiteSpace(markdown) ? "" : Markdown.ToHtml(markdown, Pipeline);
}

public record UserDisplay(string Name, string Color, string? AvatarUrl);

public class UserService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    // avatar pointers live in AppSettings ("avatar:{userId}" -> storage key)
    // so no Identity schema change is needed and existing databases keep working
    private const string AvatarKeyPrefix = "avatar:";

    private Dictionary<string, UserDisplay>? _cache;
    private DateTime _cacheAt;

    public async Task<UserDisplay> GetDisplayAsync(string? userId)
    {
        if (userId is null) return new UserDisplay("System", "#65676b", null);
        if (_cache is null || DateTime.UtcNow - _cacheAt > TimeSpan.FromMinutes(2))
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var avatars = await db.AppSettings
                .Where(s => s.Key.StartsWith(AvatarKeyPrefix))
                .ToDictionaryAsync(s => s.Key, s => s.Value);
            _cache = await db.Users.ToDictionaryAsync(
                u => u.Id,
                u => new UserDisplay(u.DisplayName, u.AvatarColor,
                    avatars.TryGetValue(AvatarKeyPrefix + u.Id, out var key) ? "/api/files/" + key : null));
            _cacheAt = DateTime.UtcNow;
        }
        return _cache.TryGetValue(userId, out var v) ? v : new UserDisplay("Unknown", "#65676b", null);
    }

    /// <summary>Sets (or clears with null) the avatar storage key. Returns the previous key, if any.</summary>
    public async Task<string?> SetAvatarAsync(string userId, string? storageKey)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var setting = await db.AppSettings.FindAsync(AvatarKeyPrefix + userId);
        var previous = setting?.Value;
        if (storageKey is null)
        {
            if (setting is not null) db.AppSettings.Remove(setting);
        }
        else if (setting is null)
        {
            db.AppSettings.Add(new AppSetting { Key = AvatarKeyPrefix + userId, Value = storageKey });
        }
        else
        {
            setting.Value = storageKey;
        }
        await db.SaveChangesAsync();
        _cache = null; // avatar changed → refresh display cache
        return previous;
    }

    public async Task<List<ApplicationUser>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Users.OrderBy(u => u.DisplayName).ToListAsync();
    }
}
