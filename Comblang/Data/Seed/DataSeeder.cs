using Comblang.Models;
using Comblang.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace Comblang.Data.Seed;

/// <summary>
/// Seeds the database with rich sample data: 20 users with full profiles,
/// interest tags, matches, messages, groups, events, and gift catalog.
/// Only runs when the Users table is empty (idempotent).
/// 
/// Default password for all demo users: "password123"
/// </summary>
public static class DataSeeder
{
    private static readonly Random _rng = new(42);
    private static readonly string _defaultHash = AuthService.HashPassword("password123");

    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync()) return; // Already seeded

        // ═══════════════════════════════════════════
        // 1. CREATE USERS (20 users: 10 female, 9 male, 1 admin)
        // ═══════════════════════════════════════════
        var users = new List<User>
        {
            // ── Female users (Jakarta) ──
            new() { Id = Guid.Parse("a0000001-0001-0001-0001-000000000001"), Email = "sarah@email.com",   Username = "sarahcantik",   PasswordHash = _defaultHash, Role = "User", IsVerified = true,  City = "Jakarta Selatan", Country = "Indonesia", Latitude = -6.2608, Longitude = 106.7816, CreatedAt = DateTime.UtcNow.AddDays(-60) },
            new() { Id = Guid.Parse("a0000001-0001-0001-0001-000000000002"), Email = "rina@email.com",    Username = "rinaceria",     PasswordHash = _defaultHash, Role = "User", IsVerified = true,  City = "Jakarta Pusat",   Country = "Indonesia", Latitude = -6.1865, Longitude = 106.8345, CreatedAt = DateTime.UtcNow.AddDays(-55) },
            new() { Id = Guid.Parse("a0000001-0001-0001-0001-000000000003"), Email = "maya@email.com",    Username = "mayagaming",    PasswordHash = _defaultHash, Role = "User", IsVerified = true,  City = "Jakarta Barat",   Country = "Indonesia", Latitude = -6.1685, Longitude = 106.7570, CreatedAt = DateTime.UtcNow.AddDays(-50) },
            new() { Id = Guid.Parse("a0000001-0001-0001-0001-000000000004"), Email = "dewi@email.com",    Username = "dewibookworm",  PasswordHash = _defaultHash, Role = "User", IsVerified = true,  City = "Jakarta Timur",   Country = "Indonesia", Latitude = -6.2250, Longitude = 106.9000, CreatedAt = DateTime.UtcNow.AddDays(-45) },
            new() { Id = Guid.Parse("a0000001-0001-0001-0001-000000000005"), Email = "anisa@email.com",   Username = "anisasenja",    PasswordHash = _defaultHash, Role = "User", IsVerified = true,  City = "Jakarta Selatan", Country = "Indonesia", Latitude = -6.2750, Longitude = 106.8000, CreatedAt = DateTime.UtcNow.AddDays(-40) },
            // ── Female users (Bandung) ──
            new() { Id = Guid.Parse("a0000001-0001-0001-0001-000000000006"), Email = "dinda@email.com",   Username = "dindakutubuku", PasswordHash = _defaultHash, Role = "User", IsVerified = true,  City = "Bandung", Country = "Indonesia", Latitude = -6.9175, Longitude = 107.6191, CreatedAt = DateTime.UtcNow.AddDays(-35) },
            new() { Id = Guid.Parse("a0000001-0001-0001-0001-000000000007"), Email = "citra@email.com",   Username = "citraphoto",    PasswordHash = _defaultHash, Role = "User", IsVerified = true,  City = "Bandung", Country = "Indonesia", Latitude = -6.9030, Longitude = 107.6100, CreatedAt = DateTime.UtcNow.AddDays(-30) },
            new() { Id = Guid.Parse("a0000001-0001-0001-0001-000000000008"), Email = "lia@email.com",     Username = "liatraveller",  PasswordHash = _defaultHash, Role = "User", IsVerified = true,  City = "Bandung", Country = "Indonesia", Latitude = -6.9300, Longitude = 107.6300, CreatedAt = DateTime.UtcNow.AddDays(-28) },
            // ── Female users (Surabaya) ──
            new() { Id = Guid.Parse("a0000001-0001-0001-0001-000000000009"), Email = "ratna@email.com",   Username = "ratnasehat",    PasswordHash = _defaultHash, Role = "User", IsVerified = true,  City = "Surabaya", Country = "Indonesia", Latitude = -7.2575, Longitude = 112.7521, CreatedAt = DateTime.UtcNow.AddDays(-25) },
            new() { Id = Guid.Parse("a0000001-0001-0001-0001-000000000010"), Email = "sinta@email.com",   Username = "sintamusik",    PasswordHash = _defaultHash, Role = "User", IsVerified = true,  City = "Surabaya", Country = "Indonesia", Latitude = -7.2700, Longitude = 112.7600, CreatedAt = DateTime.UtcNow.AddDays(-22) },
            // ── Male users (Jakarta) ──
            new() { Id = Guid.Parse("a0000001-0001-0001-0001-000000000011"), Email = "andi@email.com",    Username = "andiganteng",   PasswordHash = _defaultHash, Role = "User", IsVerified = true,  IsPremium = true, City = "Jakarta Selatan", Country = "Indonesia", Latitude = -6.2500, Longitude = 106.7900, CreatedAt = DateTime.UtcNow.AddDays(-58) },
            new() { Id = Guid.Parse("a0000001-0001-0001-0001-000000000012"), Email = "budi@email.com",    Username = "budifoodie",    PasswordHash = _defaultHash, Role = "User", IsVerified = true,  City = "Jakarta Pusat",   Country = "Indonesia", Latitude = -6.1800, Longitude = 106.8300, CreatedAt = DateTime.UtcNow.AddDays(-52) },
            new() { Id = Guid.Parse("a0000001-0001-0001-0001-000000000013"), Email = "cahyo@email.com",   Username = "cahyoadventure",PasswordHash = _defaultHash, Role = "User", IsVerified = true,  City = "Jakarta Timur",   Country = "Indonesia", Latitude = -6.2300, Longitude = 106.9100, CreatedAt = DateTime.UtcNow.AddDays(-48) },
            new() { Id = Guid.Parse("a0000001-0001-0001-0001-000000000014"), Email = "doni@email.com",    Username = "donimusic",     PasswordHash = _defaultHash, Role = "User", IsVerified = true,  IsPremium = true, City = "Jakarta Barat", Country = "Indonesia", Latitude = -6.1700, Longitude = 106.7600, CreatedAt = DateTime.UtcNow.AddDays(-42) },
            new() { Id = Guid.Parse("a0000001-0001-0001-0001-000000000015"), Email = "eko@email.com",     Username = "ekopecintaalam",PasswordHash = _defaultHash, Role = "User", IsVerified = true,  City = "Jakarta Selatan", Country = "Indonesia", Latitude = -6.2700, Longitude = 106.8100, CreatedAt = DateTime.UtcNow.AddDays(-38) },
            // ── Male users (Bandung) ──
            new() { Id = Guid.Parse("a0000001-0001-0001-0001-000000000016"), Email = "fajar@email.com",   Username = "fajarbike",     PasswordHash = _defaultHash, Role = "User", IsVerified = true,  City = "Bandung", Country = "Indonesia", Latitude = -6.9200, Longitude = 107.6200, CreatedAt = DateTime.UtcNow.AddDays(-33) },
            new() { Id = Guid.Parse("a0000001-0001-0001-0001-000000000017"), Email = "gilang@email.com",  Username = "gilangkopi",    PasswordHash = _defaultHash, Role = "User", IsVerified = true,  City = "Bandung", Country = "Indonesia", Latitude = -6.9100, Longitude = 107.6050, CreatedAt = DateTime.UtcNow.AddDays(-27) },
            // ── Male users (Surabaya) ──
            new() { Id = Guid.Parse("a0000001-0001-0001-0001-000000000018"), Email = "hendra@email.com",  Username = "hendrafitness", PasswordHash = _defaultHash, Role = "User", IsVerified = true,  City = "Surabaya", Country = "Indonesia", Latitude = -7.2600, Longitude = 112.7500, CreatedAt = DateTime.UtcNow.AddDays(-24) },
            new() { Id = Guid.Parse("a0000001-0001-0001-0001-000000000019"), Email = "irfan@email.com",   Username = "irfantech",     PasswordHash = _defaultHash, Role = "User", IsVerified = true,  IsPremium = true, City = "Surabaya", Country = "Indonesia", Latitude = -7.2800, Longitude = 112.7700, CreatedAt = DateTime.UtcNow.AddDays(-20) },
            // ── Admin ──
            new() { Id = Guid.Parse("a0000001-0001-0001-0001-000000000020"), Email = "admin@comblang.com", Username = "admincomblang", PasswordHash = _defaultHash, Role = "Admin", IsVerified = true, City = "Jakarta", Country = "Indonesia", Latitude = -6.2088, Longitude = 106.8456, CreatedAt = DateTime.UtcNow.AddDays(-90) },
        };
        db.Users.AddRange(users);
        await db.SaveChangesAsync();

        // ═══════════════════════════════════════════
        // 2. PROFILES (rich bios)
        // ═══════════════════════════════════════════
        var profiles = new List<Profile>
        {
            new() { UserId = users[0].Id,  Bio = "Suka traveling, kopi, dan sunset 🌅✨ Foodie paruh waktu yang selalu cari hidden gem cafe. Libra yang easygoing.", Gender = "Female", DateOfBirth = new DateTime(1999, 10, 5),  Occupation = "UX Designer", Education = "S1 Desain Komunikasi Visual", RelationshipGoal = "Serious", HeightCm = 162, Religion = "Islam", ZodiacSign = "Libra", IsVerifiedPhoto = true },
            new() { UserId = users[1].Id,  Bio = "Foodie sejati 🍜, pecinta kucing 🐱, dan music lover 🎵. Cari partner concert date! Enfp-t di sini.", Gender = "Female", DateOfBirth = new DateTime(2001, 3, 15),  Occupation = "Content Creator", Education = "S1 Ilmu Komunikasi", RelationshipGoal = "Casual", HeightCm = 158, Religion = "Islam", ZodiacSign = "Pisces", IsVerifiedPhoto = true },
            new() { UserId = users[2].Id,  Bio = "Gamer girl 🎮, suka hiking 🏔️, dan main Valorant tiap malam. Cari duo ranked! Taurus yang setia kawan.", Gender = "Female", DateOfBirth = new DateTime(2000, 5, 10),  Occupation = "Software Engineer", Education = "S1 Teknik Informatika", RelationshipGoal = "Friendship", HeightCm = 165, Religion = "Kristen", ZodiacSign = "Taurus", IsVerifiedPhoto = true },
            new() { UserId = users[3].Id,  Bio = "Bookworm 📚, yoga enthusiast 🧘‍♀️, cari yang serius buat diskusi filosofi hidup. INTJ personality.", Gender = "Female", DateOfBirth = new DateTime(1997, 8, 22),  Occupation = "Data Analyst", Education = "S1 Statistika", RelationshipGoal = "Marriage", HeightCm = 160, Religion = "Islam", ZodiacSign = "Leo", IsVerifiedPhoto = true },
            new() { UserId = users[4].Id,  Bio = "Seniman 🎨, suka senja 🌆, pameran seni, dan ngobrolin makna hidup. Sagitarius yang bebas.", Gender = "Female", DateOfBirth = new DateTime(1998, 12, 1),  Occupation = "Graphic Designer", Education = "S1 Seni Rupa", RelationshipGoal = "Serious", HeightCm = 163, Religion = "Islam", ZodiacSign = "Sagittarius", IsVerifiedPhoto = true },
            new() { UserId = users[5].Id,  Bio = "Kutu buku Bandung 📖, suka ngafe sambil baca novel. Pecinta hujan dan musik indie.", Gender = "Female", DateOfBirth = new DateTime(2000, 2, 14),  Occupation = "Penulis", Education = "S1 Sastra Indonesia", RelationshipGoal = "Serious", HeightCm = 155, Religion = "Islam", ZodiacSign = "Aquarius", IsVerifiedPhoto = true },
            new() { UserId = users[6].Id,  Bio = "Fotografer profesional 📸, cari model buat project street photography. Suka kopi tubruk!", Gender = "Female", DateOfBirth = new DateTime(1999, 7, 30),  Occupation = "Fotografer", Education = "S1 Fotografi", RelationshipGoal = "Casual", HeightCm = 167, Religion = "Kristen", ZodiacSign = "Leo", IsVerifiedPhoto = true },
            new() { UserId = users[7].Id,  Bio = "Backpacker 🌍 udah 15 negara. Next trip: Jepang! Cari travel buddy yang asik diajak keliling dunia.", Gender = "Female", DateOfBirth = new DateTime(1998, 4, 18),  Occupation = "Digital Nomad", Education = "S1 Hubungan Internasional", RelationshipGoal = "Friendship", HeightCm = 161, Religion = "Islam", ZodiacSign = "Aries", IsVerifiedPhoto = true },
            new() { UserId = users[8].Id,  Bio = "Fitness instructor 💪, meal prep enthusiast 🥗, lari pagi tiap hari. Cari gym partner yang semangat!", Gender = "Female", DateOfBirth = new DateTime(1997, 1, 25),  Occupation = "Fitness Coach", Education = "S1 Ilmu Keolahragaan", RelationshipGoal = "Serious", HeightCm = 170, Religion = "Islam", ZodiacSign = "Aquarius", IsVerifiedPhoto = true },
            new() { UserId = users[9].Id,  Bio = "Pianis klasik 🎹, suka Chopin dan Debussy. Ngajar musik sambil kuliah S2. Cari partner duet!", Gender = "Female", DateOfBirth = new DateTime(1999, 9, 12),  Occupation = "Musisi / Pengajar", Education = "S2 Pendidikan Musik", RelationshipGoal = "Serious", HeightCm = 159, Religion = "Kristen", ZodiacSign = "Virgo", IsVerifiedPhoto = true },
            // ── Male ──
            new() { UserId = users[10].Id, Bio = "Software engineer 💻, photographer hobi 📸. Cari partner buat nge-teh sambil ngoding. Aquarius.", Gender = "Male", DateOfBirth = new DateTime(1998, 2, 8),  Occupation = "Senior Software Engineer", Education = "S1 Teknik Informatika", RelationshipGoal = "Serious", HeightCm = 175, Religion = "Islam", ZodiacSign = "Aquarius", IsVerifiedPhoto = true },
            new() { UserId = users[11].Id, Bio = "Chef profesional 👨‍🍳, bisa masak Italian & Japanese. Food vlogger di YouTube. Cari partner makan!", Gender = "Male", DateOfBirth = new DateTime(1997, 6, 20),  Occupation = "Chef / Content Creator", Education = "D3 Perhotelan", RelationshipGoal = "Casual", HeightCm = 172, Religion = "Islam", ZodiacSign = "Gemini", IsVerifiedPhoto = true },
            new() { UserId = users[12].Id, Bio = "Pendaki gunung ⛰️, udah summit 10+ gunung di Indonesia. Cari partner hiking yang tahan dingin!", Gender = "Male", DateOfBirth = new DateTime(1996, 11, 3),  Occupation = "Outdoor Guide", Education = "S1 Geografi", RelationshipGoal = "Friendship", HeightCm = 180, Religion = "Islam", ZodiacSign = "Scorpio", IsVerifiedPhoto = true },
            new() { UserId = users[13].Id, Bio = "DJ & music producer 🎧, main di club-club Jakarta. Cari someone who loves EDM and night vibes!", Gender = "Male", DateOfBirth = new DateTime(1999, 4, 15),  Occupation = "DJ / Music Producer", Education = "S1 Teknik Audio", RelationshipGoal = "Casual", HeightCm = 178, Religion = "Kristen", ZodiacSign = "Aries", IsVerifiedPhoto = true },
            new() { UserId = users[14].Id, Bio = "Environmental activist 🌿, suka camping & wildlife photography. Vegan. Cari soulmate yang peduli bumi.", Gender = "Male", DateOfBirth = new DateTime(1995, 8, 30),  Occupation = "Environmental Consultant", Education = "S2 Ilmu Lingkungan", RelationshipGoal = "Marriage", HeightCm = 176, Religion = "Islam", ZodiacSign = "Virgo", IsVerifiedPhoto = true },
            new() { UserId = users[15].Id, Bio = "Cyclist Bandung 🚴, udah touring Jawa-Bali. Cari geng cycling weekend! Capricorn yang rajin.", Gender = "Male", DateOfBirth = new DateTime(1998, 1, 12),  Occupation = "Arsitek", Education = "S1 Arsitektur", RelationshipGoal = "Serious", HeightCm = 174, Religion = "Islam", ZodiacSign = "Capricorn", IsVerifiedPhoto = true },
            new() { UserId = users[16].Id, Bio = "Coffee snob ☕, freelance barista, suka ngulik manual brew. Cari partner ngafe sore-sore.", Gender = "Male", DateOfBirth = new DateTime(1997, 10, 28), Occupation = "Barista / Bar Owner", Education = "S1 Manajemen Bisnis", RelationshipGoal = "Serious", HeightCm = 170, Religion = "Islam", ZodiacSign = "Scorpio", IsVerifiedPhoto = true },
            new() { UserId = users[17].Id, Bio = "Gym rat 🏋️, certified PT. Bisa bantu kamu reach fitness goals! Pisces yang perhatian.", Gender = "Male", DateOfBirth = new DateTime(1996, 3, 5),  Occupation = "Personal Trainer", Education = "S1 Ilmu Keolahragaan", RelationshipGoal = "Serious", HeightCm = 183, Religion = "Islam", ZodiacSign = "Pisces", IsVerifiedPhoto = true },
            new() { UserId = users[18].Id, Bio = "Tech entrepreneur 🚀, founder startup AI. Cari someone who gets the hustle & grind life.", Gender = "Male", DateOfBirth = new DateTime(1995, 7, 19),  Occupation = "CEO / Founder", Education = "S2 Computer Science", RelationshipGoal = "Serious", HeightCm = 177, Religion = "Islam", ZodiacSign = "Cancer", IsVerifiedPhoto = true },
            new() { UserId = users[19].Id, Bio = "Admin Comblang 🛡️. Siap bantu kalian semua menemukan jodoh! DM aja kalo ada masalah.", Gender = "Male", DateOfBirth = new DateTime(1994, 5, 1),  Occupation = "System Administrator", Education = "S1 Teknik Komputer", RelationshipGoal = "Marriage", HeightCm = 173, Religion = "Islam", ZodiacSign = "Taurus", IsVerifiedPhoto = true },
        };
        db.Profiles.AddRange(profiles);
        await db.SaveChangesAsync();

        // ═══════════════════════════════════════════
        // 3. INTEREST TAGS (5 per user)
        // ═══════════════════════════════════════════
        var tags = new List<InterestTag>();
        var tagData = new Dictionary<int, (string tag, string cat)[]>
        {
            [0]  = new[] { ("Travel", "Hobby"), ("Coffee", "Food"), ("Photography", "Hobby"), ("Sunset", "Lifestyle"), ("Cafe Hopping", "Food") },
            [1]  = new[] { ("Food", "Food"), ("Cat", "Pet"), ("Music", "Hobby"), ("Concert", "Entertainment"), ("K-Drama", "Entertainment") },
            [2]  = new[] { ("Gaming", "Hobby"), ("Hiking", "Sport"), ("Valorant", "Gaming"), ("Nature", "Travel"), ("Tech", "Career") },
            [3]  = new[] { ("Reading", "Hobby"), ("Yoga", "Sport"), ("Philosophy", "Education"), ("Mindfulness", "Lifestyle"), ("Writing", "Hobby") },
            [4]  = new[] { ("Art", "Hobby"), ("Sunset", "Lifestyle"), ("Painting", "Hobby"), ("Museum", "Entertainment"), ("Indie Music", "Music") },
            [5]  = new[] { ("Reading", "Hobby"), ("Coffee", "Food"), ("Indie Music", "Music"), ("Rain", "Lifestyle"), ("Novel", "Hobby") },
            [6]  = new[] { ("Photography", "Hobby"), ("Coffee", "Food"), ("Street Art", "Art"), ("Film", "Entertainment"), ("Vintage", "Lifestyle") },
            [7]  = new[] { ("Travel", "Hobby"), ("Backpacking", "Travel"), ("Culture", "Education"), ("Photography", "Hobby"), ("Food", "Food") },
            [8]  = new[] { ("Fitness", "Sport"), ("Cooking", "Food"), ("Running", "Sport"), ("Meal Prep", "Lifestyle"), ("Wellness", "Health") },
            [9]  = new[] { ("Music", "Hobby"), ("Piano", "Music"), ("Classical", "Music"), ("Teaching", "Career"), ("Reading", "Hobby") },
            [10] = new[] { ("Coding", "Career"), ("Photography", "Hobby"), ("Coffee", "Food"), ("Open Source", "Tech"), ("Gaming", "Hobby") },
            [11] = new[] { ("Cooking", "Food"), ("Italian Food", "Food"), ("Japanese Food", "Food"), ("Vlogging", "Career"), ("Travel", "Hobby") },
            [12] = new[] { ("Hiking", "Sport"), ("Camping", "Travel"), ("Mountaineering", "Sport"), ("Nature", "Lifestyle"), ("Photography", "Hobby") },
            [13] = new[] { ("Music", "Hobby"), ("DJ", "Music"), ("EDM", "Music"), ("Nightlife", "Entertainment"), ("Producing", "Career") },
            [14] = new[] { ("Environment", "Education"), ("Camping", "Travel"), ("Vegan", "Food"), ("Wildlife", "Nature"), ("Reading", "Hobby") },
            [15] = new[] { ("Cycling", "Sport"), ("Bike Touring", "Travel"), ("Architecture", "Career"), ("Coffee", "Food"), ("Photography", "Hobby") },
            [16] = new[] { ("Coffee", "Food"), ("Barista", "Career"), ("Manual Brew", "Hobby"), ("Latte Art", "Art"), ("Jazz", "Music") },
            [17] = new[] { ("Fitness", "Sport"), ("Gym", "Sport"), ("Bodybuilding", "Sport"), ("Nutrition", "Health"), ("Running", "Sport") },
            [18] = new[] { ("Startup", "Career"), ("AI", "Tech"), ("Entrepreneurship", "Career"), ("Reading", "Hobby"), ("Fitness", "Sport") },
            [19] = new[] { ("Tech", "Career"), ("System Admin", "Career"), ("Coffee", "Food"), ("Reading", "Hobby"), ("Badminton", "Sport") },
        };

        for (int i = 0; i < users.Count; i++)
        {
            if (tagData.TryGetValue(i, out var userTags))
                foreach (var (tag, cat) in userTags)
                    tags.Add(new InterestTag { UserId = users[i].Id, TagName = tag, Category = cat });
        }
        db.InterestTags.AddRange(tags);
        await db.SaveChangesAsync();

        // ═══════════════════════════════════════════
        // 4. MATCHES (10 mutual matches)
        // ═══════════════════════════════════════════
        var matches = new List<Match>
        {
            new() { UserId1 = users[0].Id,  UserId2 = users[10].Id, CompatibilityScore = 87, MatchedAt = DateTime.UtcNow.AddDays(-10), IsActive = true },
            new() { UserId1 = users[1].Id,  UserId2 = users[11].Id, CompatibilityScore = 72, MatchedAt = DateTime.UtcNow.AddDays(-8),  IsActive = true },
            new() { UserId1 = users[2].Id,  UserId2 = users[12].Id, CompatibilityScore = 91, MatchedAt = DateTime.UtcNow.AddDays(-7),  IsActive = true },
            new() { UserId1 = users[5].Id,  UserId2 = users[15].Id, CompatibilityScore = 78, MatchedAt = DateTime.UtcNow.AddDays(-5),  IsActive = true },
            new() { UserId1 = users[6].Id,  UserId2 = users[16].Id, CompatibilityScore = 84, MatchedAt = DateTime.UtcNow.AddDays(-4),  IsActive = true },
            new() { UserId1 = users[8].Id,  UserId2 = users[17].Id, CompatibilityScore = 95, MatchedAt = DateTime.UtcNow.AddDays(-3),  IsActive = true },
            new() { UserId1 = users[9].Id,  UserId2 = users[13].Id, CompatibilityScore = 66, MatchedAt = DateTime.UtcNow.AddDays(-2),  IsActive = true },
            new() { UserId1 = users[4].Id,  UserId2 = users[14].Id, CompatibilityScore = 81, MatchedAt = DateTime.UtcNow.AddDays(-1),  IsActive = true },
            new() { UserId1 = users[3].Id,  UserId2 = users[18].Id, CompatibilityScore = 73, MatchedAt = DateTime.UtcNow.AddDays(-6),  IsActive = true },
            new() { UserId1 = users[7].Id,  UserId2 = users[11].Id, CompatibilityScore = 59, MatchedAt = DateTime.UtcNow.AddDays(-12), IsActive = false },
        };
        db.Matches.AddRange(matches);
        await db.SaveChangesAsync();

        // ═══════════════════════════════════════════
        // 5. MESSAGES (3 conversations)
        // ═══════════════════════════════════════════
        var messages = new List<Message>();
        // Sarah ↔ Andi
        AddConversation(messages, users[0].Id, users[10].Id, new[] {
            ("Halo! Senang bisa match sama kamu~ 😊", false, 0), ("Halo juga! Aku juga senang banget! Kamu UX Designer ya? Keren!", true, 2),
            ("Iyaa! Lagi ngerjain project redesign app e-commerce nih. Kamu software engineer kan?", false, 3), ("Betul! Fullstack developer. Btw foto profil kamu bagus, di Bali ya?", true, 5),
            ("Iya! Di Ubud. Kamu suka traveling juga?", false, 7), ("Suka banget! Terakhir ke Raja Ampat. Yuk kapan-kapan traveling bareng?", true, 10),
            ("Wah boleh banget! 😍 Udah lama pengen ke Raja Ampat!", false, 12),
        });
        // Rina ↔ Budi
        AddConversation(messages, users[1].Id, users[11].Id, new[] {
            ("Hai! Kamu chef ya? Keren banget!", false, 0), ("Hai! Iya nih, lagi prep buat opening resto baru. Kamu content creator?", true, 3),
            ("Yes! Food vlogger. Mampir dong ke resto kamu nanti!", false, 5), ("Boleh banget! Nanti aku bikinin special menu buat kamu 😉", true, 8),
        });
        // Maya ↔ Cahyo
        AddConversation(messages, users[2].Id, users[12].Id, new[] {
            ("Main Valorant yuk! Rank apa?", false, 0), ("Diamond 2! Kamu?", true, 1),
            ("Plat 3 aja 😅 Tapi jago main Sage!", false, 3), ("Gas mabar weekend! Sekalian hiking abis itu?", true, 5),
            ("GASS! Double date: gaming + hiking! 🎮🏔️", false, 7),
        });
        db.Messages.AddRange(messages);
        await db.SaveChangesAsync();

        // ═══════════════════════════════════════════
        // 6. GROUP ROOMS
        // ═══════════════════════════════════════════
        var groups = new List<GroupRoom>
        {
            new() { Id = Guid.Parse("b0000001-0001-0001-0001-000000000001"), Name = "Coffee Lovers Indonesia", Description = "Buat pecinta kopi! Sharing resep, rekomendasi cafe, dan teknik brewing.", Category = "Food", CreatedById = users[16].Id, CreatedAt = DateTime.UtcNow.AddDays(-20), MaxMembers = 500 },
            new() { Id = Guid.Parse("b0000001-0001-0001-0001-000000000002"), Name = "Backpacker Nusantara", Description = "Tips traveling hemat, rekomendasi destinasi, dan cari teman perjalanan.", Category = "Travel", CreatedById = users[7].Id, CreatedAt = DateTime.UtcNow.AddDays(-15), MaxMembers = 1000 },
            new() { Id = Guid.Parse("b0000001-0001-0001-0001-000000000003"), Name = "Musik Indie & Alternatif", Description = "Diskusi musik indie lokal & internasional, info konser, rekomendasi lagu.", Category = "Music", CreatedById = users[13].Id, CreatedAt = DateTime.UtcNow.AddDays(-10), MaxMembers = 500 },
            new() { Id = Guid.Parse("b0000001-0001-0001-0001-000000000004"), Name = "Fotografi & Videografi", Description = "Sharing teknik foto, review gear, hunting bareng, dan photo contest!", Category = "Hobby", CreatedById = users[6].Id, CreatedAt = DateTime.UtcNow.AddDays(-8), MaxMembers = 300 },
            new() { Id = Guid.Parse("b0000001-0001-0001-0001-000000000005"), Name = "Gaming Community", Description = "Main bareng, tips game, turnamen seru, dan giveaway item!", Category = "Hobby", CreatedById = users[2].Id, CreatedAt = DateTime.UtcNow.AddDays(-5), MaxMembers = 800 },
        };
        db.GroupRooms.AddRange(groups);
        await db.SaveChangesAsync();

        // Group members
        var groupMembers = new List<GroupMember>();
        foreach (var g in groups)
        {
            var memberIds = users.OrderBy(_ => _rng.Next()).Take(_rng.Next(3, 7)).Select(u => u.Id).ToList();
            foreach (var uid in memberIds)
                groupMembers.Add(new GroupMember { GroupRoomId = g.Id, UserId = uid, Role = uid == g.CreatedById ? "Admin" : "Member" });
        }
        db.GroupMembers.AddRange(groupMembers);
        await db.SaveChangesAsync();

        // ═══════════════════════════════════════════
        // 7. GIFT CATALOG
        // ═══════════════════════════════════════════
        db.Gifts.AddRange(
            new() { Name = "Mawar Merah", Description = "Simbol cinta klasik yang tak lekang waktu", IconUrl = "🌹", Price = 50, Category = "Romantic" },
            new() { Name = "Coklat Premium", Description = "Manis seperti senyummu", IconUrl = "🍫", Price = 30, Category = "Romantic" },
            new() { Name = "Teddy Bear", Description = "Teman peluk yang lucu dan menggemaskan", IconUrl = "🧸", Price = 80, Category = "Cute" },
            new() { Name = "Cincin Berlian", Description = "Untuk yang paling spesial di hatimu 💍", IconUrl = "💍", Price = 500, Category = "Premium" },
            new() { Name = "Buket Bunga", Description = "Cantik seperti senyummu di pagi hari", IconUrl = "💐", Price = 100, Category = "Romantic" },
            new() { Name = "Surat Cinta", Description = "Kata-kata tulus dari lubuk hati terdalam", IconUrl = "💌", Price = 15, Category = "Classic" },
            new() { Name = "Balon Hati", Description = "Terbang tinggi membawa cinta", IconUrl = "🎈", Price = 25, Category = "Cute" },
            new() { Name = "Mahkota", Description = "Kamu adalah ratu/raja di hatiku 👑", IconUrl = "👑", Price = 500, Category = "Premium" },
            new() { Name = "Kopi Spesial", Description = "Buat coffee date virtual ☕", IconUrl = "☕", Price = 20, Category = "Casual" },
            new() { Name = "Pizza", Description = "Makan malam romantis ala Italia 🍕", IconUrl = "🍕", Price = 35, Category = "Food" },
            new() { Name = "Gitar", Description = "Untuk yang hobi musik 🎸", IconUrl = "🎸", Price = 150, Category = "Hobby" },
            new() { Name = "Tiket Pesawat", Description = "Liburan romantis berdua ✈️", IconUrl = "✈️", Price = 300, Category = "Premium" }
        );
        await db.SaveChangesAsync();

        // ═══════════════════════════════════════════
        // 8. EVENTS
        // ═══════════════════════════════════════════
        db.Events.AddRange(
            new() { Title = "Speed Dating Online", Description = "Kenalan cepat dengan 10 peserta dalam 1 jam. Siapa tahu jodoh! Hosted by Si Mak Comblang 🤖", EventType = "Online", Location = "Zoom", StartTime = DateTime.UtcNow.AddDays(7).Date.AddHours(12), EndTime = DateTime.UtcNow.AddDays(7).Date.AddHours(14), MaxParticipants = 50, CreatedById = users[19].Id, IsActive = true },
            new() { Title = "Coffee Meetup Jakarta", Description = "Ngopi bareng dan kenalan dengan sesama single di Jakarta. Casual & fun! ☕", EventType = "Offline", Location = "Anomali Coffee, Menteng", StartTime = DateTime.UtcNow.AddDays(14).Date.AddHours(8), EndTime = DateTime.UtcNow.AddDays(14).Date.AddHours(11), MaxParticipants = 30, CreatedById = users[16].Id, IsActive = true },
            new() { Title = "Hiking Date to Sentul", Description = "Hiking bareng di Sentul. Random trekking partner! 🏔️", EventType = "Offline", Location = "Gunung Pancar, Sentul", StartTime = DateTime.UtcNow.AddDays(21).Date.AddHours(23), EndTime = DateTime.UtcNow.AddDays(22).Date.AddHours(8), MaxParticipants = 24, CreatedById = users[12].Id, IsActive = true },
            new() { Title = "Game Night Virtual", Description = "Main game online bareng: Among Us, Mobile Legends, Valorant! 🎮", EventType = "Online", Location = "Discord", StartTime = DateTime.UtcNow.AddDays(3).Date.AddHours(13), EndTime = DateTime.UtcNow.AddDays(3).Date.AddHours(17), MaxParticipants = 100, CreatedById = users[2].Id, IsActive = true }
        );
        await db.SaveChangesAsync();

        // ═══════════════════════════════════════════
        // 9. BOOSTS (premium users)
        // ═══════════════════════════════════════════
        db.Boosts.AddRange(
            new() { UserId = users[10].Id, BoostType = "Super", StartTime = DateTime.UtcNow.AddHours(-2), EndTime = DateTime.UtcNow.AddHours(22), IsActive = true, ImpressionsGained = 120 },
            new() { UserId = users[13].Id, BoostType = "Standard", StartTime = DateTime.UtcNow.AddHours(-5), EndTime = DateTime.UtcNow.AddHours(19), IsActive = true, ImpressionsGained = 85 },
            new() { UserId = users[18].Id, BoostType = "Mega", StartTime = DateTime.UtcNow.AddHours(-1), EndTime = DateTime.UtcNow.AddHours(47), IsActive = true, ImpressionsGained = 200 }
        );
        await db.SaveChangesAsync();
    }

    private static void AddConversation(List<Message> list, Guid user1, Guid user2, (string text, bool fromUser2, int minsAgo)[] msgs)
    {
        var now = DateTime.UtcNow;
        foreach (var (text, fromUser2, minsAgo) in msgs)
        {
            list.Add(new Message
            {
                SenderId = fromUser2 ? user2 : user1,
                ReceiverId = fromUser2 ? user1 : user2,
                Content = text,
                MessageType = "Text",
                SentAt = now.AddMinutes(-minsAgo),
                IsRead = true
            });
        }
    }
}
