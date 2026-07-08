using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PadelHub.Data;
using PadelHub.Models;

namespace PadelHub.Services;

public class SeedDataService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<SeedDataService> _logger;

    public SeedDataService(AppDbContext db, UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager, ILogger<SeedDataService> logger)
    {
        _db = db; _userManager = userManager; _roleManager = roleManager; _logger = logger;
    }

    public async Task SeedAsync()
    {
        _logger.LogInformation("Starting database seeding...");
        await SeedRolesAsync();
        await SeedUsersAsync();
        SeedClubs();
        SeedMembershipPackages();
        SeedBadges();
        SeedSystemConfigs();
        SeedTournaments();
        SeedSocialData();
        await _db.SaveChangesAsync();
        _logger.LogInformation("Database seeding completed!");
    }

    private async Task SeedRolesAsync()
    {
        string[] roles = { "Admin", "Operator", "Member", "Coach" };
        foreach (var role in roles)
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new IdentityRole(role));
    }

    private async Task SeedUsersAsync()
    {
        var admin = new ApplicationUser { UserName = "admin@padelhub.com", Email = "admin@padelhub.com", FullName = "Administrator PadelHub", MemberNumber = "ADM-001", PhoneNumber = "081234567890", CreatedAt = DateTime.UtcNow, IsActive = true, EmailConfirmed = true };
        await _userManager.CreateAsync(admin, "Admin@123"); await _userManager.AddToRoleAsync(admin, "Admin");

        var op = new ApplicationUser { UserName = "operator@padelhub.com", Email = "operator@padelhub.com", FullName = "Budi Operator", MemberNumber = "OPR-001", CreatedAt = DateTime.UtcNow, IsActive = true, EmailConfirmed = true };
        await _userManager.CreateAsync(op, "Operator@123"); await _userManager.AddToRoleAsync(op, "Operator");

        var coach = new ApplicationUser { UserName = "coach.andi@padelhub.com", Email = "coach.andi@padelhub.com", FullName = "Andi Pratama", MemberNumber = "COA-001", CreatedAt = DateTime.UtcNow, IsActive = true, EmailConfirmed = true };
        await _userManager.CreateAsync(coach, "Coach@123"); await _userManager.AddToRoleAsync(coach, "Coach");

        var members = new List<(ApplicationUser user, string level)>
        {
            (new ApplicationUser { UserName = "rina@padelhub.com", Email = "rina@padelhub.com", FullName = "Rina Wijaya", MemberNumber = "MEM-001", CreatedAt = DateTime.UtcNow, IsActive = true, EmailConfirmed = true }, "Advanced"),
            (new ApplicationUser { UserName = "doni@padelhub.com", Email = "doni@padelhub.com", FullName = "Doni Kusuma", MemberNumber = "MEM-002", CreatedAt = DateTime.UtcNow, IsActive = true, EmailConfirmed = true }, "Intermediate"),
            (new ApplicationUser { UserName = "sari@padelhub.com", Email = "sari@padelhub.com", FullName = "Sari Indah", MemberNumber = "MEM-003", CreatedAt = DateTime.UtcNow, IsActive = true, EmailConfirmed = true }, "Beginner"),
            (new ApplicationUser { UserName = "bambang@padelhub.com", Email = "bambang@padelhub.com", FullName = "Bambang Hartono", MemberNumber = "MEM-004", CreatedAt = DateTime.UtcNow, IsActive = true, EmailConfirmed = true }, "Advanced"),
            (new ApplicationUser { UserName = "dewi@padelhub.com", Email = "dewi@padelhub.com", FullName = "Dewi Lestari", MemberNumber = "MEM-005", CreatedAt = DateTime.UtcNow, IsActive = true, EmailConfirmed = true }, "Professional"),
        };

        foreach (var (member, level) in members)
        {
            await _userManager.CreateAsync(member, "Member@123");
            await _userManager.AddToRoleAsync(member, "Member");
            _db.PlayerProfiles.Add(new PlayerProfile
            {
                UserId = member.Id, Level = level, Ranking = Random.Shared.Next(1, 100),
                Rating = Random.Shared.Next(800, 2200), DominantHand = "Right",
                TotalMatches = Random.Shared.Next(0, 200), Wins = Random.Shared.Next(0, 100),
                Losses = Random.Shared.Next(0, 80), CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(30, 365))
            });
        }

        _db.Coaches.Add(new Coach
        {
            UserId = coach.Id, Bio = "Pelatih padel profesional 10+ tahun.", Specialization = "Teknik, Strategi",
            ExperienceYears = 10, HourlyRate = 250000, IsAvailable = true, CreatedAt = DateTime.UtcNow
        });
    }

    private void SeedClubs()
    {
        var clubs = new List<Club>
        {
            new Club { Name = "PadelHub Jakarta Pusat", Description = "Klub premium di pusat Jakarta.", Address = "Jl. Sudirman No. 100", City = "Jakarta", Country = "Indonesia", Phone = "021-555-0101", Email = "jakarta@padelhub.com", CreatedAt = DateTime.UtcNow.AddDays(-365) },
            new Club { Name = "PadelHub Bandung", Description = "Klub padel udara sejuk Bandung.", Address = "Jl. Dago No. 50", City = "Bandung", Country = "Indonesia", Phone = "022-555-0202", Email = "bandung@padelhub.com", CreatedAt = DateTime.UtcNow.AddDays(-200) },
            new Club { Name = "PadelHub Surabaya", Description = "Klub terbesar di Surabaya.", Address = "Jl. Mayjen Sungkono No. 80", City = "Surabaya", Country = "Indonesia", Phone = "031-555-0303", Email = "surabaya@padelhub.com", CreatedAt = DateTime.UtcNow.AddDays(-150) }
        };
        _db.Clubs.AddRange(clubs);
        _db.SaveChanges();

        foreach (var club in _db.Clubs.ToList())
        {
            int count = club.City == "Surabaya" ? 8 : 6;
            for (int i = 1; i <= count; i++)
                _db.Courts.Add(new Court { ClubId = club.Id, Name = "Lapangan " + i, SurfaceType = "Artificial Grass", Type = i <= count / 2 ? "Indoor" : "Outdoor", HasLighting = true, PricePerHour = 150000 + (i * 10000), PeakPricePerHour = 200000 + (i * 15000), IsActive = true, CreatedAt = DateTime.UtcNow });
        }

        _db.Facilities.AddRange(new List<Facility> {
            new Facility { ClubId = 1, Name = "Pro Shop", Description = "Toko perlengkapan" },
            new Facility { ClubId = 1, Name = "Cafe & Lounge", Description = "Area santai" },
            new Facility { ClubId = 2, Name = "Restaurant", Description = "Restoran" }
        });
        _db.SaveChanges();
    }

    private void SeedMembershipPackages()
    {
        _db.MembershipPackages.AddRange(new List<MembershipPackage>
        {
            new MembershipPackage { Name = "Basic Monthly", Type = "Monthly", Price = 500000, DiscountPercent = 5, DurationDays = 30, MaxReservationsPerMonth = 8, IsActive = true },
            new MembershipPackage { Name = "Premium Monthly", Type = "Monthly", Price = 900000, DiscountPercent = 10, DurationDays = 30, MaxReservationsPerMonth = 16, IsActive = true },
            new MembershipPackage { Name = "VIP Monthly", Type = "Monthly", Price = 1500000, DiscountPercent = 15, DurationDays = 30, MaxReservationsPerMonth = 30, IsActive = true },
            new MembershipPackage { Name = "Basic Yearly", Type = "Yearly", Price = 4800000, DiscountPercent = 20, DurationDays = 365, MaxReservationsPerMonth = 8, IsActive = true }
        });
    }

    private void SeedBadges()
    {
        _db.Badges.AddRange(new List<Badge> {
            new Badge { Name = "First Win", Description = "Menang pertama", Category = "Match" },
            new Badge { Name = "10 Wins", Description = "10 kemenangan", Category = "Match" },
            new Badge { Name = "Early Bird", Description = "Reservasi pagi", Category = "Reservation" },
            new Badge { Name = "Tournament Champion", Description = "Juara turnamen", Category = "Tournament" }
        });
    }

    private void SeedSystemConfigs()
    {
        _db.SystemConfigs.AddRange(new List<SystemConfig> {
            new SystemConfig { Key = "App.MaxReservationDaysAhead", Value = "30", Category = "Reservation" },
            new SystemConfig { Key = "App.EnableRegistration", Value = "true", Category = "General" }
        });
    }

    private void SeedTournaments()
    {
        _db.Tournaments.AddRange(new List<Tournament> {
            new Tournament { Name = "PadelHub Open 2024", ClubId = 1, Type = "Single", Format = "Knockout", Level = "Open", StartDate = DateTime.UtcNow.AddDays(30), EndDate = DateTime.UtcNow.AddDays(33), RegistrationDeadline = DateTime.UtcNow.AddDays(20), MaxParticipants = 64, EntryFee = 250000, PrizeMoney = 50000000, Status = "Registration" },
            new Tournament { Name = "Bandung Championship", ClubId = 2, Type = "Double", Format = "GroupStage", Level = "Intermediate", StartDate = DateTime.UtcNow.AddDays(14), EndDate = DateTime.UtcNow.AddDays(16), RegistrationDeadline = DateTime.UtcNow.AddDays(7), MaxParticipants = 32, EntryFee = 200000, Status = "Upcoming" }
        });
    }

    private void SeedSocialData()
    {
        var firstUserId = _db.Users.FirstOrDefault()?.Id ?? "";
        if (!string.IsNullOrEmpty(firstUserId))
        {
            _db.TimelinePosts.AddRange(new List<TimelinePost> {
                new TimelinePost { UserId = firstUserId, Content = "Baru selesai pertandingan seru! 🎾", PostType = "MatchResult", LikesCount = 12, CommentsCount = 3, CreatedAt = DateTime.UtcNow.AddHours(-2) },
                new TimelinePost { UserId = firstUserId, Content = "Rekomendasi raket padel intermediate?", PostType = "General", LikesCount = 8, CommentsCount = 5, CreatedAt = DateTime.UtcNow.AddHours(-5) }
            });
        }
    }
}
