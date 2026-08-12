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
            (new ApplicationUser { UserName="trainer5@fitnesscenter.com", Email="trainer5@fitnesscenter.com", FullName="Bima Prasetya", Role=UserRole.Trainer, Gender=Gender.Male, PhoneNumber="081234567897", RegisteredAt=DateTime.UtcNow.AddMonths(-4), LoyaltyPoints=300, EmailConfirmed=true }, "Trainer123!", "Boxing & Martial Arts", "Mantan atlet tinju amatir, pelatih bersertifikat sejak 2019"),
            (new ApplicationUser { UserName="trainer6@fitnesscenter.com", Email="trainer6@fitnesscenter.com", FullName="Kirana Puspita", Role=UserRole.Trainer, Gender=Gender.Female, PhoneNumber="081234567898", RegisteredAt=DateTime.UtcNow.AddMonths(-3), LoyaltyPoints=280, EmailConfirmed=true }, "Trainer123!", "Swimming & Aqua Fitness", "Pelatih renang lisensi nasional, fokus teknik dan pernapasan"),
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

        // ---- Members (40 sample members) ----
        var memberNames = new[] { "Dewi Lestari", "Rudi Hartono", "Fitriani", "Bayu Saputra", "Nina Marlina", "Hendra Gunawan",
            "Putri Ayu", "Dimas Ardian", "Rina Susanti", "Adi Nugroho", "Siska Wulandari", "Fajar Setiawan",
            "Lina Kurnia", "Agus Prayogo", "Dian Permata", "Eko Prasetyo", "Ratna Sari", "Indra Kusuma",
            "Mega Safitri", "Tono Wijoyo",
            "Yuni Astuti", "Bagas Ramadhan", "Citra Handayani", "Doni Firmansyah", "Elsa Novita", "Galih Pranata",
            "Hesti Rahayu", "Ilham Maulana", "Jasmine Aulia", "Krisna Wibowo", "Laras Ayuningtyas", "Miko Saputro",
            "Nadia Rahma", "Oki Darmawan", "Prita Anggraini", "Qori Hidayat", "Rendi Alfarizi", "Sinta Melati",
            "Teguh Santoso", "Vina Oktaviani" };

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
        // ImageUrl menunjuk ke sampul SVG di wwwroot/images/classes.
        var trainers = await _db.Trainers.ToListAsync();
        var classes = new[]
        {
            new FitnessClass { Name="Morning Yoga Flow", Description="Yoga untuk memulai hari dengan energi positif", Type=ClassType.Yoga, Level=ClassLevel.AllLevels, TrainerId=trainers[1].Id, MaxParticipants=25, Room="Studio 1", Duration=TimeSpan.FromMinutes(60), ImageUrl="/images/classes/yoga.svg" },
            new FitnessClass { Name="HIIT Blast", Description="Interval intensitas tinggi untuk bakar kalori maksimal", Type=ClassType.HIIT, Level=ClassLevel.Advanced, TrainerId=trainers[2].Id, MaxParticipants=20, Room="Studio 2", Duration=TimeSpan.FromMinutes(45), ImageUrl="/images/classes/hiit.svg" },
            new FitnessClass { Name="Zumba Party", Description="Dance fitness yang seru dan energik", Type=ClassType.Zumba, Level=ClassLevel.AllLevels, TrainerId=trainers[3].Id, MaxParticipants=30, Room="Aerobics Hall", Duration=TimeSpan.FromMinutes(60), ImageUrl="/images/classes/zumba.svg" },
            new FitnessClass { Name="Strength Training", Description="Latihan beban untuk membangun kekuatan dasar", Type=ClassType.Strength, Level=ClassLevel.Intermediate, TrainerId=trainers[0].Id, MaxParticipants=15, Room="Gym Floor", Duration=TimeSpan.FromMinutes(60), ImageUrl="/images/classes/strength.svg" },
            new FitnessClass { Name="Pilates Core", Description="Memperkuat core dan memperbaiki postur tubuh", Type=ClassType.Pilates, Level=ClassLevel.Beginner, TrainerId=trainers[1].Id, MaxParticipants=20, Room="Studio 1", Duration=TimeSpan.FromMinutes(50), ImageUrl="/images/classes/pilates.svg" },
            new FitnessClass { Name="Boxing Circuit", Description="Tinju untuk kebugaran sekaligus bela diri", Type=ClassType.Boxing, Level=ClassLevel.Intermediate, TrainerId=trainers[4].Id, MaxParticipants=15, Room="Boxing Ring", Duration=TimeSpan.FromMinutes(60), ImageUrl="/images/classes/boxing.svg" },
            new FitnessClass { Name="Spin Night Ride", Description="Bersepeda dalam ruangan dengan musik dan interval menanjak", Type=ClassType.Spinning, Level=ClassLevel.AllLevels, TrainerId=trainers[2].Id, MaxParticipants=24, Room="Cycle Studio", Duration=TimeSpan.FromMinutes(45), ImageUrl="/images/classes/spinning.svg" },
            new FitnessClass { Name="Aqua Fitness", Description="Latihan low impact di kolam, ramah untuk sendi", Type=ClassType.Swimming, Level=ClassLevel.Beginner, TrainerId=trainers[5].Id, MaxParticipants=18, Room="Kolam Renang", Duration=TimeSpan.FromMinutes(50), ImageUrl="/images/classes/swimming.svg" },
            new FitnessClass { Name="Step Aerobics", Description="Kardio bertahap dengan step board", Type=ClassType.Aerobics, Level=ClassLevel.AllLevels, TrainerId=trainers[3].Id, MaxParticipants=28, Room="Aerobics Hall", Duration=TimeSpan.FromMinutes(55), ImageUrl="/images/classes/aerobics.svg" },
            new FitnessClass { Name="Body Combat", Description="Gerakan bela diri campuran tanpa kontak fisik", Type=ClassType.MartialArts, Level=ClassLevel.Advanced, TrainerId=trainers[4].Id, MaxParticipants=20, Room="Studio 2", Duration=TimeSpan.FromMinutes(60), ImageUrl="/images/classes/martialarts.svg" },
            new FitnessClass { Name="Evening Meditation", Description="Latihan pernapasan dan relaksasi penutup hari", Type=ClassType.Meditation, Level=ClassLevel.AllLevels, TrainerId=trainers[1].Id, MaxParticipants=30, Room="Studio 3", Duration=TimeSpan.FromMinutes(40), ImageUrl="/images/classes/meditation.svg" },
            new FitnessClass { Name="Virtual Dance Cardio", Description="Kelas dansa daring lewat Zoom, bisa diikuti dari rumah", Type=ClassType.Dance, Level=ClassLevel.AllLevels, TrainerId=trainers[3].Id, MaxParticipants=50, Room="Online", Duration=TimeSpan.FromMinutes(45), IsVirtual=true, VirtualLink="https://zoom.us/j/000000000", ImageUrl="/images/classes/dance.svg" },
        };
        _db.FitnessClasses.AddRange(classes);
        await _db.SaveChangesAsync();

        // ---- Class Schedules ----
        // Tiap kelas dapat jadwal berbeda supaya papan jadwal terlihat wajar,
        // bukan enam kelas di jam yang sama.
        var savedClasses = await _db.FitnessClasses.OrderBy(c => c.Id).ToListAsync();
        var slotPlan = new (DayOfWeek[] Days, int Hour)[]
        {
            (new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday }, 6),
            (new[] { DayOfWeek.Tuesday, DayOfWeek.Thursday }, 7),
            (new[] { DayOfWeek.Monday, DayOfWeek.Thursday, DayOfWeek.Saturday }, 9),
            (new[] { DayOfWeek.Wednesday, DayOfWeek.Saturday }, 10),
            (new[] { DayOfWeek.Tuesday, DayOfWeek.Friday }, 16),
            (new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday }, 17),
            (new[] { DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Sunday }, 18),
            (new[] { DayOfWeek.Saturday, DayOfWeek.Sunday }, 8),
        };

        for (int i = 0; i < savedClasses.Count; i++)
        {
            var c = savedClasses[i];
            var (days, hour) = slotPlan[i % slotPlan.Length];
            var minutes = (int)c.Duration.TotalMinutes;

            foreach (var day in days)
            {
                _db.ClassSchedules.Add(new ClassSchedule
                {
                    FitnessClassId = c.Id,
                    DayOfWeek = day,
                    StartTime = new TimeSpan(hour, 0, 0),
                    EndTime = new TimeSpan(hour, 0, 0).Add(TimeSpan.FromMinutes(minutes)),
                    CurrentBookings = rand.Next(0, c.MaxParticipants + 1),
                    ValidFrom = DateTime.UtcNow.AddMonths(-3)
                });
            }
        }
        await _db.SaveChangesAsync();

        // ---- Membership aktif per member ----
        // Sebelumnya tidak ada MemberMembership sama sekali, sehingga paket yang
        // dibeli member tidak pernah terlihat di aplikasi.
        var allMembers = await _db.Users.Where(u => u.Role == UserRole.Member).OrderBy(u => u.RegisteredAt).ToListAsync();
        var savedPlans = await _db.MembershipPlans.OrderBy(p => p.Price).ToListAsync();

        foreach (var member in allMembers)
        {
            var plan = savedPlans[rand.Next(savedPlans.Count)];
            var months = plan.Duration switch
            {
                MembershipDuration.Daily => 1.0 / 30,
                MembershipDuration.Weekly => 0.25,
                MembershipDuration.Monthly => 1,
                MembershipDuration.Quarterly => 3,
                MembershipDuration.Yearly => 12,
                _ => 1
            };

            var start = DateTime.UtcNow.AddDays(-rand.Next(5, 200));
            var end = start.AddDays(months * 30);

            var status = end < DateTime.UtcNow
                ? (rand.Next(4) == 0 ? MembershipStatus.Suspended : MembershipStatus.Expired)
                : MembershipStatus.Active;

            _db.MemberMemberships.Add(new MemberMembership
            {
                UserId = member.Id,
                MembershipPlanId = plan.Id,
                StartDate = start,
                EndDate = end,
                Status = status,
                AutoRenew = plan.AllowAutoRenew && rand.Next(2) == 0,
                AmountPaid = plan.DiscountedPrice ?? plan.Price,
                CreatedAt = start
            });

            // Tanggal kedaluwarsa di profil disamakan dengan membership terbaru
            member.MembershipExpiryDate = end;
        }
        await _db.SaveChangesAsync();

        // ---- Booking kelas ----
        // Indeks unik (ScheduleId, UserId) dijaga lewat HashSet.
        var schedules = await _db.ClassSchedules.ToListAsync();
        var bookedPairs = new HashSet<(int, string)>();
        var bookings = new List<ClassBooking>();

        foreach (var schedule in schedules)
        {
            var seats = rand.Next(2, 9);
            for (int s = 0; s < seats; s++)
            {
                var member = allMembers[rand.Next(allMembers.Count)];
                if (!bookedPairs.Add((schedule.Id, member.Id))) continue;

                var bookedAt = DateTime.UtcNow.AddDays(-rand.Next(0, 40));
                bookings.Add(new ClassBooking
                {
                    ScheduleId = schedule.Id,
                    UserId = member.Id,
                    BookedAt = bookedAt,
                    IsAttended = bookedAt < DateTime.UtcNow.AddDays(-1) && rand.Next(10) < 8,
                    IsCancelled = rand.Next(12) == 0
                });
            }
        }
        _db.ClassBookings.AddRange(bookings);

        // Jumlah booking pada jadwal disamakan dengan data booking yang baru dibuat
        foreach (var schedule in schedules)
            schedule.CurrentBookings = bookings.Count(b => b.ScheduleId == schedule.Id && !b.IsCancelled);

        await _db.SaveChangesAsync();

        // ---- Forum ----
        var members = allMembers.Take(12).ToList();
        var forumPosts = new[]
        {
            new ForumPost { Title="Tips menjaga motivasi olahraga", Content="Halo semuanya! Aku mau sharing tips nih. Gimana caranya kalian tetap semangat olahraga? Aku biasanya set target kecil dan tracking progress. Share tips kalian juga ya! 💪", UserId=members[0].Id, Likes=15, CreatedAt=DateTime.UtcNow.AddDays(-21) },
            new ForumPost { Title="Weekly challenge: 100 push-up sehari", Content="Challenge minggu ini: 100 push-up per hari selama 7 hari! Siapa yang mau ikut? Drop comment dan update progress kalian setiap hari. 🔥", UserId=members[1].Id, Likes=41, IsPinned=true, CreatedAt=DateTime.UtcNow.AddDays(-3) },
            new ForumPost { Title="Review: kelas Zumba Party seru banget", Content="Baru pertama kali ikut Zumba Party dan wow, seru banget! Trainernya energik, musiknya asik. Recommended buat yang mau olahraga sambil having fun.", UserId=members[2].Id, Likes=12, ImageUrl="/images/classes/zumba.svg", CreatedAt=DateTime.UtcNow.AddDays(-2) },
            new ForumPost { Title="Meal prep untuk seminggu", Content="Ini meal prep aku untuk seminggu ke depan. Budget 300 ribu untuk 7 hari, isinya ayam, telur, tempe, dan sayur. Ada yang mau resepnya?", UserId=members[3].Id, Likes=18, CreatedAt=DateTime.UtcNow.AddDays(-5) },
            new ForumPost { Title="Progress 3 bulan: turun 8 kg", Content="Akhirnya setelah 3 bulan konsisten, turun 8 kg! Kuncinya disiplin pola makan dan olahraga rutin. Jangan menyerah ya teman-teman.", UserId=members[4].Id, Likes=35, CreatedAt=DateTime.UtcNow.AddDays(-1) },
            new ForumPost { Title="Rekomendasi sepatu lari untuk pemula", Content="Mau mulai rutin lari pagi. Ada rekomendasi sepatu yang nyaman tapi tidak terlalu mahal? Budget sekitar 700 ribu.", UserId=members[5].Id, Likes=9, CreatedAt=DateTime.UtcNow.AddDays(-9) },
            new ForumPost { Title="Cara mengatasi nyeri otot setelah HIIT", Content="Setiap habis kelas HIIT badan pegal 2 hari. Apakah normal? Ada tips recovery yang efektif selain istirahat?", UserId=members[6].Id, Likes=27, CreatedAt=DateTime.UtcNow.AddDays(-13) },
            new ForumPost { Title="Jadwal latihan untuk yang kerja kantoran", Content="Share jadwal latihanku yang muat di sela kerja 9-5: Senin dan Kamis pagi angkat beban, Selasa dan Jumat sore kardio, Sabtu kelas yoga.", UserId=members[7].Id, Likes=31, CreatedAt=DateTime.UtcNow.AddDays(-16) },
            new ForumPost { Title="Ada yang ikut kelas Aqua Fitness?", Content="Baru lihat ada kelas Aqua Fitness di jadwal. Ada yang sudah pernah coba? Kira-kira cocok untuk yang lututnya bermasalah?", UserId=members[8].Id, Likes=6, CreatedAt=DateTime.UtcNow.AddDays(-4) },
            new ForumPost { Title="Terima kasih Coach Rizky", Content="Mau bilang terima kasih ke Coach Rizky. Programnya jelas, korekasi formnya detail, dan selalu sabar. Deadlift aku naik 20 kg dalam 2 bulan.", UserId=members[9].Id, Likes=48, CreatedAt=DateTime.UtcNow.AddDays(-7) },
        };
        _db.ForumPosts.AddRange(forumPosts);
        await _db.SaveChangesAsync();

        var savedPosts = await _db.ForumPosts.OrderBy(p => p.Id).ToListAsync();
        var commentTexts = new[]
        {
            "Setuju banget! Aku juga mulai dari target kecil dulu.",
            "Ikut challenge-nya ya, hari pertama sudah selesai 💪",
            "Terima kasih sharingnya, sangat membantu.",
            "Boleh minta detail programnya?",
            "Wah keren, semangat terus!",
            "Aku juga pernah begitu, coba pemanasan lebih lama sebelum kelas.",
            "Jadwalnya realistis, aku mau coba tiru.",
            "Sudah pernah, enak banget buat sendi. Recommended.",
            "Coach-nya memang detail, aku juga belajar banyak.",
            "Ada yang tahu jam kelas ini yang paling sepi?"
        };

        var comments = new List<ForumComment>();
        foreach (var post in savedPosts)
        {
            var n = rand.Next(1, 5);
            for (int k = 0; k < n; k++)
            {
                comments.Add(new ForumComment
                {
                    PostId = post.Id,
                    UserId = members[rand.Next(members.Count)].Id,
                    Content = commentTexts[rand.Next(commentTexts.Length)],
                    Likes = rand.Next(0, 12),
                    CreatedAt = post.CreatedAt.AddHours(rand.Next(1, 60))
                });
            }
        }
        _db.ForumComments.AddRange(comments);
        await _db.SaveChangesAsync();

        // Reaksi: satu member maksimal satu reaksi per post
        var reactionTypes = new[] { "like", "love", "haha", "wow" };
        var reacted = new HashSet<(int, string)>();
        var reactions = new List<ForumReaction>();
        foreach (var post in savedPosts)
        {
            for (int k = 0; k < rand.Next(2, 9); k++)
            {
                var member = members[rand.Next(members.Count)];
                if (!reacted.Add((post.Id, member.Id))) continue;
                reactions.Add(new ForumReaction
                {
                    PostId = post.Id,
                    UserId = member.Id,
                    ReactionType = reactionTypes[rand.Next(reactionTypes.Length)],
                    CreatedAt = post.CreatedAt.AddHours(rand.Next(1, 72))
                });
            }
        }
        _db.ForumReactions.AddRange(reactions);
        await _db.SaveChangesAsync();

        // ---- Events ----
        _db.Events.AddRange(new[]
        {
            new Event { Title="Fitness Competition 2026", Content="<h2>Kompetisi Fitness Tahunan</h2><p>Ayo ikuti kompetisi fitness tahunan FitnessCenter! Ada kategori: <b>Body Transformation</b>, <b>Strength Challenge</b>, dan <b>Endurance Race</b>.</p><p>Hadiah total <b>Rp 50 juta</b>.</p>", Summary="Kompetisi fitness tahunan dengan hadiah 50 juta", Status=EventStatus.Published, EventDate=DateTime.UtcNow.AddDays(30), Location="Main Hall FitnessCenter", MaxParticipants=200, ImageUrl="/images/events/competition.svg", PublishedAt=DateTime.UtcNow.AddDays(-10), Likes=45 },
            new Event { Title="Workshop: Healthy Meal Planning", Content="<h2>Workshop Meal Planning</h2><p>Belajar cara menyusun meal plan sehat bersama ahli gizi profesional.</p><ul><li>Modul lengkap</li><li>Demo memasak</li><li>Konsultasi gizi gratis</li></ul>", Summary="Workshop menyusun menu sehat bersama ahli gizi", Status=EventStatus.Published, EventDate=DateTime.UtcNow.AddDays(14), Location="Seminar Room", MaxParticipants=50, ImageUrl="/images/events/workshop.svg", PublishedAt=DateTime.UtcNow.AddDays(-5), Likes=28 },
            new Event { Title="Seminar: Mental Health & Fitness", Content="<h2>Kesehatan Mental dan Kebugaran</h2><p>Seminar tentang hubungan antara kesehatan mental dan kebugaran fisik. Pembicara: <b>Dr. Amanda Putri, M.Psi</b>.</p>", Summary="Seminar kesehatan mental bersama psikolog", Status=EventStatus.Published, EventDate=DateTime.UtcNow.AddDays(21), Location="Seminar Room", MaxParticipants=100, ImageUrl="/images/events/seminar.svg", PublishedAt=DateTime.UtcNow.AddDays(-2), Likes=32 },
            new Event { Title="Charity Run 5K", Content="<h2>Lari Amal 5 Kilometer</h2><p>Seluruh biaya pendaftaran disalurkan ke program olahraga sekolah dasar di Jakarta Timur.</p><p>Rute mengelilingi kompleks gym, start pukul 06.00.</p>", Summary="Lari amal 5K, hasil pendaftaran untuk program olahraga sekolah", Status=EventStatus.Published, EventDate=DateTime.UtcNow.AddDays(45), Location="Halaman FitnessCenter", MaxParticipants=300, ImageUrl="/images/events/competition.svg", PublishedAt=DateTime.UtcNow.AddDays(-1), Likes=17 },
            new Event { Title="Open Gym Day", Content="<h2>Coba Semua Fasilitas, Gratis</h2><p>Satu hari penuh membuka seluruh fasilitas untuk umum. Bawa teman, coba kelas apa pun tanpa biaya.</p>", Summary="Satu hari akses gratis untuk umum", Status=EventStatus.Completed, EventDate=DateTime.UtcNow.AddDays(-20), Location="Seluruh area gym", MaxParticipants=250, ImageUrl="/images/events/workshop.svg", PublishedAt=DateTime.UtcNow.AddDays(-40), Likes=63 },
            new Event { Title="Workshop: Teknik Angkat Beban yang Aman", Content="<h2>Form Dulu, Beban Kemudian</h2><p>Sesi praktik memperbaiki teknik squat, deadlift, dan bench press bersama pelatih senior.</p>", Summary="Perbaiki teknik angkat beban bersama pelatih senior", Status=EventStatus.Draft, EventDate=DateTime.UtcNow.AddDays(60), Location="Gym Floor", MaxParticipants=30, ImageUrl="/images/events/seminar.svg", Likes=0 },
        });
        await _db.SaveChangesAsync();

        var publishedEvents = await _db.Events.Where(e => e.Status != EventStatus.Draft).ToListAsync();
        var eventRegs = new HashSet<(int, string)>();
        foreach (var ev in publishedEvents)
        {
            for (int k = 0; k < rand.Next(5, 20); k++)
            {
                var member = allMembers[rand.Next(allMembers.Count)];
                if (!eventRegs.Add((ev.Id, member.Id))) continue;
                _db.EventRegistrations.Add(new EventRegistration
                {
                    EventId = ev.Id,
                    UserId = member.Id,
                    RegisteredAt = DateTime.UtcNow.AddDays(-rand.Next(1, 30)),
                    IsAttended = ev.Status == EventStatus.Completed && rand.Next(10) < 7
                });
            }

            for (int k = 0; k < rand.Next(0, 4); k++)
            {
                _db.EventComments.Add(new EventComment
                {
                    EventId = ev.Id,
                    UserId = members[rand.Next(members.Count)].Id,
                    Content = new[] { "Sudah daftar, tidak sabar!", "Apakah pendaftaran masih dibuka?", "Acara tahun lalu bagus banget.", "Boleh ajak teman yang bukan member?" }[rand.Next(4)],
                    Likes = rand.Next(0, 9),
                    CreatedAt = DateTime.UtcNow.AddDays(-rand.Next(1, 12))
                });
            }
        }
        await _db.SaveChangesAsync();

        // ---- Configurations ----
        _db.AppConfigurations.AddRange(new[]
        {
            new AppConfiguration { Key="GymName", Value="FitnessCenter Premium", Description="Nama gym" },
            new AppConfiguration { Key="GymAddress", Value="Jl. Fitness No.1, Jakarta Pusat", Description="Alamat gym" },
            new AppConfiguration { Key="OperatingHours", Value="05:00-22:00", Description="Jam operasional" },
            new AppConfiguration { Key="MaxMembers", Value="500", Description="Kapasitas maksimum member" },
            new AppConfiguration { Key="FloorCapacity", Value="80", Description="Kapasitas orang di lantai gym pada satu waktu" },
            new AppConfiguration { Key="SupportEmail", Value="halo@fitnesscenter.com", Description="Email bantuan member" },
            new AppConfiguration { Key="SupportPhone", Value="021-5550100", Description="Telepon resepsionis" },
        });
        await _db.SaveChangesAsync();

        // ---- Absensi 45 hari terakhir ----
        // Akhir pekan sengaja lebih ramai, dan sebagian check-in punya check-out
        // agar grafik kunjungan tidak terlihat datar.
        var attendanceUsers = allMembers.Concat(await _db.Users.Where(u => u.Role == UserRole.Trainer).ToListAsync()).ToList();
        var attendances = new List<Attendance>();

        for (int d = 0; d < 45; d++)
        {
            var day = DateTime.UtcNow.Date.AddDays(-d);
            var isWeekend = day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var visitors = rand.Next(isWeekend ? 14 : 6, isWeekend ? 30 : 20);

            var seen = new HashSet<string>();
            for (int v = 0; v < visitors; v++)
            {
                var user = attendanceUsers[rand.Next(attendanceUsers.Count)];
                if (!seen.Add(user.Id)) continue;

                var checkIn = day.AddHours(rand.Next(5, 21)).AddMinutes(rand.Next(0, 60));
                attendances.Add(new Attendance { UserId = user.Id, Type = AttendanceType.CheckIn, Timestamp = checkIn, DeviceInfo = rand.Next(2) == 0 ? "QR Scanner Lobby" : "Kartu member" });

                if (rand.Next(10) < 7)
                    attendances.Add(new Attendance { UserId = user.Id, Type = AttendanceType.CheckOut, Timestamp = checkIn.AddMinutes(rand.Next(45, 150)) });
            }
        }
        _db.Attendances.AddRange(attendances);
        await _db.SaveChangesAsync();

        // ---- Catatan latihan ----
        var exercises = new (string Name, int Cal)[]
        {
            ("Bench Press", 180), ("Squat", 260), ("Deadlift", 300), ("Pull Up", 150),
            ("Lat Pulldown", 140), ("Leg Press", 220), ("Shoulder Press", 160),
            ("Treadmill 5K", 380), ("Rowing 2000m", 250), ("Plank", 90), ("Burpee", 210)
        };

        var workoutLogs = new List<WorkoutLog>();
        foreach (var member in allMembers.Take(25))
        {
            for (int k = 0; k < rand.Next(3, 14); k++)
            {
                var (name, cal) = exercises[rand.Next(exercises.Length)];
                var isCardio = name.Contains('K') || name.Contains("Rowing") || name == "Plank";

                workoutLogs.Add(new WorkoutLog
                {
                    UserId = member.Id,
                    ExerciseName = name,
                    Sets = isCardio ? 1 : rand.Next(3, 6),
                    Reps = isCardio ? 1 : rand.Next(6, 15),
                    Weight = isCardio ? null : rand.Next(20, 110),
                    DurationMinutes = rand.Next(15, 75),
                    CaloriesBurned = cal + rand.Next(-40, 60),
                    Notes = rand.Next(4) == 0 ? "Form terasa lebih stabil dari minggu lalu." : null,
                    LoggedAt = DateTime.UtcNow.AddDays(-rand.Next(0, 45)).AddHours(rand.Next(6, 21)),
                    DeviceSource = new[] { "Manual", "Fitbit", "Apple Watch" }[rand.Next(3)]
                });
            }
        }
        _db.WorkoutLogs.AddRange(workoutLogs);
        await _db.SaveChangesAsync();

        // ---- Meal plan ----
        var savedNutrition = await _db.NutritionPlans.ToListAsync();
        var meals = new (string Type, string Food, int Cal)[]
        {
            ("Breakfast", "Oatmeal + pisang + selai kacang", 420),
            ("Breakfast", "Telur dadar 2 butir + roti gandum", 380),
            ("Lunch", "Nasi merah + ayam panggang + brokoli", 620),
            ("Lunch", "Nasi + ikan kembung + tumis kangkung", 580),
            ("Dinner", "Sup ayam + kentang rebus", 450),
            ("Dinner", "Tahu tempe bacem + sayur asem", 400),
            ("Snack", "Greek yogurt + granola", 210),
            ("Snack", "Buah potong + almond", 180),
        };

        var mealPlans = new List<MealPlan>();
        foreach (var member in allMembers.Take(12))
        {
            var plan = savedNutrition[rand.Next(savedNutrition.Count)];
            for (int d = 0; d < 5; d++)
            {
                foreach (var slot in new[] { "Breakfast", "Lunch", "Dinner", "Snack" })
                {
                    var pick = meals.Where(m => m.Type == slot).ToArray()[rand.Next(meals.Count(m => m.Type == slot))];
                    mealPlans.Add(new MealPlan
                    {
                        UserId = member.Id,
                        NutritionPlanId = plan.Id,
                        Date = DateTime.UtcNow.Date.AddDays(-d),
                        MealType = slot,
                        FoodName = pick.Food,
                        Calories = pick.Cal
                    });
                }
            }
        }
        _db.MealPlans.AddRange(mealPlans);
        await _db.SaveChangesAsync();

        // ---- Feedback ----
        var savedTrainers = await _db.Trainers.ToListAsync();
        var feedbackComments = new[]
        {
            "Penjelasannya jelas dan sabar mengoreksi gerakan.",
            "Kelasnya seru, tapi ruangan agak panas di jam sore.",
            "Fasilitas bersih, loker cukup, air minum selalu tersedia.",
            "Alat kardio kadang antre di jam pulang kantor.",
            "Trainer selalu tepat waktu dan menyiapkan alat lebih dulu.",
            "Musik di studio agak terlalu keras menurut saya.",
            "Secara umum puas, akan perpanjang membership.",
        };

        var feedbacks = new List<Feedback>();
        foreach (var member in allMembers.Take(22))
        {
            var count = rand.Next(1, 4);
            for (int k = 0; k < count; k++)
            {
                var type = (FeedbackType)rand.Next(4);
                feedbacks.Add(new Feedback
                {
                    UserId = member.Id,
                    Type = type,
                    ReferenceId = type switch
                    {
                        FeedbackType.Trainer => savedTrainers[rand.Next(savedTrainers.Count)].Id,
                        FeedbackType.Class => savedClasses[rand.Next(savedClasses.Count)].Id,
                        _ => null
                    },
                    Rating = rand.Next(10) < 7 ? rand.Next(4, 6) : rand.Next(2, 4),
                    Comment = feedbackComments[rand.Next(feedbackComments.Length)],
                    CreatedAt = DateTime.UtcNow.AddDays(-rand.Next(1, 60))
                });
            }
        }
        _db.Feedbacks.AddRange(feedbacks);
        await _db.SaveChangesAsync();

        // ---- Lencana ----
        // Nama dan poin mengikuti Gamification:Badges di appsettings.json.
        var badges = new (string Name, string Desc, AchievementCategory Cat, int Points)[]
        {
            ("First Steps", "Check-in pertama di FitnessCenter", AchievementCategory.Attendance, 50),
            ("Dedicated", "10 kali check-in berturut-turut", AchievementCategory.Streak, 100),
            ("Class Master", "Mengikuti 50 kelas", AchievementCategory.Class, 200),
            ("Iron Body", "100 workout log dicatat", AchievementCategory.Workout, 500),
            ("Social Butterfly", "50 post di forum komunitas", AchievementCategory.Social, 150),
            ("Early Bird", "20 check-in sebelum pukul 07.00", AchievementCategory.Special, 120),
        };

        var achievements = new List<Achievement>();
        foreach (var member in allMembers.Take(30))
        {
            foreach (var badge in badges.Take(rand.Next(1, badges.Length + 1)))
            {
                achievements.Add(new Achievement
                {
                    UserId = member.Id,
                    Name = badge.Name,
                    Description = badge.Desc,
                    Category = badge.Cat,
                    Points = badge.Points,
                    EarnedAt = DateTime.UtcNow.AddDays(-rand.Next(1, 180))
                });
            }
        }
        _db.Achievements.AddRange(achievements);
        await _db.SaveChangesAsync();

        // ---- Pembayaran dengan berbagai status ----
        // Sebelumnya semua tagihan berstatus Completed, sehingga alur bayar,
        // verifikasi, dan tagihan gagal tidak pernah terlihat.
        var payments = new List<Payment>();
        var invoiceSeq = 1;

        foreach (var member in allMembers)
        {
            var membership = await _db.MemberMemberships
                .Include(m => m.MembershipPlan)
                .FirstOrDefaultAsync(m => m.UserId == member.Id);

            var amount = membership?.AmountPaid ?? 450000m;
            var planName = membership?.MembershipPlan?.Name ?? "Monthly Basic";

            // Riwayat 1–3 bulan ke belakang, seluruhnya sudah lunas
            var history = rand.Next(1, 4);
            for (int h = history; h >= 1; h--)
            {
                var created = DateTime.UtcNow.AddMonths(-h);
                payments.Add(new Payment
                {
                    UserId = member.Id,
                    InvoiceNumber = $"INV-{created:yyyyMM}-{invoiceSeq++:D5}",
                    Amount = amount,
                    Method = (PaymentMethod)rand.Next(6),
                    Status = PaymentStatus.Completed,
                    Description = $"Membership {created:MMMM yyyy} — {planName}",
                    TransactionId = $"TXN-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                    Gateway = (PaymentGatewayProvider)rand.Next(3),
                    GatewayStatus = "settlement",
                    PaymentChannel = new[] { "gopay", "bca_va", "qris", "credit_card", "transfer" }[rand.Next(5)],
                    CreatedAt = created,
                    PaidAt = created.AddDays(rand.Next(1, 6)),
                    LastSyncedAt = created.AddDays(rand.Next(1, 6))
                });
            }

            // Tagihan bulan berjalan, statusnya dibuat beragam
            var roll = rand.Next(10);
            var status = roll switch
            {
                < 4 => PaymentStatus.Pending,
                < 6 => PaymentStatus.Confirmed,
                < 8 => PaymentStatus.Completed,
                8 => PaymentStatus.Failed,
                _ => PaymentStatus.Cancelled
            };

            var now = DateTime.UtcNow;
            payments.Add(new Payment
            {
                UserId = member.Id,
                InvoiceNumber = $"INV-{now:yyyyMM}-{invoiceSeq++:D5}",
                Amount = amount,
                Method = status == PaymentStatus.Pending ? PaymentMethod.BankTransfer : (PaymentMethod)rand.Next(6),
                Status = status,
                Description = $"Membership {now:MMMM yyyy} — {planName}",
                TransactionId = status is PaymentStatus.Completed or PaymentStatus.Confirmed
                    ? $"TXN-{Guid.NewGuid().ToString()[..8].ToUpper()}" : null,
                Gateway = status == PaymentStatus.Pending ? PaymentGatewayProvider.Manual : (PaymentGatewayProvider)rand.Next(3),
                GatewayStatus = status switch
                {
                    PaymentStatus.Completed => "settlement",
                    PaymentStatus.Failed => "deny",
                    PaymentStatus.Cancelled => "expire",
                    _ => "pending"
                },
                CreatedAt = now.AddDays(-rand.Next(1, 12)),
                PaidAt = status == PaymentStatus.Completed ? now.AddDays(-rand.Next(0, 5)) : null
            });
        }

        // Satu tagihan yang dikembalikan, supaya status Refunded juga terwakili
        payments[0].Status = PaymentStatus.Refunded;
        payments[0].GatewayStatus = "refund";

        _db.Payments.AddRange(payments);
        await _db.SaveChangesAsync();

        // ---- Notifikasi ----
        var notifications = new List<Notification>();
        foreach (var member in allMembers.Take(20))
        {
            notifications.Add(new Notification
            {
                UserId = member.Id,
                Title = "Kelas besok pagi",
                Message = "Morning Yoga Flow pukul 06.00 di Studio 1. Datang 10 menit lebih awal ya.",
                Type = NotificationType.ClassReminder,
                ActionUrl = "/classes",
                IsRead = rand.Next(2) == 0,
                CreatedAt = DateTime.UtcNow.AddDays(-rand.Next(0, 5))
            });

            if (rand.Next(2) == 0)
            {
                notifications.Add(new Notification
                {
                    UserId = member.Id,
                    Title = "Tagihan menunggu pembayaran",
                    Message = "Membership bulan ini belum dibayar. Buka halaman Payments untuk menyelesaikannya.",
                    Type = NotificationType.PaymentReminder,
                    ActionUrl = "/payments",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow.AddDays(-rand.Next(0, 3))
                });
            }

            if (rand.Next(3) == 0)
            {
                notifications.Add(new Notification
                {
                    UserId = member.Id,
                    Title = "Promo perpanjangan",
                    Message = "Perpanjang paket tahunan bulan ini dan hemat 25 persen.",
                    Type = NotificationType.Promotion,
                    ActionUrl = "/memberships",
                    IsRead = rand.Next(2) == 0,
                    CreatedAt = DateTime.UtcNow.AddDays(-rand.Next(1, 14))
                });
            }
        }
        _db.Notifications.AddRange(notifications);
        await _db.SaveChangesAsync();

        // ---- Contoh percakapan dengan Coach Tommy ----
        var chatMember = allMembers[0];
        var session = new ChatSession
        {
            UserId = chatMember.Id,
            Title = "Program latihan untuk pemula",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            LastActivity = DateTime.UtcNow.AddDays(-2)
        };
        _db.ChatSessions.Add(session);
        await _db.SaveChangesAsync();

        _db.ChatMessages.AddRange(new[]
        {
            new ChatMessage { SessionId=session.Id, Role="assistant", Content="Halo! Saya Coach Tommy, asisten virtual kamu di FitnessCenter. Ada yang bisa saya bantu hari ini? 💪", ModelUsed="system", CreatedAt=session.CreatedAt },
            new ChatMessage { SessionId=session.Id, Role="user", Content="Aku baru mulai nge-gym, sebaiknya latihan berapa kali seminggu?", CreatedAt=session.CreatedAt.AddMinutes(1) },
            new ChatMessage { SessionId=session.Id, Role="assistant", Content="Untuk pemula, 3 kali seminggu dengan jeda satu hari sudah bagus. Contohnya Senin, Rabu, Jumat. Fokus dulu ke gerakan dasar: squat, push-up, row, dan plank. Tambah beban setelah teknikmu stabil, bukan sebaliknya.", ModelUsed="gpt-4o-mini", CreatedAt=session.CreatedAt.AddMinutes(1).AddSeconds(20) },
            new ChatMessage { SessionId=session.Id, Role="user", Content="Kelas apa yang cocok buat aku?", CreatedAt=session.CreatedAt.AddMinutes(3) },
            new ChatMessage { SessionId=session.Id, Role="assistant", Content="Kalau baru mulai, coba Pilates Core (level Beginner, Studio 1) untuk membangun kekuatan inti, lalu Step Aerobics untuk kardio ringan. Hindari HIIT Blast dulu karena levelnya Advanced.", ModelUsed="gpt-4o-mini", CreatedAt=session.CreatedAt.AddMinutes(3).AddSeconds(15) },
        });
        await _db.SaveChangesAsync();
    }
}
