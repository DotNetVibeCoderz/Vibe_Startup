using Microsoft.EntityFrameworkCore;
using EstateHub.Models;

namespace EstateHub.Data;

/// <summary>
/// Main database context for EstateHub
/// Supports: SQLite (default), SQL Server, MySQL, PostgreSQL
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Core entities
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<Review> Reviews => Set<Review>();

    // Transactions
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Notification> Notifications => Set<Notification>();

    // AI & Marketing
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatHistory> ChatHistories => Set<ChatHistory>();
    public DbSet<KprSimulation> KprSimulations => Set<KprSimulation>();
    public DbSet<Advertisement> Advertisements => Set<Advertisement>();
    public DbSet<Lead> Leads => Set<Lead>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure relationships and indexes

        // User -> Properties (one user can have many listed properties)
        modelBuilder.Entity<Property>()
            .HasOne(p => p.Owner)
            .WithMany(u => u.ListedProperties)
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Booking relationships
        modelBuilder.Entity<Booking>()
            .HasOne(b => b.User)
            .WithMany(u => u.Bookings)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Booking>()
            .HasOne(b => b.Property)
            .WithMany(p => p.Bookings)
            .HasForeignKey(b => b.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        // Wishlist
        modelBuilder.Entity<WishlistItem>()
            .HasOne(w => w.User)
            .WithMany(u => u.Wishlist)
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WishlistItem>()
            .HasOne(w => w.Property)
            .WithMany(p => p.WishlistedBy)
            .HasForeignKey(w => w.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        // Review
        modelBuilder.Entity<Review>()
            .HasOne(r => r.User)
            .WithMany(u => u.Reviews)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Review>()
            .HasOne(r => r.Property)
            .WithMany(p => p.Reviews)
            .HasForeignKey(r => r.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        // Chat messages
        modelBuilder.Entity<ChatMessage>()
            .HasOne(c => c.Sender)
            .WithMany(u => u.SentMessages)
            .HasForeignKey(c => c.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ChatMessage>()
            .HasOne(c => c.Receiver)
            .WithMany(u => u.ReceivedMessages)
            .HasForeignKey(c => c.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        // Contract
        modelBuilder.Entity<Contract>()
            .HasOne(c => c.Buyer)
            .WithMany()
            .HasForeignKey(c => c.BuyerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Contract>()
            .HasOne(c => c.Seller)
            .WithMany()
            .HasForeignKey(c => c.SellerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Chat session -> history
        modelBuilder.Entity<ChatHistory>()
            .HasOne(h => h.Session)
            .WithMany(s => s.Messages)
            .HasForeignKey(h => h.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance
        modelBuilder.Entity<Property>()
            .HasIndex(p => new { p.PropertyType, p.ListingType, p.Status });

        modelBuilder.Entity<Property>()
            .HasIndex(p => p.Price);

        modelBuilder.Entity<Property>()
            .HasIndex(p => p.City);

        modelBuilder.Entity<Booking>()
            .HasIndex(b => b.ScheduledDate);

        modelBuilder.Entity<ChatMessage>()
            .HasIndex(c => new { c.SenderId, c.ReceiverId, c.SentAt });

        modelBuilder.Entity<Notification>()
            .HasIndex(n => new { n.UserId, n.IsRead });

        modelBuilder.Entity<Lead>()
            .HasIndex(l => new { l.AgentId, l.Status });

        // Seed sample data
        SeedData(modelBuilder);
    }

    /// <summary>
    /// Seed sample data for development
    /// </summary>
    private void SeedData(ModelBuilder modelBuilder)
    {
        // Sample users
        var adminId = "admin-001";
        var agentId = "agent-001";
        var buyerId = "buyer-001";
        var tenantId = "tenant-001";

        modelBuilder.Entity<ApplicationUser>().HasData(
            new ApplicationUser { Id = adminId, FullName = "Admin EstateHub", Role = "Admin", PhoneNumber = "081111111111", Address = "Jakarta Pusat", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new ApplicationUser { Id = agentId, FullName = "Budi Agen Properti", Role = "Agent", PhoneNumber = "082222222222", Address = "Jakarta Selatan", CreatedAt = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc) },
            new ApplicationUser { Id = buyerId, FullName = "Siti Pembeli", Role = "Buyer", PhoneNumber = "083333333333", PreferredLocation = "Jakarta", MinBudget = 500000000, MaxBudget = 2000000000, PreferredType = "House", CreatedAt = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc) },
            new ApplicationUser { Id = tenantId, FullName = "Rudi Penyewa", Role = "Tenant", PhoneNumber = "084444444444", PreferredLocation = "Bandung", MinBudget = 2000000, MaxBudget = 15000000, PreferredType = "Apartment", CreatedAt = new DateTime(2024, 2, 15, 0, 0, 0, DateTimeKind.Utc) }
        );

        // Sample properties
        modelBuilder.Entity<Property>().HasData(
            new Property
            {
                Id = 1, Title = "Rumah Minimalis Jakarta Selatan",
                Description = "Rumah minimalis 3 lantai dengan taman luas, lokasi strategis dekat pusat perbelanjaan dan akses tol. Cocok untuk keluarga.",
                PropertyType = "House", ListingType = "Sale", Status = "Available",
                Price = 1500000000, LandArea = 150, BuildingArea = 200,
                Bedrooms = 4, Bathrooms = 3, Floors = 2, YearBuilt = 2020,
                Facilities = "Carport,Garden,Security 24 Jam", NearbyFacilities = "Mall,Sekolah,Rumah Sakit",
                Address = "Jl. Fatmawati No. 123", City = "Jakarta Selatan", District = "Cilandak",
                Latitude = -6.2886, Longitude = 106.7960, ZipCode = "12430",
                OwnerId = agentId, IsVerified = true, ViewCount = 250,
                CreatedAt = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new Property
            {
                Id = 2, Title = "Apartemen Premium SCBD",
                Description = "Apartemen mewah 2BR dengan view kota, fully furnished, fasilitas lengkap: kolam renang, gym, sauna.",
                PropertyType = "Apartment", ListingType = "Rent", Status = "Available",
                Price = 15000000, LandArea = 0, BuildingArea = 80,
                Bedrooms = 2, Bathrooms = 1, Floors = 1, YearBuilt = 2019,
                Facilities = "Pool,Gym,Sauna,Parking Basement", NearbyFacilities = "Perkantoran,Restoran,Cafe",
                Address = "Jl. Jend. Sudirman Kav. 52-53", City = "Jakarta Pusat", District = "Tanah Abang",
                Latitude = -6.2254, Longitude = 106.8093, ZipCode = "12190",
                OwnerId = agentId, IsVerified = true, ViewCount = 180,
                CreatedAt = new DateTime(2024, 3, 5, 0, 0, 0, DateTimeKind.Utc)
            },
            new Property
            {
                Id = 3, Title = "Ruko Strategis Bandung",
                Description = "Ruko 2 lantai di pusat kota Bandung, cocok untuk bisnis retail atau cafe. Lalu lintas tinggi.",
                PropertyType = "ShopHouse", ListingType = "Sale", Status = "Available",
                Price = 2500000000, LandArea = 100, BuildingArea = 180,
                Bedrooms = 0, Bathrooms = 2, Floors = 2, YearBuilt = 2018,
                Facilities = "Parking,Toilet,Kamar Karyawan", NearbyFacilities = "Pasar,Terminal,Kuliner",
                Address = "Jl. Asia Afrika No. 45", City = "Bandung", District = "Sumur Bandung",
                Latitude = -6.9175, Longitude = 107.6191, ZipCode = "40111",
                OwnerId = agentId, IsVerified = true, ViewCount = 320,
                CreatedAt = new DateTime(2024, 3, 10, 0, 0, 0, DateTimeKind.Utc)
            },
            new Property
            {
                Id = 4, Title = "Villa Mewah Puncak",
                Description = "Villa 5 kamar dengan pemandangan gunung, udara sejuk, cocok untuk investasi atau tempat peristirahatan.",
                PropertyType = "Villa", ListingType = "Rent", Status = "Available",
                Price = 7500000, LandArea = 500, BuildingArea = 300,
                Bedrooms = 5, Bathrooms = 4, Floors = 1, YearBuilt = 2021,
                Facilities = "Kolam Renang Pribadi,Gazebo,BBQ Area,Parkir Luas", NearbyFacilities = "Wisata Alam,Taman Bunga",
                Address = "Jl. Raya Puncak Km. 87", City = "Bogor", District = "Cisarua",
                Latitude = -6.6788, Longitude = 106.9366, ZipCode = "16750",
                OwnerId = agentId, IsVerified = true, ViewCount = 410,
                CreatedAt = new DateTime(2024, 3, 15, 0, 0, 0, DateTimeKind.Utc)
            },
            new Property
            {
                Id = 5, Title = "Tanah Kavling Premium BSD",
                Description = "Tanah kavling siap bangun di BSD City, dekat dengan akses tol, sekolah internasional, dan pusat bisnis.",
                PropertyType = "Land", ListingType = "Sale", Status = "Available",
                Price = 800000000, LandArea = 300, BuildingArea = 0,
                Bedrooms = 0, Bathrooms = 0, Floors = 0,
                Facilities = "Akses Jalan Lebar,Listrik,Air PDAM", NearbyFacilities = "Sekolah Internasional,Mall,RS",
                Address = "BSD City Blok G No. 12", City = "Tangerang Selatan", District = "Serpong",
                Latitude = -6.3016, Longitude = 106.6647, ZipCode = "15310",
                OwnerId = agentId, IsVerified = true, ViewCount = 150,
                CreatedAt = new DateTime(2024, 3, 20, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        // Sample reviews
        modelBuilder.Entity<Review>().HasData(
            new Review { Id = 1, PropertyId = 1, UserId = buyerId, Rating = 5, Comment = "Rumah bagus, lokasi strategis, recommended!", CreatedAt = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Review { Id = 2, PropertyId = 2, UserId = tenantId, Rating = 4, Comment = "Apartemen nyaman dan fasilitas lengkap", CreatedAt = new DateTime(2024, 4, 5, 0, 0, 0, DateTimeKind.Utc) }
        );
    }
}
