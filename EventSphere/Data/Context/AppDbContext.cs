using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using EventSphere.Data.Models;

namespace EventSphere.Data.Context;

/// <summary>
/// Application Database Context - mendukung SQLite, SqlServer, MySQL, PostgreSQL
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Core
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventAttendee> EventAttendees => Set<EventAttendee>();
    
    // Vendor
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<VendorContract> VendorContracts => Set<VendorContract>();
    public DbSet<VendorReview> VendorReviews => Set<VendorReview>();
    public DbSet<VendorPortfolio> VendorPortfolios => Set<VendorPortfolio>();
    
    // Budget & Task
    public DbSet<BudgetItem> BudgetItems => Set<BudgetItem>();
    public DbSet<TaskItem> TaskItems => Set<TaskItem>();
    
    // Media & Docs
    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    
    // Seating
    public DbSet<TableArrangement> TableArrangements => Set<TableArrangement>();
    
    // Chat & Notification
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatSessionMember> ChatSessionMembers => Set<ChatSessionMember>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    
    // Community
    public DbSet<ForumPost> ForumPosts => Set<ForumPost>();
    public DbSet<ForumComment> ForumComments => Set<ForumComment>();
    public DbSet<LoyaltyPoint> LoyaltyPoints => Set<LoyaltyPoint>();
    
    // AI ChatBot
    public DbSet<ChatBotSession> ChatBotSessions => Set<ChatBotSession>();
    public DbSet<ChatBotMessage> ChatBotMessages => Set<ChatBotMessage>();
    
    // Invoices
    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // --- Event relationships ---
        builder.Entity<Event>()
            .HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Event>()
            .HasOne(e => e.Organizer)
            .WithMany()
            .HasForeignKey(e => e.OrganizerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Event>()
            .HasOne(e => e.Client)
            .WithMany()
            .HasForeignKey(e => e.ClientId)
            .OnDelete(DeleteBehavior.SetNull);

        // --- EventAttendee composite unique ---
        builder.Entity<EventAttendee>()
            .HasIndex(ea => new { ea.EventId, ea.UserId })
            .IsUnique();

        builder.Entity<EventAttendee>()
            .HasOne(ea => ea.Event)
            .WithMany(e => e.Attendees)
            .HasForeignKey(ea => ea.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<EventAttendee>()
            .HasOne(ea => ea.User)
            .WithMany(u => u.AttendeeEvents)
            .HasForeignKey(ea => ea.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<EventAttendee>()
            .HasOne(ea => ea.Table)
            .WithMany(t => t.Attendees)
            .HasForeignKey(ea => ea.TableId)
            .OnDelete(DeleteBehavior.SetNull);

        // --- VendorContract ---
        builder.Entity<VendorContract>()
            .HasOne(vc => vc.Event)
            .WithMany(e => e.VendorContracts)
            .HasForeignKey(vc => vc.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<VendorContract>()
            .HasOne(vc => vc.Vendor)
            .WithMany(v => v.Contracts)
            .HasForeignKey(vc => vc.VendorId)
            .OnDelete(DeleteBehavior.Restrict);

        // --- Invoice ---
        builder.Entity<Invoice>()
            .HasOne(i => i.Contract)
            .WithMany(vc => vc.Invoices)
            .HasForeignKey(i => i.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Invoice>()
            .HasOne(i => i.PaidBy)
            .WithMany(u => u.Invoices)
            .HasForeignKey(i => i.PaidById)
            .OnDelete(DeleteBehavior.SetNull);

        // --- Budget Items ---
        builder.Entity<BudgetItem>()
            .HasOne(bi => bi.Event)
            .WithMany(e => e.BudgetItems)
            .HasForeignKey(bi => bi.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- Task Items ---
        builder.Entity<TaskItem>()
            .HasOne(ti => ti.Event)
            .WithMany(e => e.TaskItems)
            .HasForeignKey(ti => ti.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TaskItem>()
            .HasOne(ti => ti.AssignedTo)
            .WithMany()
            .HasForeignKey(ti => ti.AssignedToId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<TaskItem>()
            .HasOne(ti => ti.CompletedBy)
            .WithMany()
            .HasForeignKey(ti => ti.CompletedById)
            .OnDelete(DeleteBehavior.SetNull);

        // --- Media ---
        builder.Entity<MediaItem>()
            .HasOne(m => m.Event)
            .WithMany(e => e.MediaItems)
            .HasForeignKey(m => m.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- Document ---
        builder.Entity<Document>()
            .HasOne(d => d.Event)
            .WithMany(e => e.Documents)
            .HasForeignKey(d => d.EventId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Document>()
            .HasOne(d => d.UploadedBy)
            .WithMany(u => u.UploadedDocuments)
            .HasForeignKey(d => d.UploadedById)
            .OnDelete(DeleteBehavior.SetNull);

        // --- Feedback ---
        builder.Entity<Feedback>()
            .HasOne(f => f.Event)
            .WithMany(e => e.Feedbacks)
            .HasForeignKey(f => f.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Feedback>()
            .HasOne(f => f.User)
            .WithMany(u => u.Feedbacks)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- Table Arrangement ---
        builder.Entity<TableArrangement>()
            .HasOne(t => t.Event)
            .WithMany(e => e.TableArrangements)
            .HasForeignKey(t => t.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- Chat Messages ---
        builder.Entity<ChatMessage>()
            .HasOne(cm => cm.Sender)
            .WithMany(u => u.ChatMessages)
            .HasForeignKey(cm => cm.SenderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ChatMessage>()
            .HasOne(cm => cm.Session)
            .WithMany(cs => cs.Messages)
            .HasForeignKey(cm => cm.ChatSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- Chat Session Members ---
        builder.Entity<ChatSessionMember>()
            .HasIndex(csm => new { csm.SessionId, csm.UserId })
            .IsUnique();

        // --- ChatBot ---
        builder.Entity<ChatBotMessage>()
            .HasOne(cbm => cbm.Session)
            .WithMany(cbs => cbs.Messages)
            .HasForeignKey(cbm => cbm.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- Forum ---
        builder.Entity<ForumComment>()
            .HasOne(fc => fc.Post)
            .WithMany(fp => fp.Comments)
            .HasForeignKey(fc => fc.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- Indexes for performance ---
        builder.Entity<Event>().HasIndex(e => e.EventDate);
        builder.Entity<Event>().HasIndex(e => e.Status);
        builder.Entity<Vendor>().HasIndex(v => v.Category);
        builder.Entity<TaskItem>().HasIndex(t => t.Status);
        builder.Entity<TaskItem>().HasIndex(t => t.DueDate);
        builder.Entity<Notification>().HasIndex(n => new { n.UserId, n.IsRead });
        builder.Entity<ChatMessage>().HasIndex(cm => cm.SentAt);
                    
        // --- Seed Data ---
        SeedData(builder);
    }

    private static void SeedData(ModelBuilder builder)
    {
        // Seed roles
        var adminRoleId = "role-admin-id-001";
        var organizerRoleId = "role-organizer-id-002";
        var clientRoleId = "role-client-id-003";
        var vendorRoleId = "role-vendor-id-004";
        var guestRoleId = "role-guest-id-005";
        var moderatorRoleId = "role-moderator-id-006";

        builder.Entity<IdentityRole>().HasData(
            new IdentityRole { Id = adminRoleId, Name = "Admin", NormalizedName = "ADMIN" },
            new IdentityRole { Id = organizerRoleId, Name = "Organizer", NormalizedName = "ORGANIZER" },
            new IdentityRole { Id = clientRoleId, Name = "Client", NormalizedName = "CLIENT" },
            new IdentityRole { Id = vendorRoleId, Name = "Vendor", NormalizedName = "VENDOR" },
            new IdentityRole { Id = guestRoleId, Name = "Guest", NormalizedName = "GUEST" },
            new IdentityRole { Id = moderatorRoleId, Name = "Moderator", NormalizedName = "MODERATOR" }
        );

        // Seed default admin user (password: Admin123!)
        var adminUserId = "user-admin-id-001";
        var hasher = new PasswordHasher<ApplicationUser>();
        var adminUser = new ApplicationUser
        {
            Id = adminUserId,
            UserName = "admin@eventsphere.com",
            NormalizedUserName = "ADMIN@EVENTSPHERE.COM",
            Email = "admin@eventsphere.com",
            NormalizedEmail = "ADMIN@EVENTSPHERE.COM",
            EmailConfirmed = true,
            FullName = "System Admin",
            Company = "EventSphere",
            Bio = "System Administrator",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        adminUser.PasswordHash = hasher.HashPassword(adminUser, "Admin123!");
        builder.Entity<ApplicationUser>().HasData(adminUser);

        builder.Entity<IdentityUserRole<string>>().HasData(
            new IdentityUserRole<string> { RoleId = adminRoleId, UserId = adminUserId }
        );
    }
}
