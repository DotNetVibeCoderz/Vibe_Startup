using BlazePoint.Data;
using BlazePoint.Services.Search;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace BlazePoint.Services;

public class CalendarService(IDbContextFactory<ApplicationDbContext> dbFactory, AuditService audit)
{
    public async Task<List<CalendarEvent>> GetRangeAsync(DateTime from, DateTime to, int? siteId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.CalendarEvents.Where(e => e.Start < to && e.End > from);
        if (siteId.HasValue) q = q.Where(e => e.SiteId == siteId);
        return await q.OrderBy(e => e.Start).ToListAsync();
    }

    public async Task<List<CalendarEvent>> GetUpcomingAsync(int count = 10, int? siteId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.CalendarEvents.Where(e => e.End >= DateTime.Now);
        if (siteId.HasValue) q = q.Where(e => e.SiteId == siteId);
        return await q.OrderBy(e => e.Start).Take(count).ToListAsync();
    }

    public async Task<CalendarEvent> SaveAsync(CalendarEvent evt, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (evt.Id == 0) { evt.CreatedById = userId; db.CalendarEvents.Add(evt); }
        else db.CalendarEvents.Update(evt);
        await db.SaveChangesAsync();
        await audit.LogAsync("Calendar", $"Simpan event '{evt.Title}'", userId);
        return evt;
    }

    public async Task DeleteAsync(int id, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var evt = await db.CalendarEvents.FindAsync(id);
        if (evt is null) return;
        db.CalendarEvents.Remove(evt);
        await db.SaveChangesAsync();
        await audit.LogAsync("Calendar", $"Hapus event '{evt.Title}'", userId);
    }

    /// <summary>ICS export — importable into Outlook & Google Calendar (sync via subscribe URL /api/calendar/feed.ics).</summary>
    public static string ToIcs(IEnumerable<CalendarEvent> events)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//BlazePoint//Calendar//EN");
        foreach (var e in events)
        {
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"UID:blazepoint-{e.Id}@blazepoint.local");
            sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
            if (e.AllDay)
            {
                sb.AppendLine($"DTSTART;VALUE=DATE:{e.Start:yyyyMMdd}");
                sb.AppendLine($"DTEND;VALUE=DATE:{e.End:yyyyMMdd}");
            }
            else
            {
                sb.AppendLine($"DTSTART:{e.Start.ToUniversalTime():yyyyMMddTHHmmssZ}");
                sb.AppendLine($"DTEND:{e.End.ToUniversalTime():yyyyMMddTHHmmssZ}");
            }
            sb.AppendLine($"SUMMARY:{Escape(e.Title)}");
            if (!string.IsNullOrEmpty(e.Description)) sb.AppendLine($"DESCRIPTION:{Escape(e.Description)}");
            if (!string.IsNullOrEmpty(e.Location)) sb.AppendLine($"LOCATION:{Escape(e.Location)}");
            if (e.ReminderMinutes > 0)
            {
                sb.AppendLine("BEGIN:VALARM");
                sb.AppendLine($"TRIGGER:-PT{e.ReminderMinutes}M");
                sb.AppendLine("ACTION:DISPLAY");
                sb.AppendLine($"DESCRIPTION:{Escape(e.Title)}");
                sb.AppendLine("END:VALARM");
            }
            sb.AppendLine("END:VEVENT");
        }
        sb.AppendLine("END:VCALENDAR");
        return sb.ToString();

        static string Escape(string s) => s.Replace(@"\", @"\\").Replace(",", @"\,").Replace(";", @"\;").Replace("\n", @"\n");
    }

    public static string GoogleCalendarUrl(CalendarEvent e) =>
        "https://calendar.google.com/calendar/render?action=TEMPLATE" +
        $"&text={Uri.EscapeDataString(e.Title)}" +
        $"&dates={e.Start.ToUniversalTime():yyyyMMddTHHmmssZ}/{e.End.ToUniversalTime():yyyyMMddTHHmmssZ}" +
        $"&details={Uri.EscapeDataString(e.Description)}" +
        $"&location={Uri.EscapeDataString(e.Location)}";
}

public class ShareLinkService(IDbContextFactory<ApplicationDbContext> dbFactory, AuditService audit)
{
    public async Task<ShareLink> CreateAsync(int documentId, bool isPublic, DateTime? expiresAt, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var link = new ShareLink
        {
            DocumentId = documentId, IsPublic = isPublic, ExpiresAt = expiresAt, CreatedById = userId,
            Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()
        };
        db.ShareLinks.Add(link);
        await db.SaveChangesAsync();
        await audit.LogAsync("Document", $"Buat share link untuk dokumen #{documentId}", userId);
        return link;
    }

    public async Task<List<ShareLink>> GetAllAsync(string? userId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.ShareLinks.Include(s => s.Document).AsQueryable();
        if (userId is not null) q = q.Where(s => s.CreatedById == userId);
        return await q.OrderByDescending(s => s.CreatedAt).ToListAsync();
    }

    public async Task<ShareLink?> GetByTokenAsync(string token)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ShareLinks.Include(s => s.Document)
            .FirstOrDefaultAsync(s => s.Token == token);
    }

    public async Task RegisterDownloadAsync(int linkId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.ShareLinks.Where(s => s.Id == linkId)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.DownloadCount, l => l.DownloadCount + 1));
    }

    public async Task DeleteAsync(int id, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var link = await db.ShareLinks.FindAsync(id);
        if (link is null) return;
        db.ShareLinks.Remove(link);
        await db.SaveChangesAsync();
        await audit.LogAsync("Document", $"Hapus share link #{id}", userId);
    }
}

public class DiscussionService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    NotificationService notifications,
    SearchService search)
{
    public async Task<List<DiscussionThread>> GetThreadsAsync(int? siteId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.DiscussionThreads.Include(t => t.Posts).AsQueryable();
        if (siteId.HasValue) q = q.Where(t => t.SiteId == siteId);
        return await q.OrderByDescending(t => t.CreatedAt).ToListAsync();
    }

    public async Task<DiscussionThread?> GetThreadAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.DiscussionThreads.Include(t => t.Posts)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<DiscussionThread> CreateThreadAsync(string title, string body, string? userId, int? siteId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var thread = new DiscussionThread { Title = title, Body = body, CreatedById = userId, SiteId = siteId };
        db.DiscussionThreads.Add(thread);
        await db.SaveChangesAsync();
        await search.IndexAsync("Discussion", thread.Id, title, body, $"/discussions/{thread.Id}");
        await notifications.NotifyMentionsAsync(body, $"Disebut di: {title}", $"/discussions/{thread.Id}", userId);
        return thread;
    }

    public async Task<DiscussionPost> ReplyAsync(int threadId, int? parentPostId, string body, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var post = new DiscussionPost { ThreadId = threadId, ParentPostId = parentPostId, Body = body, CreatedById = userId };
        db.DiscussionPosts.Add(post);
        await db.SaveChangesAsync();

        var thread = await db.DiscussionThreads.FindAsync(threadId);
        if (thread is not null)
        {
            await notifications.NotifyMentionsAsync(body, $"Disebut di: {thread.Title}", $"/discussions/{threadId}", userId);
            if (thread.CreatedById is not null && thread.CreatedById != userId)
                await notifications.NotifyAsync(thread.CreatedById, "Balasan baru",
                    $"Ada balasan baru di '{thread.Title}'", $"/discussions/{threadId}");
        }
        return post;
    }

    public async Task DeleteThreadAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var thread = await db.DiscussionThreads.FindAsync(id);
        if (thread is null) return;
        db.DiscussionThreads.Remove(thread);
        await db.SaveChangesAsync();
        await search.RemoveAsync("Discussion", id);
    }
}

public class FormService(IDbContextFactory<ApplicationDbContext> dbFactory, AuditService audit)
{
    public async Task<List<FormDefinition>> GetAllAsync(bool? templates = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.Forms.AsQueryable();
        if (templates.HasValue) q = q.Where(f => f.IsTemplate == templates);
        return await q.OrderBy(f => f.Name).ToListAsync();
    }

    public async Task<FormDefinition?> GetAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Forms.FindAsync(id);
    }

    public async Task<FormDefinition> SaveAsync(FormDefinition form, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (form.Id == 0) { form.CreatedById = userId; db.Forms.Add(form); }
        else db.Forms.Update(form);
        await db.SaveChangesAsync();
        await audit.LogAsync("Form", $"Simpan form '{form.Name}'", userId);
        return form;
    }

    public async Task DeleteAsync(int id, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var form = await db.Forms.FindAsync(id);
        if (form is null) return;
        db.Forms.Remove(form);
        await db.SaveChangesAsync();
        await audit.LogAsync("Form", $"Hapus form '{form.Name}'", userId);
    }

    public async Task<FormSubmission> SubmitAsync(int formId, string dataJson, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var submission = new FormSubmission { FormId = formId, DataJson = dataJson, SubmittedById = userId };
        db.FormSubmissions.Add(submission);
        await db.SaveChangesAsync();
        return submission;
    }

    public async Task<List<FormSubmission>> GetSubmissionsAsync(int formId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.FormSubmissions.Where(s => s.FormId == formId)
            .OrderByDescending(s => s.SubmittedAt).ToListAsync();
    }
}
