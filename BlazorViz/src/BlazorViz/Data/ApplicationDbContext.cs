using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BlazorViz.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<DataConnection> Connections => Set<DataConnection>();
    public DbSet<Dataset> Datasets => Set<Dataset>();
    public DbSet<Dashboard> Dashboards => Set<Dashboard>();
    public DbSet<DashboardVersion> DashboardVersions => Set<DashboardVersion>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<UsageMetric> UsageMetrics => Set<UsageMetric>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessageEntity> ChatMessages => Set<ChatMessageEntity>();
    public DbSet<RagDocument> RagDocuments => Set<RagDocument>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Dashboard>().HasIndex(d => d.ShareToken);
        builder.Entity<AuditLog>().HasIndex(a => a.TimestampUtc);
        builder.Entity<UsageMetric>().HasIndex(u => new { u.Kind, u.TimestampUtc });
        builder.Entity<ChatMessageEntity>().HasIndex(m => m.SessionId);
        builder.Entity<DashboardVersion>().HasIndex(v => new { v.DashboardId, v.Version });
        builder.Entity<ApiKey>().HasIndex(k => k.Key).IsUnique();
    }
}
