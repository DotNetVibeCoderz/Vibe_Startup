using Microsoft.EntityFrameworkCore;
using LandLord.Models;

namespace LandLord.Data;

/// <summary>
/// Application DbContext dengan dukungan multi-database provider
/// Mendukung: SQLite, SQL Server, MySQL, PostgreSQL
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // DbSets untuk setiap entity
    public DbSet<Tanah> Tanah { get; set; } = null!;
    public DbSet<Bangunan> Bangunan { get; set; } = null!;
    public DbSet<Document> Documents { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<ChatSession> ChatSessions { get; set; } = null!;
    public DbSet<ChatMessage> ChatMessages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Konfigurasi Tanah ---
        modelBuilder.Entity<Tanah>(entity =>
        {
            entity.HasIndex(e => e.NomorSertifikat).IsUnique();
            entity.HasIndex(e => e.NIB);
            entity.HasIndex(e => e.JenisHak);
            entity.HasIndex(e => e.Pemilik);
            entity.HasIndex(e => e.Lokasi);
        });

        // --- Konfigurasi Bangunan ---
        modelBuilder.Entity<Bangunan>(entity =>
        {
            entity.HasIndex(e => e.NomorIimbPbg).IsUnique();
            entity.HasIndex(e => e.JenisBangunan);
            entity.HasIndex(e => e.FungsiBangunan);
            entity.HasIndex(e => e.NamaPemilik);
            entity.HasIndex(e => e.Lokasi);
        });

        // --- Konfigurasi Document ---
        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasOne(d => d.Tanah)
                .WithMany(t => t.Documents)
                .HasForeignKey(d => d.TanahId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Bangunan)
                .WithMany(b => b.Documents)
                .HasForeignKey(d => d.BangunanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --- Konfigurasi User ---
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // --- Konfigurasi ChatSession ---
        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.LastUpdatedAt);
        });

        // --- Konfigurasi ChatMessage ---
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasOne(m => m.ChatSession)
                .WithMany(s => s.Messages)
                .HasForeignKey(m => m.ChatSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.ChatSessionId);
            entity.HasIndex(e => e.SentAt);
        });
    }
}
