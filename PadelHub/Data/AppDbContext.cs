using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PadelHub.Models;

namespace PadelHub.Data;

/// <summary>
/// Main application database context with multi-database provider support.
/// Supports SQLite (default), SQL Server, MySQL, and PostgreSQL.
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly IConfiguration _configuration;

    public AppDbContext(DbContextOptions<AppDbContext> options, IConfiguration configuration)
        : base(options)
    {
        _configuration = configuration;
    }

    // Core Tables
    public DbSet<Club> Clubs => Set<Club>();
    public DbSet<Court> Courts => Set<Court>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<OperatingHour> OperatingHours => Set<OperatingHour>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Payment> Payments => Set<Payment>();

    // Membership & Finance
    public DbSet<MembershipPackage> MembershipPackages => Set<MembershipPackage>();
    public DbSet<UserMembership> UserMemberships => Set<UserMembership>();
    public DbSet<LoyaltyPoint> LoyaltyPoints => Set<LoyaltyPoint>();

    // Player & Coach
    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();
    public DbSet<PlayerStat> PlayerStats => Set<PlayerStat>();
    public DbSet<PlayerAchievement> PlayerAchievements => Set<PlayerAchievement>();
    public DbSet<Coach> Coaches => Set<Coach>();
    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();
    public DbSet<CourseMaterial> CourseMaterials => Set<CourseMaterial>();

    // Tournament
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<TournamentRegistration> TournamentRegistrations => Set<TournamentRegistration>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchPlayer> MatchPlayers => Set<MatchPlayer>();

    // Social
    public DbSet<TimelinePost> TimelinePosts => Set<TimelinePost>();
    public DbSet<TimelineComment> TimelineComments => Set<TimelineComment>();
    public DbSet<TimelineLike> TimelineLikes => Set<TimelineLike>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatGroup> ChatGroups => Set<ChatGroup>();
    public DbSet<ChatGroupMember> ChatGroupMembers => Set<ChatGroupMember>();
    public DbSet<ForumTopic> ForumTopics => Set<ForumTopic>();
    public DbSet<ForumPost> ForumPosts => Set<ForumPost>();
    public DbSet<SocialEvent> SocialEvents => Set<SocialEvent>();

    // System
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<UserBadge> UserBadges => Set<UserBadge>();
    public DbSet<SensorData> SensorData => Set<SensorData>();
    public DbSet<IoTSimulator> IoTSimulators => Set<IoTSimulator>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<CourtMaintenance> CourtMaintenances => Set<CourtMaintenance>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure relationships and indexes

        // Reservation → Court
        builder.Entity<Reservation>()
            .HasOne(r => r.Court)
            .WithMany(c => c.Reservations)
            .HasForeignKey(r => r.CourtId)
            .OnDelete(DeleteBehavior.Restrict);

        // Reservation → User
        builder.Entity<Reservation>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Payment → Reservation
        builder.Entity<Payment>()
            .HasOne(p => p.Reservation)
            .WithOne(r => r.Payment)
            .HasForeignKey<Payment>(p => p.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        // PlayerProfile → User
        builder.Entity<PlayerProfile>()
            .HasOne(p => p.User)
            .WithOne(u => u.PlayerProfile)
            .HasForeignKey<PlayerProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Coach → User
        builder.Entity<Coach>()
            .HasOne(c => c.User)
            .WithOne(u => u.Coach)
            .HasForeignKey<Coach>(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Match → Tournament
        builder.Entity<Match>()
            .HasOne(m => m.Tournament)
            .WithMany(t => t.Matches)
            .HasForeignKey(m => m.TournamentId)
            .OnDelete(DeleteBehavior.Cascade);

        // MatchPlayer → Match & PlayerProfile
        builder.Entity<MatchPlayer>()
            .HasOne(mp => mp.Match)
            .WithMany(m => m.MatchPlayers)
            .HasForeignKey(mp => mp.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MatchPlayer>()
            .HasOne(mp => mp.PlayerProfile)
            .WithMany(p => p.MatchPlayers)
            .HasForeignKey(mp => mp.PlayerProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        // Timeline relationships
        builder.Entity<TimelineComment>()
            .HasOne(c => c.Post)
            .WithMany(p => p.Comments)
            .HasForeignKey(c => c.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TimelineLike>()
            .HasOne(l => l.Post)
            .WithMany(p => p.Likes)
            .HasForeignKey(l => l.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        // Chat
        builder.Entity<ChatGroupMember>()
            .HasOne(m => m.Group)
            .WithMany(g => g.Members)
            .HasForeignKey(m => m.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ChatMessage>()
            .HasOne(m => m.Group)
            .WithMany(g => g.Messages)
            .HasForeignKey(m => m.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // Forum
        builder.Entity<ForumPost>()
            .HasOne(p => p.Topic)
            .WithMany(t => t.Posts)
            .HasForeignKey(p => p.TopicId)
            .OnDelete(DeleteBehavior.Cascade);

        // Training Session
        builder.Entity<TrainingSession>()
            .HasOne(t => t.Coach)
            .WithMany(c => c.TrainingSessions)
            .HasForeignKey(t => t.CoachId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<TrainingSession>()
            .HasOne(t => t.Student)
            .WithMany()
            .HasForeignKey(t => t.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // AuditLog
        builder.Entity<AuditLog>()
            .HasOne(a => a.User)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes for performance
        builder.Entity<Reservation>().HasIndex(r => new { r.CourtId, r.ReservationDate });
        builder.Entity<Reservation>().HasIndex(r => r.UserId);
        builder.Entity<Match>().HasIndex(m => m.TournamentId);
        builder.Entity<TimelinePost>().HasIndex(p => p.CreatedAt);
        builder.Entity<AuditLog>().HasIndex(a => a.CreatedAt);
        builder.Entity<AuditLog>().HasIndex(a => a.Action);
        builder.Entity<ChatMessage>().HasIndex(m => new { m.SenderId, m.ReceiverId });
        builder.Entity<SensorData>().HasIndex(s => new { s.CourtId, s.RecordedAt });

        // Unique indexes
        builder.Entity<ApplicationUser>().HasIndex(u => u.MemberNumber).IsUnique();
        builder.Entity<SystemConfig>().HasIndex(c => c.Key).IsUnique();
    }
}
