using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PDA.Data;

/// <summary>
/// Main application database context
/// Supports SQLite, SQLServer, MySQL providers
/// </summary>
public class AppDbContext : IdentityDbContext<Models.ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Core tables
    public DbSet<Models.DatabaseConnection> DatabaseConnections => Set<Models.DatabaseConnection>();
    public DbSet<Models.ChatSession> ChatSessions => Set<Models.ChatSession>();
    public DbSet<Models.ChatMessage> ChatMessages => Set<Models.ChatMessage>();
    public DbSet<Models.AuditLog> AuditLogs => Set<Models.AuditLog>();
    public DbSet<Models.RagIndexedDocument> RagIndexedDocuments => Set<Models.RagIndexedDocument>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ChatSession
        builder.Entity<Models.ChatSession>(entity =>
        {
            entity.HasOne(e => e.User)
                .WithMany(u => u.ChatSessions)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.DatabaseConnection)
                .WithMany(d => d.ChatSessions)
                .HasForeignKey(e => e.DatabaseConnectionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.UpdatedAt);
        });

        // ChatMessage
        builder.Entity<Models.ChatMessage>(entity =>
        {
            entity.HasOne(e => e.ChatSession)
                .WithMany(s => s.Messages)
                .HasForeignKey(e => e.ChatSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ChatSessionId);
            entity.HasIndex(e => e.Timestamp);
        });

        // DatabaseConnection
        builder.Entity<Models.DatabaseConnection>(entity =>
        {
            entity.HasOne(e => e.User)
                .WithMany(u => u.DatabaseConnections)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Name);
        });

        // AuditLog
        builder.Entity<Models.AuditLog>(entity =>
        {
            entity.HasOne(e => e.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => e.UserId);
        });

        // RagIndexedDocument
        builder.Entity<Models.RagIndexedDocument>(entity =>
        {
            entity.HasIndex(e => e.FilePath);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.IndexedAt);
        });
    }
}
