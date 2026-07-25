using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Ngibrid.Models;

namespace Ngibrid.Data;

/// <summary>
/// Multi-provider database context supporting SQLite, SQLServer, MySQL, and PostgreSQL
/// </summary>
public class NgibridDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, long, 
    IdentityUserClaim<long>, ApplicationUserRole, IdentityUserLogin<long>, 
    IdentityRoleClaim<long>, IdentityUserToken<long>>
{
    private readonly IConfiguration _configuration;

    public NgibridDbContext(DbContextOptions<NgibridDbContext> options, IConfiguration configuration) 
        : base(options)
    {
        _configuration = configuration;
    }

    // Master data
    public DbSet<City> Cities => Set<City>();

    // Order & Tracking
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<ShipmentTracking> ShipmentTrackings => Set<ShipmentTracking>();

    // Payment & Finance
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InsuranceClaim> InsuranceClaims => Set<InsuranceClaim>();

    // Warehouse & Inventory
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<WarehouseSection> WarehouseSections => Set<WarehouseSection>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    // Courier & Support
    public DbSet<CourierProfile> CourierProfiles => Set<CourierProfile>();
    public DbSet<DeliverySchedule> DeliverySchedules => Set<DeliverySchedule>();
    public DbSet<PickupRequest> PickupRequests => Set<PickupRequest>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportMessage> SupportMessages => Set<SupportMessage>();

    // Chat & System
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AnalyticsData> AnalyticsData => Set<AnalyticsData>();
    public DbSet<SystemConfiguration> SystemConfigurations => Set<SystemConfiguration>();
    public DbSet<Notification> Notifications => Set<Notification>();

    // Integrations & third-party logistics
    public DbSet<Integration> Integrations => Set<Integration>();
    public DbSet<IntegrationSyncLog> IntegrationSyncLogs => Set<IntegrationSyncLog>();
    public DbSet<LogisticsPartner> LogisticsPartners => Set<LogisticsPartner>();
    public DbSet<PartnerShipment> PartnerShipments => Set<PartnerShipment>();

    // IoT smart lockers
    public DbSet<SmartLocker> SmartLockers => Set<SmartLocker>();
    public DbSet<LockerCompartment> LockerCompartments => Set<LockerCompartment>();

    // Compliance, loyalty & forecasting
    public DbSet<TaxRecord> TaxRecords => Set<TaxRecord>();
    public DbSet<CustomsDeclaration> CustomsDeclarations => Set<CustomsDeclaration>();
    public DbSet<ComplianceDocument> ComplianceDocuments => Set<ComplianceDocument>();
    public DbSet<LoyaltyTransaction> LoyaltyTransactions => Set<LoyaltyTransaction>();
    public DbSet<DemandForecast> DemandForecasts => Set<DemandForecast>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Rename Identity tables
        builder.Entity<ApplicationUser>(e => e.ToTable("Users"));
        builder.Entity<ApplicationRole>(e => e.ToTable("Roles"));
        builder.Entity<ApplicationUserRole>(e => 
        {
            e.ToTable("UserRoles");
            e.HasKey(r => new { r.UserId, r.RoleId });
        });
        builder.Entity<IdentityUserClaim<long>>(e => e.ToTable("UserClaims"));
        builder.Entity<IdentityUserLogin<long>>(e => e.ToTable("UserLogins"));
        builder.Entity<IdentityRoleClaim<long>>(e => e.ToTable("RoleClaims"));
        builder.Entity<IdentityUserToken<long>>(e => e.ToTable("UserTokens"));

        // City master data. Unique on (Province, Type, Name) rather than name alone: 32 names are
        // carried by both a kota and a kabupaten (Bandung, Solok, Tasikmalaya, Sorong, …), and a
        // handful repeat across provinces.
        builder.Entity<City>(e =>
        {
            e.HasIndex(c => new { c.Province, c.Type, c.Name }).IsUnique();
            e.HasIndex(c => c.Name);
        });

        // Order configuration
        builder.Entity<Order>(e =>
        {
            e.HasIndex(o => o.OrderNumber).IsUnique();
            e.HasIndex(o => o.TrackingNumber).IsUnique();
            e.HasIndex(o => o.Status);
            e.HasIndex(o => o.CustomerId);
            e.HasOne(o => o.Customer)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(o => o.AssignedCourier)
                .WithMany()
                .HasForeignKey(o => o.AssignedCourierId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Payment configuration
        builder.Entity<Payment>(e =>
        {
            e.HasIndex(p => p.PaymentNumber).IsUnique();
            e.HasOne(p => p.Order)
                .WithOne(o => o.Payment)
                .HasForeignKey<Payment>(p => p.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Invoice configuration
        builder.Entity<Invoice>(e =>
        {
            e.HasIndex(i => i.InvoiceNumber).IsUnique();
            e.HasOne(i => i.Order)
                .WithOne(o => o.Invoice)
                .HasForeignKey<Invoice>(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Courier profile
        builder.Entity<CourierProfile>(e =>
        {
            e.HasIndex(c => c.CourierId).IsUnique();
            e.HasOne(c => c.User)
                .WithOne(u => u.CourierProfile)
                .HasForeignKey<CourierProfile>(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Delivery schedule
        builder.Entity<DeliverySchedule>(e =>
        {
            e.HasIndex(d => new { d.CourierProfileId, d.ScheduledDate });
            e.HasOne(d => d.CourierProfile)
                .WithMany(c => c.Schedules)
                .HasForeignKey(d => d.CourierProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Warehouse section
        builder.Entity<WarehouseSection>(e =>
        {
            e.HasOne(s => s.Warehouse)
                .WithMany(w => w.Sections)
                .HasForeignKey(s => s.WarehouseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Inventory
        builder.Entity<InventoryItem>(e =>
        {
            e.HasIndex(i => i.Sku);
            e.HasIndex(i => i.RfidTag);
            e.HasOne(i => i.Warehouse)
                .WithMany(w => w.InventoryItems)
                .HasForeignKey(i => i.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Chat session
        builder.Entity<ChatSession>(e =>
        {
            e.HasIndex(c => c.UserId);
            e.HasMany(c => c.Messages)
                .WithOne(m => m.Session)
                .HasForeignKey(m => m.ChatSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Support ticket
        builder.Entity<SupportTicket>(e =>
        {
            e.HasIndex(t => t.TicketNumber).IsUnique();
            e.HasMany(t => t.Messages)
                .WithOne(m => m.Ticket)
                .HasForeignKey(m => m.SupportTicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Pickup request
        builder.Entity<PickupRequest>(e =>
        {
            e.HasIndex(p => p.RequestNumber).IsUnique();
            e.HasOne(p => p.Customer)
                .WithMany(u => u.PickupRequests)
                .HasForeignKey(p => p.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // System configuration
        builder.Entity<SystemConfiguration>(e =>
        {
            e.HasIndex(c => c.Key).IsUnique();
        });

        // Audit log index
        builder.Entity<AuditLog>(e =>
        {
            e.HasIndex(a => new { a.EntityType, a.EntityId });
            e.HasIndex(a => a.CreatedAt);
        });

        // Analytics index
        builder.Entity<AnalyticsData>(e =>
        {
            e.HasIndex(a => new { a.MetricName, a.Period });
        });

        // Integrations
        builder.Entity<Integration>(e =>
        {
            e.HasIndex(i => new { i.Platform, i.ShopId });
            e.HasMany(i => i.SyncLogs)
                .WithOne(l => l.Integration)
                .HasForeignKey(l => l.IntegrationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Third-party logistics
        builder.Entity<LogisticsPartner>(e =>
        {
            e.HasIndex(p => p.Code).IsUnique();
            e.HasMany(p => p.Shipments)
                .WithOne(s => s.Partner)
                .HasForeignKey(s => s.LogisticsPartnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PartnerShipment>(e =>
        {
            e.HasIndex(s => s.HandoverNumber).IsUnique();
            e.HasIndex(s => s.PartnerTrackingNumber);
            e.HasOne(s => s.Order)
                .WithMany()
                .HasForeignKey(s => s.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Smart lockers
        builder.Entity<SmartLocker>(e =>
        {
            e.HasIndex(l => l.Code).IsUnique();
            e.HasMany(l => l.Compartments)
                .WithOne(c => c.Locker)
                .HasForeignKey(c => c.SmartLockerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<LockerCompartment>(e =>
        {
            e.HasIndex(c => new { c.SmartLockerId, c.CompartmentNumber }).IsUnique();
            e.HasOne(c => c.Order)
                .WithMany()
                .HasForeignKey(c => c.OrderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Compliance
        builder.Entity<TaxRecord>(e =>
        {
            e.HasIndex(t => t.TaxNumber).IsUnique();
            e.HasIndex(t => new { t.TaxType, t.Period });
            e.HasOne(t => t.Order)
                .WithMany()
                .HasForeignKey(t => t.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<CustomsDeclaration>(e =>
        {
            e.HasIndex(c => c.DeclarationNumber).IsUnique();
            e.HasOne(c => c.Order)
                .WithMany()
                .HasForeignKey(c => c.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(c => c.Documents)
                .WithOne(d => d.Declaration)
                .HasForeignKey(d => d.CustomsDeclarationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Loyalty
        builder.Entity<LoyaltyTransaction>(e =>
        {
            e.HasIndex(l => new { l.UserId, l.CreatedAt });
            e.HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.Order)
                .WithMany()
                .HasForeignKey(l => l.OrderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Forecasting
        builder.Entity<DemandForecast>(e =>
        {
            e.HasIndex(f => new { f.ForecastDate, f.City });
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        UpdateTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is BaseEntity && 
                (e.State == EntityState.Added || e.State == EntityState.Modified));

        foreach (var entry in entries)
        {
            var entity = (BaseEntity)entry.Entity;
            if (entry.State == EntityState.Added)
            {
                // Only stamp when the caller left it unset. Overwriting unconditionally flattened
                // every backdated row (seeded history, imported orders) to "now", which collapsed
                // the volume, trend, SLA, and forecast series to a single day.
                if (entity.CreatedAt == default) entity.CreatedAt = DateTime.UtcNow;
            }
            else
            {
                entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
