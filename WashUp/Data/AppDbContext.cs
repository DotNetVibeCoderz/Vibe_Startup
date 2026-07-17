using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WashUp.Models;

namespace WashUp.Data;

/// <summary>
/// Main database context for WashUp application.
/// Supports SQLite, PostgreSQL, and SQL Server.
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Core entities
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderStatusLog> OrderStatusLogs => Set<OrderStatusLog>();
    
    // Finance
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();
    public DbSet<TaxRecord> TaxRecords => Set<TaxRecord>();
    
    // Inventory
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    
    // Staff & Courier
    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<StaffSchedule> StaffSchedules => Set<StaffSchedule>();
    public DbSet<StaffPerformance> StaffPerformances => Set<StaffPerformance>();
    public DbSet<CourierAssignment> CourierAssignments => Set<CourierAssignment>();
    public DbSet<GpsTrackingLog> GpsTrackingLogs => Set<GpsTrackingLog>();
    
    // IoT
    public DbSet<IoTDevice> IoTDevices => Set<IoTDevice>();
    public DbSet<IoTSensorReading> IoTSensorReadings => Set<IoTSensorReading>();
    
    // Social
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<ComplaintAttachment> ComplaintAttachments => Set<ComplaintAttachment>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<LoyaltyPointTransaction> LoyaltyPointTransactions => Set<LoyaltyPointTransaction>();
    
    // Chat
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    
    // Misc
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<MarketplaceListing> MarketplaceListings => Set<MarketplaceListing>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // --- Order configuration ---
        builder.Entity<Order>(o =>
        {
            o.HasIndex(x => x.OrderNumber).IsUnique();
            o.HasIndex(x => x.Status);
            o.HasIndex(x => x.CreatedAt);
            o.HasOne(x => x.User).WithMany(u => u.Orders).HasForeignKey(x => x.UserId);
            o.HasOne(x => x.Branch).WithMany(b => b.Orders).HasForeignKey(x => x.BranchId);
            o.HasOne(x => x.CourierAssignment).WithOne(c => c.Order).HasForeignKey<CourierAssignment>(c => c.OrderId);
            o.HasOne(x => x.Invoice).WithOne(i => i.Order).HasForeignKey<Invoice>(i => i.OrderId);
            o.HasOne(x => x.Review).WithOne(r => r.Order).HasForeignKey<Review>(r => r.OrderId);
        });

        // --- Invoice ---
        builder.Entity<Invoice>(i =>
        {
            i.HasIndex(x => x.InvoiceNumber).IsUnique();
        });

        // --- OrderStatusLog ---
        builder.Entity<OrderStatusLog>(l =>
        {
            l.HasIndex(x => x.OrderId);
        });

        // --- Inventory ---
        builder.Entity<InventoryItem>(i =>
        {
            i.HasIndex(x => x.BranchId);
        });

        // --- Staff ---
        builder.Entity<StaffMember>(s =>
        {
            s.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        builder.Entity<StaffSchedule>(s =>
        {
            s.HasOne(x => x.StaffMember).WithMany(m => m.Schedules).HasForeignKey(x => x.StaffMemberId);
        });

        builder.Entity<StaffPerformance>(p =>
        {
            p.HasOne(x => x.StaffMember).WithMany(m => m.Performances).HasForeignKey(x => x.StaffMemberId);
        });

        // --- Courier ---
        builder.Entity<CourierAssignment>(c =>
        {
            c.HasOne(x => x.Courier).WithMany(s => s.CourierAssignments).HasForeignKey(x => x.StaffMemberId);
            c.HasMany(x => x.TrackingLogs).WithOne(t => t.CourierAssignment).HasForeignKey(t => t.CourierAssignmentId);
        });

        // --- IoT ---
        builder.Entity<IoTDevice>(d =>
        {
            d.HasMany(x => x.Readings).WithOne(r => r.Device).HasForeignKey(r => r.IoTDeviceId);
        });

        // --- Chat ---
        builder.Entity<ChatSession>(c =>
        {
            c.HasMany(x => x.Messages).WithOne(m => m.ChatSession).HasForeignKey(m => m.ChatSessionId);
            c.HasOne(x => x.User).WithMany(u => u.ChatSessions).HasForeignKey(x => x.UserId);
        });

        // --- Social ---
        builder.Entity<Complaint>(c =>
        {
            c.HasIndex(x => x.ComplaintNumber).IsUnique();
            c.HasMany(x => x.Attachments).WithOne(a => a.Complaint).HasForeignKey(a => a.ComplaintId);
        });

        builder.Entity<Subscription>(s =>
        {
            s.HasMany(x => x.PointTransactions).WithOne(p => p.Subscription).HasForeignKey(p => p.SubscriptionId);
        });

        // --- Identity role configuration ---
        var roles = new[]
        {
            new IdentityRole { Id = "role-owner", Name = "Pemilik", NormalizedName = "PEMILIK" },
            new IdentityRole { Id = "role-admin", Name = "Admin", NormalizedName = "ADMIN" },
            new IdentityRole { Id = "role-courier", Name = "Kurir", NormalizedName = "KURIR" },
            new IdentityRole { Id = "role-customer", Name = "Pelanggan", NormalizedName = "PELANGGAN" }
        };
        builder.Entity<IdentityRole>().HasData(roles);
    }
}
