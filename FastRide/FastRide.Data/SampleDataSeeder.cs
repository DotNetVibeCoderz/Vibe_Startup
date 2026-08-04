using FastRide.Shared.Common;
using FastRide.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FastRide.Data;

/// <summary>
/// Seeds realistic sample data using BCrypt password hashing.
/// All demo accounts use password: "Password123".
///
/// The generator is seeded with a fixed value so every developer gets the same database,
/// and the three accounts quoted in the README are created explicitly rather than by chance.
/// </summary>
public static class SampleDataSeeder
{
    public const string DemoPassword = "Password123";
    public const string AdminEmail = "admin@fastride.com";
    public const string DemoRiderEmail = "budi.santoso@email.com";
    public const string DemoDriverEmail = "andi.santoso@drive.com";

    private static readonly Random _random = new(42);
    private static readonly string _demoPasswordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword, 12);

    private static readonly string[] RiderFirstNames =
    {
        "Budi","Siti","Ahmad","Dewi","Rina","Hendra","Mega","Doni",
        "Ratna","Agus","Fitri","Bambang","Lina","Eko","Putri","Wawan",
        "Dian","Rudi","Yanti","Slamet","Intan","Joko","Tuti","Adi",
        "Maya","Feri","Citra","Gunawan","Sari","Hadi","Nia","Rizky",
        "Dina","Bayu","Rani","Irfan","Vina","Arif","Susan","Denny",
        "Indah","Galih","Retno","Yoga","Wulan","Bagus","Nita","Faisal",
        "Laras","Teguh"
    };

    private static readonly string[] DriverFirstNames =
    {
        "Andi","Surya","Dedi","Yusuf","Heri","Tono","Rahmat","Supri",
        "Joko","Anton","Bowo","Karno","Udin","Maman","Cecep","Asep",
        "Nana","Iwan","Dadang","Ujang","Saepul","Doddy","Robby",
        "Benny","Ricky","Iqbal","Fajar","Gilang","Widodo","Herman"
    };

    private static readonly string[] Surnames =
    {
        "Santoso","Wijaya","Kusuma","Pratama","Setiawan",
        "Permana","Saputra","Nugroho","Hartono","Ramadhan"
    };

    private static readonly string[] JakartaStreets =
    {
        "Jl. Sudirman","Jl. Thamrin","Jl. Gatot Subroto","Jl. Rasuna Said",
        "Jl. HR Rasuna Said","Jl. MH Thamrin","Jl. Medan Merdeka","Jl. Hayam Wuruk",
        "Jl. Gajah Mada","Jl. Veteran","Jl. Kebon Sirih","Jl. Diponegoro",
        "Jl. Cikini Raya","Jl. Salemba","Jl. Matraman","Jl. Pemuda",
        "Jl. Otista","Jl. Jatinegara","Jl. Daan Mogot","Jl. Pantai Indah Kapuk"
    };

    private static readonly (string Type, VehicleCategory Category)[] Vehicles =
    {
        ("Toyota Avanza", VehicleCategory.Economy),
        ("Honda Brio", VehicleCategory.Economy),
        ("Daihatsu Xenia", VehicleCategory.Economy),
        ("Suzuki Ertiga", VehicleCategory.Comfort),
        ("Honda Mobilio", VehicleCategory.Comfort),
        ("Mitsubishi Xpander", VehicleCategory.Comfort),
        ("Toyota Innova", VehicleCategory.Premium),
        ("Toyota Alphard", VehicleCategory.Premium),
        ("Honda Beat", VehicleCategory.Bike),
        ("Yamaha NMAX", VehicleCategory.Bike),
        ("Wuling Air EV", VehicleCategory.Electric),
        ("Hyundai Ioniq 5", VehicleCategory.Electric)
    };

    /// <summary>
    /// Share of daily orders per hour — a real Jakarta demand curve with morning and evening peaks.
    /// Makes the dashboard charts show something believable instead of uniform noise.
    /// </summary>
    private static readonly double[] HourWeights =
    {
        0.010, 0.006, 0.004, 0.004, 0.008, 0.022, 0.055, 0.092,
        0.080, 0.052, 0.038, 0.036, 0.045, 0.040, 0.036, 0.042,
        0.058, 0.086, 0.095, 0.070, 0.048, 0.035, 0.024, 0.014
    };

    public static async Task SeedAsync(FastRideDbContext db, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct)) return;

        Console.WriteLine("Seeding sample data (BCrypt password hashing)...");
        var now = DateTime.UtcNow;

        // ── Riders ──
        var riders = new List<User>(RiderFirstNames.Length);
        for (var i = 0; i < RiderFirstNames.Length; i++)
        {
            // Rider 0 is the documented demo account, so the README credentials always work.
            var name = i == 0 ? "Budi Santoso" : $"{RiderFirstNames[i]} {Surnames[_random.Next(Surnames.Length)]}";
            riders.Add(new User
            {
                FullName = name,
                Email = EmailFor(name, "email.com"),
                PhoneNumber = RandomPhone(),
                PasswordHash = _demoPasswordHash,
                Role = UserRole.Rider,
                PhotoUrl = AvatarUrl(name),
                IsVerified = i == 0 || _random.NextDouble() > 0.1,
                IsActive = true,
                CreatedAt = now.AddDays(-_random.Next(1, 365))
            });
        }
        db.Users.AddRange(riders);

        // ── Drivers ──
        var drivers = new List<User>(DriverFirstNames.Length);
        var driverProfiles = new List<DriverProfile>(DriverFirstNames.Length);
        var documents = new List<DriverDocument>();

        for (var i = 0; i < DriverFirstNames.Length; i++)
        {
            var name = i == 0 ? "Andi Santoso" : $"{DriverFirstNames[i]} {Surnames[_random.Next(Surnames.Length)]}";
            var vehicle = Vehicles[i % Vehicles.Length];
            var verified = i == 0 || _random.NextDouble() > 0.15;

            var driver = new User
            {
                FullName = name,
                Email = EmailFor(name, "drive.com"),
                PhoneNumber = RandomPhone(),
                PasswordHash = _demoPasswordHash,
                Role = UserRole.Driver,
                PhotoUrl = AvatarUrl(name),
                IsVerified = true,
                IsActive = true,
                CreatedAt = now.AddDays(-_random.Next(30, 730))
            };

            var profile = new DriverProfile
            {
                UserId = driver.Id,
                LicenseNumber = $"SIM-{_random.Next(100000, 999999)}",
                VehicleType = vehicle.Type,
                VehicleCategory = vehicle.Category,
                VehiclePlate = $"B {_random.Next(1000, 9999)} {RandomLetters(3)}",
                Status = i == 0 ? DriverStatus.Online : RandomDriverStatus(),
                TotalTrips = _random.Next(50, 2500),
                TotalEarnings = _random.Next(5_000_000, 150_000_000),
                CurrentLatitude = RandomLat(),
                CurrentLongitude = RandomLon(),
                Heading = _random.Next(0, 360),
                LocationUpdatedAt = now.AddSeconds(-_random.Next(5, 900)),
                IsDocumentVerified = verified,
                VerifiedAt = verified ? driver.CreatedAt.AddDays(1) : null,
                Rating = 5.0,
                RatingCount = 0
            };

            documents.Add(NewDocument(profile.Id, DocumentType.DriverLicense, verified, driver.CreatedAt, now));
            documents.Add(NewDocument(profile.Id, DocumentType.VehicleRegistration, verified, driver.CreatedAt, now));
            documents.Add(NewDocument(profile.Id, DocumentType.IdentityCard, verified, driver.CreatedAt, now));

            drivers.Add(driver);
            driverProfiles.Add(profile);
        }
        db.Users.AddRange(drivers);
        db.DriverProfiles.AddRange(driverProfiles);
        db.DriverDocuments.AddRange(documents);

        // ── Admin ──
        db.Users.Add(new User
        {
            FullName = "Admin FastRide",
            Email = AdminEmail,
            PhoneNumber = "0800-0000-0000",
            PasswordHash = _demoPasswordHash,
            Role = UserRole.Admin,
            PhotoUrl = AvatarUrl("Admin FastRide"),
            IsVerified = true,
            IsActive = true,
            CreatedAt = now.AddDays(-500)
        });

        // ── Fare table (needed here because orders are priced with it) ──
        var fares = new Dictionary<VehicleCategory, FareConfig>
        {
            [VehicleCategory.Economy] = new() { VehicleCategory = VehicleCategory.Economy, BaseFare = 5000m, CostPerKm = 3000m, CostPerMinute = 500m, MinimumFare = 10000m },
            [VehicleCategory.Comfort] = new() { VehicleCategory = VehicleCategory.Comfort, BaseFare = 7000m, CostPerKm = 4000m, CostPerMinute = 700m, MinimumFare = 15000m },
            [VehicleCategory.Premium] = new() { VehicleCategory = VehicleCategory.Premium, BaseFare = 10000m, CostPerKm = 6000m, CostPerMinute = 1000m, MinimumFare = 25000m },
            [VehicleCategory.Bike] = new() { VehicleCategory = VehicleCategory.Bike, BaseFare = 3000m, CostPerKm = 2000m, CostPerMinute = 300m, MinimumFare = 7000m },
            [VehicleCategory.Electric] = new() { VehicleCategory = VehicleCategory.Electric, BaseFare = 5000m, CostPerKm = 3000m, CostPerMinute = 500m, MinimumFare = 10000m }
        };

        // ── Orders, payments, reviews, stops ──
        var orders = new List<Order>(420);
        var payments = new List<Payment>();
        var reviews = new List<Review>();
        var stops = new List<TripStop>();
        var ratingTally = new Dictionary<Guid, (int Sum, int Count)>();

        // 90 days of history, weighted so recent days (and today) are well populated —
        // the dashboard's "today" panels are the first thing anyone looks at.
        for (var dayOffset = 89; dayOffset >= 0; dayOffset--)
        {
            var ordersThatDay = dayOffset switch
            {
                0 => _random.Next(28, 42),          // today
                < 7 => _random.Next(14, 24),        // this week
                < 30 => _random.Next(4, 10),
                _ => _random.Next(1, 4)
            };

            for (var n = 0; n < ordersThatDay; n++)
            {
                var day = now.Date.AddDays(-dayOffset);
                var hour = WeightedHour();
                var created = day.AddHours(hour).AddMinutes(_random.Next(0, 60)).AddSeconds(_random.Next(0, 60));
                if (created > now) created = now.AddMinutes(-_random.Next(1, 90));

                var rider = riders[_random.Next(riders.Count)];
                var driverIdx = _random.Next(drivers.Count);
                var driver = drivers[driverIdx];
                var profile = driverProfiles[driverIdx];
                var status = RandomOrderStatus(dayOffset);

                var pickupLat = RandomLat();
                var pickupLon = RandomLon();
                var dropLat = RandomLat();
                var dropLon = RandomLon();
                var distance = Math.Round(GeoUtils.DistanceKm(pickupLat, pickupLon, dropLat, dropLon) + 0.4, 1);
                if (distance < 0.8) distance = Math.Round(0.8 + (_random.NextDouble() * 3), 1);

                var duration = GeoUtils.EstimateDurationMinutes(distance);
                var fare = fares[profile.VehicleCategory];
                var estimated = fare.Quote(distance, duration);

                // Roughly one in five bookings uses a promo.
                var usedPromo = _random.NextDouble() < 0.2;
                var discount = usedPromo ? Math.Round(Math.Min(estimated * 0.25m, 20000m), 0) : 0m;

                // Expired orders were never picked up; roughly half of the cancellations
                // happen before a driver ever accepts.
                var wasAccepted = status switch
                {
                    OrderStatus.Requested or OrderStatus.Expired => false,
                    OrderStatus.Cancelled => _random.NextDouble() < 0.5,
                    _ => true
                };

                var order = new Order
                {
                    Code = OrderCode(),
                    RiderId = rider.Id,
                    DriverId = wasAccepted ? driver.Id : null,
                    PickupLatitude = pickupLat,
                    PickupLongitude = pickupLon,
                    PickupAddress = RandomAddress(),
                    DropoffLatitude = dropLat,
                    DropoffLongitude = dropLon,
                    DropoffAddress = RandomAddress(),
                    DistanceKm = distance,
                    EstimatedDurationMinutes = duration,
                    EstimatedFare = estimated,
                    DiscountAmount = discount,
                    FinalFare = estimated - discount,
                    PromoCode = usedPromo ? "WEEKEND20" : null,
                    SurgeMultiplier = 1.0m,
                    VehicleCategory = profile.VehicleCategory,
                    PaymentMethod = RandomPaymentMethod(),
                    Status = status,
                    CreatedAt = created,
                    AcceptedAt = wasAccepted ? created.AddMinutes(_random.Next(1, 6)) : null,
                    ArrivedAt = wasAccepted && status is OrderStatus.DriverArrived or OrderStatus.Started or OrderStatus.Completed ? created.AddMinutes(_random.Next(6, 12)) : null,
                    StartedAt = wasAccepted && status is OrderStatus.Started or OrderStatus.Completed ? created.AddMinutes(_random.Next(12, 18)) : null,
                    CompletedAt = status == OrderStatus.Completed ? created.AddMinutes(duration + _random.Next(12, 25)) : null,
                    CancelledAt = status == OrderStatus.Cancelled ? created.AddMinutes(_random.Next(1, 15)) : null,
                    CancellationReason = status == OrderStatus.Cancelled ? RandomCancelReason() : null,
                    CancelledBy = status == OrderStatus.Cancelled ? (_random.NextDouble() < 0.7 ? CancelledByParty.Rider : CancelledByParty.Driver) : null
                };

                if (order.CompletedAt > now) order.CompletedAt = now.AddMinutes(-_random.Next(1, 30));

                // One order in eight is a multi-stop trip.
                if (_random.NextDouble() < 0.12)
                {
                    stops.Add(new TripStop
                    {
                        OrderId = order.Id,
                        SequenceNumber = 1,
                        Latitude = RandomLat(),
                        Longitude = RandomLon(),
                        Address = RandomAddress(),
                        StopType = TripStopType.Waypoint,
                        ReachedAt = order.Status == OrderStatus.Completed ? order.StartedAt?.AddMinutes(6) : null
                    });
                }

                if (order.Status == OrderStatus.Completed)
                {
                    var reference = TransactionRef(order.CompletedAt!.Value);

                    payments.Add(new Payment
                    {
                        OrderId = order.Id,
                        Amount = order.FinalFare,
                        DiscountAmount = order.DiscountAmount,
                        Method = order.PaymentMethod,
                        Status = PaymentStatus.Completed,
                        CreatedAt = order.CompletedAt!.Value,
                        CompletedAt = order.CompletedAt,
                        TransactionReference = reference,
                        // Historical sample data predates the provider integration; cash is
                        // the honest label for money that was settled outside any gateway.
                        ProviderName = order.PaymentMethod == PaymentMethod.Cash ? "manual" : "simulated",
                        ProviderReference = reference,
                        AttemptCount = 1
                    });

                    if (_random.NextDouble() > 0.3)
                    {
                        var rating = _random.NextDouble() switch { < 0.62 => 5, < 0.85 => 4, < 0.95 => 3, _ => _random.Next(1, 3) };
                        order.DriverRating = rating;
                        order.ReviewComment = RandomReview(rating);

                        reviews.Add(new Review
                        {
                            OrderId = order.Id,
                            ReviewerId = order.RiderId,
                            TargetUserId = driver.Id,
                            Rating = rating,
                            Comment = order.ReviewComment,
                            CreatedAt = order.CompletedAt!.Value.AddMinutes(5)
                        });

                        var current = ratingTally.GetValueOrDefault(driver.Id);
                        ratingTally[driver.Id] = (current.Sum + rating, current.Count + 1);
                    }
                }

                orders.Add(order);
            }
        }

        // Driver rating must agree with the reviews that were actually written.
        foreach (var profile in driverProfiles)
        {
            if (ratingTally.TryGetValue(profile.UserId, out var tally) && tally.Count > 0)
            {
                profile.Rating = Math.Round((double)tally.Sum / tally.Count, 2);
                profile.RatingCount = tally.Count;
            }
            else
            {
                profile.Rating = Math.Round(4.2 + (_random.NextDouble() * 0.8), 2);
                profile.RatingCount = 0;
            }
        }

        db.Orders.AddRange(orders);
        db.TripStops.AddRange(stops);
        db.Payments.AddRange(payments);
        db.Reviews.AddRange(reviews);

        // ── Promos ──
        db.Promos.AddRange(
            new Promo { Code = "WELCOME50", Description = "Diskon 50% perjalanan pertama", Type = PromoType.Percentage, Value = 50m, MaxDiscount = 20000m, ValidUntil = now.AddMonths(3), UsageLimit = 500, UsageCount = 234 },
            new Promo { Code = "WEEKEND20", Description = "Potongan Rp 20rb tiap akhir pekan", Type = PromoType.FixedAmount, Value = 20000m, MinOrderAmount = 30000m, ValidUntil = now.AddMonths(6), UsageLimit = 1000, UsageCount = 456 },
            new Promo { Code = "PAYDAY", Description = "Diskon 30% tanggal gajian", Type = PromoType.Percentage, Value = 30m, MaxDiscount = 30000m, ValidUntil = now.AddMonths(1), UsageLimit = 300, UsageCount = 89 },
            new Promo { Code = "FRIENDS15", Description = "Diskon 15% ajak teman", Type = PromoType.Percentage, Value = 15m, MaxDiscount = 15000m, ValidUntil = now.AddYears(1), UsageLimit = 2000, UsageCount = 1023 },
            new Promo { Code = "MORNING", Description = "Potongan Rp 5rb jam 5-9 pagi", Type = PromoType.FixedAmount, Value = 5000m, ValidUntil = now.AddMonths(2), UsageLimit = 800, UsageCount = 321 },
            new Promo { Code = "NEWYEAR", Description = "Diskon 25% tahun baru (kedaluwarsa)", Type = PromoType.Percentage, Value = 25m, MaxDiscount = 25000m, ValidFrom = now.AddMonths(-8), ValidUntil = now.AddMonths(-7), IsActive = false, UsageLimit = 100, UsageCount = 100 },
            new Promo { Code = "EVOLUSI", Description = "Diskon 10% khusus kendaraan listrik", Type = PromoType.Percentage, Value = 10m, MaxDiscount = 10000m, VehicleCategory = VehicleCategory.Electric, ValidUntil = now.AddMonths(4), UsageLimit = 500, UsageCount = 67 },
            new Promo { Code = "BIKER10", Description = "Potongan Rp 10rb khusus motor", Type = PromoType.FixedAmount, Value = 10000m, VehicleCategory = VehicleCategory.Bike, ValidUntil = now.AddMonths(3), UsageLimit = 600, UsageCount = 189 });

        // ── Notifications ──
        var notifications = new List<Notification>();
        foreach (var rider in riders.Take(25))
        {
            notifications.Add(new Notification
            {
                UserId = rider.Id,
                Title = "Selamat datang di FastRide",
                Message = "Pakai kode WELCOME50 untuk diskon 50% di perjalanan pertama kamu.",
                Type = NotificationType.Promo,
                IsRead = _random.NextDouble() > 0.5,
                CreatedAt = rider.CreatedAt
            });
        }
        foreach (var order in orders.Where(o => o.Status == OrderStatus.Completed).TakeLast(40))
        {
            notifications.Add(new Notification
            {
                UserId = order.RiderId,
                OrderId = order.Id,
                Title = "Perjalanan selesai",
                Message = $"Trip {order.Code} selesai. Total Rp {order.FinalFare:N0}. Beri rating untuk driver kamu.",
                Type = NotificationType.OrderUpdate,
                IsRead = _random.NextDouble() > 0.4,
                CreatedAt = order.CompletedAt!.Value
            });
        }
        db.Notifications.AddRange(notifications);

        await db.SaveChangesAsync(ct);

        Console.WriteLine($"Seeded: {riders.Count} riders, {drivers.Count} drivers, 1 admin, " +
                          $"{orders.Count} orders, {payments.Count} payments, {reviews.Count} reviews, " +
                          $"{documents.Count} driver documents, {notifications.Count} notifications.");
        Console.WriteLine($"Demo login — admin: {AdminEmail} | rider: {DemoRiderEmail} | driver: {DemoDriverEmail} | password: {DemoPassword}");
    }

    // ─────────────────────────── generators ───────────────────────────

    private static DriverDocument NewDocument(Guid profileId, DocumentType type, bool approved, DateTime joined, DateTime now) => new()
    {
        DriverProfileId = profileId,
        Type = type,
        Status = approved ? DocumentStatus.Approved : DocumentStatus.Pending,
        FileUrl = $"/uploads/documents/sample-{type.ToString().ToLowerInvariant()}.jpg",
        UploadedAt = joined,
        ReviewedAt = approved ? joined.AddDays(1) : null,
        ExpiresAt = type == DocumentType.DriverLicense ? now.AddYears(_random.Next(1, 5)) : null
    };

    private static int WeightedHour()
    {
        var roll = _random.NextDouble();
        var cumulative = 0.0;
        for (var hour = 0; hour < HourWeights.Length; hour++)
        {
            cumulative += HourWeights[hour];
            if (roll <= cumulative) return hour;
        }
        return 18;
    }

    /// <summary>Older days are settled; today still has trips in flight.</summary>
    private static OrderStatus RandomOrderStatus(int dayOffset)
    {
        if (dayOffset > 0)
        {
            return _random.NextDouble() switch
            {
                < 0.84 => OrderStatus.Completed,
                < 0.95 => OrderStatus.Cancelled,
                _ => OrderStatus.Expired
            };
        }

        return _random.NextDouble() switch
        {
            < 0.55 => OrderStatus.Completed,
            < 0.64 => OrderStatus.Started,
            < 0.70 => OrderStatus.DriverArrived,
            < 0.78 => OrderStatus.Accepted,
            < 0.90 => OrderStatus.Requested,
            < 0.97 => OrderStatus.Cancelled,
            _ => OrderStatus.Expired
        };
    }

    private static DriverStatus RandomDriverStatus() => _random.NextDouble() switch
    {
        < 0.45 => DriverStatus.Online,
        < 0.60 => DriverStatus.OnTrip,
        < 0.70 => DriverStatus.Break,
        _ => DriverStatus.Offline
    };

    private static PaymentMethod RandomPaymentMethod() => _random.NextDouble() switch
    {
        < 0.42 => PaymentMethod.EWallet,
        < 0.75 => PaymentMethod.Cash,
        < 0.92 => PaymentMethod.CreditCard,
        _ => PaymentMethod.BankTransfer
    };

    private static double RandomLat() => -6.30 + (_random.NextDouble() * 0.25);
    private static double RandomLon() => 106.72 + (_random.NextDouble() * 0.28);

    private static string RandomAddress() =>
        $"{JakartaStreets[_random.Next(JakartaStreets.Length)]} No. {_random.Next(1, 200)}";

    private static string RandomPhone() =>
        $"08{_random.Next(10, 99)}{_random.Next(1000, 9999)}{_random.Next(1000, 9999)}";

    private static string RandomLetters(int count) =>
        new(Enumerable.Range(0, count).Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZ"[_random.Next(26)]).ToArray());

    private static int _orderSequence;

    /// <summary>Reads like a real booking code but stays unique — the column has a unique index.</summary>
    private static string OrderCode() =>
        $"FR-{RandomLetters(2)}{++_orderSequence:D4}";

    private static int _transactionSequence;

    /// <summary>
    /// Unique by construction — <c>Payment.TransactionReference</c> carries a unique index
    /// because provider callbacks are matched on it.
    /// </summary>
    private static string TransactionRef(DateTime at) =>
        $"TRX-{at:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}{++_transactionSequence:D5}";

    private static string EmailFor(string fullName, string domain)
    {
        var local = fullName.ToLowerInvariant()
            .Replace(".", string.Empty)
            .Replace("  ", " ")
            .Trim()
            .Replace(" ", ".");
        return $"{local}@{domain}";
    }

    /// <summary>Generate an SVG initials avatar as a data URI.</summary>
    private static string AvatarUrl(string fullName)
    {
        var initials = string.Concat(fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(word => char.ToUpperInvariant(word[0])));

        var palette = new[] { "#FF6B35", "#FFB020", "#23C48E", "#2979FF", "#AA00FF", "#FF5A45", "#00BCD4", "#FF9100" };
        var color = palette[Math.Abs(fullName.GetHashCode(StringComparison.Ordinal)) % palette.Length];

        var svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='200' height='200'>" +
                  $"<rect width='200' height='200' rx='100' fill='{color}'/>" +
                  $"<text x='100' y='132' font-size='88' font-family='Arial,sans-serif' font-weight='bold' " +
                  $"fill='white' text-anchor='middle'>{initials}</text></svg>";

        return $"data:image/svg+xml;base64,{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg))}";
    }

    private static string RandomCancelReason() => new[]
    {
        "Driver terlalu lama datang",
        "Salah memasukkan alamat tujuan",
        "Rencana perjalanan berubah",
        "Menemukan tumpangan lain",
        "Driver membatalkan perjalanan"
    }[_random.Next(5)];

    private static string RandomReview(int rating) => rating >= 4
        ? new[]
        {
            "Mantap, drivernya ramah dan tepat waktu!",
            "Mobil bersih, AC dingin. Recommended!",
            "Driver baik, bantu bawain barang. Terima kasih!",
            "Oke banget, harga sesuai aplikasi.",
            "Sangat memuaskan, pasti pesan lagi.",
            "Driver ramah, mobil wangi. Top!",
            "Tepat waktu, sesuai estimasi. Kerja bagus!"
        }[_random.Next(7)]
        : new[]
        {
            "Driver datang cukup lama.",
            "Rute agak memutar, jadi lebih mahal.",
            "Mobil kurang bersih.",
            "Sopir kurang responsif saat dihubungi."
        }[_random.Next(4)];
}
