using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using FuelStation.Models;

namespace FuelStation.Data;

/// <summary>
/// Main database context supporting multiple database providers
/// Configured via appsettings.json: SQLite (default), SQLServer, MySQL, PostgreSQL
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Master Data
    public DbSet<FuelStationLocation> FuelStations => Set<FuelStationLocation>();
    public DbSet<FuelProduct> FuelProducts => Set<FuelProduct>();
    public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();
    public DbSet<Tank> Tanks => Set<Tank>();
    public DbSet<TankReading> TankReadings => Set<TankReading>();
    public DbSet<FuelPump> FuelPumps => Set<FuelPump>();

    // Customer & Employee
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Shift> Shifts => Set<Shift>();

    // Transactions
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionDetail> TransactionDetails => Set<TransactionDetail>();

    // Marketplace
    public DbSet<NonFuelProduct> NonFuelProducts => Set<NonFuelProduct>();
    public DbSet<MarketplaceOrder> MarketplaceOrders => Set<MarketplaceOrder>();
    public DbSet<MarketplaceOrderItem> MarketplaceOrderItems => Set<MarketplaceOrderItem>();

    // Feedback & Notifications
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<Notification> Notifications => Set<Notification>();

    // Audit & Alerts
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<EmergencyAlert> EmergencyAlerts => Set<EmergencyAlert>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure Identity tables
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(e => e.FullName).HasMaxLength(200);
        });

        builder.Entity<ApplicationRole>(entity =>
        {
            entity.ToTable("Roles");
        });

        builder.Entity<IdentityUserRole<Guid>>(entity => entity.ToTable("UserRoles"));
        builder.Entity<IdentityUserClaim<Guid>>(entity => entity.ToTable("UserClaims"));
        builder.Entity<IdentityUserLogin<Guid>>(entity => entity.ToTable("UserLogins"));
        builder.Entity<IdentityRoleClaim<Guid>>(entity => entity.ToTable("RoleClaims"));
        builder.Entity<IdentityUserToken<Guid>>(entity => entity.ToTable("UserTokens"));

        // TransactionNumber unique index
        builder.Entity<Transaction>()
            .HasIndex(t => t.TransactionNumber)
            .IsUnique();

        builder.Entity<MarketplaceOrder>()
            .HasIndex(o => o.OrderNumber)
            .IsUnique();

        // Soft delete query filter
        builder.Entity<FuelStationLocation>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<FuelProduct>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Tank>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<FuelPump>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Customer>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Employee>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<Transaction>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<NonFuelProduct>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<MarketplaceOrder>().HasQueryFilter(e => !e.IsDeleted);

        // Relationships
        builder.Entity<Tank>()
            .HasOne(t => t.FuelStation)
            .WithMany(s => s.Tanks)
            .HasForeignKey(t => t.FuelStationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<FuelPump>()
            .HasOne(p => p.FuelStation)
            .WithMany(s => s.Pumps)
            .HasForeignKey(p => p.FuelStationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Transaction>()
            .HasOne(t => t.Customer)
            .WithMany(c => c.Transactions)
            .HasForeignKey(t => t.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

/// <summary>
/// Extended Application User with additional profile fields
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    [MaxLength(200)]
    public string? FullName { get; set; }

    public Guid? FuelStationId { get; set; }

    [MaxLength(50)]
    public string? EmployeeCode { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    [MaxLength(50)]
    public string? ThemePreference { get; set; } = "Light";
}

/// <summary>
/// Application Role
/// </summary>
public class ApplicationRole : IdentityRole<Guid>
{
    [MaxLength(200)]
    public string? Description { get; set; }
}
