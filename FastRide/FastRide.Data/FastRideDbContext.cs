using FastRide.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FastRide.Data;

/// <summary>
/// FastRide database context - manages all entity mappings and database operations.
/// Supports multiple database providers: SQLite, SQL Server, MySQL, PostgreSQL.
/// </summary>
public class FastRideDbContext : DbContext
{
    public FastRideDbContext(DbContextOptions<FastRideDbContext> options) 
        : base(options)
    {
    }

    // Entity tables
    public DbSet<User> Users => Set<User>();
    public DbSet<DriverProfile> DriverProfiles => Set<DriverProfile>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<TripStop> TripStops => Set<TripStop>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Promo> Promos => Set<Promo>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<FareConfig> FareConfigs => Set<FareConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // === User Configuration ===
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(256).IsRequired();
            entity.Property(u => u.PhoneNumber).HasMaxLength(20);
            entity.Property(u => u.PasswordHash).IsRequired();

            // Rider -> Orders relationship
            entity.HasMany(u => u.RiderOrders)
                  .WithOne(o => o.Rider)
                  .HasForeignKey(o => o.RiderId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // === DriverProfile Configuration ===
        modelBuilder.Entity<DriverProfile>(entity =>
        {
            entity.HasKey(dp => dp.Id);
            entity.HasOne(dp => dp.User)
                  .WithOne(u => u.DriverProfile)
                  .HasForeignKey<DriverProfile>(dp => dp.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.Property(dp => dp.LicenseNumber).HasMaxLength(50);
            entity.Property(dp => dp.VehicleType).HasMaxLength(50);
            entity.Property(dp => dp.VehiclePlate).HasMaxLength(20);
            entity.Property(dp => dp.Rating).HasDefaultValue(5.0);
        });

        // === Order Configuration ===
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.HasOne(o => o.Driver)
                  .WithMany()
                  .HasForeignKey(o => o.DriverId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.Property(o => o.PickupAddress).HasMaxLength(500);
            entity.Property(o => o.DropoffAddress).HasMaxLength(500);
            entity.Property(o => o.EstimatedFare).HasColumnType("decimal(18,2)");
            entity.Property(o => o.FinalFare).HasColumnType("decimal(18,2)");
            entity.HasIndex(o => o.Status);
            entity.HasIndex(o => o.CreatedAt);
        });

        // === TripStop Configuration ===
        modelBuilder.Entity<TripStop>(entity =>
        {
            entity.HasKey(ts => ts.Id);
            entity.HasOne(ts => ts.Order)
                  .WithMany(o => o.Stops)
                  .HasForeignKey(ts => ts.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.Property(ts => ts.Address).HasMaxLength(500);
        });

        // === Payment Configuration ===
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasOne(p => p.Order)
                  .WithMany()
                  .HasForeignKey(p => p.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.Property(p => p.Amount).HasColumnType("decimal(18,2)");
            entity.HasIndex(p => p.Status);
        });

        // === Promo Configuration ===
        modelBuilder.Entity<Promo>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.Code).IsUnique();
            entity.Property(p => p.Code).HasMaxLength(50);
            entity.Property(p => p.Value).HasColumnType("decimal(18,2)");
            entity.Property(p => p.MaxDiscount).HasColumnType("decimal(18,2)");
        });

        // === FareConfig Configuration ===
        modelBuilder.Entity<FareConfig>(entity =>
        {
            entity.HasKey(fc => fc.Id);
            entity.Property(fc => fc.BaseFare).HasColumnType("decimal(18,2)");
            entity.Property(fc => fc.CostPerKm).HasColumnType("decimal(18,2)");
            entity.Property(fc => fc.CostPerMinute).HasColumnType("decimal(18,2)");
            entity.Property(fc => fc.MinimumFare).HasColumnType("decimal(18,2)");
            entity.Property(fc => fc.SurgeMultiplier).HasColumnType("decimal(5,2)");
        });

        // === Review Configuration ===
        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasOne(r => r.Order)
                  .WithMany()
                  .HasForeignKey(r => r.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // === Seed default fare configurations ===
        modelBuilder.Entity<FareConfig>().HasData(
            new FareConfig 
            { 
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000001"),
                VehicleCategory = VehicleCategory.Economy, 
                BaseFare = 5000m, 
                CostPerKm = 3000m, 
                CostPerMinute = 500m,
                MinimumFare = 10000m 
            },
            new FareConfig 
            { 
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000002"),
                VehicleCategory = VehicleCategory.Comfort, 
                BaseFare = 7000m, 
                CostPerKm = 4000m, 
                CostPerMinute = 700m,
                MinimumFare = 15000m 
            },
            new FareConfig 
            { 
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000003"),
                VehicleCategory = VehicleCategory.Premium, 
                BaseFare = 10000m, 
                CostPerKm = 6000m, 
                CostPerMinute = 1000m,
                MinimumFare = 25000m 
            },
            new FareConfig 
            { 
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000004"),
                VehicleCategory = VehicleCategory.Bike, 
                BaseFare = 3000m, 
                CostPerKm = 2000m, 
                CostPerMinute = 300m,
                MinimumFare = 7000m 
            },
            new FareConfig 
            { 
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000005"),
                VehicleCategory = VehicleCategory.Electric, 
                BaseFare = 5000m, 
                CostPerKm = 3000m, 
                CostPerMinute = 500m,
                MinimumFare = 10000m 
            }
        );
    }
}
