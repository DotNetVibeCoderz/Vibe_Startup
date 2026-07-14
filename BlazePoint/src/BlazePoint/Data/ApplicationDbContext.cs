using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BlazePoint.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<NavigationItem> NavigationItems => Set<NavigationItem>();
    public DbSet<CmsPage> CmsPages => Set<CmsPage>();
    public DbSet<CmsPageVersion> CmsPageVersions => Set<CmsPageVersion>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<ListDefinition> Lists => Set<ListDefinition>();
    public DbSet<ListItemEntity> ListItems => Set<ListItemEntity>();
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<ApprovalTask> ApprovalTasks => Set<ApprovalTask>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<DiscussionThread> DiscussionThreads => Set<DiscussionThread>();
    public DbSet<DiscussionPost> DiscussionPosts => Set<DiscussionPost>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<ShareLink> ShareLinks => Set<ShareLink>();
    public DbSet<FormDefinition> Forms => Set<FormDefinition>();
    public DbSet<FormSubmission> FormSubmissions => Set<FormSubmission>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessageEntity> ChatMessages => Set<ChatMessageEntity>();
    public DbSet<SearchIndexEntry> SearchIndex => Set<SearchIndexEntry>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Site>().HasIndex(s => s.Slug).IsUnique();
        builder.Entity<CmsPage>().HasIndex(p => p.Slug);
        builder.Entity<Document>().HasIndex(d => new { d.FolderPath, d.IsDeleted });
        builder.Entity<DocumentVersion>().HasIndex(v => v.DocumentId);
        builder.Entity<ListItemEntity>().HasIndex(i => i.ListId);
        builder.Entity<Notification>().HasIndex(n => new { n.UserId, n.IsRead });
        builder.Entity<ShareLink>().HasIndex(s => s.Token).IsUnique();
        builder.Entity<ChatSession>().HasIndex(s => s.UserId);
        builder.Entity<SearchIndexEntry>().HasIndex(e => new { e.EntityType, e.EntityId });
        builder.Entity<CalendarEvent>().HasIndex(e => e.Start);

        builder.Entity<Document>()
            .HasMany(d => d.Versions).WithOne().HasForeignKey(v => v.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<CmsPage>()
            .HasMany(p => p.Versions).WithOne().HasForeignKey(v => v.PageId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<ListDefinition>()
            .HasMany(l => l.Items).WithOne().HasForeignKey(i => i.ListId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<ChatSession>()
            .HasMany(s => s.Messages).WithOne().HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<WorkflowInstance>()
            .HasMany(i => i.Tasks).WithOne(t => t.Instance).HasForeignKey(t => t.InstanceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<FormDefinition>()
            .HasMany(f => f.Submissions).WithOne().HasForeignKey(s => s.FormId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<DiscussionThread>()
            .HasMany(t => t.Posts).WithOne().HasForeignKey(p => p.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
