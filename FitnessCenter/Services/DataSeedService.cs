using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FitnessCenter.Data;
using FitnessCenter.Models;

namespace FitnessCenter.Services;

/// <summary>
/// Service untuk seeding sample data ke database
/// </summary>
public class DataSeedService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public DataSeedService(AppDbContext db, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        _db = db; _userManager = userManager; _roleManager = roleManager;
    }

    public async Task SeedAsync()
    {
        // Cek apakah sudah ada data
        if (await _db.Users.AnyAsync()) return;

        // ---- Roles ----
        foreach (var role in Enum.GetNames<UserRole>())
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new IdentityRole(role));

        // ---- Admin ----
        var admin = new ApplicationUser
        {
            UserName = "admin@fitnesscenter.com",
            Email = "admin@fitnesscenter.com",
            FullName = "Admin Fitness Center",
            Role = UserRole.Admin,
            Gender = Gender.Male,
            PhoneNumber = "081234567890",
            Address = "Jl. Fitness No.1, Jakarta",
            RegisteredAt = DateTime.UtcNow.AddMonths(-12),
            IsActive = true,
            LoyaltyPoints = 1000,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(admin, "Admin123!");
        await _userManager.AddToRoleAsync(admin, "Admin");

        // ---- Staff ----
        var staffUsers = new List<(ApplicationUser, string)>
        {
            (new ApplicationUser { UserName="staff1@fitnesscenter.com", Email="staff1@fitnesscenter.com", FullName="Budi Santoso", Role=UserRole.Staff, Gender=Gender.Male, PhoneNumber="081234567891", Address="Jl. Merdeka No.10", RegisteredAt=DateTime.UtcNow.AddMonths(-10), LoyaltyPoints=200, EmailConfirmed=true }, "Staff123!"),
            (new ApplicationUser { UserName="staff2@fitnesscenter.com", Email="staff2@fitnesscenter.com", FullName="Ani Wijaya", Role=UserRole.Staff, Gender=Gender.Female, PhoneNumber="081234567892", Address="Jl. Sudirman No.20", RegisteredAt=DateTime.UtcNow.AddMonths(-8), LoyaltyPoints=150, EmailConfirmed=true }, "Staff123!"),
        };
        foreach (var (user, pw) in staffUsers) { await _userManager.CreateAsync(user, pw); await _userManager.AddToRoleAsync(user, "Staff"); }

        // ---- Trainers ----
        var trainerUsers = new List<(ApplicationUser, string, string, string)>
        {
            (new ApplicationUser { UserName="trainer1@fitnesscenter.com", Email="trainer1@fitnesscenter.com", FullName="Rizky Pratama", Role=UserRole.Trainer, Gender=Gender.Male, PhoneNumber="081234567893", RegisteredAt=DateTime.UtcNow.AddMonths(-9), LoyaltyPoints=500, EmailConfirmed=true }, "Trainer123!", "Strength & Conditioning", "Certified Personal Trainer with 8 years experience"),
            (new ApplicationUser { UserName="trainer2@fitnesscenter.com", Email="trainer2@fitnesscenter.com", FullName="Sari Dewi", Role=UserRole.Trainer, Gender=Gender.Female, PhoneNumber="081234567894", RegisteredAt=DateTime.UtcNow.AddMonths(-7), LoyaltyPoints=450, EmailConfirmed=true }, "Trainer123!", "Yoga & Pilates", "RYT-500 Certified Yoga Instructor"),
            (new ApplicationUser { UserName="trainer3@fitnesscenter.com", Email="trainer3@fitnesscenter.com", FullName="Andi Gunawan", Role=UserRole.Trainer, Gender=Gender.Male, PhoneNumber="081234567895", RegisteredAt=DateTime.UtcNow.AddMonths(-6), LoyaltyPoints=400, EmailConfirmed=true }, "Trainer123!", "HIIT & Cardio", "ACE Certified, 5 years experience"),
            (new ApplicationUser { UserName="trainer4@fitnesscenter.com", Email="trainer4@fitnesscenter.com", FullName="Maya Indah", Role=UserRole.Trainer, Gender=Gender.Female, PhoneNumber="081234567896", RegisteredAt=DateTime.UtcNow.AddMonths(-5), LoyaltyPoints=350, EmailConfirmed=true }, "Trainer123!", "Zumba & Dance", "ZIN Member, 6 years experience"),
        };
        foreach (var (user, pw, spec, bio) in trainerUsers)
        {
            await _userManager.CreateAsync(user, pw);
            await _userManager.AddToRoleAsync(user, "Trainer");
            _db.Trainers.Add(new Trainer
            {
                FullName = user.FullName, Specialization = spec, Bio = bio, Email = user.Email, Phone = user.PhoneNumber,
                UserId = user.Id, Rating = 4.0 + new Random().NextDouble() * 1.0, IsActive = true
            });
        }
        await _db.SaveChangesAsync();

        // ---- Members (20 sample members) ----
        var memberNames = new[] { "Dewi Lestari", "Rudi Hartono", "Fitriani", "Bayu Saputra", "Nina Marlina", "Hendra Gunawan",
            "Putri Ayu", "Dimas Ardian", "Rina Susanti", "Adi Nugroho", "Siska Wulandari", "Fajar Setiawan",
            "Lina Kurnia", "Agus Prayogo", "Dian Permata", "Eko Prasetyo", "Ratna Sari", "Indra Kusuma",
            "Mega Safitri", "Tono Wijoyo" };

        var rand = new Random(42);
        for (int i = 0; i < memberNames.Length; i++)
        {
            var member = new ApplicationUser
            {
                UserName = $"member{i + 1}@email.com",
                Email = $"member{i + 1}@email.com",
                FullName = memberNames[i],
                Role = UserRole.Member,
                Gender = i % 3 == 0 ? Gender.Female : Gender.Male,
                PhoneNumber = $"0812{rand.Next(1000, 9999)}{rand.Next(1000, 9999)}",
                Address = $"Jl. Anggrek No.{rand.Next(1, 100)}, Jakarta",
                RegisteredAt = DateTime.UtcNow.AddDays(-rand.Next(30, 365)),
                IsActive = true,
                LoyaltyPoints = rand.Next(50, 800),
                MembershipExpiryDate = DateTime.UtcNow.AddDays(rand.Next(-10, 60)),
                EmailConfirmed = true
            };
            await _userManager.CreateAsync(member, "Member123!");
            await _userManager.AddToRoleAsync(member, "Member");
        }
        await _db.SaveChangesAsync();

        // ---- Membership Plans ----
        var plans = new[]
        {
            new MembershipPlan { Name="Daily Pass", Description="Akses 1 hari penuh ke semua fasilitas", Duration=MembershipDuration.Daily, Price=75000, AllowAutoRenew=false, MaxClassesPerMonth=0, IsActive=true },
            new MembershipPlan { Name="Weekly Warrior", Description="Akses 7 hari berturut-turut", Duration=MembershipDuration.Weekly, Price=250000, AllowAutoRenew=false, MaxClassesPerMonth=8, IsActive=true },
            new MembershipPlan { Name="Monthly Basic", Description="Paket bulanan - akses gym & kolam renang", Duration=MembershipDuration.Monthly, Price=450000, AllowAutoRenew=true, MaxClassesPerMonth=12, IsActive=true },
            new MembershipPlan { Name="Monthly Pro", Description="Paket bulanan - semua fasilitas + kelas unlimited", Duration=MembershipDuration.Monthly, Price=750000, AllowAutoRenew=true, MaxClassesPerMonth=30, IncludesPersonalTrainer=true, IsActive=true },
            new MembershipPlan { Name="Quarterly Boost", Description="Paket 3 bulan - hemat 15%", Duration=MembershipDuration.Quarterly, Price=1200000, DiscountedPrice=1020000, AllowAutoRenew=true, MaxClassesPerMonth=30, IncludesPersonalTrainer=true, IsActive=true },
            new MembershipPlan { Name="Yearly Champion", Description="Paket 1 tahun - hemat 25% + nutrition plan", Duration=MembershipDuration.Yearly, Price=4800000, DiscountedPrice=3600000, AllowAutoRenew=true, MaxClassesPerMonth=30, IncludesPersonalTrainer=true, IncludesNutritionPlan=true, IsActive=true },
        };
        _db.MembershipPlans.AddRange(plans);
        await _db.SaveChangesAsync();

        // ---- Discounts ----
        _db.Discounts.AddRange(new[]
        {
            new Discount { Code="WELCOME10", Description="Diskon 10% untuk member baru", Type=DiscountType.Percentage, Value=10, MaxUses=100, ValidFrom=DateTime.UtcNow.AddMonths(-6), ValidUntil=DateTime.UtcNow.AddMonths(6) },
            new Discount { Code="SUMMER50", Description="Diskon 50K untuk paket bulanan", Type=DiscountType.FixedAmount, Value=50000, MinPurchase=400000, MaxUses=50, ValidFrom=DateTime.UtcNow, ValidUntil=DateTime.UtcNow.AddMonths(3) },
            new Discount { Code="REFERRAL25", Description="Bonus referral 25%", Type=DiscountType.Percentage, Value=25, MaxUses=200, ValidFrom=DateTime.UtcNow.AddMonths(-3), ValidUntil=DateTime.UtcNow.AddMonths(9) },
        });
        await _db.SaveChangesAsync();

        // ---- Nutrition Plans ----
        _db.NutritionPlans.AddRange(new[]
        {
            new NutritionPlan { Name="Weight Loss Plan", Description="Program diet untuk menurunkan berat badan", DailyCalories=1800, Goal="Weight Loss" },
            new NutritionPlan { Name="Muscle Building Plan", Description="Program diet untuk membangun otot", DailyCalories=2800, Goal="Muscle Gain" },
            new NutritionPlan { Name="Balanced Lifestyle", Description="Program diet seimbang untuk gaya hidup sehat", DailyCalories=2200, Goal="Maintenance" },
        });
        await _db.SaveChangesAsync();

        // ---- Fitness Classes ----
        var trainers = await _db.Trainers.ToListAsync();
        var classes = new[]
        {
            new FitnessClass { Name="Morning Yoga Flow", Description="Yoga untuk memulai hari dengan energi positif", Type=ClassType.Yoga, Level=ClassLevel.AllLevels, TrainerId=trainers[1].Id, MaxParticipants=25, Room="Studio 1", Duration=TimeSpan.FromMinutes(60), ImageUrl="/images/yoga.jpg" },
            new FitnessClass { Name="HIIT Blast", Description="High Intensity Interval Training untuk bakar kalori maksimal", Type=ClassType.HIIT, Level=ClassLevel.Advanced, TrainerId=trainers[2].Id, MaxParticipants=20, Room="Studio 2", Duration=TimeSpan.FromMinutes(45), ImageUrl="/images/hiit.jpg" },
            new FitnessClass { Name="Zumba Party", Description="Dance fitness yang seru dan energik!", Type=ClassType.Zumba, Level=ClassLevel.AllLevels, TrainerId=trainers[3].Id, MaxParticipants=30, Room="Aerobics Hall", Duration=TimeSpan.FromMinutes(60), ImageUrl="/images/zumba.jpg" },
            new FitnessClass { Name="Strength Training", Description="Latihan beban untuk membangun kekuatan", Type=ClassType.Strength, Level=ClassLevel.Intermediate, TrainerId=trainers[0].Id, MaxParticipants=15, Room="Gym Floor", Duration=TimeSpan.FromMinutes(60), ImageUrl="/images/strength.jpg" },
            new FitnessClass { Name="Pilates Core", Description="Memperkuat core dan postur tubuh", Type=ClassType.Pilates, Level=ClassLevel.Beginner, TrainerId=trainers[1].Id, MaxParticipants=20, Room="Studio 1", Duration=TimeSpan.FromMinutes(50), ImageUrl="/images/pilates.jpg" },
            new FitnessClass { Name="Boxing Circuit", Description="Boxing untuk fitness dan self-defense", Type=ClassType.Boxing, Level=ClassLevel.Intermediate, TrainerId=trainers[2].Id, MaxParticipants=15, Room="Boxing Ring", Duration=TimeSpan.FromMinutes(60), ImageUrl="/images/boxing.jpg" },
        };
        _db.FitnessClasses.AddRange(classes);
        await _db.SaveChangesAsync();

        // ---- Class Schedules ----
        var savedClasses = await _db.FitnessClasses.ToListAsync();
        foreach (var c in savedClasses)
        {
            _db.ClassSchedules.AddRange(new[]
            {
                new ClassSchedule { FitnessClassId=c.Id, DayOfWeek=DayOfWeek.Monday, StartTime=new TimeSpan(7,0,0), EndTime=new TimeSpan(8,0,0) },
                new ClassSchedule { FitnessClassId=c.Id, DayOfWeek=DayOfWeek.Wednesday, StartTime=new TimeSpan(7,0,0), EndTime=new TimeSpan(8,0,0) },
                new ClassSchedule { FitnessClassId=c.Id, DayOfWeek=DayOfWeek.Friday, StartTime=new TimeSpan(17,0,0), EndTime=new TimeSpan(18,0,0) },
            });
        }
        await _db.SaveChangesAsync();

        // ---- Forum Posts ----
        var members = await _db.Users.Where(u => u.Role == UserRole.Member).Take(10).ToListAsync();
        var forumPosts = new[]
        {
            new ForumPost { Title="Tips Menjaga Motivasi Olahraga", Content="Halo semuanya! Aku mau sharing tips nih... Gimana caranya kalian tetap semangat olahraga? Aku biasanya set target kecil dan tracking progress. Share tips kalian juga ya! 💪", UserId=members[0].Id, Likes=15, CreatedAt=DateTime.UtcNow.AddDays(-7) },
            new ForumPost { Title="Weekly Challenge: 100 Push-up Sehari!", Content="Challenge minggu ini: 100 push-up per hari selama 7 hari! Siapa yang mau ikut? Drop comment dan update progress kalian setiap hari! 🔥", UserId=members[1].Id, Likes=23, IsPinned=true, CreatedAt=DateTime.UtcNow.AddDays(-3) },
            new ForumPost { Title="Review: Kelas Zumba Party Seru Banget!", Content="Baru pertama kali ikut Zumba Party dan wow... seru banget! Trainernya energik, musiknya asik. Recommended buat yang mau olahraga sambil having fun!", UserId=members[2].Id, Likes=12, ImageUrl="/images/zumba_review.jpg", CreatedAt=DateTime.UtcNow.AddDays(-2) },
            new ForumPost { Title="Meal Prep untuk Seminggu", Content="Ini meal prep aku untuk seminggu ke depan. Budget 300k untuk 7 hari. Ada yang mau resepnya?", UserId=members[3].Id, Likes=18, CreatedAt=DateTime.UtcNow.AddDays(-5) },
            new ForumPost { Title="Progress 3 Bulan: -8kg!", Content="Akhirnya setelah 3 bulan konsisten, turun 8kg! Kuncinya: disiplin pola makan + olahraga rutin. Jangan nyerah ya teman-teman! 💪", UserId=members[4].Id, Likes=35, CreatedAt=DateTime.UtcNow.AddDays(-1) },
        };
        _db.ForumPosts.AddRange(forumPosts);
        await _db.SaveChangesAsync();

        // ---- Events ----
        _db.Events.AddRange(new[]
        {
            new Event { Title="Fitness Competition 2025", Content="<h2>Kompetisi Fitness Tahunan</h2><p>Ayo ikuti kompetisi fitness tahunan FitnessCenter! Ada kategori: <b>Body Transformation</b>, <b>Strength Challenge</b>, dan <b>Endurance Race</b>.</p><p>Hadiah total <b>Rp 50 Juta!</b></p>", Summary="Kompetisi fitness tahunan dengan hadiah 50 juta!", Status=EventStatus.Published, EventDate=DateTime.UtcNow.AddDays(30), Location="Main Hall FitnessCenter", MaxParticipants=200, ImageUrl="/images/competition.jpg", PublishedAt=DateTime.UtcNow.AddDays(-10), Likes=45 },
            new Event { Title="Workshop: Healthy Meal Planning", Content="<h2>Workshop Meal Planning</h2><p>Belajar cara menyusun meal plan sehat bersama ahli gizi profesional.</p><p>Fasilitas: <ul><li>Modul lengkap</li><li>Demo memasak</li><li>Konsultasi gizi gratis</li></ul></p>", Summary="Workshop menyusun menu sehat bersama ahli gizi", Status=EventStatus.Published, EventDate=DateTime.UtcNow.AddDays(14), Location="Seminar Room", MaxParticipants=50, PublishedAt=DateTime.UtcNow.AddDays(-5), Likes=28 },
            new Event { Title="Seminar: Mental Health & Fitness", Content="<h2>Kesehatan Mental dan Kebugaran</h2><p>Seminar tentang hubungan antara kesehatan mental dan kebugaran fisik. Pembicara: <b>Dr. Amanda Putri, M.Psi</b></p>", Summary="Seminar kesehatan mental bersama psikolog", Status=EventStatus.Published, EventDate=DateTime.UtcNow.AddDays(21), Location="Seminar Room", MaxParticipants=100, PublishedAt=DateTime.UtcNow.AddDays(-2), Likes=32 },
        });
        await _db.SaveChangesAsync();

        // ---- Configurations ----
        _db.AppConfigurations.AddRange(new[]
        {
            new AppConfiguration { Key="GymName", Value="FitnessCenter Premium", Description="Nama gym" },
            new AppConfiguration { Key="GymAddress", Value="Jl. Fitness No.1, Jakarta Pusat", Description="Alamat gym" },
            new AppConfiguration { Key="OperatingHours", Value="05:00-22:00", Description="Jam operasional" },
            new AppConfiguration { Key="MaxMembers", Value="500", Description="Kapasitas maksimum member" },
        });
        await _db.SaveChangesAsync();

        // ---- Sample attendances (last 14 days) ----
        var allUsers = await _db.Users.ToListAsync();
        var attendances = new List<Attendance>();
        for (int d = 0; d < 14; d++)
        {
            foreach (var user in allUsers.Take(rand.Next(5, 15)))
            {
                attendances.Add(new Attendance
                {
                    UserId = user.Id,
                    Type = AttendanceType.CheckIn,
                    Timestamp = DateTime.UtcNow.AddDays(-d).Date.AddHours(rand.Next(6, 20)).AddMinutes(rand.Next(0, 60))
                });
            }
        }
        _db.Attendances.AddRange(attendances);
        await _db.SaveChangesAsync();

        // ---- Sample payments ----
        var samplePayments = new List<Payment>();
        foreach (var member in members.Take(8))
        {
            samplePayments.Add(new Payment
            {
                UserId = member.Id, InvoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                Amount = rand.Next(250000, 750000), Method = PaymentMethod.EWallet,
                Status = PaymentStatus.Completed, Description = "Monthly Pro Membership",
                TransactionId = $"TXN-{Guid.NewGuid().ToString()[..8]}", PaidAt = DateTime.UtcNow.AddDays(-rand.Next(1, 90))
            });
        }
        _db.Payments.AddRange(samplePayments);
        await _db.SaveChangesAsync();
    }
}
