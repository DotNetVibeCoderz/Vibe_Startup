using FastRide.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FastRide.Data;

/// <summary>
/// Seeds realistic sample data using BCrypt password hashing.
/// All demo accounts use password: "Password123"
/// </summary>
public static class SampleDataSeeder
{
    private static readonly Random _random = new(42);
    private static readonly string _demoPassword = BCrypt.Net.BCrypt.HashPassword("Password123", 12);

    // 50+ first names to safely generate 50 riders
    private static readonly string[] RiderFirstNames = new[]
    {
        "Budi","Siti","Ahmad","Dewi","Rina","Hendra","Mega","Doni",
        "Ratna","Agus","Fitri","Bambang","Lina","Eko","Putri","Wawan",
        "Dian","Rudi","Yanti","Slamet","Intan","Joko","Tuti","Adi",
        "Maya","Feri","Citra","Gunawan","Sari","Hadi","Nia","Rizky",
        "Dina","Bayu","Rani","Irfan","Vina","Arif","Susan","Denny",
        "Indah","Galih","Retno","Yoga","Wulan","Bagus","Nita","Faisal",
        "Laras","Teguh","Kiki","Syifa"
    };

    private static readonly string[] DriverFirstNames = new[]
    {
        "Andi","Surya","Dedi","Yusuf","Heri","Tono","Rahmat","Supri",
        "Joko","Anton","Bowo","Karno","Udin","Maman","Cecep","Asep",
        "Nana","Iwan","Dadang","Ujang","Saepul","Asep K.","Doddy","Robby",
        "Benny","Ricky","Iqbal","Fajar","Gilang","Widodo"
    };

    private static readonly string[] DriverSurnames = new[]
    {
        "Santoso","Wijaya","Kusuma","Pratama","Setiawan",
        "Permana","Saputra","Nugroho","Hartono","Ramadhan"
    };

    private static readonly string[] JakartaStreets = new[]
    {
        "Jl. Sudirman","Jl. Thamrin","Jl. Gatot Subroto","Jl. Rasuna Said",
        "Jl. HR Rasuna Said","Jl. MH Thamrin","Jl. Medan Merdeka","Jl. Hayam Wuruk",
        "Jl. Gajah Mada","Jl. Veteran","Jl. Kebon Sirih","Jl. Diponegoro",
        "Jl. Cikini Raya","Jl. Salemba","Jl. Matraman","Jl. Pemuda",
        "Jl. Otista","Jl. Jatinegara","Jl. Daan Mogot","Jl. Pantai Indah Kapuk"
    };

    /// <summary>Generate SVG avatar data URI from user's full name.</summary>
    private static string AvatarUrl(string fullName)
    {
        var initials = string.Concat(fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpper(w[0])));
        var color = new[] { "#FF6B35", "#FFD700", "#00C853", "#2979FF", "#AA00FF", "#FF1744", "#00BCD4", "#FF9100" }[Math.Abs(fullName.GetHashCode()) % 8];
        var svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='200' height='200'><rect width='200' height='200' rx='100' fill='{color}'/><text x='100' y='130' font-size='90' font-family='Arial,sans-serif' font-weight='bold' fill='white' text-anchor='middle'>{initials}</text></svg>";
        return $"data:image/svg+xml;base64,{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg))}";
    }

    public static async Task SeedAsync(FastRideDbContext db)
    {
        if (await db.Users.AnyAsync()) return;

        Console.WriteLine("🌱 Seeding sample data (BCrypt passwords)...");

        // RIDERS (50)
        var riders = new List<User>();
        for (int i = 0; i < 50; i++)
        {
            var name = $"{RiderFirstNames[i]} {DriverSurnames[_random.Next(DriverSurnames.Length)]}";
            riders.Add(new User
            {
                Id = Guid.NewGuid(), FullName = name,
                Email = $"{name.ToLower().Replace(" ", ".")}@email.com",
                PhoneNumber = $"08{_random.Next(10, 99)}-{_random.Next(1000, 9999)}-{_random.Next(1000, 9999)}",
                PasswordHash = _demoPassword, Role = UserRole.Rider,
                PhotoUrl = AvatarUrl(name),
                IsVerified = _random.NextDouble() > 0.1,
                CreatedAt = DateTime.UtcNow.AddDays(-_random.Next(1, 365))
            });
        }
        db.Users.AddRange(riders);
        Console.WriteLine($"   ✅ {riders.Count} riders");

        // DRIVERS (30)
        var drivers = new List<User>();
        var driverProfiles = new List<DriverProfile>();
        var vehicleTypes = new[] { "Toyota Avanza", "Honda Brio", "Suzuki Ertiga", "Toyota Innova", "Honda Mobilio", "Daihatsu Xenia", "Mitsubishi Xpander", "Honda Beat", "Yamaha NMAX", "Wuling Air EV" };

        for (int i = 0; i < 30; i++)
        {
            var name = $"{DriverFirstNames[i]} {DriverSurnames[_random.Next(DriverSurnames.Length)]}";
            var driver = new User
            {
                Id = Guid.NewGuid(), FullName = name,
                Email = $"{name.ToLower().Replace(" ", ".")}@drive.com",
                PhoneNumber = $"08{_random.Next(10, 99)}-{_random.Next(1000, 9999)}-{_random.Next(1000, 9999)}",
                PasswordHash = _demoPassword, Role = UserRole.Driver,
                PhotoUrl = AvatarUrl(name),
                IsVerified = true, CreatedAt = DateTime.UtcNow.AddDays(-_random.Next(30, 730))
            };
            var catIdx = i < 15 ? 0 : (i < 23 ? 1 : (i < 27 ? 2 : (i < 29 ? 3 : 4)));
            driverProfiles.Add(new DriverProfile
            {
                Id = Guid.NewGuid(), UserId = driver.Id,
                LicenseNumber = $"SIM-{_random.Next(100000, 999999)}",
                VehicleType = vehicleTypes[catIdx],
                VehiclePlate = $"B {_random.Next(1000, 9999)} {GetRandomLetters(3)}",
                Status = _random.NextDouble() > 0.3 ? DriverStatus.Online : DriverStatus.Offline,
                Rating = Math.Round(3.5 + _random.NextDouble() * 1.5, 1),
                TotalTrips = _random.Next(50, 2500),
                TotalEarnings = _random.Next(5000000, 150000000),
                CurrentLatitude = -6.2 + _random.NextDouble() * 0.2,
                CurrentLongitude = 106.8 + _random.NextDouble() * 0.2
            });
            drivers.Add(driver);
        }
        db.Users.AddRange(drivers);
        db.DriverProfiles.AddRange(driverProfiles);
        Console.WriteLine($"   ✅ {drivers.Count} drivers");

        // ADMIN
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(), FullName = "Admin FastRide",
            Email = "admin@fastride.com", PhoneNumber = "0800-0000-0000",
            PasswordHash = _demoPassword, Role = UserRole.Admin,
            PhotoUrl = AvatarUrl("Admin FastRide"),
            IsVerified = true, CreatedAt = DateTime.UtcNow.AddDays(-500)
        });
        Console.WriteLine("   ✅ 1 admin");

        // ORDERS (200)
        var orders = new List<Order>();
        var payments = new List<Payment>();
        var reviews = new List<Review>();
        for (int i = 0; i < 200; i++)
        {
            var rider = riders[_random.Next(riders.Count)];
            var driver = drivers[_random.Next(drivers.Count)];
            var status = GetRandomOrderStatus();
            var dist = Math.Round(1.0 + _random.NextDouble() * 40, 1);
            var vcat = (VehicleCategory)_random.Next(1, 6);
            var estFare = CalculateFare(vcat, dist);
            var created = DateTime.UtcNow.AddDays(-_random.Next(0, 90)).AddHours(-_random.Next(0, 24));
            var pIdx = _random.Next(JakartaStreets.Length);
            var dIdx = _random.Next(JakartaStreets.Length);

            orders.Add(new Order
            {
                Id = Guid.NewGuid(), RiderId = rider.Id,
                DriverId = status >= OrderStatus.Accepted ? driver.Id : null,
                PickupLatitude = -6.2 + _random.NextDouble() * 0.2,
                PickupLongitude = 106.8 + _random.NextDouble() * 0.2,
                PickupAddress = $"{JakartaStreets[pIdx]} No. {_random.Next(1, 200)}",
                DropoffLatitude = -6.2 + _random.NextDouble() * 0.2,
                DropoffLongitude = 106.8 + _random.NextDouble() * 0.2,
                DropoffAddress = $"{JakartaStreets[dIdx]} No. {_random.Next(1, 200)}",
                DistanceKm = dist, EstimatedDurationMinutes = (int)(dist * 2 + _random.Next(5, 15)),
                EstimatedFare = estFare, FinalFare = status == OrderStatus.Completed ? estFare + _random.Next(-5000, 10000) : estFare,
                VehicleCategory = vcat, PaymentMethod = (PaymentMethod)_random.Next(1, 5),
                Status = status, CreatedAt = created,
                AcceptedAt = status >= OrderStatus.Accepted ? created.AddMinutes(_random.Next(1, 10)) : null,
                StartedAt = status >= OrderStatus.Started ? created.AddMinutes(_random.Next(10, 20)) : null,
                CompletedAt = status == OrderStatus.Completed ? created.AddMinutes(_random.Next(20, 60)) : null,
                CancelledAt = status == OrderStatus.Cancelled ? created.AddMinutes(_random.Next(1, 15)) : null,
                RiderRating = status == OrderStatus.Completed ? _random.Next(3, 6) : null,
                DriverRating = status == OrderStatus.Completed ? _random.Next(3, 6) : null
            });
        }

        // Add associated payments & reviews
        foreach (var o in orders.Where(o => o.Status == OrderStatus.Completed))
        {
            payments.Add(new Payment
            {
                Id = Guid.NewGuid(), OrderId = o.Id, Amount = o.FinalFare,
                Method = o.PaymentMethod, Status = PaymentStatus.Completed,
                CreatedAt = o.CreatedAt, CompletedAt = o.CompletedAt,
                TransactionReference = $"TRX-{_random.Next(100000, 999999)}"
            });
            if (_random.NextDouble() > 0.3)
                reviews.Add(new Review
                {
                    Id = Guid.NewGuid(), OrderId = o.Id,
                    ReviewerId = o.RiderId, TargetUserId = o.DriverId!.Value,
                    Rating = _random.Next(3, 6), Comment = GetRandomReview(),
                    CreatedAt = o.CompletedAt!.Value.AddMinutes(5)
                });
        }

        db.Orders.AddRange(orders);
        db.Payments.AddRange(payments);
        db.Reviews.AddRange(reviews);
        Console.WriteLine($"   ✅ {orders.Count} orders, {payments.Count} payments, {reviews.Count} reviews");

        // PROMOS (8)
        db.Promos.AddRange(new[]
        {
            new Promo { Code = "WELCOME50", Description = "50% off first ride (max Rp 20rb)", Type = PromoType.Percentage, Value = 50m, MaxDiscount = 20000m, ValidUntil = DateTime.UtcNow.AddMonths(3), IsActive = true, UsageLimit = 500, UsageCount = 234 },
            new Promo { Code = "WEEKEND20", Description = "Rp 20rb off weekends", Type = PromoType.FixedAmount, Value = 20000m, ValidUntil = DateTime.UtcNow.AddMonths(6), IsActive = true, UsageLimit = 1000, UsageCount = 456 },
            new Promo { Code = "PAYDAY", Description = "30% off payday (max Rp 30rb)", Type = PromoType.Percentage, Value = 30m, MaxDiscount = 30000m, ValidUntil = DateTime.UtcNow.AddMonths(1), IsActive = true, UsageLimit = 300, UsageCount = 89 },
            new Promo { Code = "FRIENDS15", Description = "15% off share with friends", Type = PromoType.Percentage, Value = 15m, MaxDiscount = 15000m, ValidUntil = DateTime.UtcNow.AddYears(1), IsActive = true, UsageLimit = 2000, UsageCount = 1023 },
            new Promo { Code = "MORNING", Description = "Rp 5rb off 5-9 AM", Type = PromoType.FixedAmount, Value = 5000m, ValidUntil = DateTime.UtcNow.AddMonths(2), IsActive = true, UsageLimit = 800, UsageCount = 321 },
            new Promo { Code = "NEWYEAR", Description = "25% off new year", Type = PromoType.Percentage, Value = 25m, MaxDiscount = 25000m, ValidFrom = new DateTime(2025, 12, 24), ValidUntil = new DateTime(2026, 1, 5), IsActive = false, UsageLimit = 100 },
            new Promo { Code = "EBOLUSI", Description = "10% off Electric rides", Type = PromoType.Percentage, Value = 10m, MaxDiscount = 10000m, ValidUntil = DateTime.UtcNow.AddMonths(4), IsActive = true, UsageLimit = 500, UsageCount = 67 },
            new Promo { Code = "BIKER50", Description = "Rp 10rb off Bike rides", Type = PromoType.FixedAmount, Value = 10000m, ValidUntil = DateTime.UtcNow.AddMonths(3), IsActive = true, UsageLimit = 600, UsageCount = 189 },
        });
        Console.WriteLine("   ✅ 8 promos");

        // NOTIFICATIONS (40)
        var notifications = new List<Notification>();
        foreach (var r in riders.Take(20))
        {
            notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = r.Id, Title = "Welcome to FastRide! 🚖", Message = "Diskon 50% perjalanan pertama: WELCOME50", Type = NotificationType.Promo, IsRead = _random.NextDouble() > 0.5, CreatedAt = r.CreatedAt });
            notifications.Add(new Notification { Id = Guid.NewGuid(), UserId = r.Id, Title = "Trip Completed", Message = "Perjalanan selesai. Beri rating ya!", Type = NotificationType.OrderUpdate, IsRead = true, CreatedAt = DateTime.UtcNow.AddDays(-_random.Next(1, 30)) });
        }
        db.Notifications.AddRange(notifications);
        Console.WriteLine($"   ✅ {notifications.Count} notifications");

        await db.SaveChangesAsync();
        Console.WriteLine($"🎉 Complete: {riders.Count + drivers.Count + 1} users, {orders.Count} orders");
    }

    private static OrderStatus GetRandomOrderStatus() => _random.NextDouble() switch
    {
        < 0.55 => OrderStatus.Completed, < 0.65 => OrderStatus.Started,
        < 0.72 => OrderStatus.Accepted, < 0.82 => OrderStatus.Requested,
        < 0.90 => OrderStatus.Cancelled, < 0.95 => OrderStatus.DriverArrived,
        _ => OrderStatus.Expired
    };

    private static decimal CalculateFare(VehicleCategory c, double d) => c switch
    {
        VehicleCategory.Economy => 5000 + 3000 * (decimal)d,
        VehicleCategory.Comfort => 7000 + 4000 * (decimal)d,
        VehicleCategory.Premium => 10000 + 6000 * (decimal)d,
        VehicleCategory.Bike => 3000 + 2000 * (decimal)d,
        VehicleCategory.Electric => 5000 + 3000 * (decimal)d,
        _ => 5000 + 3000 * (decimal)d
    };

    private static string GetRandomLetters(int count) => new(Enumerable.Range(0, count).Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[_random.Next(26)]).ToArray());

    private static string GetRandomReview() => new[]
    {
        "Mantap, drivernya ramah dan tepat waktu!", "Mobil bersih, AC dingin. Recommended!",
        "Driver baik, bantu bawain barang. Thanks!", "Oke banget, harga sesuai aplikasi.",
        "Sangat memuaskan! Pasti pesan lagi.", "Driver friendly, mobil wangi. Top!",
        "Excellent service! Very professional.", "Rekomendasi buat yang mau nyaman dan aman.",
        "Mobilnya nyaman, bisa tidur selama perjalanan.", "Tepat waktu, sesuai estimasi. Good job!"
    }[_random.Next(10)];
}
