using Comblang.Models;
using Microsoft.EntityFrameworkCore;

namespace Comblang.Data;

/// <summary>
/// Main database context for Comblang. Supports SQLite, SQL Server, MySQL, and PostgreSQL.
/// Provider is configured via appsettings.json "DatabaseProvider" key.
/// All relationships are configured via Fluent API in OnModelCreating.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ==========================================
    // Core Tables
    // ==========================================
    public DbSet<User> Users => Set<User>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<Swipe> Swipes => Set<Swipe>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<GroupRoom> GroupRooms => Set<GroupRoom>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<InterestTag> InterestTags => Set<InterestTag>();
    public DbSet<Gift> Gifts => Set<Gift>();
    public DbSet<GiftTransaction> GiftTransactions => Set<GiftTransaction>();
    public DbSet<Boost> Boosts => Set<Boost>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventParticipant> EventParticipants => Set<EventParticipant>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<TrafficLog> TrafficLogs => Set<TrafficLog>();

    // ==========================================
    // AI ChatBot Tables
    // ==========================================
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ==========================================
        // USER
        // ==========================================
        modelBuilder.Entity<User>(entity =>
        {
            // Unique indexes
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.Username).IsUnique();

            // Lookup / filter indexes
            entity.HasIndex(u => u.Role);
            entity.HasIndex(u => u.IsVerified);
            entity.HasIndex(u => u.IsPremium);
            entity.HasIndex(u => u.IsBanned);
            entity.HasIndex(u => u.LastActiveAt);
            entity.HasIndex(u => u.CreatedAt);

            // Location-based matching indexes
            entity.HasIndex(u => new { u.Latitude, u.Longitude });
            entity.HasIndex(u => u.City);
            entity.HasIndex(u => u.Country);

            // Self-referencing many-to-many for blocks
            entity.HasMany(u => u.BlockedUsers)
                .WithOne(b => b.Blocker)
                .HasForeignKey(b => b.BlockerId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(u => u.BlockedByUsers)
                .WithOne(b => b.BlockedUser)
                .HasForeignKey(b => b.BlockedUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ==========================================
        // PROFILE
        // ==========================================
        modelBuilder.Entity<Profile>(entity =>
        {
            // One-to-one with User
            entity.HasIndex(p => p.UserId).IsUnique();

            entity.HasOne(p => p.User)
                .WithOne(u => u.Profile)
                .HasForeignKey<Profile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Filter indexes for matching queries
            entity.HasIndex(p => p.Gender);
            entity.HasIndex(p => p.RelationshipGoal);
            entity.HasIndex(p => p.Religion);
            entity.HasIndex(p => p.ZodiacSign);
            entity.HasIndex(p => p.IsVerifiedPhoto);
        });

        // ==========================================
        // MATCH
        // ==========================================
        modelBuilder.Entity<Match>(entity =>
        {
            // Prevent duplicate matches between the same pair
            entity.HasIndex(m => new { m.UserId1, m.UserId2 }).IsUnique();

            entity.HasIndex(m => m.MatchedAt);
            entity.HasIndex(m => m.IsActive);
            entity.HasIndex(m => m.CompatibilityScore);

            entity.HasOne(m => m.User1)
                .WithMany(u => u.Matches)
                .HasForeignKey(m => m.UserId1)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(m => m.User2)
                .WithMany()
                .HasForeignKey(m => m.UserId2)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ==========================================
        // SWIPE
        // ==========================================
        modelBuilder.Entity<Swipe>(entity =>
        {
            // One swipe per pair (prevent re-swiping without reset)
            entity.HasIndex(s => new { s.SwiperId, s.TargetId }).IsUnique();

            entity.HasIndex(s => s.SwipedAt);
            entity.HasIndex(s => s.SwipeType);

            entity.HasOne(s => s.Swiper)
                .WithMany(u => u.Swipes)
                .HasForeignKey(s => s.SwiperId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(s => s.Target)
                .WithMany()
                .HasForeignKey(s => s.TargetId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ==========================================
        // MESSAGE
        // ==========================================
        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasIndex(m => m.SentAt);
            entity.HasIndex(m => m.IsRead);
            entity.HasIndex(m => m.SenderId);
            entity.HasIndex(m => m.ReceiverId);
            entity.HasIndex(m => m.GroupRoomId);
            entity.HasIndex(m => new { m.SenderId, m.ReceiverId, m.SentAt });

            entity.HasOne(m => m.Sender)
                .WithMany(u => u.Messages)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(m => m.Receiver)
                .WithMany()
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(m => m.GroupRoom)
                .WithMany(g => g.Messages)
                .HasForeignKey(m => m.GroupRoomId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ==========================================
        // GROUP ROOM
        // ==========================================
        modelBuilder.Entity<GroupRoom>(entity =>
        {
            entity.HasIndex(g => g.Category);
            entity.HasIndex(g => g.IsActive);
            entity.HasIndex(g => g.CreatedAt);
            entity.HasIndex(g => g.Name);
        });

        // ==========================================
        // GROUP MEMBER
        // ==========================================
        modelBuilder.Entity<GroupMember>(entity =>
        {
            // Prevent duplicate membership
            entity.HasIndex(gm => new { gm.GroupRoomId, gm.UserId }).IsUnique();

            entity.HasIndex(gm => gm.JoinedAt);
            entity.HasIndex(gm => gm.Role);

            entity.HasOne(gm => gm.GroupRoom)
                .WithMany(g => g.Members)
                .HasForeignKey(gm => gm.GroupRoomId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(gm => gm.User)
                .WithMany()
                .HasForeignKey(gm => gm.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ==========================================
        // INTEREST TAG
        // ==========================================
        modelBuilder.Entity<InterestTag>(entity =>
        {
            entity.HasIndex(t => t.UserId);
            entity.HasIndex(t => t.TagName);
            entity.HasIndex(t => t.Category);

            // Prevent duplicate tag per user
            entity.HasIndex(t => new { t.UserId, t.TagName }).IsUnique();

            entity.HasOne(t => t.User)
                .WithMany(u => u.InterestTags)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ==========================================
        // GIFT (Catalog — no FK relationships)
        // ==========================================
        modelBuilder.Entity<Gift>(entity =>
        {
            entity.HasIndex(g => g.Category);
            entity.HasIndex(g => g.Price);
        });

        // ==========================================
        // GIFT TRANSACTION
        // ==========================================
        modelBuilder.Entity<GiftTransaction>(entity =>
        {
            entity.HasIndex(gt => gt.SentAt);
            entity.HasIndex(gt => gt.SenderId);
            entity.HasIndex(gt => gt.ReceiverId);

            entity.HasOne(gt => gt.Gift)
                .WithMany()
                .HasForeignKey(gt => gt.GiftId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(gt => gt.Sender)
                .WithMany(u => u.GiftTransactionsSent)
                .HasForeignKey(gt => gt.SenderId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(gt => gt.Receiver)
                .WithMany(u => u.GiftTransactionsReceived)
                .HasForeignKey(gt => gt.ReceiverId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ==========================================
        // BOOST
        // ==========================================
        modelBuilder.Entity<Boost>(entity =>
        {
            entity.HasIndex(b => b.UserId);
            entity.HasIndex(b => b.IsActive);
            entity.HasIndex(b => b.EndTime);
            entity.HasIndex(b => b.BoostType);

            entity.HasOne(b => b.User)
                .WithMany(u => u.Boosts)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ==========================================
        // REPORT
        // ==========================================
        modelBuilder.Entity<Report>(entity =>
        {
            entity.HasIndex(r => r.ReporterId);
            entity.HasIndex(r => r.ReportedUserId);
            entity.HasIndex(r => r.Status);
            entity.HasIndex(r => r.ReportedAt);

            // Prevent duplicate report from same reporter on same user (pending)
            entity.HasIndex(r => new { r.ReporterId, r.ReportedUserId }).IsUnique();

            entity.HasOne(r => r.Reporter)
                .WithMany(u => u.Reports)
                .HasForeignKey(r => r.ReporterId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(r => r.ReportedUser)
                .WithMany()
                .HasForeignKey(r => r.ReportedUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ==========================================
        // USER BLOCK
        // ==========================================
        modelBuilder.Entity<UserBlock>(entity =>
        {
            entity.HasIndex(ub => new { ub.BlockerId, ub.BlockedUserId }).IsUnique();
            entity.HasIndex(ub => ub.BlockedAt);
        });

        // ==========================================
        // EVENT
        // ==========================================
        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.StartTime);
            entity.HasIndex(e => e.EndTime);
            entity.HasIndex(e => e.EventType);

            entity.HasMany(e => e.Participants)
                .WithOne(ep => ep.Event)
                .HasForeignKey(ep => ep.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ==========================================
        // EVENT PARTICIPANT
        // ==========================================
        modelBuilder.Entity<EventParticipant>(entity =>
        {
            // Prevent duplicate registration
            entity.HasIndex(ep => new { ep.EventId, ep.UserId }).IsUnique();

            entity.HasIndex(ep => ep.JoinedAt);

            entity.HasOne(ep => ep.User)
                .WithMany()
                .HasForeignKey(ep => ep.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ==========================================
        // AUDIT LOG
        // ==========================================
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(a => a.Timestamp);
            entity.HasIndex(a => a.UserId);
            entity.HasIndex(a => a.Action);
            entity.HasIndex(a => a.Entity);
            entity.HasIndex(a => new { a.UserId, a.Timestamp });

            entity.HasOne(a => a.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ==========================================
        // TRAFFIC LOG
        // ==========================================
        modelBuilder.Entity<TrafficLog>(entity =>
        {
            entity.HasIndex(t => t.Timestamp);
            entity.HasIndex(t => t.UserId);
            entity.HasIndex(t => t.SessionId);
            entity.HasIndex(t => t.PageUrl);
            entity.HasIndex(t => t.IpAddress);
            entity.HasIndex(t => new { t.Timestamp, t.PageUrl });
        });

        // ==========================================
        // CHAT SESSION (AI ChatBot)
        // ==========================================
        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.HasIndex(cs => cs.UserId);
            entity.HasIndex(cs => cs.IsActive);
            entity.HasIndex(cs => cs.CreatedAt);

            entity.HasOne(cs => cs.User)
                .WithMany()
                .HasForeignKey(cs => cs.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(cs => cs.Messages)
                .WithOne(cm => cm.Session)
                .HasForeignKey(cm => cm.ChatSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ==========================================
        // CHAT MESSAGE (AI ChatBot)
        // ==========================================
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasIndex(cm => cm.ChatSessionId);
            entity.HasIndex(cm => cm.CreatedAt);
            entity.HasIndex(cm => cm.Role);
            entity.HasIndex(cm => new { cm.ChatSessionId, cm.CreatedAt });
        });
    }
}
