using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using EventSphere.Data.Context;
using EventSphere.Data.Models;

namespace EventSphere.Services;

/// <summary>
/// Seeds sample data untuk development & demo
/// </summary>
public class DataSeeder
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public DataSeeder(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task SeedAsync()
    {
        // Skip if already seeded
        if (await _db.Users.CountAsync() > 1) return;

        // ── Create Users ──
        var users = new Dictionary<string, (ApplicationUser user, string password, string role)>
        {
            ["organizer"] = (new ApplicationUser
            {
                UserName = "organizer@eventsphere.com",
                Email = "organizer@eventsphere.com",
                EmailConfirmed = true,
                FullName = "Budi Santoso",
                Company = "Santoso Events",
                Bio = "Professional event organizer dengan 10 tahun pengalaman",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            }, "Organizer123!", "Organizer"),

            ["client1"] = (new ApplicationUser
            {
                UserName = "rini@email.com",
                Email = "rini@email.com",
                EmailConfirmed = true,
                FullName = "Rini Wijaya",
                Bio = "Bride-to-be, excited untuk wedding bulan depan!",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            }, "Client123!", "Client"),

            ["client2"] = (new ApplicationUser
            {
                UserName = "andi@email.com",
                Email = "andi@email.com",
                EmailConfirmed = true,
                FullName = "Andi Pratama",
                Bio = "Corporate event planner",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            }, "Client123!", "Client"),

            ["vendor1"] = (new ApplicationUser
            {
                UserName = "catering@berkah.com",
                Email = "catering@berkah.com",
                EmailConfirmed = true,
                FullName = "Berkah Catering",
                Company = "Berkah Catering Nusantara",
                Bio = "Catering terbaik untuk wedding dan acara formal",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            }, "Vendor123!", "Vendor"),

            ["vendor2"] = (new ApplicationUser
            {
                UserName = "dekorasi@indah.com",
                Email = "dekorasi@indah.com",
                EmailConfirmed = true,
                FullName = "Indah Dekorasi",
                Company = "Indah Dekorasi & Florist",
                Bio = "Spesialis dekorasi pernikahan mewah",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            }, "Vendor123!", "Vendor"),

            ["vendor3"] = (new ApplicationUser
            {
                UserName = "foto@moment.com",
                Email = "foto@moment.com",
                EmailConfirmed = true,
                FullName = "Moment Photography",
                Company = "Moment Photography Studio",
                Bio = "Abadikan momen terindah Anda",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            }, "Vendor123!", "Vendor"),

            ["guest1"] = (new ApplicationUser
            {
                UserName = "sari@email.com",
                Email = "sari@email.com",
                EmailConfirmed = true,
                FullName = "Sari Dewi",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            }, "Guest123!", "Guest"),

            ["guest2"] = (new ApplicationUser
            {
                UserName = "bambang@email.com",
                Email = "bambang@email.com",
                EmailConfirmed = true,
                FullName = "Bambang Hermanto",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            }, "Guest123!", "Guest"),

            ["moderator"] = (new ApplicationUser
            {
                UserName = "moderator@eventsphere.com",
                Email = "moderator@eventsphere.com",
                EmailConfirmed = true,
                FullName = "Dian Permata",
                Bio = "Community moderator",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            }, "Moderator123!", "Moderator"),
        };

        foreach (var (_, (user, password, role)) in users)
        {
            await _userManager.CreateAsync(user, password);
            await _userManager.AddToRoleAsync(user, role);
            await Task.Delay(50); // Ensure unique timestamps
        }

        // ── Create Vendors ──
        var vendors = new List<Vendor>
        {
            new() { Name = "Berkah Catering Nusantara", Category = "Catering", Description = "Catering premium dengan menu Nusantara dan Internasional. Melayani wedding, corporate event, dan private party.", City = "Jakarta", Phone = "021-5551234", Email = "info@berkahcatering.com", Rating = 4.8m, ReviewCount = 156, PriceRange = "Premium", IsVerified = true, LogoUrl = "/images/vendors/catering1.png" },
            new() { Name = "Indah Dekorasi & Florist", Category = "Dekorasi", Description = "Spesialis dekorasi pernikahan dengan gaya modern, classic, dan rustic.", City = "Bandung", Phone = "022-5556789", Email = "hello@indahdekorasi.com", Rating = 4.7m, ReviewCount = 89, PriceRange = "Premium", IsVerified = true },
            new() { Name = "Moment Photography", Category = "Fotografi", Description = "Fotografi wedding & event dengan style candid dan cinematic.", City = "Surabaya", Phone = "031-5554321", Email = "info@momentphoto.com", Rating = 4.9m, ReviewCount = 234, PriceRange = "Luxury", IsVerified = true },
            new() { Name = "Harmony Music Band", Category = "Musik", Description = "Live music untuk wedding, party, dan corporate event. Genre: Jazz, Pop, Acoustic.", City = "Jakarta", Phone = "021-5559876", Email = "book@harmonymusic.com", Rating = 4.6m, ReviewCount = 67, PriceRange = "Standard", IsVerified = true },
            new() { Name = "Grand Ballroom Jakarta", Category = "Venue", Description = "Ballroom mewah kapasitas 500-2000 orang dengan fasilitas lengkap.", City = "Jakarta", Phone = "021-5550000", Email = "sales@grandballroom.com", Rating = 4.9m, ReviewCount = 312, PriceRange = "Luxury", IsVerified = true },
            new() { Name = "Sweet Memory Cake", Category = "Kue & Dessert", Description = "Wedding cake artistik dan dessert table yang memukau.", City = "Jakarta", Phone = "021-5552468", Email = "order@sweetmemory.com", Rating = 4.5m, ReviewCount = 45, PriceRange = "Standard", IsVerified = false },
            new() { Name = "Glamour Makeup Artist", Category = "Kecantikan", Description = "Makeup artist profesional untuk pengantin dan acara spesial.", City = "Bandung", Phone = "022-5551357", Email = "book@glamourmua.com", Rating = 4.7m, ReviewCount = 123, PriceRange = "Premium", IsVerified = true },
            new() { Name = "Elite Entertainment", Category = "Hiburan", Description = "MC, DJ, dan entertainment untuk berbagai jenis acara.", City = "Jakarta", Phone = "021-5553690", Email = "info@eliteentertainment.com", Rating = 4.4m, ReviewCount = 78, PriceRange = "Standard", IsVerified = true },
        };
        await _db.Vendors.AddRangeAsync(vendors);
        await _db.SaveChangesAsync();

        // ── Create Sample Events ──
        var event1 = new Event
        {
            Name = "Wedding Rini & Raka",
            Description = "Pernikahan intimate dengan tema garden party. Warna dominan dusty pink dan sage green.",
            EventDate = DateTime.UtcNow.AddDays(45),
            EndDate = DateTime.UtcNow.AddDays(45).AddHours(8),
            Location = "Grand Ballroom Jakarta, Jl. Sudirman No. 123",
            Theme = "Garden Party",
            PrimaryColor = "#D4A5A5",
            SecondaryColor = "#A8D8B9",
            Status = EventStatus.Confirmed,
            BudgetTotal = 150_000_000,
            BudgetSpent = 45_000_000,
            ExpectedGuests = 200,
            EventType = "Wedding",
            CreatedById = users["organizer"].user.Id,
            OrganizerId = users["organizer"].user.Id,
            ClientId = users["client1"].user.Id,
        };

        var event2 = new Event
        {
            Name = "Annual Corporate Gala 2025",
            Description = "Gala dinner tahunan perusahaan dengan tema 'Future Forward'. Acara mencakup award ceremony dan networking session.",
            EventDate = DateTime.UtcNow.AddDays(90),
            EndDate = DateTime.UtcNow.AddDays(90).AddHours(6),
            Location = "Jakarta Convention Center",
            Theme = "Future Forward",
            PrimaryColor = "#1A1A2E",
            SecondaryColor = "#E94560",
            Status = EventStatus.Planned,
            BudgetTotal = 500_000_000,
            BudgetSpent = 50_000_000,
            ExpectedGuests = 500,
            EventType = "Corporate",
            CreatedById = users["organizer"].user.Id,
            OrganizerId = users["organizer"].user.Id,
            ClientId = users["client2"].user.Id,
        };

        await _db.Events.AddRangeAsync(event1, event2);
        await _db.SaveChangesAsync();

        // ── Sample Budget Items ──
        var budgetItems = new List<BudgetItem>
        {
            new() { EventId = event1.Id, Name = "Venue Rental", Category = "Venue", EstimatedCost = 30_000_000, ActualCost = 30_000_000, IsPaid = true, SortOrder = 1 },
            new() { EventId = event1.Id, Name = "Catering (200 pax)", Category = "Catering", EstimatedCost = 40_000_000, ActualCost = 0, SortOrder = 2 },
            new() { EventId = event1.Id, Name = "Dekorasi", Category = "Dekorasi", EstimatedCost = 25_000_000, ActualCost = 15_000_000, IsPaid = true, SortOrder = 3 },
            new() { EventId = event1.Id, Name = "Fotografi & Videografi", Category = "Dokumentasi", EstimatedCost = 20_000_000, ActualCost = 0, SortOrder = 4 },
            new() { EventId = event1.Id, Name = "Music & Entertainment", Category = "Hiburan", EstimatedCost = 15_000_000, ActualCost = 0, SortOrder = 5 },
            new() { EventId = event1.Id, Name = "Wedding Cake", Category = "Kue", EstimatedCost = 5_000_000, ActualCost = 0, SortOrder = 6 },
            new() { EventId = event1.Id, Name = "Undangan & Souvenir", Category = "Lainnya", EstimatedCost = 10_000_000, ActualCost = 0, SortOrder = 7 },
            new() { EventId = event1.Id, Name = "Transportasi", Category = "Logistik", EstimatedCost = 5_000_000, ActualCost = 0, SortOrder = 8 },
        };
        await _db.BudgetItems.AddRangeAsync(budgetItems);

        // ── Sample Tasks ──
        var tasks = new List<TaskItem>
        {
            new() { EventId = event1.Id, Title = "Book venue & sign contract", Category = "Venue", Priority = TaskPriority.High, Status = TaskItemStatus.Done, DueDate = DateTime.UtcNow.AddDays(-30), CompletedAt = DateTime.UtcNow.AddDays(-32), AssignedToId = users["organizer"].user.Id, SortOrder = 1, Progress = 100 },
            new() { EventId = event1.Id, Title = "Food tasting session", Category = "Catering", Priority = TaskPriority.High, Status = TaskItemStatus.InProgress, DueDate = DateTime.UtcNow.AddDays(5), AssignedToId = users["organizer"].user.Id, SortOrder = 2, Progress = 50 },
            new() { EventId = event1.Id, Title = "Finalize guest list", Category = "Guest", Priority = TaskPriority.High, Status = TaskItemStatus.Todo, DueDate = DateTime.UtcNow.AddDays(14), AssignedToId = users["client1"].user.Id, SortOrder = 3, Progress = 0 },
            new() { EventId = event1.Id, Title = "Choose wedding dress", Category = "Personal", Priority = TaskPriority.Medium, Status = TaskItemStatus.InProgress, DueDate = DateTime.UtcNow.AddDays(10), AssignedToId = users["client1"].user.Id, SortOrder = 4, Progress = 60 },
            new() { EventId = event1.Id, Title = "Send digital invitations", Category = "Guest", Priority = TaskPriority.High, Status = TaskItemStatus.Todo, DueDate = DateTime.UtcNow.AddDays(20), AssignedToId = users["organizer"].user.Id, SortOrder = 5, Progress = 0 },
            new() { EventId = event1.Id, Title = "Decorate venue - floral arrangement", Category = "Dekorasi", Priority = TaskPriority.Medium, Status = TaskItemStatus.Todo, DueDate = DateTime.UtcNow.AddDays(40), AssignedToId = users["vendor2"].user.Id, SortOrder = 6, Progress = 0 },
            new() { EventId = event1.Id, Title = "Rehearsal dinner", Category = "Ceremony", Priority = TaskPriority.Medium, Status = TaskItemStatus.Todo, DueDate = DateTime.UtcNow.AddDays(43), SortOrder = 7, Progress = 0 },
        };
        await _db.TaskItems.AddRangeAsync(tasks);

        // ── Sample Attendees ──
        var attendees = new List<EventAttendee>
        {
            new() { EventId = event1.Id, UserId = users["guest1"].user.Id, Role = AttendeeRole.Family, RsvpStatus = RsvpStatus.Accepted, DietaryRestrictions = "Vegetarian", RsvpDate = DateTime.UtcNow.AddDays(-5) },
            new() { EventId = event1.Id, UserId = users["guest2"].user.Id, Role = AttendeeRole.Guest, RsvpStatus = RsvpStatus.Pending },
        };
        await _db.EventAttendees.AddRangeAsync(attendees);

        // ── Sample Vendor Contracts ──
        var contracts = new List<VendorContract>
        {
            new() { EventId = event1.Id, VendorId = vendors[0].Id, ContractTitle = "Catering Package Premium", Amount = 40_000_000, PaidAmount = 10_000_000, Status = ContractStatus.Signed, SignedDate = DateTime.UtcNow.AddDays(-20) },
            new() { EventId = event1.Id, VendorId = vendors[1].Id, ContractTitle = "Dekorasi Garden Party", Amount = 25_000_000, PaidAmount = 15_000_000, Status = ContractStatus.Active, SignedDate = DateTime.UtcNow.AddDays(-15) },
            new() { EventId = event1.Id, VendorId = vendors[2].Id, ContractTitle = "Foto & Video Wedding Package", Amount = 20_000_000, PaidAmount = 0, Status = ContractStatus.Sent },
        };
        await _db.VendorContracts.AddRangeAsync(contracts);
        await _db.SaveChangesAsync();

        // ── Sample Table Arrangements ──
        var tables = new List<TableArrangement>
        {
            new() { EventId = event1.Id, TableName = "VIP Table", Shape = "Rectangle", Capacity = 12, PositionX = 400, PositionY = 200, Color = "#FFD700", SortOrder = 1 },
            new() { EventId = event1.Id, TableName = "Table 1", Shape = "Round", Capacity = 8, PositionX = 200, PositionY = 400, SortOrder = 2 },
            new() { EventId = event1.Id, TableName = "Table 2", Shape = "Round", Capacity = 8, PositionX = 400, PositionY = 400, SortOrder = 3 },
            new() { EventId = event1.Id, TableName = "Table 3", Shape = "Round", Capacity = 8, PositionX = 600, PositionY = 400, SortOrder = 4 },
            new() { EventId = event1.Id, TableName = "Table 4", Shape = "Round", Capacity = 10, PositionX = 200, PositionY = 550, SortOrder = 5 },
            new() { EventId = event1.Id, TableName = "Table 5", Shape = "Round", Capacity = 10, PositionX = 400, PositionY = 550, SortOrder = 6 },
            new() { EventId = event1.Id, TableName = "Table 6", Shape = "Round", Capacity = 10, PositionX = 600, PositionY = 550, SortOrder = 7 },
        };
        await _db.TableArrangements.AddRangeAsync(tables);
        await _db.SaveChangesAsync();

        // ── Sample Forum Posts ──
        var posts = new List<ForumPost>
        {
            new() { Title = "Tips memilih vendor catering yang tepat", Content = "Hallo semuanya! Saya lagi bingung milih vendor catering buat wedding bulan depan. Ada yang punya tips atau rekomendasi? Budget sekitar 40jt untuk 200 tamu. Thanks!", AuthorId = users["client1"].user.Id, Category = "Tips", ViewCount = 120, LikeCount = 15 },
            new() { Title = "Review: Grand Ballroom Jakarta - Wedding Venue", Content = "Baru saja selesai wedding di Grand Ballroom Jakarta. Overall pengalaman luar biasa! Ballroomnya megah, pelayanannya top, dan makanan enak. Recommended!", AuthorId = users["guest2"].user.Id, Category = "Review", ViewCount = 89, LikeCount = 23 },
        };
        await _db.ForumPosts.AddRangeAsync(posts);
        await _db.SaveChangesAsync();

        // ── Sample Feedback ──
        var feedbacks = new List<Feedback>
        {
            new() { EventId = event1.Id, UserId = users["guest1"].user.Id, Rating = 5, Comment = "Venue-nya bagus banget, dekorasi cantik!", Category = "Overall" },
        };
        await _db.Feedbacks.AddRangeAsync(feedbacks);
        await _db.SaveChangesAsync();
    }
}
