using AppBender.Core.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AppBender.Core.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<EntityDefinition> EntityDefinitions => Set<EntityDefinition>();
    public DbSet<DataRecord> DataRecords => Set<DataRecord>();
    public DbSet<FormDefinition> FormDefinitions => Set<FormDefinition>();
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowRun> WorkflowRuns => Set<WorkflowRun>();
    public DbSet<AppDefinition> AppDefinitions => Set<AppDefinition>();
    public DbSet<ConnectorDefinition> ConnectorDefinitions => Set<ConnectorDefinition>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<MlModelDefinition> MlModels => Set<MlModelDefinition>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<UsageMetric> UsageMetrics => Set<UsageMetric>();
    public DbSet<VersionSnapshot> VersionSnapshots => Set<VersionSnapshot>();
    public DbSet<CodeSnippet> CodeSnippets => Set<CodeSnippet>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Organization>().HasIndex(o => o.Slug).IsUnique();
        builder.Entity<EntityDefinition>().HasIndex(e => new { e.TenantId, e.Name }).IsUnique();
        builder.Entity<DataRecord>().HasIndex(r => new { r.TenantId, r.EntityName });
        builder.Entity<FormDefinition>().HasIndex(f => new { f.TenantId, f.Slug }).IsUnique();
        builder.Entity<AppDefinition>().HasIndex(a => a.Slug).IsUnique();
        builder.Entity<WorkflowRun>().HasIndex(r => new { r.TenantId, r.WorkflowId, r.StartedAt });
        builder.Entity<ChatMessage>().HasIndex(m => m.SessionId);
        builder.Entity<KnowledgeChunk>().HasIndex(c => new { c.TenantId, c.DocumentId });
        builder.Entity<AuditLog>().HasIndex(a => new { a.TenantId, a.Timestamp });
        builder.Entity<UsageMetric>().HasIndex(u => new { u.TenantId, u.Type, u.Timestamp });
        builder.Entity<VersionSnapshot>().HasIndex(v => new { v.TenantId, v.ItemType, v.ItemId });
    }
}
