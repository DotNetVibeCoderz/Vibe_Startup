using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartDrive.Models.Entities;
using SmartDrive.Models.Enums;

namespace SmartDrive.Data;

/// <summary>
/// Database context utama untuk SmartDrive
/// Mendukung multiple database provider: SQLite, SQLServer, MySQL, PostgreSQL
/// </summary>
public class SmartDriveDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly IConfiguration _configuration;

    public SmartDriveDbContext(DbContextOptions<SmartDriveDbContext> options, IConfiguration configuration)
        : base(options)
    {
        _configuration = configuration;
    }

    // Master Data
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleServiceRecord> VehicleServiceRecords => Set<VehicleServiceRecord>();
    public DbSet<TrainingLocation> TrainingLocations => Set<TrainingLocation>();
    public DbSet<TheoryModule> TheoryModules => Set<TheoryModule>();
    public DbSet<ExamQuestion> ExamQuestions => Set<ExamQuestion>();

    // User Profiles
    public DbSet<InstructorProfile> InstructorProfiles => Set<InstructorProfile>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();

    // Booking & Schedule
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<InstructorSchedule> InstructorSchedules => Set<InstructorSchedule>();

    // Payment & Order
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<MarketplaceProduct> MarketplaceProducts => Set<MarketplaceProduct>();
    public DbSet<MarketplaceOrder> MarketplaceOrders => Set<MarketplaceOrder>();
    public DbSet<MarketplaceOrderItem> MarketplaceOrderItems => Set<MarketplaceOrderItem>();

    // Communication
    public DbSet<StudentFeedback> StudentFeedbacks => Set<StudentFeedback>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatBotSession> ChatBotSessions => Set<ChatBotSession>();
    public DbSet<ChatBotMessage> ChatBotMessages => Set<ChatBotMessage>();

    // Gamification
    public DbSet<StudentBadge> StudentBadges => Set<StudentBadge>();
    public DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();

    // Others
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<GpsTrackingData> GpsTrackingData => Set<GpsTrackingData>();
    public DbSet<InsurancePolicy> InsurancePolicies => Set<InsurancePolicy>();
    public DbSet<InsuranceClaim> InsuranceClaims => Set<InsuranceClaim>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ChatMessage relationships
        builder.Entity<ChatMessage>()
            .HasOne(c => c.Sender)
            .WithMany(u => u.SentMessages)
            .HasForeignKey(c => c.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ChatMessage>()
            .HasOne(c => c.Receiver)
            .WithMany(u => u.ReceivedMessages)
            .HasForeignKey(c => c.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        // Booking relationships
        builder.Entity<Booking>()
            .HasOne(b => b.Student)
            .WithMany(u => u.Bookings)
            .HasForeignKey(b => b.StudentUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Booking>()
            .HasOne(b => b.Instructor)
            .WithMany(i => i.AssignedBookings)
            .HasForeignKey(b => b.InstructorId)
            .OnDelete(DeleteBehavior.Restrict);

        // StudentProfile - Instructor relationship
        builder.Entity<StudentProfile>()
            .HasOne(s => s.AssignedInstructor)
            .WithMany()
            .HasForeignKey(s => s.AssignedInstructorId)
            .OnDelete(DeleteBehavior.SetNull);

        // Unique indexes
        builder.Entity<Vehicle>()
            .HasIndex(v => v.PlateNumber)
            .IsUnique();

        builder.Entity<TrainingLocation>()
            .HasIndex(l => l.Name);

        builder.Entity<SystemConfig>()
            .HasIndex(c => c.ConfigKey)
            .IsUnique();

        builder.Entity<MarketplaceOrder>()
            .HasIndex(o => o.OrderNumber)
            .IsUnique();

        // Composite indexes for performance
        builder.Entity<Booking>()
            .HasIndex(b => new { b.StartTime, b.Status });

        builder.Entity<GpsTrackingData>()
            .HasIndex(g => new { g.BookingId, g.Timestamp });

        builder.Entity<Notification>()
            .HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });

        builder.Entity<Payment>()
            .HasIndex(p => new { p.UserId, p.Status });

        // Decimal precision
        builder.Entity<Payment>()
            .Property(p => p.Amount)
            .HasPrecision(18, 2);

        builder.Entity<MarketplaceProduct>()
            .Property(p => p.Price)
            .HasPrecision(18, 2);

        builder.Entity<MarketplaceOrder>()
            .Property(o => o.TotalAmount)
            .HasPrecision(18, 2);

        builder.Entity<MarketplaceOrderItem>()
            .Property(i => i.UnitPrice)
            .HasPrecision(18, 2);

        builder.Entity<MarketplaceOrderItem>()
            .Property(i => i.SubTotal)
            .HasPrecision(18, 2);

        builder.Entity<InsurancePolicy>()
            .Property(i => i.CoverageAmount)
            .HasPrecision(18, 2);

        builder.Entity<InsurancePolicy>()
            .Property(i => i.Premium)
            .HasPrecision(18, 2);

        builder.Entity<InsuranceClaim>()
            .Property(c => c.ClaimAmount)
            .HasPrecision(18, 2);
    }
}
