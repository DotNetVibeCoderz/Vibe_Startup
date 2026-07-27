// Seed data for sample content
using Joka.Models.Users;
using Joka.Models.Flights;
using Joka.Models.Trains;
using Joka.Models.Buses;
using Joka.Models.Backoffice;
using Joka.Models.Common;
using Joka.Models.Hotels;
using Joka.Models.Payments;
using Joka.Models.Activities;
using Joka.Models.Support;
using Joka.Models.Transport;

namespace Joka.Data;

public static class SeedData
{
    // Hotlinked from Unsplash, which explicitly permits it. Every id below was
    // checked for a 200 response. Sized down at the CDN so pages stay light.
    private static string Img(string id, int w = 800) =>
        $"https://images.unsplash.com/photo-{id}?w={w}&q=75&auto=format&fit=crop";

    private static string Gallery(params string[] ids) =>
        System.Text.Json.JsonSerializer.Serialize(ids.Select(i => Img(i)).ToArray());

    private static string Avatar(string seed) =>
        $"https://api.dicebear.com/9.x/avataaars/svg?seed={seed}&backgroundColor=ffb800,ff5c35,10556e";

    /// <summary>
    /// Same hasher AuthService uses (PBKDF2 with a per-user random salt), so a
    /// fresh install never carries the old SHA256 format.
    /// </summary>
    private static readonly Microsoft.AspNetCore.Identity.PasswordHasher<User> Hasher = new();

    private static string Hash(string password) => Hasher.HashPassword(new User(), password);

    public static async Task InitializeAsync(AppDbContext context)
    {
        if (context.Users.Any()) return;

        // === MERCHANTS / PARTNERS ===
        // Seeded before users so merchant accounts can be linked to them.
        var merchants = new List<Merchant>
        {
            new() { Code = "PDM", Name = "Padma Hotels Group", Category = "Hotel", ContactEmail = "partner@padmahotels.id", ContactPhone = "+62361752111", CommissionRate = 12m, AverageRating = 4.8, TotalProducts = 2, Status = "Active", JoinedAt = DateTime.UtcNow.AddMonths(-18), Description = "Grup hotel dan resor dengan properti di Bali dan Jawa." },
            new() { Code = "GIA", Name = "Garuda Indonesia", Category = "Airline", ContactEmail = "b2b@garuda-indonesia.com", ContactPhone = "+62212311801", CommissionRate = 8m, AverageRating = 4.6, TotalProducts = 4, Status = "Active", JoinedAt = DateTime.UtcNow.AddMonths(-30) },
            new() { Code = "DAY", Name = "DayTrans Shuttle", Category = "Transport", ContactEmail = "ops@daytrans.co.id", ContactPhone = "+62221500100", CommissionRate = 15m, AverageRating = 4.4, TotalProducts = 3, Status = "Active", JoinedAt = DateTime.UtcNow.AddMonths(-8) },
            new() { Code = "JVJ", Name = "Java Jazz Production", Category = "Activity", ContactEmail = "ticketing@javajazz.id", CommissionRate = 18m, AverageRating = 4.5, TotalProducts = 1, Status = "Pending", JoinedAt = DateTime.UtcNow.AddDays(-12), Description = "Menunggu verifikasi dokumen legal." }
        };
        context.Merchants.AddRange(merchants);

        // === USERS ===
        // Password for every seeded account is "Joka123!" - demo credentials only.
        const string demoPassword = "Joka123!";
        var users = new List<User>
        {
            new() { Username = "budi_traveler", Email = "budi@example.com", FullName = "Budi Santoso", PhoneNumber = "+62812345678", LoyaltyPoints = 1500, MembershipTier = "Gold", AvatarUrl = Avatar("budi"), Role = Roles.Customer, PasswordHash = Hash(demoPassword), IsEmailVerified = true },
            new() { Username = "siti_wanderlust", Email = "siti@example.com", FullName = "Siti Nurhaliza", PhoneNumber = "+62898765432", LoyaltyPoints = 3200, MembershipTier = "Platinum", AvatarUrl = Avatar("siti"), Role = Roles.Customer, PasswordHash = Hash(demoPassword), IsEmailVerified = true },
            new() { Username = "demo_user", Email = "demo@joka.id", FullName = "Demo User", PhoneNumber = "+62800000000", LoyaltyPoints = 500, MembershipTier = "Classic", AvatarUrl = Avatar("demo"), Role = Roles.Customer, PasswordHash = Hash(demoPassword), IsEmailVerified = true },

            new() { Username = "admin", Email = "admin@joka.id", FullName = "Rina Admin", PhoneNumber = "+62811000001", Role = Roles.Admin, AvatarUrl = Avatar("admin"), PasswordHash = Hash(demoPassword), IsEmailVerified = true },
            new() { Username = "operator", Email = "operator@joka.id", FullName = "Dedi Operator", PhoneNumber = "+62811000002", Role = Roles.Operator, AvatarUrl = Avatar("operator"), PasswordHash = Hash(demoPassword), IsEmailVerified = true },
            new() { Username = "merchant_padma", Email = "merchant@joka.id", FullName = "Wayan Partner", PhoneNumber = "+62811000003", Role = Roles.Merchant, MerchantId = merchants[0].Id, AvatarUrl = Avatar("merchant"), PasswordHash = Hash(demoPassword), IsEmailVerified = true },
            new() { Username = "merchant_daytrans", Email = "merchant2@joka.id", FullName = "Agus Transport", PhoneNumber = "+62811000004", Role = Roles.Merchant, MerchantId = merchants[2].Id, AvatarUrl = Avatar("agus"), PasswordHash = Hash(demoPassword), IsEmailVerified = true },

            new() { Username = "user_blocked", Email = "blocked@example.com", FullName = "Akun Diblokir", Role = Roles.Customer, AvatarUrl = Avatar("blocked"), PasswordHash = Hash(demoPassword), IsBlocked = true, BlockedReason = "Terindikasi transaksi fraud berulang", BlockedAt = DateTime.UtcNow.AddDays(-5), BlockedBy = "admin@joka.id" }
        };
        context.Users.AddRange(users);

        // === AIRPORTS ===
        var airports = new List<Airport>
        {
            new() { Code = "CGK", Name = "Soekarno-Hatta International Airport", City = "Jakarta", Country = "Indonesia", ImageUrl = "/images/airports/cgk.jpg" },
            new() { Code = "DPS", Name = "Ngurah Rai International Airport", City = "Denpasar", Country = "Indonesia", ImageUrl = "/images/airports/dps.jpg" },
            new() { Code = "SUB", Name = "Juanda International Airport", City = "Surabaya", Country = "Indonesia", ImageUrl = "/images/airports/sub.jpg" },
            new() { Code = "YIA", Name = "Yogyakarta International Airport", City = "Yogyakarta", Country = "Indonesia", ImageUrl = "/images/airports/yia.jpg" },
            new() { Code = "KNO", Name = "Kualanamu International Airport", City = "Medan", Country = "Indonesia", ImageUrl = "/images/airports/kno.jpg" },
            new() { Code = "UPG", Name = "Sultan Hasanuddin Airport", City = "Makassar", Country = "Indonesia", ImageUrl = "/images/airports/upg.jpg" },
            new() { Code = "BPN", Name = "Sepinggan Airport", City = "Balikpapan", Country = "Indonesia", ImageUrl = "/images/airports/bpn.jpg" },
            new() { Code = "SIN", Name = "Changi Airport", City = "Singapore", Country = "Singapore", ImageUrl = "/images/airports/sin.jpg" },
            new() { Code = "KUL", Name = "KLIA", City = "Kuala Lumpur", Country = "Malaysia", ImageUrl = "/images/airports/kul.jpg" },
            new() { Code = "BKK", Name = "Suvarnabhumi Airport", City = "Bangkok", Country = "Thailand", ImageUrl = "/images/airports/bkk.jpg" }
        };
        context.Airports.AddRange(airports);

        // === AIRLINES ===
        var airlines = new List<Airline>
        {
            new() { Code = "GA", Name = "Garuda Indonesia", Country = "Indonesia", Rating = 5, LogoUrl = "/images/airlines/garuda.svg" },
            new() { Code = "QZ", Name = "AirAsia Indonesia", Country = "Indonesia", Rating = 4, LogoUrl = "/images/airlines/airasia.svg" },
            new() { Code = "JT", Name = "Lion Air", Country = "Indonesia", Rating = 3, LogoUrl = "/images/airlines/lion.svg" },
            new() { Code = "SJ", Name = "Super Air Jet", Country = "Indonesia", Rating = 4, LogoUrl = "/images/airlines/superairjet.svg" },
            new() { Code = "ID", Name = "Batik Air", Country = "Indonesia", Rating = 4, LogoUrl = "/images/airlines/batik.svg" },
            new() { Code = "QG", Name = "Citilink", Country = "Indonesia", Rating = 4, LogoUrl = "/images/airlines/citilink.svg" },
            new() { Code = "SQ", Name = "Singapore Airlines", Country = "Singapore", Rating = 5, LogoUrl = "/images/airlines/singapore.svg" }
        };
        context.Airlines.AddRange(airlines);

        // === FLIGHTS ===
        var flights = new List<Flight>
        {
            new() { FlightNumber = "GA-201", Airline = airlines[0], DepartureAirport = airports[0], ArrivalAirport = airports[1], DepartureTime = DateTime.UtcNow.AddDays(1).Date.AddHours(7), ArrivalTime = DateTime.UtcNow.AddDays(1).Date.AddHours(9).AddMinutes(30), DurationMinutes = 150, BasePrice = 1200000m, TotalSeats = 180, AvailableSeats = 45, CabinClass = "Economy", HasMeal = true, BaggageAllowanceKg = 20, IsRefundable = true },
            new() { FlightNumber = "QZ-755", Airline = airlines[1], DepartureAirport = airports[0], ArrivalAirport = airports[1], DepartureTime = DateTime.UtcNow.AddDays(1).Date.AddHours(10), ArrivalTime = DateTime.UtcNow.AddDays(1).Date.AddHours(12).AddMinutes(15), DurationMinutes = 135, BasePrice = 650000m, TotalSeats = 180, AvailableSeats = 120, CabinClass = "Economy", HasMeal = false, BaggageAllowanceKg = 15, IsRefundable = false },
            new() { FlightNumber = "GA-301", Airline = airlines[0], DepartureAirport = airports[0], ArrivalAirport = airports[2], DepartureTime = DateTime.UtcNow.AddDays(1).Date.AddHours(6), ArrivalTime = DateTime.UtcNow.AddDays(1).Date.AddHours(7).AddMinutes(30), DurationMinutes = 90, BasePrice = 950000m, TotalSeats = 180, AvailableSeats = 67, CabinClass = "Economy", HasMeal = true, BaggageAllowanceKg = 20, IsRefundable = true },
            new() { FlightNumber = "JT-501", Airline = airlines[2], DepartureAirport = airports[0], ArrivalAirport = airports[2], DepartureTime = DateTime.UtcNow.AddDays(1).Date.AddHours(14), ArrivalTime = DateTime.UtcNow.AddDays(1).Date.AddHours(15).AddMinutes(30), DurationMinutes = 90, BasePrice = 450000m, TotalSeats = 189, AvailableSeats = 150, CabinClass = "Economy", HasMeal = false, BaggageAllowanceKg = 10, IsRefundable = false },
            new() { FlightNumber = "GA-401", Airline = airlines[0], DepartureAirport = airports[0], ArrivalAirport = airports[7], DepartureTime = DateTime.UtcNow.AddDays(3).Date.AddHours(8), ArrivalTime = DateTime.UtcNow.AddDays(3).Date.AddHours(10).AddMinutes(30), DurationMinutes = 150, BasePrice = 2500000m, TotalSeats = 180, AvailableSeats = 89, CabinClass = "Economy", HasMeal = true, BaggageAllowanceKg = 25, IsRefundable = true },
            new() { FlightNumber = "SQ-951", Airline = airlines[6], DepartureAirport = airports[7], ArrivalAirport = airports[0], DepartureTime = DateTime.UtcNow.AddDays(5).Date.AddHours(14), ArrivalTime = DateTime.UtcNow.AddDays(5).Date.AddHours(16).AddMinutes(30), DurationMinutes = 150, BasePrice = 3200000m, TotalSeats = 250, AvailableSeats = 100, CabinClass = "Economy", HasMeal = true, BaggageAllowanceKg = 30, IsRefundable = true },
            new() { FlightNumber = "ID-601", Airline = airlines[4], DepartureAirport = airports[0], ArrivalAirport = airports[3], DepartureTime = DateTime.UtcNow.AddDays(2).Date.AddHours(9), ArrivalTime = DateTime.UtcNow.AddDays(2).Date.AddHours(10).AddMinutes(15), DurationMinutes = 75, BasePrice = 780000m, TotalSeats = 160, AvailableSeats = 55, CabinClass = "Economy", HasMeal = true, BaggageAllowanceKg = 20, IsRefundable = true },
            new() { FlightNumber = "QG-801", Airline = airlines[5], DepartureAirport = airports[0], ArrivalAirport = airports[5], DepartureTime = DateTime.UtcNow.AddDays(4).Date.AddHours(5), ArrivalTime = DateTime.UtcNow.AddDays(4).Date.AddHours(7).AddMinutes(45), DurationMinutes = 165, BasePrice = 1100000m, TotalSeats = 180, AvailableSeats = 130, CabinClass = "Economy", HasMeal = false, BaggageAllowanceKg = 15, IsRefundable = false }
        };
        context.Flights.AddRange(flights);

        // === TRAIN STATIONS ===
        var stations = new List<TrainStation>
        {
            new() { Code = "GMR", Name = "Stasiun Gambir", City = "Jakarta" },
            new() { Code = "BD", Name = "Stasiun Bandung", City = "Bandung" },
            new() { Code = "SBY", Name = "Stasiun Surabaya Gubeng", City = "Surabaya" },
            new() { Code = "YK", Name = "Stasiun Yogyakarta", City = "Yogyakarta" },
            new() { Code = "ML", Name = "Stasiun Malang", City = "Malang" },
            new() { Code = "SLO", Name = "Stasiun Solo Balapan", City = "Solo" }
        };
        context.TrainStations.AddRange(stations);

        // === TRAINS ===
        var trains = new List<Train>
        {
            new() { TrainNumber = "KA-001", Name = "Argo Bromo Anggrek", Class = "Eksekutif", TotalSeats = 400, HasWifi = true, HasMeal = true, HasEntertainment = true },
            new() { TrainNumber = "KA-002", Name = "Argo Parahyangan", Class = "Eksekutif", TotalSeats = 350, HasWifi = true, HasMeal = true, HasEntertainment = true },
            new() { TrainNumber = "KA-003", Name = "Gajayana", Class = "Eksekutif", TotalSeats = 360, HasWifi = true, HasMeal = true, HasEntertainment = false },
            new() { TrainNumber = "KA-004", Name = "Taksaka", Class = "Eksekutif", TotalSeats = 320, HasWifi = true, HasMeal = true, HasEntertainment = true }
        };
        context.Trains.AddRange(trains);

        // === TRAIN SCHEDULES ===
        var trainSchedules = new List<TrainSchedule>
        {
            new() { Train = trains[0], DepartureStation = stations[0], ArrivalStation = stations[2], DepartureTime = DateTime.UtcNow.AddDays(1).Date.AddHours(8), ArrivalTime = DateTime.UtcNow.AddDays(1).Date.AddHours(17), DurationMinutes = 540, BasePrice = 650000m, AvailableSeats = 200 },
            new() { Train = trains[1], DepartureStation = stations[0], ArrivalStation = stations[1], DepartureTime = DateTime.UtcNow.AddDays(1).Date.AddHours(7), ArrivalTime = DateTime.UtcNow.AddDays(1).Date.AddHours(10), DurationMinutes = 180, BasePrice = 250000m, AvailableSeats = 150 },
            new() { Train = trains[2], DepartureStation = stations[2], ArrivalStation = stations[4], DepartureTime = DateTime.UtcNow.AddDays(2).Date.AddHours(9), ArrivalTime = DateTime.UtcNow.AddDays(2).Date.AddHours(16), DurationMinutes = 420, BasePrice = 450000m, AvailableSeats = 180 },
            new() { Train = trains[3], DepartureStation = stations[3], ArrivalStation = stations[5], DepartureTime = DateTime.UtcNow.AddDays(1).Date.AddHours(10), ArrivalTime = DateTime.UtcNow.AddDays(1).Date.AddHours(12).AddMinutes(30), DurationMinutes = 150, BasePrice = 180000m, AvailableSeats = 200 },
        };
        context.TrainSchedules.AddRange(trainSchedules);

        // === BUS TERMINALS ===
        var terminals = new List<BusTerminal>
        {
            new() { Code = "PLG", Name = "Terminal Pulo Gebang", City = "Jakarta", Address = "Jl. Raya Bekasi, Jakarta Timur" },
            new() { Code = "KLD", Name = "Terminal Kampung Rambutan", City = "Jakarta", Address = "Jl. TB Simatupang, Jakarta Timur" },
            new() { Code = "LBA", Name = "Terminal Leuwipanjang", City = "Bandung", Address = "Jl. Soekarno Hatta, Bandung" },
            new() { Code = "GIW", Name = "Terminal Giwangan", City = "Yogyakarta", Address = "Jl. Imogiri Timur, Yogyakarta" },
            new() { Code = "PBN", Name = "Terminal Purabaya (Bungurasih)", City = "Surabaya", Address = "Jl. Letjen Sutoyo, Sidoarjo" },
            new() { Code = "TRB", Name = "Terminal Tirtonadi", City = "Solo", Address = "Jl. Ahmad Yani, Surakarta" },
            new() { Code = "ABT", Name = "Terminal Arjosari", City = "Malang", Address = "Jl. Raden Intan, Malang" },
            new() { Code = "CRB", Name = "Terminal Harjamukti", City = "Cirebon", Address = "Jl. Ahmad Yani, Cirebon" },
            new() { Code = "SMG", Name = "Terminal Terboyo", City = "Semarang", Address = "Jl. Kaligawe Raya, Semarang" }
        };
        context.BusTerminals.AddRange(terminals);

        // === BUS OPERATORS ===
        var busOperators = new List<BusOperator>
        {
            new() { Code = "RSL", Name = "PO Rosalia Indah", Rating = 4, LogoUrl = "/images/bus/rosalia.svg" },
            new() { Code = "SBT", Name = "PO Sinar Jaya", Rating = 4, LogoUrl = "/images/bus/sinarjaya.svg" },
            new() { Code = "PHL", Name = "PO Pahala Kencana", Rating = 4, LogoUrl = "/images/bus/pahala.svg" },
            new() { Code = "HRP", Name = "PO Harapan Jaya", Rating = 3, LogoUrl = "/images/bus/harapan.svg" },
            new() { Code = "DAY", Name = "DayTrans Shuttle", Rating = 5, LogoUrl = "/images/bus/daytrans.svg" },
            new() { Code = "CTS", Name = "Cititrans Shuttle", Rating = 5, LogoUrl = "/images/bus/cititrans.svg" }
        };
        context.BusOperators.AddRange(busOperators);

        // === BUS & SHUTTLE FLEET ===
        var busServices = new List<BusService>
        {
            new() { Operator = busOperators[0], BusNumber = "RSL-EXE-01", Name = "Rosalia Indah Executive", ServiceType = "Bus", Class = "Eksekutif", SeatLayout = "2-2", TotalSeats = 32, HasWifi = true, HasToilet = true, HasRecliningSeat = true, HasEntertainment = true },
            new() { Operator = busOperators[0], BusNumber = "RSL-SLP-02", Name = "Rosalia Indah Sleeper", ServiceType = "Bus", Class = "Sleeper", SeatLayout = "1-1", TotalSeats = 21, HasWifi = true, HasToilet = true, HasRecliningSeat = true, HasEntertainment = true },
            new() { Operator = busOperators[1], BusNumber = "SJ-EKO-11", Name = "Sinar Jaya Ekonomi AC", ServiceType = "Bus", Class = "Ekonomi", SeatLayout = "2-3", TotalSeats = 59, HasWifi = false, HasToilet = false, HasRecliningSeat = false },
            new() { Operator = busOperators[2], BusNumber = "PHL-BIS-07", Name = "Pahala Kencana Bisnis", ServiceType = "Bus", Class = "Bisnis", SeatLayout = "2-2", TotalSeats = 40, HasWifi = true, HasToilet = true, HasRecliningSeat = true },
            new() { Operator = busOperators[3], BusNumber = "HRP-EKO-21", Name = "Harapan Jaya Ekonomi", ServiceType = "Bus", Class = "Ekonomi", SeatLayout = "2-3", TotalSeats = 55, HasWifi = false, HasToilet = false },
            new() { Operator = busOperators[4], BusNumber = "DAY-SHT-05", Name = "DayTrans Executive Shuttle", ServiceType = "Shuttle", Class = "Eksekutif", SeatLayout = "2-1", TotalSeats = 11, HasWifi = true, HasRecliningSeat = true, HasDoorToDoor = true },
            new() { Operator = busOperators[5], BusNumber = "CTS-SHT-09", Name = "Cititrans Premium Shuttle", ServiceType = "Shuttle", Class = "Eksekutif", SeatLayout = "2-1", TotalSeats = 9, HasWifi = true, HasRecliningSeat = true, HasEntertainment = true, HasDoorToDoor = true }
        };
        context.BusServices.AddRange(busServices);

        // === BUS & SHUTTLE SCHEDULES ===
        var today = DateTime.UtcNow.Date;
        var busSchedules = new List<BusSchedule>
        {
            new() { BusService = busServices[0], DepartureTerminal = terminals[0], ArrivalTerminal = terminals[5], DepartureTime = today.AddDays(1).AddHours(15), ArrivalTime = today.AddDays(2).AddHours(3), DurationMinutes = 720, BasePrice = 380000m, AvailableSeats = 18 },
            new() { BusService = busServices[1], DepartureTerminal = terminals[0], ArrivalTerminal = terminals[4], DepartureTime = today.AddDays(1).AddHours(16), ArrivalTime = today.AddDays(2).AddHours(8), DurationMinutes = 960, BasePrice = 620000m, AvailableSeats = 9 },
            new() { BusService = busServices[2], DepartureTerminal = terminals[1], ArrivalTerminal = terminals[3], DepartureTime = today.AddDays(1).AddHours(18), ArrivalTime = today.AddDays(2).AddHours(5), DurationMinutes = 660, BasePrice = 180000m, AvailableSeats = 41 },
            new() { BusService = busServices[3], DepartureTerminal = terminals[0], ArrivalTerminal = terminals[8], DepartureTime = today.AddDays(1).AddHours(20), ArrivalTime = today.AddDays(2).AddHours(6), DurationMinutes = 600, BasePrice = 275000m, AvailableSeats = 26 },
            new() { BusService = busServices[4], DepartureTerminal = terminals[1], ArrivalTerminal = terminals[7], DepartureTime = today.AddDays(1).AddHours(9), ArrivalTime = today.AddDays(1).AddHours(14), DurationMinutes = 300, BasePrice = 120000m, AvailableSeats = 38 },
            new() { BusService = busServices[3], DepartureTerminal = terminals[4], ArrivalTerminal = terminals[6], DepartureTime = today.AddDays(2).AddHours(7), ArrivalTime = today.AddDays(2).AddHours(10), DurationMinutes = 180, BasePrice = 95000m, AvailableSeats = 33 },
            new() { BusService = busServices[5], DepartureTerminal = terminals[0], ArrivalTerminal = terminals[2], DepartureTime = today.AddDays(1).AddHours(7), ArrivalTime = today.AddDays(1).AddHours(10).AddMinutes(30), DurationMinutes = 210, BasePrice = 165000m, AvailableSeats = 6 },
            new() { BusService = busServices[6], DepartureTerminal = terminals[0], ArrivalTerminal = terminals[2], DepartureTime = today.AddDays(1).AddHours(11), ArrivalTime = today.AddDays(1).AddHours(14).AddMinutes(15), DurationMinutes = 195, BasePrice = 185000m, AvailableSeats = 4 },
            new() { BusService = busServices[5], DepartureTerminal = terminals[2], ArrivalTerminal = terminals[0], DepartureTime = today.AddDays(1).AddHours(16), ArrivalTime = today.AddDays(1).AddHours(19).AddMinutes(30), DurationMinutes = 210, BasePrice = 165000m, AvailableSeats = 8 },
            new() { BusService = busServices[0], DepartureTerminal = terminals[5], ArrivalTerminal = terminals[0], DepartureTime = today.AddDays(2).AddHours(14), ArrivalTime = today.AddDays(3).AddHours(2), DurationMinutes = 720, BasePrice = 380000m, AvailableSeats = 22 },
            new() { BusService = busServices[2], DepartureTerminal = terminals[3], ArrivalTerminal = terminals[1], DepartureTime = today.AddDays(2).AddHours(17), ArrivalTime = today.AddDays(3).AddHours(4), DurationMinutes = 660, BasePrice = 180000m, AvailableSeats = 47 },
            new() { BusService = busServices[1], DepartureTerminal = terminals[4], ArrivalTerminal = terminals[0], DepartureTime = today.AddDays(2).AddHours(15), ArrivalTime = today.AddDays(3).AddHours(7), DurationMinutes = 960, BasePrice = 620000m, AvailableSeats = 12 }
        };
        context.BusSchedules.AddRange(busSchedules);

        // === HOTELS ===
        // AverageRating/ReviewCount are not set here: they are derived from the
        // seeded Approved reviews further down, same as at runtime.
        var hotels = new List<Hotel>
        {
            new() { Name = "Grand Hyatt Jakarta", Description = "Hotel bintang 5 di jantung Jakarta", Type = "Hotel", StarRating = 5, City = "Jakarta", Country = "Indonesia", ImageUrl = Img("1566665797739-1674de7a421a"), ImageUrls = Gallery("1566665797739-1674de7a421a", "1590490360182-c33d57733427", "1551882547-ff40c63fe5fa", "1496417263034-38ec4f0b665a"), Facilities = "[\"Pool\",\"Spa\",\"Gym\",\"Restaurant\",\"Bar\"]" },
            new() { Name = "Padma Resort Legian", Description = "Resor mewah dengan akses langsung ke pantai Legian", Type = "Resort", StarRating = 5, City = "Bali", Country = "Indonesia", ImageUrl = Img("1540541338287-41700207dee6"), ImageUrls = Gallery("1540541338287-41700207dee6", "1520250497591-112f2f40a3f4", "1571003123894-1f0594d2b5d9", "1601701119495-d6e39b664001"), Facilities = "[\"Pool\",\"Beach Access\",\"Spa\",\"Kids Club\"]" },
            new() { Name = "Amaris Hotel Malioboro", Description = "Hotel budget strategis di pusat Malioboro", Type = "Hotel", StarRating = 3, City = "Yogyakarta", Country = "Indonesia", ImageUrl = Img("1568495248636-6432b97bd949"), ImageUrls = Gallery("1568495248636-6432b97bd949", "1582719478250-c89cae4dc85b", "1621293954908-907159247fc8"), Facilities = "[\"WiFi\",\"AC\",\"Restaurant\"]" },
            new() { Name = "The Trans Luxury Hotel", Description = "Hotel premium di Bandung dengan fasilitas lengkap", Type = "Hotel", StarRating = 5, City = "Bandung", Country = "Indonesia", ImageUrl = Img("1551918120-9739cb430c6d"), ImageUrls = Gallery("1551918120-9739cb430c6d", "1589632732202-bd154e6e116d", "1568006511106-29531b8e9ab9", "1578683010236-d716f9a3f461"), Facilities = "[\"Pool\",\"Spa\",\"Gym\",\"Shopping Mall\"]" },
            new() { Name = "Villa Kayu Raja", Description = "Villa privat dengan kolam renang pribadi di Seminyak", Type = "Villa", StarRating = 5, City = "Bali", Country = "Indonesia", ImageUrl = Img("1520250497591-112f2f40a3f4"), ImageUrls = Gallery("1520250497591-112f2f40a3f4", "1601701119495-d6e39b664001", "1583847268964-b28dc8f51f92"), Facilities = "[\"Private Pool\",\"Kitchen\",\"Garden\"]" }
        };
        context.Hotels.AddRange(hotels);

        // === ROOMS ===
        var rooms = new List<Room>();
        foreach (var hotel in hotels)
        {
            rooms.AddRange(new[]
            {
                new Room { Hotel = hotel, Name = "Standard Room", Type = "Standard", Capacity = 2, PricePerNight = hotel.StarRating >= 5 ? 1200000m : 350000m, TotalRooms = 20, AvailableRooms = 15, HasBreakfast = true,
                    ImageUrl = Img("1631049307264-da0ec9d70304"),
                    ImageUrls = Gallery("1631049307264-da0ec9d70304", "1611892440504-42a792e24d32", "1590490360182-c33d57733427") },
                new Room { Hotel = hotel, Name = "Deluxe Room", Type = "Deluxe", Capacity = 2, PricePerNight = hotel.StarRating >= 5 ? 2000000m : 550000m, TotalRooms = 15, AvailableRooms = 8, HasBreakfast = true,
                    ImageUrl = Img("1618773928121-c32242e63f39"),
                    ImageUrls = Gallery("1618773928121-c32242e63f39", "1629140727571-9b5c6f6267b4", "1631049552057-403cdb8f0658", "1566665797739-1674de7a421a") },
                new Room { Hotel = hotel, Name = "Suite Room", Type = "Suite", Capacity = 4, PricePerNight = hotel.StarRating >= 5 ? 4500000m : 1200000m, TotalRooms = 5, AvailableRooms = 3, HasBreakfast = true,
                    ImageUrl = Img("1582719478250-c89cae4dc85b"),
                    ImageUrls = Gallery("1582719478250-c89cae4dc85b", "1578683010236-d716f9a3f461", "1621293954908-907159247fc8", "1583847268964-b28dc8f51f92") }
            });
        }
        context.Rooms.AddRange(rooms);

        // === HOTEL BOOKINGS (riwayat) ===
        // Menginap yang sudah lewat. Ini yang membuat lencana "Terverifikasi
        // menginap" pada ulasan punya dasar - ReviewService memeriksa booking
        // nyata, bukan klaim penulisnya.
        var hotelBookings = new List<HotelBooking>
        {
            new() { UserId = users[0].Id, Room = rooms[3], BookingCode = "JKA-HTL-000101", Status = "Completed", CheckInDate = DateTime.UtcNow.AddDays(-62), CheckOutDate = DateTime.UtcNow.AddDays(-59), Nights = 3, GuestCount = 2, TotalPrice = 3600000m, ContactEmail = users[0].Email, BookingDate = DateTime.UtcNow.AddDays(-70) },
            new() { UserId = users[1].Id, Room = rooms[4], BookingCode = "JKA-HTL-000102", Status = "Completed", CheckInDate = DateTime.UtcNow.AddDays(-61), CheckOutDate = DateTime.UtcNow.AddDays(-57), Nights = 4, GuestCount = 2, TotalPrice = 8000000m, ContactEmail = users[1].Email, BookingDate = DateTime.UtcNow.AddDays(-75) },
            new() { UserId = users[0].Id, Room = rooms[0], BookingCode = "JKA-HTL-000103", Status = "Completed", CheckInDate = DateTime.UtcNow.AddDays(-42), CheckOutDate = DateTime.UtcNow.AddDays(-40), Nights = 2, GuestCount = 1, TotalPrice = 2400000m, ContactEmail = users[0].Email, BookingDate = DateTime.UtcNow.AddDays(-50) },
            new() { UserId = users[1].Id, Room = rooms[1], BookingCode = "JKA-HTL-000104", Status = "Completed", CheckInDate = DateTime.UtcNow.AddDays(-22), CheckOutDate = DateTime.UtcNow.AddDays(-20), Nights = 2, GuestCount = 1, TotalPrice = 4000000m, ContactEmail = users[1].Email, BookingDate = DateTime.UtcNow.AddDays(-28) },
            new() { UserId = users[2].Id, Room = rooms[10], BookingCode = "JKA-HTL-000105", Status = "Completed", CheckInDate = DateTime.UtcNow.AddDays(-26), CheckOutDate = DateTime.UtcNow.AddDays(-24), Nights = 2, GuestCount = 2, TotalPrice = 4000000m, ContactEmail = users[2].Email, BookingDate = DateTime.UtcNow.AddDays(-33) }
        };
        context.HotelBookings.AddRange(hotelBookings);

        // === HOTEL REVIEWS ===
        // Real rows rather than a made-up ReviewCount: the rating on the hotel
        // card is derived from the Approved ones a few lines below, which is the
        // same rule ReviewService.RecalculateRatingAsync enforces at runtime.
        // The Pending and Rejected entries give the admin moderation queue
        // something to act on from a fresh install.
        var reviews = new List<HotelReview>
        {
            // Grand Hyatt Jakarta
            new() { Hotel = hotels[0], UserId = users[0].Id, AuthorName = "Budi Santoso", Rating = 5, Title = "Lokasi tak tertandingi", Comment = "Langsung terhubung ke mal dan stasiun MRT, jadi tidak perlu taksi sama sekali selama tiga hari. Sarapannya juga variatif.", Pros = "Lokasi, sarapan", Cons = "Parkir agak jauh", StayDate = DateTime.UtcNow.AddDays(-40), Status = "Approved", ModeratedBy = "admin@joka.id", ModeratedAt = DateTime.UtcNow.AddDays(-38) },
            new() { Hotel = hotels[0], UserId = users[1].Id, AuthorName = "Siti Nurhaliza", Rating = 4, Title = "Nyaman untuk kerja", Comment = "Kamar kedap suara dan meja kerjanya lega. WiFi stabil buat video call seharian. Kolam renangnya rame kalau akhir pekan.", Pros = "WiFi kencang", Cons = "Kolam ramai", StayDate = DateTime.UtcNow.AddDays(-20), Status = "Approved", ModeratedBy = "admin@joka.id", ModeratedAt = DateTime.UtcNow.AddDays(-18) },

            // Padma Resort Legian
            new() { Hotel = hotels[1], UserId = users[1].Id, AuthorName = "Siti Nurhaliza", Rating = 5, Title = "Sunset dari kamar", Comment = "Akses pantainya benar-benar langsung, tinggal jalan lewat taman belakang. Staf ingat nama anak saya sejak hari pertama.", Pros = "Akses pantai, staf ramah", Cons = "Harga makanan mahal", StayDate = DateTime.UtcNow.AddDays(-60), Status = "Approved", ModeratedBy = "admin@joka.id", ModeratedAt = DateTime.UtcNow.AddDays(-58) },
            new() { Hotel = hotels[1], UserId = users[2].Id, AuthorName = "Demo User", Rating = 5, Title = "Cocok untuk keluarga", Comment = "Kids club-nya niat, anak-anak betah sampai sore. Kamar keluarga muat berempat tanpa extra bed.", Pros = "Kids club", StayDate = DateTime.UtcNow.AddDays(-15), Status = "Approved", ModeratedBy = "admin@joka.id", ModeratedAt = DateTime.UtcNow.AddDays(-13) },

            // Amaris Malioboro
            new() { Hotel = hotels[2], UserId = users[0].Id, AuthorName = "Budi Santoso", Rating = 4, Title = "Sesuai harganya", Comment = "Kamar kecil tapi bersih dan ke Malioboro cuma jalan kaki lima menit. Untuk transit semalam sudah lebih dari cukup.", Pros = "Bersih, dekat Malioboro", Cons = "Kamar sempit", StayDate = DateTime.UtcNow.AddDays(-30), Status = "Approved", ModeratedBy = "admin@joka.id", ModeratedAt = DateTime.UtcNow.AddDays(-29) },

            // The Trans Luxury
            new() { Hotel = hotels[3], UserId = users[2].Id, AuthorName = "Demo User", Rating = 5, Title = "Kolam di lantai atas", Comment = "Pemandangan Bandung dari kolam renang lantai atas juara, apalagi menjelang malam. Check-in cepat walau sedang penuh.", Pros = "Kolam, check-in cepat", StayDate = DateTime.UtcNow.AddDays(-25), Status = "Approved", ModeratedBy = "admin@joka.id", ModeratedAt = DateTime.UtcNow.AddDays(-24) },
            new() { Hotel = hotels[3], UserId = users[1].Id, AuthorName = "Siti Nurhaliza", Rating = 4, Title = "Sarapan perlu ditambah", Comment = "Semua bagus kecuali antrean sarapan jam delapan yang panjang sekali. Sisanya tidak ada keluhan.", Cons = "Antre sarapan", StayDate = DateTime.UtcNow.AddDays(-10), Status = "Approved", ModeratedBy = "admin@joka.id", ModeratedAt = DateTime.UtcNow.AddDays(-9) },

            // Villa Kayu Raja
            new() { Hotel = hotels[4], UserId = users[0].Id, AuthorName = "Budi Santoso", Rating = 5, Title = "Privat betul", Comment = "Kolam pribadi dan dapur lengkap, jadi bisa masak sendiri. Cocok untuk yang tidak suka keramaian resor besar.", Pros = "Privasi, dapur lengkap", StayDate = DateTime.UtcNow.AddDays(-50), Status = "Approved", ModeratedBy = "admin@joka.id", ModeratedAt = DateTime.UtcNow.AddDays(-49) },

            // --- menunggu moderasi ---
            new() { Hotel = hotels[1], UserId = users[0].Id, AuthorName = "Budi Santoso", Rating = 2, Title = "AC bocor", Comment = "AC di kamar 214 menetes ke lantai semalaman dan baru ditangani setelah dua kali telepon. Sayang, karena selebihnya bagus.", Cons = "AC bocor, respons lambat", StayDate = DateTime.UtcNow.AddDays(-4), Status = "Pending" },
            new() { Hotel = hotels[0], UserId = users[2].Id, AuthorName = "Demo User", Rating = 5, Title = "PROMO TIKET MURAH KLIK LINK DI BIO", Comment = "Hotel bagus banget! Kunjungi juga toko saya untuk diskon tiket 90% cek profil ya kak, dijamin termurah se-Indonesia!!!", StayDate = DateTime.UtcNow.AddDays(-2), Status = "Pending" },
            new() { Hotel = hotels[2], UserId = users[1].Id, AuthorName = "Siti Nurhaliza", Rating = 3, Title = "Standar saja", Comment = "Tidak ada yang salah, tapi juga tidak ada yang berkesan. Kamar bersih, staf secukupnya, lokasi memang kuat.", StayDate = DateTime.UtcNow.AddDays(-6), Status = "Pending" },

            // --- pernah ditolak, dipakai untuk menunjukkan riwayat moderasi ---
            new() { Hotel = hotels[4], UserId = users[1].Id, AuthorName = "Siti Nurhaliza", Rating = 1, Title = "Kecewa", Comment = "Ulasan ini memuat tuduhan pribadi terhadap salah satu staf tanpa bukti, sehingga tidak ditayangkan.", StayDate = DateTime.UtcNow.AddDays(-35), Status = "Rejected", ModeratedBy = "admin@joka.id", ModeratedAt = DateTime.UtcNow.AddDays(-34), ModerationNote = "Menyebut nama staf dengan tuduhan yang tidak bisa diverifikasi." }
        };
        // Lencana "terverifikasi" tidak ditulis tangan: dihitung dari booking di
        // atas dengan aturan yang sama seperti ReviewService.SubmitAsync.
        foreach (var review in reviews)
        {
            review.IsVerified = hotelBookings.Any(b =>
                b.UserId == review.UserId && b.Room!.Hotel == review.Hotel);
        }

        context.HotelReviews.AddRange(reviews);

        // Rating hotel diturunkan dari ulasan Approved - bukan angka karangan.
        foreach (var hotel in hotels)
        {
            var approved = reviews.Where(r => r.Hotel == hotel && r.Status == "Approved").Select(r => r.Rating).ToList();
            hotel.ReviewCount = approved.Count;
            hotel.AverageRating = approved.Count == 0 ? 0 : Math.Round(approved.Average(), 2);
        }

        // === ACTIVITIES ===
        var activities = new List<Activity>
        {
            new() { Name = "Borobudur Sunrise Tour", Description = "Tur matahari terbit di Candi Borobudur", Category = "Tour", City = "Yogyakarta", Location = "Candi Borobudur", Price = 350000m, DurationMinutes = 240, TotalTickets = 100, SoldTickets = 45, AverageRating = 4.8, ReviewCount = 320, ImageUrl = Img("1501179691627-eeaa65ea017c"), ImageUrls = Gallery("1501179691627-eeaa65ea017c", "1544644181-1484b3fdfc62", "1532186651327-6ac23687d189") },
            new() { Name = "Bali Swing Experience", Description = "Pengalaman ayunan ekstrem dengan pemandangan sawah", Category = "Attraction", City = "Bali", Location = "Ubud", Price = 250000m, DurationMinutes = 120, TotalTickets = 200, SoldTickets = 150, AverageRating = 4.6, ReviewCount = 540, ImageUrl = Img("1524675053444-52c3ca294ad2"), ImageUrls = Gallery("1524675053444-52c3ca294ad2", "1530080338378-25af8876ae2e", "1592364395653-83e648b20cc2") },
            new() { Name = "Raja Ampat Diving", Description = "Snorkeling dan diving di surga bawah laut Raja Ampat", Category = "Sports", City = "Raja Ampat", Location = "Pulau Wayag", Price = 1500000m, DurationMinutes = 480, TotalTickets = 30, SoldTickets = 12, AverageRating = 4.9, ReviewCount = 180, ImageUrl = Img("1544644181-1484b3fdfc62"), ImageUrls = Gallery("1544644181-1484b3fdfc62", "1578469550956-0e16b69c6a3d", "1559998560-deb30d24ea50", "1620549146396-9024d914cd99") },
            new() { Name = "Java Jazz Festival 2025", Description = "Festival musik jazz terbesar di Indonesia", Category = "Concert", City = "Jakarta", Location = "JIExpo Kemayoran", Price = 750000m, EventDate = DateTime.UtcNow.AddDays(30), TotalTickets = 5000, SoldTickets = 3200, AverageRating = 4.5, ReviewCount = 890, ImageUrl = Img("1553902000-e036b7d05af5"), ImageUrls = Gallery("1553902000-e036b7d05af5", "1444194563460-454833ba6005", "1542897643-8158da5b4607") },
            new() { Name = "Cooking Class Bali", Description = "Belajar masak masakan tradisional Bali", Category = "Workshop", City = "Bali", Location = "Ubud Cooking Studio", Price = 400000m, DurationMinutes = 180, TotalTickets = 50, SoldTickets = 28, AverageRating = 4.7, ReviewCount = 210, ImageUrl = Img("1542897643-8158da5b4607"), ImageUrls = Gallery("1542897643-8158da5b4607", "1532186651327-6ac23687d189", "1592364395653-83e648b20cc2") }
        };
        context.Activities.AddRange(activities);

        // === CAR RENTALS ===
        var cars = new List<CarRental>
        {
            new() { Name = "Toyota Avanza", Type = "MPV", Seats = 7, Transmission = "Automatic", PricePerDay = 350000m, IncludeDriver = false, DriverPricePerDay = 150000m, TotalUnits = 10, AvailableUnits = 7, PickupLocations = "[\"Jakarta\",\"Bandung\",\"Surabaya\",\"Bali\"]", ImageUrl = Img("1519641471654-76ce0107ad1b"), ImageUrls = Gallery("1519641471654-76ce0107ad1b", "1511527844068-006b95d162c2") },
            new() { Name = "Honda Brio", Type = "Hatchback", Seats = 5, Transmission = "Automatic", PricePerDay = 250000m, IncludeDriver = false, DriverPricePerDay = 150000m, TotalUnits = 8, AvailableUnits = 5, PickupLocations = "[\"Jakarta\",\"Bandung\"]", ImageUrl = Img("1517942491415-4fc176d3c2f7"), ImageUrls = Gallery("1517942491415-4fc176d3c2f7", "1532931899774-fbd4de0008fb") },
            new() { Name = "Mitsubishi Xpander", Type = "MPV", Seats = 7, Transmission = "Automatic", PricePerDay = 400000m, IncludeDriver = false, DriverPricePerDay = 150000m, TotalUnits = 5, AvailableUnits = 3, PickupLocations = "[\"Jakarta\",\"Surabaya\",\"Bali\"]", ImageUrl = Img("1533473359331-0135ef1b58bf"), ImageUrls = Gallery("1533473359331-0135ef1b58bf", "1557825631-19082bca3803") },
            new() { Name = "Toyota Fortuner", Type = "SUV", Seats = 7, Transmission = "Automatic", PricePerDay = 750000m, IncludeDriver = true, DriverPricePerDay = 200000m, TotalUnits = 3, AvailableUnits = 2, PickupLocations = "[\"Jakarta\",\"Bali\"]", ImageUrl = Img("1618353482480-61ca5a9a7879"), ImageUrls = Gallery("1618353482480-61ca5a9a7879", "1615887110697-0819ec23465f", "1604657645490-c228a616c7ee") }
        };
        context.CarRentals.AddRange(cars);

        // === PROMO VOUCHERS ===
        var vouchers = new List<PromoVoucher>
        {
            new() { Code = "JOKA50", Name = "Diskon 50%", Description = "Diskon 50% pembelian pertama, maks Rp100rb", Type = "Percentage", Value = 50m, MinPurchase = 100000m, MaxDiscount = 100000m, ValidFrom = DateTime.UtcNow.AddDays(-30), ValidUntil = DateTime.UtcNow.AddDays(60), TotalQuota = 1000, UsedCount = 234, ApplicableTo = "All" },
            new() { Code = "FLYHIGH", Name = "Diskon Tiket Pesawat", Description = "Potongan Rp150rb untuk tiket pesawat", Type = "FixedAmount", Value = 150000m, MinPurchase = 1000000m, MaxDiscount = 150000m, ValidFrom = DateTime.UtcNow.AddDays(-7), ValidUntil = DateTime.UtcNow.AddDays(30), TotalQuota = 500, UsedCount = 123, ApplicableTo = "Flight" },
            new() { Code = "STAYWELL", Name = "Cashback Hotel 20%", Description = "Cashback 20% untuk booking hotel", Type = "Cashback", Value = 20m, MinPurchase = 500000m, MaxDiscount = 200000m, ValidFrom = DateTime.UtcNow.AddDays(-14), ValidUntil = DateTime.UtcNow.AddDays(45), TotalQuota = 300, UsedCount = 87, ApplicableTo = "Hotel" },
            new() { Code = "WELCOME25", Name = "Welcome Bonus IDR 25K", Description = "Voucher welcome bonus", Type = "FixedAmount", Value = 25000m, MinPurchase = 100000m, MaxDiscount = 25000m, ValidFrom = DateTime.UtcNow.AddDays(-90), ValidUntil = DateTime.UtcNow.AddDays(90), TotalQuota = 5000, UsedCount = 1250, ApplicableTo = "All" }
        };
        context.PromoVouchers.AddRange(vouchers);

        // === INSURANCE ===
        var insurances = new List<TravelInsurance>
        {
            new() { Name = "Perlindungan Basic", Provider = "JokaSure", Description = "Proteksi keterlambatan", Coverage = "Basic", Price = 25000m, Benefits = "[\"Delay\",\"Cancellation\"]" },
            new() { Name = "Perlindungan Standard", Provider = "JokaSure", Description = "Proteksi komprehensif domestik", Coverage = "Standard", Price = 50000m, Benefits = "[\"Delay\",\"Cancellation\",\"Medical\",\"Baggage\"]" },
            new() { Name = "Perlindungan Premium", Provider = "JokaSure", Description = "Proteksi lengkap internasional", Coverage = "Premium", Price = 100000m, Benefits = "[\"Delay\",\"Cancellation\",\"Medical\",\"Baggage\",\"Accident\",\"Evacuation\"]" }
        };
        context.TravelInsurances.AddRange(insurances);

        // === PACKAGES ===
        var packages = new List<TravelPackage>
        {
            new() { Name = "Bali Honeymoon Package", Description = "Paket romantis 4H3M di Bali", Destination = "Bali", DurationDays = 4, Price = 5500000m, Includes = "[\"Hotel 5★\",\"Flight CGK-DPS PP\",\"Tour Ubud\",\"Romantic Dinner\"]", ImageUrl = Img("1540541338287-41700207dee6"), ImageUrls = Gallery("1540541338287-41700207dee6", "1520250497591-112f2f40a3f4", "1524675053444-52c3ca294ad2", "1601701119495-d6e39b664001") },
            new() { Name = "Yogyakarta Cultural Trip", Description = "Jelajahi budaya dan sejarah Yogyakarta", Destination = "Yogyakarta", DurationDays = 3, Price = 2500000m, Includes = "[\"Hotel 4★\",\"Borobudur Tour\",\"Malioboro Walk\",\"Kraton Visit\"]", ImageUrl = Img("1501179691627-eeaa65ea017c"), ImageUrls = Gallery("1501179691627-eeaa65ea017c", "1568495248636-6432b97bd949", "1532186651327-6ac23687d189") },
            new() { Name = "Labuan Bajo Adventure", Description = "Petualangan ke surga Flores", Destination = "Labuan Bajo", DurationDays = 5, Price = 8500000m, Includes = "[\"Flight + Hotel\",\"Komodo Tour\",\"Snorkeling\",\"Island Hopping\"]", ImageUrl = Img("1578469550956-0e16b69c6a3d"), ImageUrls = Gallery("1578469550956-0e16b69c6a3d", "1544644181-1484b3fdfc62", "1559998560-deb30d24ea50", "1620549146396-9024d914cd99") }
        };
        context.TravelPackages.AddRange(packages);

        // === LINK PRODUCTS TO PARTNERS (Fase B) ===
        // Ownership is explicit now, so the merchant portal can filter on it
        // instead of guessing from the category.
        hotels[1].MerchantId = merchants[0].Id;   // Padma Resort Legian
        hotels[4].MerchantId = merchants[0].Id;   // Villa Kayu Raja

        foreach (var f in flights.Where(f => f.FlightNumber.StartsWith("GA-")))
            f.MerchantId = merchants[1].Id;       // Garuda Indonesia

        busServices[5].MerchantId = merchants[2].Id;  // DayTrans Executive Shuttle
        busServices[6].MerchantId = merchants[2].Id;  // Cititrans (dikelola DayTrans di demo)

        activities[3].MerchantId = merchants[3].Id;   // Java Jazz Festival

        // === BACK OFFICE SAMPLE DATA ===
        var now = DateTime.UtcNow;

        context.ApiIntegrations.AddRange(
            new ApiIntegration { Name = "Garuda NDC", Provider = "Garuda Indonesia", Category = "Flight", BaseUrl = "https://api.garuda-indonesia.com/ndc", Environment = "Sandbox", Status = "Connected", LastSyncAt = now.AddMinutes(-8), LastLatencyMs = 320 },
            new ApiIntegration { Name = "KAI Ticketing", Provider = "PT Kereta Api Indonesia", Category = "Train", BaseUrl = "https://api.kai.id", Environment = "Sandbox", Status = "NotConfigured", IsEnabled = false, LastError = "API key belum diisi di appsettings" },
            new ApiIntegration { Name = "Midtrans Snap", Provider = "Midtrans", Category = "Payment", BaseUrl = "https://api.sandbox.midtrans.com", Environment = "Sandbox", Status = "NotConfigured", IsEnabled = false, LastError = "ServerKey belum diisi" },
            new ApiIntegration { Name = "DayTrans Fleet", Provider = "DayTrans", Category = "Bus", BaseUrl = "https://partner.daytrans.co.id/api", Environment = "Production", Status = "Degraded", LastSyncAt = now.AddHours(-3), LastLatencyMs = 2450, LastError = "Latency di atas ambang 2000ms" },
            new ApiIntegration { Name = "Google Maps Places", Provider = "Google", Category = "Maps", BaseUrl = "https://maps.googleapis.com", Environment = "Production", Status = "NotConfigured", IsEnabled = false }
        );

        context.SystemHealthChecks.AddRange(
            new SystemHealthCheck { Component = "Web (Blazor Server)", Status = "Healthy", ResponseTimeMs = 42, UptimePercent = 99.98, ErrorCount24h = 0, CheckedAt = now },
            new SystemHealthCheck { Component = "Database (SQLite)", Status = "Healthy", ResponseTimeMs = 6, UptimePercent = 100, ErrorCount24h = 0, CheckedAt = now },
            new SystemHealthCheck { Component = "Storage (FileSystem)", Status = "Healthy", ResponseTimeMs = 11, UptimePercent = 100, ErrorCount24h = 0, CheckedAt = now },
            new SystemHealthCheck { Component = "ChatBot (Semantic Kernel)", Status = "Healthy", ResponseTimeMs = 1180, UptimePercent = 99.4, ErrorCount24h = 2, Message = "Latensi tergantung provider LLM", CheckedAt = now },
            new SystemHealthCheck { Component = "Payment Gateway", Status = "Down", ResponseTimeMs = 0, UptimePercent = 0, ErrorCount24h = 0, Message = "Belum dikonfigurasi", CheckedAt = now }
        );

        context.FraudAlerts.AddRange(
            new FraudAlert { TransactionCode = "JKA-PAY-260726-8891", UserEmail = "blocked@example.com", Rule = "VelocityCheck", Reason = "7 transaksi dalam 10 menit dari 1 akun", RiskScore = 92, Amount = 14500000m, Severity = "Critical", Status = "Confirmed", ReviewedBy = "admin@joka.id", ReviewedAt = now.AddDays(-5), ReviewNote = "Akun diblokir." },
            new FraudAlert { TransactionCode = "JKA-PAY-260727-1043", UserEmail = "budi@example.com", Rule = "GeoMismatch", Reason = "Kartu terdaftar di Jakarta, transaksi dari IP luar negeri", RiskScore = 68, Amount = 2500000m, Severity = "High", Status = "Reviewing" },
            new FraudAlert { TransactionCode = "JKA-PAY-260727-1120", UserEmail = "siti@example.com", Rule = "AmountAnomaly", Reason = "Nominal 9x lebih tinggi dari rata-rata akun", RiskScore = 55, Amount = 8500000m, Severity = "Medium", Status = "Open" },
            new FraudAlert { TransactionCode = "JKA-PAY-260727-1315", UserEmail = "demo@joka.id", Rule = "CardTesting", Reason = "3 kartu berbeda gagal berurutan", RiskScore = 41, Amount = 450000m, Severity = "Low", Status = "Cleared", ReviewedBy = "operator@joka.id", ReviewedAt = now.AddHours(-6), ReviewNote = "Salah input, sudah dikonfirmasi via telepon." }
        );

        context.RefundRequests.AddRange(
            new RefundRequest { BookingCode = "JKA-260727-4821", BookingType = "flight", CustomerName = "Budi Santoso", RequestType = "Refund", Reason = "Sakit, ada surat dokter", Amount = 1200000m, Status = "Pending" },
            new RefundRequest { BookingCode = "JKA-BS-260727-2210", BookingType = "bus", CustomerName = "Siti Nurhaliza", RequestType = "Reschedule", Reason = "Rapat mendadak", Amount = 165000m, NewDepartureDate = now.AddDays(4), Status = "Pending" },
            new RefundRequest { BookingCode = "JKA-HT-260726-9930", BookingType = "hotel", CustomerName = "Demo User", RequestType = "Refund", Reason = "Salah tanggal check-in", Amount = 2400000m, Status = "Approved", HandledBy = "operator@joka.id", HandledAt = now.AddHours(-20), HandlingNote = "Refund 80% sesuai kebijakan." }
        );

        context.IncidentReports.AddRange(
            new IncidentReport { Title = "Pembayaran QRIS gagal berulang", Category = "Technical", Severity = "High", Description = "Beberapa pelanggan melaporkan QRIS timeout saat checkout.", ReportedBy = "operator@joka.id", Status = "InProgress", AssignedTo = "admin@joka.id" },
            new IncidentReport { Title = "Jadwal bus DayTrans tidak sinkron", Category = "Partner", Severity = "Medium", Description = "Kursi tersedia di Joka tidak cocok dengan sistem partner.", RelatedBookingCode = "JKA-BS-260727-2210", ReportedBy = "operator@joka.id", Status = "Open" },
            new IncidentReport { Title = "Dugaan penyalahgunaan voucher JOKA50", Category = "Fraud", Severity = "High", Description = "Satu akun memakai voucher 12 kali dalam sehari.", ReportedBy = "operator@joka.id", Status = "Resolved", AssignedTo = "admin@joka.id", Resolution = "Kuota per akun dibatasi.", ResolvedAt = now.AddDays(-2) }
        );

        // === TRANSPORTASI LOKAL ===
        // Tarif mengikuti pola nyata: ojek/mobil dihitung per kilometer dengan
        // tarif minimum, airport transfer memakai harga rute tetap.
        var transportProviders = new List<TransportProvider>
        {
            new() { Code = "JEK", Name = "JekRide", Description = "Ojek dan mobil online, tersedia 24 jam.", Rating = 4.6, LogoUrl = Img("1558981806-ec527fa84c39", 200) },
            new() { Code = "BLB", Name = "Bluebird Group", Description = "Taksi resmi dengan argo dan armada terawat.", Rating = 4.7, LogoUrl = Img("1549194388-f61be84a6e9e", 200) },
            new() { Code = "JKT", Name = "Joka Airport Transfer", Description = "Penjemputan bandara dengan harga rute tetap, sopir menunggu di kedatangan.", Rating = 4.8, LogoUrl = Img("1436491865332-7a61a109cc05", 200) }
        };
        context.TransportProviders.AddRange(transportProviders);

        var transportOptions = new List<TransportOption>
        {
            // --- ride hailing, per kilometer ---
            new() { Provider = transportProviders[0], Name = "Ojek Instan", Description = "Motor, paling cepat menembus macet.", ServiceType = "RideHailing", VehicleType = "Motorcycle", City = "Jakarta", Capacity = 1, PricingMode = "PerKm", BasePrice = 5000m, PricePerKm = 2500m, MinimumFare = 10000m, EstimatedMinutes = 10, ImageUrl = Img("1558981806-ec527fa84c39") },
            new() { Provider = transportProviders[0], Name = "Mobil Hemat", Description = "Mobil ber-AC untuk 4 penumpang.", ServiceType = "RideHailing", VehicleType = "Car", City = "Jakarta", Capacity = 4, PricingMode = "PerKm", BasePrice = 10000m, PricePerKm = 4500m, MinimumFare = 20000m, EstimatedMinutes = 15, ImageUrl = Img("1449965408869-eaa3f722e40d") },
            new() { Provider = transportProviders[1], Name = "Taksi Reguler", Description = "Taksi resmi dengan argo.", ServiceType = "RideHailing", VehicleType = "Car", City = "Jakarta", Capacity = 4, PricingMode = "PerKm", BasePrice = 12000m, PricePerKm = 5000m, MinimumFare = 25000m, EstimatedMinutes = 15, ImageUrl = Img("1549194388-f61be84a6e9e") },
            new() { Provider = transportProviders[0], Name = "Ojek Instan", Description = "Motor, cocok untuk jarak pendek.", ServiceType = "RideHailing", VehicleType = "Motorcycle", City = "Bali", Capacity = 1, PricingMode = "PerKm", BasePrice = 5000m, PricePerKm = 2800m, MinimumFare = 12000m, EstimatedMinutes = 10, ImageUrl = Img("1558981806-ec527fa84c39") },
            new() { Provider = transportProviders[0], Name = "Mobil Hemat", Description = "Mobil ber-AC untuk 4 penumpang.", ServiceType = "RideHailing", VehicleType = "Car", City = "Bali", Capacity = 4, PricingMode = "PerKm", BasePrice = 12000m, PricePerKm = 5000m, MinimumFare = 25000m, EstimatedMinutes = 15, ImageUrl = Img("1449965408869-eaa3f722e40d") },
            new() { Provider = transportProviders[0], Name = "Ojek Instan", Description = "Motor, keliling kota pelajar.", ServiceType = "RideHailing", VehicleType = "Motorcycle", City = "Yogyakarta", Capacity = 1, PricingMode = "PerKm", BasePrice = 4000m, PricePerKm = 2200m, MinimumFare = 9000m, EstimatedMinutes = 10, ImageUrl = Img("1558981806-ec527fa84c39") },
            new() { Provider = transportProviders[0], Name = "Ojek Instan", Description = "Motor, lincah di jalan sempit Bandung.", ServiceType = "RideHailing", VehicleType = "Motorcycle", City = "Bandung", Capacity = 1, PricingMode = "PerKm", BasePrice = 4000m, PricePerKm = 2300m, MinimumFare = 9000m, EstimatedMinutes = 10, ImageUrl = Img("1558981806-ec527fa84c39") },
            new() { Provider = transportProviders[0], Name = "Mobil Hemat", Description = "Mobil ber-AC untuk 4 penumpang.", ServiceType = "RideHailing", VehicleType = "Car", City = "Bandung", Capacity = 4, PricingMode = "PerKm", BasePrice = 9000m, PricePerKm = 4000m, MinimumFare = 18000m, EstimatedMinutes = 15, ImageUrl = Img("1449965408869-eaa3f722e40d") },

            // --- airport transfer, harga rute tetap ---
            new() { Provider = transportProviders[2], Name = "Airport Transfer Reguler", Description = "Sedan, sopir menunggu di pintu kedatangan dengan papan nama.", ServiceType = "AirportTransfer", VehicleType = "Car", City = "Jakarta", Capacity = 3, PricingMode = "Flat", BasePrice = 185000m, AirportCode = "CGK", RouteArea = "Jakarta Pusat & Selatan", EstimatedMinutes = 70, ImageUrl = Img("1436491865332-7a61a109cc05") },
            new() { Provider = transportProviders[2], Name = "Airport Transfer MPV", Description = "MPV untuk keluarga dengan bagasi besar.", ServiceType = "AirportTransfer", VehicleType = "MPV", City = "Jakarta", Capacity = 6, PricingMode = "Flat", BasePrice = 265000m, AirportCode = "CGK", RouteArea = "Jakarta Pusat & Selatan", EstimatedMinutes = 75, ImageUrl = Img("1533473359331-0135ef1b58bf") },
            new() { Provider = transportProviders[2], Name = "Airport Transfer Premium", Description = "Sedan premium, air mineral dan wifi di mobil.", ServiceType = "AirportTransfer", VehicleType = "Premium", City = "Jakarta", Capacity = 3, PricingMode = "Flat", BasePrice = 425000m, AirportCode = "CGK", RouteArea = "Jakarta Pusat & Selatan", EstimatedMinutes = 70, ImageUrl = Img("1550355291-bbee04a92027") },
            new() { Provider = transportProviders[2], Name = "Airport Transfer Reguler", Description = "Sedan menuju kawasan Kuta, Legian, dan Seminyak.", ServiceType = "AirportTransfer", VehicleType = "Car", City = "Bali", Capacity = 3, PricingMode = "Flat", BasePrice = 150000m, AirportCode = "DPS", RouteArea = "Kuta / Legian / Seminyak", EstimatedMinutes = 40, ImageUrl = Img("1436491865332-7a61a109cc05") },
            new() { Provider = transportProviders[2], Name = "Airport Transfer MPV", Description = "MPV menuju Ubud dan sekitarnya.", ServiceType = "AirportTransfer", VehicleType = "MPV", City = "Bali", Capacity = 6, PricingMode = "Flat", BasePrice = 350000m, AirportCode = "DPS", RouteArea = "Ubud", EstimatedMinutes = 90, ImageUrl = Img("1533473359331-0135ef1b58bf") },
            new() { Provider = transportProviders[2], Name = "Airport Transfer Reguler", Description = "Sedan dari YIA ke pusat kota Yogyakarta.", ServiceType = "AirportTransfer", VehicleType = "Car", City = "Yogyakarta", Capacity = 3, PricingMode = "Flat", BasePrice = 195000m, AirportCode = "YIA", RouteArea = "Malioboro & pusat kota", EstimatedMinutes = 85, ImageUrl = Img("1436491865332-7a61a109cc05") },
            new() { Provider = transportProviders[2], Name = "Airport Transfer Reguler", Description = "Sedan dari Juanda ke pusat kota Surabaya.", ServiceType = "AirportTransfer", VehicleType = "Car", City = "Surabaya", Capacity = 3, PricingMode = "Flat", BasePrice = 165000m, AirportCode = "SUB", RouteArea = "Surabaya Pusat", EstimatedMinutes = 45, ImageUrl = Img("1436491865332-7a61a109cc05") }
        };
        context.TransportOptions.AddRange(transportOptions);

        // === LIVE AGENT ===
        // Satu tiket belum tersentuh, satu sedang ditangani, satu sudah selesai -
        // supaya antrean operator punya ketiga keadaan sejak awal.
        var ticketUnclaimed = new SupportTicket
        {
            TicketCode = "JKA-CS-260727-1042", UserId = users[0].Id, CustomerName = "Budi Santoso", CustomerEmail = users[0].Email,
            Subject = "Nama di e-ticket salah ketik", Category = "Booking", Priority = "Normal",
            RelatedBookingCode = "JKA-260727-4821", Status = "Open", LastMessageAt = now.AddMinutes(-35)
        };

        var ticketAssigned = new SupportTicket
        {
            TicketCode = "JKA-CS-260727-0915", UserId = users[1].Id, CustomerName = "Siti Nurhaliza", CustomerEmail = users[1].Email,
            Subject = "Refund belum masuk setelah 5 hari", Category = "Refund", Priority = "High",
            RelatedBookingCode = "JKA-HT-260726-9930", Status = "Assigned",
            AssignedTo = "operator@joka.id", AssignedAt = now.AddHours(-2), LastMessageAt = now.AddMinutes(-12)
        };

        var ticketResolved = new SupportTicket
        {
            TicketCode = "JKA-CS-260725-3388", UserId = users[2].Id, CustomerName = "Demo User", CustomerEmail = users[2].Email,
            Subject = "Tidak bisa pilih kursi di kereta", Category = "Technical", Priority = "Normal",
            Status = "Resolved", AssignedTo = "operator@joka.id", AssignedAt = now.AddDays(-2),
            ResolvedAt = now.AddDays(-2).AddHours(1), ResolutionNote = "Cache browser dibersihkan, peta kursi tampil normal.",
            LastMessageAt = now.AddDays(-2).AddHours(1)
        };

        context.SupportTickets.AddRange(ticketUnclaimed, ticketAssigned, ticketResolved);

        context.SupportMessages.AddRange(
            new SupportMessage { Ticket = ticketUnclaimed, Sender = "Customer", SenderName = "Budi Santoso", Body = "Halo, nama saya di e-ticket tertulis 'Budi Santso', kurang huruf O. Apakah masih bisa diperbaiki sebelum keberangkatan besok?", SentAt = now.AddMinutes(-35) },

            new SupportMessage { Ticket = ticketAssigned, Sender = "Customer", SenderName = "Siti Nurhaliza", Body = "Refund hotel saya disetujui lima hari lalu tapi dananya belum masuk ke rekening. Nomor bookingnya JKA-HT-260726-9930.", SentAt = now.AddHours(-3), IsRead = true },
            new SupportMessage { Ticket = ticketAssigned, Sender = "Agent", SenderName = "Dedi Operator", Body = "Halo Kak Siti, saya cek dulu ya. Refund ke bank biasanya 3-7 hari kerja tergantung bank penerima.", SentAt = now.AddHours(-2), IsRead = true },
            new SupportMessage { Ticket = ticketAssigned, Sender = "Customer", SenderName = "Siti Nurhaliza", Body = "Sudah lewat 5 hari kerja kak. Boleh minta bukti transfernya?", SentAt = now.AddMinutes(-12) },

            new SupportMessage { Ticket = ticketResolved, Sender = "Customer", SenderName = "Demo User", Body = "Halaman pilih kursi kereta tidak memunculkan gerbong apa pun, cuma muter terus loadingnya.", SentAt = now.AddDays(-2), IsRead = true },
            new SupportMessage { Ticket = ticketResolved, Sender = "Agent", SenderName = "Dedi Operator", Body = "Coba bersihkan cache browser lalu buka ulang halamannya ya kak.", SentAt = now.AddDays(-2).AddMinutes(30), IsRead = true },
            new SupportMessage { Ticket = ticketResolved, Sender = "Customer", SenderName = "Demo User", Body = "Sudah bisa, terima kasih!", SentAt = now.AddDays(-2).AddHours(1), IsRead = true }
        );

        context.MerchantSettlements.AddRange(
            new MerchantSettlement { Merchant = merchants[0], ReferenceNo = "STL-2607-PDM-01", PeriodStart = now.AddDays(-30), PeriodEnd = now.AddDays(-16), TransactionCount = 148, GrossAmount = 412500000m, CommissionAmount = 49500000m, NetAmount = 363000000m, Status = "Paid", BankReference = "BCA/TRF/99182", PaidAt = now.AddDays(-12) },
            new MerchantSettlement { Merchant = merchants[0], ReferenceNo = "STL-2607-PDM-02", PeriodStart = now.AddDays(-15), PeriodEnd = now.AddDays(-1), TransactionCount = 132, GrossAmount = 388000000m, CommissionAmount = 46560000m, NetAmount = 341440000m, Status = "Reconciled" },
            new MerchantSettlement { Merchant = merchants[1], ReferenceNo = "STL-2607-GIA-01", PeriodStart = now.AddDays(-15), PeriodEnd = now.AddDays(-1), TransactionCount = 96, GrossAmount = 210000000m, CommissionAmount = 16800000m, NetAmount = 193200000m, Status = "Pending" },
            new MerchantSettlement { Merchant = merchants[2], ReferenceNo = "STL-2607-DAY-01", PeriodStart = now.AddDays(-15), PeriodEnd = now.AddDays(-1), TransactionCount = 54, GrossAmount = 9350000m, CommissionAmount = 1402500m, NetAmount = 7947500m, Status = "Disputed", VarianceAmount = -250000m, Notes = "Selisih dengan rekening koran, menunggu klarifikasi partner." }
        );

        context.ApprovalRequests.AddRange(
            new ApprovalRequest { Merchant = merchants[0], EntityType = "Room", ChangeType = "Update", Summary = "Naikkan harga Suite Room Padma Legian dari Rp4.500.000 ke Rp4.950.000", RequestedBy = "merchant@joka.id", Status = "Pending", PayloadJson = "{\"PricePerNight\":4950000}" },
            new ApprovalRequest { Merchant = merchants[2], EntityType = "BusSchedule", ChangeType = "Create", Summary = "Tambah keberangkatan Jakarta-Bandung 05.30", RequestedBy = "merchant2@joka.id", Status = "Pending", PayloadJson = "{\"DepartureTime\":\"05:30\",\"BasePrice\":155000}" },
            new ApprovalRequest { Merchant = merchants[1], EntityType = "Flight", ChangeType = "Update", Summary = "Ubah bagasi GA-201 dari 20kg ke 25kg", RequestedBy = "merchant@joka.id", Status = "Approved", ReviewedBy = "admin@joka.id", ReviewedAt = now.AddDays(-3), ReviewNote = "Sesuai kebijakan maskapai." },
            new ApprovalRequest { Merchant = merchants[3], EntityType = "Activity", ChangeType = "Create", Summary = "Daftarkan Java Jazz Festival 2026", RequestedBy = "merchant@joka.id", Status = "Rejected", ReviewedBy = "admin@joka.id", ReviewedAt = now.AddDays(-1), ReviewNote = "Dokumen izin keramaian belum dilampirkan." }
        );

        context.AuditLogs.AddRange(
            new AuditLog { EntityName = "User", EntityId = "blocked@example.com", Action = "Block", Changes = "IsBlocked: false -> true", UserId = "admin@joka.id", Timestamp = now.AddDays(-5) },
            new AuditLog { EntityName = "PromoVoucher", EntityId = "JOKA50", Action = "Update", Changes = "TotalQuota: 500 -> 1000", UserId = "admin@joka.id", Timestamp = now.AddDays(-4) },
            new AuditLog { EntityName = "ApprovalRequest", EntityId = "Flight/GA-201", Action = "Approve", Changes = "BaggageAllowanceKg: 20 -> 25", UserId = "admin@joka.id", Timestamp = now.AddDays(-3) },
            new AuditLog { EntityName = "RefundRequest", EntityId = "JKA-HT-260726-9930", Action = "Approve", Changes = "Status: Pending -> Approved", UserId = "operator@joka.id", Timestamp = now.AddHours(-20) },
            new AuditLog { EntityName = "ApiIntegration", EntityId = "DayTrans Fleet", Action = "Update", Changes = "Status: Connected -> Degraded", UserId = "system", Timestamp = now.AddHours(-3) }
        );

        await context.SaveChangesAsync();
    }
}
