using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RentalBoil.Models;

namespace RentalBoil.Data;

/// <summary>
/// Application DbContext dengan multi-database support
/// Default: SQLite, bisa switch ke SQLServer, MySQL, PostgreSQL via config
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Master Data
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehiclePhoto> VehiclePhotos => Set<VehiclePhoto>();
    public DbSet<VehicleAvailability> VehicleAvailabilities => Set<VehicleAvailability>();
    public DbSet<VehicleMaintenance> VehicleMaintenances => Set<VehicleMaintenance>();

    // Transaction Data
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();

    // Social & Interaction
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();

    // Marketing & Loyalty
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();

    // System
    public DbSet<Faq> Faqs => Set<Faq>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    // Chat Bot
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatHistory> ChatHistories => Set<ChatHistory>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ---- Vehicle ----
        builder.Entity<Vehicle>(entity =>
        {
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.PricePerDay);
            entity.HasIndex(e => e.IsAvailable);
            entity.HasIndex(e => e.Capacity);
            entity.HasIndex(e => e.Transmission);
            entity.HasIndex(e => e.OwnerId);
            entity.HasIndex(e => e.IsVerified);

            entity.Property(e => e.Description).HasColumnType("TEXT");
            entity.Property(e => e.Specifications).HasColumnType("TEXT");
        });

        // ---- Booking ----
        builder.Entity<Booking>(entity =>
        {
            entity.HasIndex(e => e.BookingNumber).IsUnique();
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.VehicleId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.PaymentStatus);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.StartDate);
        });

        // ---- Review ----
        builder.Entity<Review>(entity =>
        {
            entity.HasIndex(e => e.VehicleId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Rating);
        });

        // ---- ChatMessage ----
        builder.Entity<ChatMessage>(entity =>
        {
            entity.HasIndex(e => e.SenderId);
            entity.HasIndex(e => e.ReceiverId);
            entity.HasIndex(e => e.BookingId);
            entity.HasIndex(e => e.SentAt);
        });

        // ---- Notification ----
        builder.Entity<Notification>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.IsRead);
            entity.HasIndex(e => e.CreatedAt);
        });

        // ---- Promotion ----
        builder.Entity<Promotion>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.StartDate);
            entity.HasIndex(e => e.EndDate);
        });

        // ---- SystemSetting ----
        builder.Entity<SystemSetting>(entity =>
        {
            entity.HasIndex(e => e.Key).IsUnique();
        });

        // ---- ChatSession ----
        builder.Entity<ChatSession>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.IsActive);
        });

        // ---- LoyaltyTransaction ----
        builder.Entity<LoyaltyTransaction>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Relationship configs
        builder.Entity<Booking>()
            .HasOne(b => b.Customer)
            .WithMany(u => u.Bookings)
            .HasForeignKey(b => b.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Vehicle>()
            .HasOne(v => v.Owner)
            .WithMany(u => u.Vehicles)
            .HasForeignKey(v => v.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ChatMessage>()
            .HasOne(c => c.Sender)
            .WithMany()
            .HasForeignKey(c => c.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ChatMessage>()
            .HasOne(c => c.Receiver)
            .WithMany()
            .HasForeignKey(c => c.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
