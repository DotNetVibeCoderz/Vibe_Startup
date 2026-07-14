using BlazePoint.Data;
using Microsoft.EntityFrameworkCore;

namespace BlazePoint.Services;

public record DashboardStats(
    int DocumentCount, long TotalStorageBytes, int SiteCount, int UserCount,
    int PageCount, int ListCount, int OpenTasks, int DiscussionCount,
    int UpcomingEvents, int ChatSessions);

public class DashboardService(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    public async Task<DashboardStats> GetStatsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return new DashboardStats(
            DocumentCount: await db.Documents.CountAsync(d => !d.IsDeleted),
            TotalStorageBytes: await db.Documents.Where(d => !d.IsDeleted).SumAsync(d => (long?)d.Size) ?? 0,
            SiteCount: await db.Sites.CountAsync(),
            UserCount: await db.Users.CountAsync(),
            PageCount: await db.CmsPages.CountAsync(),
            ListCount: await db.Lists.CountAsync(),
            OpenTasks: await db.ApprovalTasks.CountAsync(t => t.Status == ApprovalStatus.Pending),
            DiscussionCount: await db.DiscussionThreads.CountAsync(),
            UpcomingEvents: await db.CalendarEvents.CountAsync(e => e.Start >= DateTime.Now),
            ChatSessions: await db.ChatSessions.CountAsync());
    }

    /// <summary>Documents uploaded per month for the last N months (trend analysis).</summary>
    public async Task<List<(string Label, int Count)>> DocumentTrendAsync(int months = 6)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var from = DateTime.UtcNow.AddMonths(-(months - 1));
        var start = new DateTime(from.Year, from.Month, 1);
        var docs = await db.Documents
            .Where(d => d.CreatedAt >= start)
            .Select(d => d.CreatedAt).ToListAsync();
        var result = new List<(string, int)>();
        for (var i = 0; i < months; i++)
        {
            var month = start.AddMonths(i);
            result.Add((month.ToString("MMM yy"),
                docs.Count(d => d.Year == month.Year && d.Month == month.Month)));
        }
        return result;
    }

    /// <summary>Storage usage grouped by content type family for the doughnut chart.</summary>
    public async Task<List<(string Label, long Bytes)>> StorageByTypeAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var docs = await db.Documents.Where(d => !d.IsDeleted)
            .Select(d => new { d.ContentType, d.Size }).ToListAsync();
        return docs
            .GroupBy(d => Family(d.ContentType))
            .Select(g => (g.Key, g.Sum(x => x.Size)))
            .OrderByDescending(x => x.Item2)
            .ToList();

        static string Family(string ct) => ct.Split('/')[0] switch
        {
            "image" => "Gambar",
            "video" => "Video",
            "audio" => "Audio",
            "text" => "Teks",
            "application" => ct.Contains("pdf") ? "PDF" : "Aplikasi",
            _ => "Lainnya"
        };
    }

    /// <summary>Activity per category from the audit log for the bar chart.</summary>
    public async Task<List<(string Label, int Count)>> ActivityByCategoryAsync(int days = 30)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var from = DateTime.UtcNow.AddDays(-days);
        var logs = await db.AuditLogs.Where(l => l.CreatedAt >= from)
            .GroupBy(l => l.Category)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync();
        return logs.OrderByDescending(l => l.Count).Select(l => (l.Key, l.Count)).ToList();
    }

    public async Task<List<AuditLog>> RecentActivityAsync(int count = 12)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.AuditLogs.OrderByDescending(l => l.CreatedAt).Take(count).ToListAsync();
    }
}
