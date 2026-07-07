using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using FitnessCenter.Models;

namespace FitnessCenter.Data;

/// <summary>
/// Main database context untuk FitnessCenter
/// Mendukung SQLite, SQLServer, MySQL, dan PostgreSQL
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Membership
    public DbSet<MembershipPlan> MembershipPlans => Set<MembershipPlan>();
    public DbSet<MemberMembership> MemberMemberships => Set<MemberMembership>();

    // Attendance
    public DbSet<Attendance> Attendances => Set<Attendance>();

    // Classes
    public DbSet<FitnessClass> FitnessClasses => Set<FitnessClass>();
    public DbSet<ClassSchedule> ClassSchedules => Set<ClassSchedule>();
    public DbSet<ClassBooking> ClassBookings => Set<ClassBooking>();

    // Trainer
    public DbSet<Trainer> Trainers => Set<Trainer>();

    // Payment
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Discount> Discounts => Set<Discount>();

    // Forum
    public DbSet<ForumPost> ForumPosts => Set<ForumPost>();
    public DbSet<ForumComment> ForumComments => Set<ForumComment>();
    public DbSet<ForumReaction> ForumReactions => Set<ForumReaction>();

    // Workout & Nutrition
    public DbSet<WorkoutLog> WorkoutLogs => Set<WorkoutLog>();
    public DbSet<NutritionPlan> NutritionPlans => Set<NutritionPlan>();
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();

    // Feedback
    public DbSet<Feedback> Feedbacks => Set<Feedback>();

    // Events
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventRegistration> EventRegistrations => Set<EventRegistration>();
    public DbSet<EventComment> EventComments => Set<EventComment>();

    // Gamification
    public DbSet<Achievement> Achievements => Set<Achievement>();

    // Notifications
    public DbSet<Notification> Notifications => Set<Notification>();

    // Chat
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    // Configuration
    public DbSet<AppConfiguration> AppConfigurations => Set<AppConfiguration>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ---- Membership ----
        builder.Entity<MemberMembership>()
            .HasOne(m => m.User)
            .WithMany(u => u.MemberMemberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MemberMembership>()
            .HasOne(m => m.MembershipPlan)
            .WithMany(p => p.MemberMemberships)
            .HasForeignKey(m => m.MembershipPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        // ---- Attendance ----
        builder.Entity<Attendance>()
            .HasOne(a => a.User)
            .WithMany(u => u.Attendances)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // ---- Classes ----
        builder.Entity<FitnessClass>()
            .HasOne(c => c.Trainer)
            .WithMany(t => t.Classes)
            .HasForeignKey(c => c.TrainerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ClassSchedule>()
            .HasOne(s => s.FitnessClass)
            .WithMany(c => c.Schedules)
            .HasForeignKey(s => s.FitnessClassId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ClassBooking>()
            .HasOne(b => b.Schedule)
            .WithMany()
            .HasForeignKey(b => b.ScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ClassBooking>()
            .HasOne(b => b.User)
            .WithMany(u => u.ClassBookings)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // ---- Forum ----
        builder.Entity<ForumPost>()
            .HasOne(p => p.User)
            .WithMany(u => u.ForumPosts)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ForumComment>()
            .HasOne(c => c.Post)
            .WithMany(p => p.Comments)
            .HasForeignKey(c => c.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ForumComment>()
            .HasOne(c => c.User)
            .WithMany(u => u.ForumComments)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ForumReaction>()
            .HasOne(r => r.Post)
            .WithMany(p => p.Reactions)
            .HasForeignKey(r => r.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ForumReaction>()
            .HasOne(r => r.Comment)
            .WithMany(c => c.Reactions)
            .HasForeignKey(r => r.CommentId)
            .OnDelete(DeleteBehavior.NoAction);

        // ---- Payment ----
        builder.Entity<Payment>()
            .HasOne(p => p.User)
            .WithMany(u => u.Payments)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Payment>()
            .HasOne(p => p.Discount)
            .WithMany(d => d.Payments)
            .HasForeignKey(p => p.DiscountId)
            .OnDelete(DeleteBehavior.SetNull);

        // ---- Events ----
        builder.Entity<EventRegistration>()
            .HasOne(r => r.Event)
            .WithMany(e => e.Registrations)
            .HasForeignKey(r => r.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<EventRegistration>()
            .HasOne(r => r.User)
            .WithMany(u => u.EventRegistrations)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<EventComment>()
            .HasOne(c => c.Event)
            .WithMany(e => e.Comments)
            .HasForeignKey(c => c.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // ---- Chat ----
        builder.Entity<ChatMessage>()
            .HasOne(m => m.Session)
            .WithMany(s => s.Messages)
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // ---- Indexes ----
        builder.Entity<Attendance>().HasIndex(a => new { a.UserId, a.Timestamp });
        builder.Entity<Payment>().HasIndex(p => p.InvoiceNumber).IsUnique();
        builder.Entity<Discount>().HasIndex(d => d.Code).IsUnique();
        builder.Entity<MemberMembership>().HasIndex(m => new { m.UserId, m.Status });
        builder.Entity<Notification>().HasIndex(n => new { n.UserId, n.IsRead });
        builder.Entity<ClassBooking>().HasIndex(b => new { b.ScheduleId, b.UserId }).IsUnique();
    }
}
