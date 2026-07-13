using PCHub.Shared.Enums;
using PCHub.Shared.Models;

namespace PCHub.Shared.Data;

/// <summary>
/// Seeder untuk membuat sample data awal aplikasi
/// </summary>
public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext db)
    {
        // Cek apakah sudah ada data
        if (db.Users.Any()) return;

        // === USERS ===
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            Email = "admin@pchub.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
            FullName = "Administrator PCHub",
            PhoneNumber = "081234567890",
            Role = UserRole.Admin,
            MembershipTier = MembershipTier.VIP,
            LoyaltyPoints = 10000,
            Balance = 500000,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-90)
        };

        var operatorUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "operator1",
            Email = "operator@pchub.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Operator123!"),
            FullName = "Operator Store 1",
            PhoneNumber = "081234567891",
            Role = UserRole.Operator,
            MembershipTier = MembershipTier.Gold,
            LoyaltyPoints = 5000,
            Balance = 100000,
            CreatedAt = DateTime.UtcNow.AddDays(-60)
        };

        var memberUsers = new List<User>();
        var memberNames = new[]
        {
            ("budi_santoso", "Budi Santoso", "budi@email.com"),
            ("siti_nurhaliza", "Siti Nurhaliza", "siti@email.com"),
            ("andi_pratama", "Andi Pratama", "andi@email.com"),
            ("dewi_lestari", "Dewi Lestari", "dewi@email.com"),
            ("reza_ramadhan", "Reza Ramadhan", "reza@email.com"),
            ("ayu_ningrum", "Ayu Ningrum", "ayu@email.com"),
            ("doni_kurniawan", "Doni Kurniawan", "doni@email.com"),
            ("rina_wati", "Rina Wati", "rina@email.com"),
            ("agung_hermawan", "Agung Hermawan", "agung@email.com"),
            ("putri_indah", "Putri Indah", "putri@email.com"),
            ("fajar_saputra", "Fajar Saputra", "fajar@email.com"),
            ("mega_pertiwi", "Mega Pertiwi", "mega@email.com"),
            ("bayu_aji", "Bayu Aji", "bayu@email.com"),
            ("citra_kirana", "Citra Kirana", "citra@email.com"),
            ("eko_wahyudi", "Eko Wahyudi", "eko@email.com")
        };

        var membershipTiers = new[] {
            MembershipTier.Basic, MembershipTier.Basic, MembershipTier.Basic,
            MembershipTier.Silver, MembershipTier.Silver, MembershipTier.Silver,
            MembershipTier.Gold, MembershipTier.Gold, MembershipTier.Gold,
            MembershipTier.Platinum, MembershipTier.Platinum, MembershipTier.Platinum,
            MembershipTier.VIP, MembershipTier.VIP, MembershipTier.VIP
        };

        for (int i = 0; i < memberNames.Length; i++)
        {
            var (username, fullName, email) = memberNames[i];
            memberUsers.Add(new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Member123!"),
                FullName = fullName,
                PhoneNumber = $"08{i + 1:D2}3456789{i:D2}",
                Role = UserRole.Member,
                MembershipTier = membershipTiers[i],
                LoyaltyPoints = new Random().Next(0, 5000),
                Balance = new Random().Next(0, 200000),
                CreatedAt = DateTime.UtcNow.AddDays(-new Random().Next(1, 90))
            });
        }

        // === PCs ===
        var pcs = new List<Pc>();
        var pcSpecs = new[]
        {
            ("PC Gaming 1", "PC-001", "Intel i9-14900K | RTX 4090 24GB | 64GB DDR5 | SSD 2TB | Monitor 360Hz"),
            ("PC Gaming 2", "PC-002", "Intel i9-14900K | RTX 4090 24GB | 64GB DDR5 | SSD 2TB | Monitor 360Hz"),
            ("PC Gaming 3", "PC-003", "Intel i7-14700K | RTX 4080 16GB | 32GB DDR5 | SSD 1TB | Monitor 240Hz"),
            ("PC Gaming 4", "PC-004", "Intel i7-14700K | RTX 4080 16GB | 32GB DDR5 | SSD 1TB | Monitor 240Hz"),
            ("PC Gaming 5", "PC-005", "AMD Ryzen 7 7800X3D | RTX 4070 Ti 12GB | 32GB DDR5 | SSD 1TB | Monitor 240Hz"),
            ("PC Gaming 6", "PC-006", "AMD Ryzen 7 7800X3D | RTX 4070 Ti 12GB | 32GB DDR5 | SSD 1TB | Monitor 240Hz"),
            ("PC Gaming 7", "PC-007", "Intel i5-14600K | RTX 4070 12GB | 32GB DDR4 | SSD 1TB | Monitor 165Hz"),
            ("PC Gaming 8", "PC-008", "Intel i5-14600K | RTX 4070 12GB | 32GB DDR4 | SSD 1TB | Monitor 165Hz"),
            ("PC Gaming 9", "PC-009", "AMD Ryzen 5 7600X | RTX 4060 Ti 8GB | 16GB DDR5 | SSD 512GB | Monitor 165Hz"),
            ("PC Gaming 10", "PC-010", "AMD Ryzen 5 7600X | RTX 4060 Ti 8GB | 16GB DDR5 | SSD 512GB | Monitor 165Hz"),
            ("PC Streaming 1", "PC-011", "Intel i7-14700K | RTX 4080 16GB | 64GB DDR5 | SSD 2TB | Monitor 4K | Capture Card"),
            ("PC Streaming 2", "PC-012", "Intel i7-14700K | RTX 4080 16GB | 64GB DDR5 | SSD 2TB | Monitor 4K | Capture Card"),
            ("PC Premium 1", "PC-013", "AMD Ryzen 9 7950X3D | RTX 4090 24GB | 128GB DDR5 | NVMe 4TB | Monitor 360Hz OLED"),
            ("PC Premium 2", "PC-014", "AMD Ryzen 9 7950X3D | RTX 4090 24GB | 128GB DDR5 | NVMe 4TB | Monitor 360Hz OLED"),
            ("PC Standard 1", "PC-015", "Intel i5-12400F | RTX 3060 12GB | 16GB DDR4 | SSD 512GB | Monitor 144Hz")
        };

        var pcStatuses = new[] {
            PcStatus.Available, PcStatus.Available, PcStatus.InUse, PcStatus.Available,
            PcStatus.Maintenance, PcStatus.Available, PcStatus.InUse, PcStatus.Available,
            PcStatus.Available, PcStatus.Available, PcStatus.Available, PcStatus.Available,
            PcStatus.Available, PcStatus.Available, PcStatus.Available
        };

        var hourlyRates = new[] {
            15000m, 15000m, 12000m, 12000m, 10000m, 10000m, 8000m, 8000m,
            7000m, 7000m, 18000m, 18000m, 25000m, 25000m, 6000m
        };

        for (int i = 0; i < pcSpecs.Length; i++)
        {
            pcs.Add(new Pc
            {
                Id = Guid.NewGuid(),
                Name = pcSpecs[i].Item1,
                PcNumber = pcSpecs[i].Item2,
                Specifications = pcSpecs[i].Item3,
                Status = pcStatuses[i],
                HourlyRate = hourlyRates[i],
                CreatedAt = DateTime.UtcNow.AddDays(-90)
            });
        }

        // === GAMES ===
        var games = new List<Game>
        {
            new() { Id = Guid.NewGuid(), Name = "Valorant", Genre = GameGenre.FPS, Description = "Tactical FPS from Riot Games", ExecutablePath = "C:\\Games\\Valorant\\Valorant.exe", IconUrl = "/images/games/valorant.png", IsPopular = true, Version = "9.0" },
            new() { Id = Guid.NewGuid(), Name = "Counter-Strike 2", Genre = GameGenre.FPS, Description = "Classic competitive FPS", ExecutablePath = "C:\\Games\\CS2\\cs2.exe", IconUrl = "/images/games/cs2.png", IsPopular = true, Version = "1.0" },
            new() { Id = Guid.NewGuid(), Name = "Dota 2", Genre = GameGenre.MOBA, Description = "MOBA from Valve", ExecutablePath = "C:\\Games\\Dota2\\dota2.exe", IconUrl = "/images/games/dota2.png", IsPopular = true, Version = "7.35" },
            new() { Id = Guid.NewGuid(), Name = "League of Legends", Genre = GameGenre.MOBA, Description = "MOBA from Riot Games", ExecutablePath = "C:\\Games\\LoL\\LeagueClient.exe", IconUrl = "/images/games/lol.png", IsPopular = true, Version = "14.1" },
            new() { Id = Guid.NewGuid(), Name = "PUBG: Battlegrounds", Genre = GameGenre.BattleRoyale, Description = "Battle Royale shooter", ExecutablePath = "C:\\Games\\PUBG\\PUBG.exe", IconUrl = "/images/games/pubg.png", IsPopular = true, Version = "28.2" },
            new() { Id = Guid.NewGuid(), Name = "Apex Legends", Genre = GameGenre.BattleRoyale, Description = "Hero battle royale", ExecutablePath = "C:\\Games\\Apex\\Apex.exe", IconUrl = "/images/games/apex.png", IsPopular = true, Version = "20.0" },
            new() { Id = Guid.NewGuid(), Name = "Genshin Impact", Genre = GameGenre.RPG, Description = "Open-world action RPG", ExecutablePath = "C:\\Games\\Genshin\\GenshinImpact.exe", IconUrl = "/images/games/genshin.png", IsPopular = true, Version = "5.0" },
            new() { Id = Guid.NewGuid(), Name = "Minecraft", Genre = GameGenre.Adventure, Description = "Sandbox creative game", ExecutablePath = "C:\\Games\\Minecraft\\Minecraft.exe", IconUrl = "/images/games/minecraft.png", IsPopular = true, Version = "1.21" },
            new() { Id = Guid.NewGuid(), Name = "FIFA 25", Genre = GameGenre.Sport, Description = "Football simulation", ExecutablePath = "C:\\Games\\FIFA25\\FIFA25.exe", IconUrl = "/images/games/fifa25.png", IsPopular = true, Version = "1.0" },
            new() { Id = Guid.NewGuid(), Name = "Call of Duty: Warzone", Genre = GameGenre.FPS, Description = "Battle royale FPS", ExecutablePath = "C:\\Games\\Warzone\\Warzone.exe", IconUrl = "/images/games/warzone.png", IsPopular = true, Version = "2.0" },
            new() { Id = Guid.NewGuid(), Name = "Fortnite", Genre = GameGenre.BattleRoyale, Description = "Free battle royale", ExecutablePath = "C:\\Games\\Fortnite\\Fortnite.exe", IconUrl = "/images/games/fortnite.png", IsPopular = true, Version = "28.0" },
            new() { Id = Guid.NewGuid(), Name = "Roblox", Genre = GameGenre.Adventure, Description = "Online game platform", ExecutablePath = "C:\\Games\\Roblox\\Roblox.exe", IconUrl = "/images/games/roblox.png", IsPopular = true, Version = "2.0" },
            new() { Id = Guid.NewGuid(), Name = "Mobile Legends: PC", Genre = GameGenre.MOBA, Description = "MOBA on PC", ExecutablePath = "C:\\Games\\MLBB\\MLBB.exe", IconUrl = "/images/games/mlbb.png", IsPopular = false, Version = "1.0" },
            new() { Id = Guid.NewGuid(), Name = "Elden Ring", Genre = GameGenre.RPG, Description = "Action RPG masterpiece", ExecutablePath = "C:\\Games\\EldenRing\\EldenRing.exe", IconUrl = "/images/games/eldenring.png", IsPopular = true, Version = "1.12" },
            new() { Id = Guid.NewGuid(), Name = "GTA V", Genre = GameGenre.Adventure, Description = "Open-world action adventure", ExecutablePath = "C:\\Games\\GTAV\\GTA5.exe", IconUrl = "/images/games/gtav.png", IsPopular = true, Version = "1.68" }
        };

        // === MEMBERSHIPS ===
        var memberships = new List<Membership>
        {
            new() { Id = Guid.NewGuid(), Name = "Basic", Tier = MembershipTier.Basic, Description = "Paket dasar untuk member baru", MonthlyPrice = 0, DiscountPercentage = 0, BonusHours = 0, LoyaltyPointsPerMonth = 50 },
            new() { Id = Guid.NewGuid(), Name = "Silver", Tier = MembershipTier.Silver, Description = "Paket hemat dengan diskon 5%", MonthlyPrice = 50000, DiscountPercentage = 5, BonusHours = 2, LoyaltyPointsPerMonth = 150 },
            new() { Id = Guid.NewGuid(), Name = "Gold", Tier = MembershipTier.Gold, Description = "Paket premium diskon 10% + bonus 5 jam", MonthlyPrice = 150000, DiscountPercentage = 10, BonusHours = 5, LoyaltyPointsPerMonth = 500 },
            new() { Id = Guid.NewGuid(), Name = "Platinum", Tier = MembershipTier.Platinum, Description = "Paket pro diskon 15% + bonus 10 jam", MonthlyPrice = 350000, DiscountPercentage = 15, BonusHours = 10, LoyaltyPointsPerMonth = 1200 },
            new() { Id = Guid.NewGuid(), Name = "VIP", Tier = MembershipTier.VIP, Description = "Paket VIP unlimited access + semua benefit", MonthlyPrice = 750000, DiscountPercentage = 25, BonusHours = 30, LoyaltyPointsPerMonth = 3000 }
        };

        // === PROMOS ===
        var promos = new List<Promo>
        {
            new() { Id = Guid.NewGuid(), Name = "Diskon Malam Minggu", Description = "Diskon 20% setiap Sabtu malam", PromoCode = "SABTUMINGGU", DiscountPercentage = 20, StartDate = DateTime.UtcNow.AddDays(-30), EndDate = DateTime.UtcNow.AddDays(60), IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "Happy Hour 10-12", Description = "Diskon 15% jam 10 pagi - 12 siang", PromoCode = "HAPPYHOUR", DiscountPercentage = 15, StartDate = DateTime.UtcNow.AddDays(-60), EndDate = DateTime.UtcNow.AddDays(120), IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "Member Get Member", Description = "Diskon 30% untuk referral baru", PromoCode = "MGM30", DiscountPercentage = 30, StartDate = DateTime.UtcNow.AddDays(-15), EndDate = DateTime.UtcNow.AddDays(45), IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "Paket Pelajar", Description = "Diskon 10% khusus pelajar (dengan kartu pelajar)", PromoCode = "PELAJAR10", DiscountPercentage = 10, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(180), IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "Turnamen Diskon", Description = "Diskon 25% untuk peserta turnamen", PromoCode = "TOURNEY25", DiscountPercentage = 25, MaxDiscount = 50000, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(30), IsActive = true }
        };

        // === TOURNAMENTS ===
        var tournaments = new List<Tournament>
        {
            new() { Id = Guid.NewGuid(), Name = "PCHub Valorant Championship", Description = "Turnamen Valorant 5v5 dengan total hadiah Rp 5.000.000", GameId = games[0].Id, StartDate = DateTime.UtcNow.AddDays(14), EndDate = DateTime.UtcNow.AddDays(16), MaxParticipants = 32, EntryFee = 50000, PrizePool = 5000000 },
            new() { Id = Guid.NewGuid(), Name = "CS2 Weekend Clash", Description = "Turnamen CS2 5v5 setiap weekend", GameId = games[1].Id, StartDate = DateTime.UtcNow.AddDays(7), EndDate = DateTime.UtcNow.AddDays(9), MaxParticipants = 16, EntryFee = 35000, PrizePool = 3000000 },
            new() { Id = Guid.NewGuid(), Name = "Mobile Legends PC Cup", Description = "Turnamen MLBB mode PC", GameId = games[12].Id, StartDate = DateTime.UtcNow.AddDays(21), EndDate = DateTime.UtcNow.AddDays(22), MaxParticipants = 64, EntryFee = 25000, PrizePool = 2000000 }
        };

        // === SYSTEM CONFIGS ===
        var configs = new List<SystemConfig>
        {
            new() { Key = "AppName", Value = "PCHub Game Center", Description = "Nama aplikasi" },
            new() { Key = "DefaultHourlyRate", Value = "8000", Description = "Tarif default per jam (Rp)" },
            new() { Key = "MaxSessionHours", Value = "12", Description = "Maksimum jam per sesi" },
            new() { Key = "AutoLockMinutes", Value = "5", Description = "Menit sebelum auto-lock saat habis" },
            new() { Key = "CurrencySymbol", Value = "Rp", Description = "Simbol mata uang" },
            new() { Key = "Timezone", Value = "Asia/Jakarta", Description = "Zona waktu" },
            new() { Key = "AiProvider", Value = "OpenAI", Description = "Provider AI untuk chatbot Koh Dedi" },
            new() { Key = "AiModel", Value = "gpt-4o", Description = "Model AI" },
            new() { Key = "AiTemperature", Value = "0.7", Description = "Temperature AI" },
            new() { Key = "EmailSmtp", Value = "smtp.gmail.com", Description = "SMTP Server" },
            new() { Key = "EmailPort", Value = "587", Description = "SMTP Port" },
            new() { Key = "WhatsAppApiUrl", Value = "https://api.whatsapp.com", Description = "WhatsApp API URL" },
            new() { Key = "StorageProvider", Value = "FileSystem", Description = "Storage provider" },
            new() { Key = "DatabaseProvider", Value = "SQLite", Description = "Database provider" }
        };

        // === SAVE ALL ===
        db.Users.Add(adminUser);
        db.Users.Add(operatorUser);
        db.Users.AddRange(memberUsers);
        db.Pcs.AddRange(pcs);
        db.Games.AddRange(games);
        db.Memberships.AddRange(memberships);
        db.Promos.AddRange(promos);
        db.Tournaments.AddRange(tournaments);
        db.SystemConfigs.AddRange(configs);

        // Add some billing sessions for sample data
        var random = new Random();
        for (int i = 0; i < 30; i++)
        {
            var userIdx = random.Next(memberUsers.Count);
            var pcIdx = random.Next(pcs.Count);
            var hours = random.Next(1, 5);
            var startTime = DateTime.UtcNow.AddDays(-random.Next(1, 30)).AddHours(-hours);

            db.BillingSessions.Add(new BillingSession
            {
                Id = Guid.NewGuid(),
                UserId = memberUsers[userIdx].Id,
                PcId = pcs[pcIdx].Id,
                StartTime = startTime,
                EndTime = startTime.AddHours(hours),
                HourlyRate = pcs[pcIdx].HourlyRate,
                TotalCost = pcs[pcIdx].HourlyRate * hours,
                Status = BillingStatus.Completed,
                PaymentMethod = (PaymentMethod)random.Next(5),
                PaymentStatus = PaymentStatus.Completed,
                CreatedAt = startTime
            });
        }

        // Add some reservations
        for (int i = 0; i < 10; i++)
        {
            var userIdx = random.Next(memberUsers.Count);
            db.Reservations.Add(new Reservation
            {
                Id = Guid.NewGuid(),
                UserId = memberUsers[userIdx].Id,
                PcId = pcs[random.Next(pcs.Count)].Id,
                ReservationDate = DateTime.UtcNow.AddDays(random.Next(1, 7)),
                DurationMinutes = random.Next(1, 6) * 60,
                GameRequested = games[random.Next(games.Count)].Name,
                Status = (ReservationStatus)random.Next(3),
                Notes = "Mohon disiapkan headset tambahan",
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 7))
            });
        }

        await db.SaveChangesAsync();
    }
}
