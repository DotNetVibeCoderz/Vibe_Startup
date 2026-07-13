using Microsoft.EntityFrameworkCore;
using PCHub.Shared.Enums;
using PCHub.Shared.Models;

namespace PCHub.Shared.Data;

/// <summary>
/// Database context utama PCHub dengan dukungan multi-provider:
/// SQLite, SQL Server, PostgreSQL, MySQL
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Master Data Tables
    public DbSet<User> Users => Set<User>();
    public DbSet<Pc> Pcs => Set<Pc>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<UserMembership> UserMemberships => Set<UserMembership>();
    public DbSet<Promo> Promos => Set<Promo>();

    // Transaction Tables
    public DbSet<BillingSession> BillingSessions => Set<BillingSession>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<PcSession> PcSessions => Set<PcSession>();

    // Support & Communication
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportReply> SupportReplies => Set<SupportReply>();
    public DbSet<Notification> Notifications => Set<Notification>();

    // Reports & Events
    public DbSet<FinancialReport> FinancialReports => Set<FinancialReport>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<TournamentParticipant> TournamentParticipants => Set<TournamentParticipant>();

    // System
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User relationships
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.Username).IsUnique();
        });

        // BillingSession relationships
        modelBuilder.Entity<BillingSession>(entity =>
        {
            entity.HasOne(b => b.User)
                  .WithMany(u => u.BillingSessions)
                  .HasForeignKey(b => b.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Pc)
                  .WithMany()
                  .HasForeignKey(b => b.PcId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Reservation relationships
        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasOne(r => r.User)
                  .WithMany(u => u.Reservations)
                  .HasForeignKey(r => r.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.Pc)
                  .WithMany()
                  .HasForeignKey(r => r.PcId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // PcSession relationships
        modelBuilder.Entity<PcSession>(entity =>
        {
            entity.HasOne(s => s.Pc)
                  .WithMany(p => p.Sessions)
                  .HasForeignKey(s => s.PcId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.Game)
                  .WithMany(g => g.Sessions)
                  .HasForeignKey(s => s.GameId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // UserMembership
        modelBuilder.Entity<UserMembership>(entity =>
        {
            entity.HasOne(um => um.User)
                  .WithMany()
                  .HasForeignKey(um => um.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(um => um.Membership)
                  .WithMany(m => m.UserMemberships)
                  .HasForeignKey(um => um.MembershipId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // SupportTicket
        modelBuilder.Entity<SupportTicket>(entity =>
        {
            entity.HasOne(t => t.User)
                  .WithMany(u => u.SupportTickets)
                  .HasForeignKey(t => t.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // SupportReply
        modelBuilder.Entity<SupportReply>(entity =>
        {
            entity.HasOne(r => r.Ticket)
                  .WithMany(t => t.Replies)
                  .HasForeignKey(r => r.TicketId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Tournament
        modelBuilder.Entity<Tournament>(entity =>
        {
            entity.HasOne(t => t.Game)
                  .WithMany()
                  .HasForeignKey(t => t.GameId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // TournamentParticipant
        modelBuilder.Entity<TournamentParticipant>(entity =>
        {
            entity.HasOne(tp => tp.Tournament)
                  .WithMany(t => t.Participants)
                  .HasForeignKey(tp => tp.TournamentId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(tp => tp.User)
                  .WithMany()
                  .HasForeignKey(tp => tp.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // SystemConfig unique key
        modelBuilder.Entity<SystemConfig>(entity =>
        {
            entity.HasIndex(c => c.Key).IsUnique();
        });
    }

    /// <summary>Factory method untuk memilih database provider</summary>
    public static DbContextOptionsBuilder<AppDbContext> ConfigureProvider(
        DbContextOptionsBuilder<AppDbContext> options,
        DatabaseProvider provider,
        string connectionString)
    {
        switch (provider)
        {
            case DatabaseProvider.SQLite:
                options.UseSqlite(connectionString);
                break;
            case DatabaseProvider.SqlServer:
                options.UseSqlServer(connectionString);
                break;
            case DatabaseProvider.PostgreSQL:
                options.UseNpgsql(connectionString);
                break;
            case DatabaseProvider.MySQL:
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
                break;
            default:
                options.UseSqlite(connectionString);
                break;
        }
        return options;
    }
}
