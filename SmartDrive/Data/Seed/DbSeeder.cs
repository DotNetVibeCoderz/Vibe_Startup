using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartDrive.Models.Entities;
using SmartDrive.Models.Enums;

namespace SmartDrive.Data.Seed;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartDriveDbContext>();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var rm = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await db.Database.EnsureCreatedAsync();
        await EnsureRoles(rm);

        // === PENTING: Selalu pastikan role identity ter-assign ke semua user ===
        await FixUserRoles(um, db);

        // Seed data hanya jika tabel kosong
        if (!await db.Vehicles.AnyAsync()) await SeedVehicles(db);
        if (!await db.TrainingLocations.AnyAsync()) await SeedLocations(db);
        if (!await db.TheoryModules.AnyAsync()) await SeedTheory(db);
        if (!await db.MarketplaceProducts.AnyAsync()) await SeedProducts(db);
        if (!await db.SystemConfigs.AnyAsync()) await SeedConfigs(db);
    }

    private static async Task EnsureRoles(RoleManager<IdentityRole> rm)
    {
        foreach (var r in new[] { "Admin", "Instructor", "Student" })
            if (!await rm.RoleExistsAsync(r))
                await rm.CreateAsync(new IdentityRole(r));
    }

    /// <summary>
    /// SELALU dipanggil — assign ulang role identity ke user yang belum punya.
    /// Ini memperbaiki user yang sudah terlanjur dibuat tanpa AddToRoleAsync.
    /// </summary>
    private static async Task FixUserRoles(UserManager<ApplicationUser> um, SmartDriveDbContext db)
    {
        var freshSeed = !await db.Users.AnyAsync();
        if (freshSeed)
        {
            // Seed user baru + langsung assign role
            await CreateUser(um, db, "admin@smartdrive.com", "Administrator SmartDrive", "Admin123!", UserRole.Admin, "Admin", "081234567890");
            await CreateUser(um, db, "budi@smartdrive.com", "Budi Santoso", "Instructor123!", UserRole.Instructor, "Instructor", "081234567891");
            await CreateUser(um, db, "sari@smartdrive.com", "Sari Dewi", "Instructor123!", UserRole.Instructor, "Instructor", "081234567892");
            await CreateUser(um, db, "doni@smartdrive.com", "Doni Pratama", "Instructor123!", UserRole.Instructor, "Instructor", "081234567893");
            await CreateUser(um, db, "andi@email.com", "Andi Pratama", "Student123!", UserRole.Student, "Student", "085612345671");
            await CreateUser(um, db, "rina@email.com", "Rina Wijaya", "Student123!", UserRole.Student, "Student", "085612345672");
            await CreateUser(um, db, "eko@email.com", "Eko Prasetyo", "Student123!", UserRole.Student, "Student", "085612345673");

            // Instructor profiles
            var iUsers = await um.Users.Where(u => u.Role == UserRole.Instructor).ToListAsync();
            int lic = 1;
            foreach (var iu in iUsers)
            {
                db.InstructorProfiles.Add(new InstructorProfile
                {
                    UserId = iu.Id, LicenseNumber = $"INST-{lic++:D3}",
                    YearsOfExperience = 5 + lic, Bio = "Instruktur profesional.", AverageRating = 4.5m + (decimal)(lic * 0.1),
                    TotalStudents = 50 + lic * 10, TotalHoursTaught = 1000 + lic * 100, IsAvailable = true
                });
            }

            // Student profiles  
            var sUsers = await um.Users.Where(u => u.Role == UserRole.Student).ToListAsync();
            foreach (var su in sUsers)
            {
                db.StudentProfiles.Add(new StudentProfile
                {
                    UserId = su.Id, CurrentLevel = 1, CurrentBadge = "Pemula",
                    ExperiencePoints = 0, EnrollmentDate = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync();

            // Instructor schedules
            var allInst = await db.InstructorProfiles.ToListAsync();
            foreach (var inst in allInst)
                for (int d = 1; d <= 5; d++)
                    db.InstructorSchedules.Add(new InstructorSchedule
                    {
                        InstructorId = inst.Id, DayOfWeek = (DayOfWeek)d,
                        StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(17, 0, 0), IsAvailable = true
                    });

            await db.SaveChangesAsync();
        }
        else
        {
            // Database sudah ada — pastikan semua user punya role identity yang benar
            var allUsers = await um.Users.ToListAsync();
            foreach (var user in allUsers)
            {
                var identityRole = user.Role switch
                {
                    UserRole.Admin => "Admin",
                    UserRole.Instructor => "Instructor",
                    _ => "Student"
                };

                if (!await um.IsInRoleAsync(user, identityRole))
                {
                    await um.AddToRoleAsync(user, identityRole);
                    Console.WriteLine($"Fixed: assigned role '{identityRole}' to user '{user.Email}'");
                }
            }
        }
    }

    private static async Task CreateUser(UserManager<ApplicationUser> um, SmartDriveDbContext db,
        string email, string fullName, string password, UserRole role, string roleName, string? phone = null)
    {
        var user = new ApplicationUser
        {
            UserName = email, Email = email, FullName = fullName,
            Role = role, IsActive = true, EmailConfirmed = true,
            PhoneNumber = phone, CreatedAt = DateTime.UtcNow
        };
        var result = await um.CreateAsync(user, password);
        if (result.Succeeded)
            await um.AddToRoleAsync(user, roleName);
        else
            Console.WriteLine($"Warning: Could not create user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
    }

    private static async Task SeedVehicles(SmartDriveDbContext db)
    {
        db.Vehicles.AddRange(
            new() { PlateNumber = "B 1234 ABC", Brand = "Toyota", Model = "Avanza", Year = 2022, Transmission = TransmissionType.Automatic, Color = "Putih", Status = VehicleStatus.Available, HasInsurance = true },
            new() { PlateNumber = "B 5678 DEF", Brand = "Honda", Model = "Jazz", Year = 2021, Transmission = TransmissionType.Automatic, Color = "Merah", Status = VehicleStatus.Available, HasInsurance = true },
            new() { PlateNumber = "B 9012 GHI", Brand = "Suzuki", Model = "Ertiga", Year = 2023, Transmission = TransmissionType.Manual, Color = "Hitam", Status = VehicleStatus.Available, HasInsurance = true },
            new() { PlateNumber = "B 3456 JKL", Brand = "Daihatsu", Model = "Xenia", Year = 2020, Transmission = TransmissionType.Manual, Color = "Silver", Status = VehicleStatus.Maintenance, HasInsurance = true },
            new() { PlateNumber = "B 7890 MNO", Brand = "Mitsubishi", Model = "Xpander", Year = 2022, Transmission = TransmissionType.Automatic, Color = "Biru", Status = VehicleStatus.Available, HasInsurance = true });
        await db.SaveChangesAsync();
    }

    private static async Task SeedLocations(SmartDriveDbContext db)
    {
        db.TrainingLocations.AddRange(
            new() { Name = "Lapangan Parkir GBK", Address = "Gelora Bung Karno, Jakarta Pusat", Latitude = -6.2185, Longitude = 106.8025, LocationType = "ParkingLot", Description = "Area luas cocok untuk latihan dasar" },
            new() { Name = "Kawasan Industri Pulogadung", Address = "Pulogadung, Jakarta Timur", Latitude = -6.1835, Longitude = 106.9094, LocationType = "Street", Description = "Jalan lebar traffic sedang" },
            new() { Name = "BSD City", Address = "BSD City, Tangerang Selatan", Latitude = -6.2835, Longitude = 106.6650, LocationType = "Street", Description = "Berbagai tipe jalan" },
            new() { Name = "Tol Jagorawi", Address = "Gerbang Tol Cibubur", Latitude = -6.3700, Longitude = 106.8950, LocationType = "Highway", Description = "Latihan mengemudi di tol" },
            new() { Name = "Pantai Indah Kapuk", Address = "PIK, Jakarta Utara", Latitude = -6.1080, Longitude = 106.7575, LocationType = "Street", Description = "Kawasan perkotaan modern" });
        await db.SaveChangesAsync();
    }

    private static async Task SeedTheory(SmartDriveDbContext db)
    {
        db.TheoryModules.AddRange(
            new() { Title = "Pengenalan Rambu Lalu Lintas", Description = "Mengenal berbagai jenis rambu lalu lintas", Content = "<h2>Rambu Lalu Lintas</h2><p>Rambu terbagi menjadi 4 jenis: peringatan, larangan, perintah, petunjuk.</p>", Category = "TrafficSigns", OrderIndex = 1 },
            new() { Title = "Aturan Dasar Berlalu Lintas", Description = "Aturan dan etika berkendara", Content = "<h2>Aturan Dasar</h2><p>Setiap pengendara wajib mematuhi rambu dan marka jalan.</p>", Category = "RoadRules", OrderIndex = 2 },
            new() { Title = "Teknik Dasar Mengemudi", Description = "Teknik dasar mengemudi untuk pemula", Content = "<h2>Teknik Dasar</h2><p>Posisi duduk yang benar adalah kunci kenyamanan.</p>", Category = "BasicTechnique", OrderIndex = 3 },
            new() { Title = "Teknik Parkir", Description = "Teknik parkir paralel, serong, tegak lurus", Content = "<h2>Teknik Parkir</h2><p>Parkir paralel memerlukan ketelitian.</p>", Category = "Parking", OrderIndex = 4 },
            new() { Title = "Mengemudi di Jalan Tol", Description = "Aturan mengemudi di tol", Content = "<h2>Mengemudi di Tol</h2><p>Kecepatan minimum 60 km/jam.</p>", Category = "Highway", OrderIndex = 5 },
            new() { Title = "Keselamatan Berkendara", Description = "Tips keselamatan", Content = "<h2>Keselamatan</h2><p>Selalu gunakan sabuk pengaman.</p>", Category = "Safety", OrderIndex = 6 });
        await db.SaveChangesAsync();

        db.ExamQuestions.AddRange(
            new() { QuestionText = "Apa arti rambu segitiga merah?", OptionA = "Larangan", OptionB = "Peringatan", OptionC = "Perintah", OptionD = "Petunjuk", CorrectAnswer = "B", Category = "TrafficSigns", Difficulty = 1 },
            new() { QuestionText = "Batas kecepatan maksimal di jalan tol?", OptionA = "60 km/jam", OptionB = "80 km/jam", OptionC = "100 km/jam", OptionD = "120 km/jam", CorrectAnswer = "C", Category = "Highway", Difficulty = 2 },
            new() { QuestionText = "Apa yang dilakukan saat lampu kuning?", OptionA = "Mempercepat", OptionB = "Berhenti jika aman", OptionC = "Terus berjalan", OptionD = "Bunyikan klakson", CorrectAnswer = "B", Category = "RoadRules", Difficulty = 1 },
            new() { QuestionText = "Fungsi sabuk pengaman?", OptionA = "Hiasan", OptionB = "Mencegah cedera", OptionC = "Membuat nyaman", OptionD = "Syarat tilang", CorrectAnswer = "B", Category = "Safety", Difficulty = 1 },
            new() { QuestionText = "Kapan menyalakan lampu sein?", OptionA = "Saat berbelok", OptionB = "30 meter sebelum", OptionC = "Setelah berbelok", OptionD = "Tidak perlu", CorrectAnswer = "B", Category = "BasicTechnique", Difficulty = 2 },
            new() { QuestionText = "Apa itu blind spot?", OptionA = "Area terlihat jelas", OptionB = "Area tak terlihat spion", OptionC = "Area parkir", OptionD = "Area berhenti", CorrectAnswer = "B", Category = "Safety", Difficulty = 2 },
            new() { QuestionText = "Teknik parkir paralel?", OptionA = "Mundur perlahan", OptionB = "Majukan mobil", OptionC = "Parkir sembarang", OptionD = "Minta bantuan", CorrectAnswer = "A", Category = "Parking", Difficulty = 3 },
            new() { QuestionText = "Tanda lingkaran merah + garis diagonal?", OptionA = "Dilarang masuk", OptionB = "Dilarang parkir", OptionC = "Dilarang berhenti", OptionD = "Semua benar", CorrectAnswer = "D", Category = "TrafficSigns", Difficulty = 1 },
            new() { QuestionText = "Jarak aman minimal di tol?", OptionA = "10 m", OptionB = "30 m", OptionC = "50 m", OptionD = "100 m", CorrectAnswer = "D", Category = "Highway", Difficulty = 2 },
            new() { QuestionText = "Ban pecah saat mengemudi?", OptionA = "Rem mendadak", OptionB = "Tahan kemudi, kurangi kecepatan", OptionC = "Tepi cepat", OptionD = "Matikan mesin", CorrectAnswer = "B", Category = "Safety", Difficulty = 3 });
        await db.SaveChangesAsync();
    }

    private static async Task SeedProducts(SmartDriveDbContext db)
    {
        db.MarketplaceProducts.AddRange(
            new() { Name = "Defensive Driving", Description = "Teknik mengemudi defensif", Price = 500000, Category = "DefensiveDriving", DurationHours = 4 },
            new() { Name = "Eco-Driving", Description = "Mengemudi hemat BBM", Price = 350000, Category = "EcoDriving", DurationHours = 3 },
            new() { Name = "Mengemudi Malam", Description = "Latihan kondisi minim cahaya", Price = 400000, Category = "NightDriving", DurationHours = 3 },
            new() { Name = "Paket Ujian SIM", Description = "Persiapan ujian SIM", Price = 750000, Category = "ExamPrep", DurationHours = 8 },
            new() { Name = "Parkir Mahir", Description = "Kuasai teknik parkir", Price = 250000, Category = "Parking", DurationHours = 2 });
        await db.SaveChangesAsync();
    }

    private static async Task SeedConfigs(SmartDriveDbContext db)
    {
        db.SystemConfigs.AddRange(
            new() { ConfigKey = "CompanyName", ConfigValue = "SmartDrive Academy", Category = "General" },
            new() { ConfigKey = "DefaultSessionDuration", ConfigValue = "120", Category = "Booking" },
            new() { ConfigKey = "MaxStudentsPerInstructor", ConfigValue = "5", Category = "Instructor" },
            new() { ConfigKey = "PaymentGatewayEnabled", ConfigValue = "true", Category = "Payment" },
            new() { ConfigKey = "GpsTrackingInterval", ConfigValue = "5", Category = "Tracking" },
            new() { ConfigKey = "ChatBotName", ConfigValue = "Om Bambang", Category = "AI" },
            new() { ConfigKey = "ChatBotGreeting", ConfigValue = "Halo! Saya Om Bambang, asisten virtual SmartDrive.", Category = "AI" });
        await db.SaveChangesAsync();
    }
}
