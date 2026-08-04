using FastRide.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FastRide.Data;

/// <summary>
/// FastRide database context — entity mappings, indexes and the default fare table.
/// Supports SQLite, SQL Server, MySQL and PostgreSQL through the provider selected in configuration.
/// </summary>
public class FastRideDbContext : DbContext
{
    public FastRideDbContext(DbContextOptions<FastRideDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<DriverProfile> DriverProfiles => Set<DriverProfile>();
    public DbSet<DriverDocument> DriverDocuments => Set<DriverDocument>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<TripStop> TripStops => Set<TripStop>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Promo> Promos => Set<Promo>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<FareConfig> FareConfigs => Set<FareConfig>();
    public DbSet<PaymentProviderConfig> PaymentProviderConfigs => Set<PaymentProviderConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        const string Money = "decimal(18,2)";

        // === User ===
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.Role);
            entity.Property(u => u.FullName).HasMaxLength(200).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(256).IsRequired();
            entity.Property(u => u.PhoneNumber).HasMaxLength(20);
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.PhotoUrl).HasMaxLength(1000);
            entity.Property(u => u.ProfilePhotoMimeType).HasMaxLength(100);

            entity.HasMany(u => u.RiderOrders)
                  .WithOne(o => o.Rider)
                  .HasForeignKey(o => o.RiderId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // === DriverProfile ===
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
            entity.Property(dp => dp.TotalEarnings).HasColumnType(Money);

            // Matching queries filter on status first, then narrow by bounding box.
            entity.HasIndex(dp => dp.Status);
            entity.HasIndex(dp => new { dp.CurrentLatitude, dp.CurrentLongitude });
            entity.HasIndex(dp => dp.UserId).IsUnique();
        });

        // === DriverDocument ===
        modelBuilder.Entity<DriverDocument>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.HasOne(d => d.DriverProfile)
                  .WithMany(dp => dp.Documents)
                  .HasForeignKey(d => d.DriverProfileId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(d => d.FileUrl).HasMaxLength(1000);
            entity.Property(d => d.Notes).HasMaxLength(500);
            // One live document per type per driver — re-uploading replaces the row.
            entity.HasIndex(d => new { d.DriverProfileId, d.Type }).IsUnique();
        });

        // === Order ===
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.HasOne(o => o.Driver)
                  .WithMany()
                  .HasForeignKey(o => o.DriverId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.Property(o => o.Code).HasMaxLength(20).IsRequired();
            entity.Property(o => o.PickupAddress).HasMaxLength(500);
            entity.Property(o => o.DropoffAddress).HasMaxLength(500);
            entity.Property(o => o.CancellationReason).HasMaxLength(500);
            entity.Property(o => o.PromoCode).HasMaxLength(50);
            entity.Property(o => o.ReviewComment).HasMaxLength(1000);
            entity.Property(o => o.EstimatedFare).HasColumnType(Money);
            entity.Property(o => o.FinalFare).HasColumnType(Money);
            entity.Property(o => o.DiscountAmount).HasColumnType(Money);
            entity.Property(o => o.SurgeMultiplier).HasColumnType("decimal(5,2)");

            entity.Ignore(o => o.IsTerminal);

            entity.HasIndex(o => o.Code).IsUnique();
            entity.HasIndex(o => o.CreatedAt);
            // Dispatch board: "open orders, newest first".
            entity.HasIndex(o => new { o.Status, o.CreatedAt });
            // Driver history and earnings roll-ups.
            entity.HasIndex(o => new { o.DriverId, o.Status, o.CompletedAt });
            // Rider trip list.
            entity.HasIndex(o => new { o.RiderId, o.CreatedAt });
        });

        // === TripStop ===
        modelBuilder.Entity<TripStop>(entity =>
        {
            entity.HasKey(ts => ts.Id);
            entity.HasOne(ts => ts.Order)
                  .WithMany(o => o.Stops)
                  .HasForeignKey(ts => ts.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(ts => ts.Address).HasMaxLength(500);
            entity.HasIndex(ts => new { ts.OrderId, ts.SequenceNumber });
        });

        // === Payment ===
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasOne(p => p.Order)
                  .WithMany()
                  .HasForeignKey(p => p.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(p => p.Amount).HasColumnType(Money);
            entity.Property(p => p.DiscountAmount).HasColumnType(Money);
            entity.Property(p => p.TransactionReference).HasMaxLength(64);
            entity.Property(p => p.FailureReason).HasMaxLength(500);
            entity.Property(p => p.ProviderName).HasMaxLength(50);
            entity.Property(p => p.ProviderReference).HasMaxLength(128);

            // QRIS payloads run to a few hundred characters; redirect URLs can be longer.
            entity.Property(p => p.PaymentPayload).HasMaxLength(2000);

            entity.Ignore(p => p.IsSettled);
            entity.Ignore(p => p.IsInFlight);
            entity.Ignore(p => p.CanRetry);

            // An order is paid exactly once. This is what stops the driver's complete-order
            // call and POST /payments from both charging the same trip. A failed attempt is
            // retried by resetting this row, never by inserting a second one.
            entity.HasIndex(p => p.OrderId).IsUnique();

            // Callbacks arrive keyed by our reference, so the lookup must be indexed and
            // unique — two payments sharing one reference would make a callback ambiguous.
            entity.HasIndex(p => p.TransactionReference).IsUnique();

            entity.HasIndex(p => new { p.Status, p.CreatedAt });
        });

        // === PaymentProviderConfig ===
        modelBuilder.Entity<PaymentProviderConfig>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.Name).IsUnique();

            entity.Property(p => p.Name).HasMaxLength(50).IsRequired();
            entity.Property(p => p.DisplayName).HasMaxLength(100);
            entity.Property(p => p.SupportedMethods).HasMaxLength(200);
            entity.Property(p => p.ClientKey).HasMaxLength(500);
            entity.Property(p => p.ServerKey).HasMaxLength(500);
            entity.Property(p => p.WebhookSecret).HasMaxLength(500);
            entity.Property(p => p.BaseUrl).HasMaxLength(300);
            entity.Property(p => p.MerchantId).HasMaxLength(50);
            entity.Property(p => p.MerchantName).HasMaxLength(100);
            entity.Property(p => p.MerchantCity).HasMaxLength(50);
        });

        // === Promo ===
        modelBuilder.Entity<Promo>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.Code).IsUnique();
            entity.Property(p => p.Code).HasMaxLength(50).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(300);
            entity.Property(p => p.Value).HasColumnType(Money);
            entity.Property(p => p.MaxDiscount).HasColumnType(Money);
            entity.Property(p => p.MinOrderAmount).HasColumnType(Money);
        });

        // === FareConfig ===
        modelBuilder.Entity<FareConfig>(entity =>
        {
            entity.HasKey(fc => fc.Id);
            entity.HasIndex(fc => fc.VehicleCategory).IsUnique();
            entity.Property(fc => fc.BaseFare).HasColumnType(Money);
            entity.Property(fc => fc.CostPerKm).HasColumnType(Money);
            entity.Property(fc => fc.CostPerMinute).HasColumnType(Money);
            entity.Property(fc => fc.MinimumFare).HasColumnType(Money);
            entity.Property(fc => fc.CancellationFee).HasColumnType(Money);
            entity.Property(fc => fc.SurgeMultiplier).HasColumnType("decimal(5,2)");
        });

        // === Review ===
        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasOne(r => r.Order)
                  .WithMany()
                  .HasForeignKey(r => r.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(r => r.Comment).HasMaxLength(1000);
            // Average rating per driver is read on every driver list.
            entity.HasIndex(r => r.TargetUserId);
            // One review per reviewer per order.
            entity.HasIndex(r => new { r.OrderId, r.ReviewerId }).IsUnique();
        });

        // === Notification ===
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Title).HasMaxLength(200);
            entity.Property(n => n.Message).HasMaxLength(1000);
            entity.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
        });

        // === Default fare table ===
        modelBuilder.Entity<FareConfig>().HasData(
            NewFare("a1000000-0000-0000-0000-000000000001", VehicleCategory.Economy, 5000m, 3000m, 500m, 10000m, 5000m),
            NewFare("a1000000-0000-0000-0000-000000000002", VehicleCategory.Comfort, 7000m, 4000m, 700m, 15000m, 7000m),
            NewFare("a1000000-0000-0000-0000-000000000003", VehicleCategory.Premium, 10000m, 6000m, 1000m, 25000m, 10000m),
            NewFare("a1000000-0000-0000-0000-000000000004", VehicleCategory.Bike, 3000m, 2000m, 300m, 7000m, 2000m),
            NewFare("a1000000-0000-0000-0000-000000000005", VehicleCategory.Electric, 5000m, 3000m, 500m, 10000m, 5000m));
    }

    private static FareConfig NewFare(string id, VehicleCategory category,
        decimal baseFare, decimal perKm, decimal perMinute, decimal minimum, decimal cancellationFee) => new()
        {
            Id = Guid.Parse(id),
            VehicleCategory = category,
            BaseFare = baseFare,
            CostPerKm = perKm,
            CostPerMinute = perMinute,
            MinimumFare = minimum,
            CancellationFee = cancellationFee,
            SurgeMultiplier = 1.0m,
            IsActive = true
        };
}
