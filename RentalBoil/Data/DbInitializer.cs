using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RentalBoil.Models;

namespace RentalBoil.Data;

/// <summary>
/// Database initializer - membuat sample data untuk development
/// </summary>
public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Pastikan database terbuat
        await context.Database.EnsureCreatedAsync();

        // ---- Roles ----
        var roles = new[] { "Admin", "Partner", "Customer" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ---- Users ----
        if (!context.Users.Any())
        {
            var users = new[]
            {
                new { User = new ApplicationUser
                {
                    UserName = "admin@rentalboil.com",
                    Email = "admin@rentalboil.com",
                    FullName = "Super Admin",
                    PhoneNumber = "081111111111",
                    Role = UserRole.Admin,
                    KtpVerified = true,
                    SimVerified = true,
                    MembershipTier = "Platinum",
                    LoyaltyPoints = 5000,
                    RegisteredAt = DateTime.UtcNow,
                    Language = "id"
                }, Password = "Admin123!", Role = "Admin" },

                new { User = new ApplicationUser
                {
                    UserName = "partner1@rentalboil.com",
                    Email = "partner1@rentalboil.com",
                    FullName = "Budi Santoso",
                    PhoneNumber = "082222222222",
                    Role = UserRole.Partner,
                    KtpVerified = true,
                    SimVerified = true,
                    MembershipTier = "Gold",
                    LoyaltyPoints = 2000,
                    RegisteredAt = DateTime.UtcNow.AddDays(-30),
                    Language = "id"
                }, Password = "Partner123!", Role = "Partner" },

                new { User = new ApplicationUser
                {
                    UserName = "partner2@rentalboil.com",
                    Email = "partner2@rentalboil.com",
                    FullName = "Siti Rahayu",
                    PhoneNumber = "082333333333",
                    Role = UserRole.Partner,
                    KtpVerified = true,
                    SimVerified = true,
                    RegisteredAt = DateTime.UtcNow.AddDays(-20),
                    Language = "id"
                }, Password = "Partner123!", Role = "Partner" },

                new { User = new ApplicationUser
                {
                    UserName = "customer1@rentalboil.com",
                    Email = "customer1@rentalboil.com",
                    FullName = "Andi Pratama",
                    PhoneNumber = "083444444444",
                    Role = UserRole.Customer,
                    KtpVerified = true,
                    SimVerified = true,
                    MembershipTier = "Silver",
                    LoyaltyPoints = 500,
                    RegisteredAt = DateTime.UtcNow.AddDays(-15),
                    Language = "id"
                }, Password = "Customer123!", Role = "Customer" },

                new { User = new ApplicationUser
                {
                    UserName = "customer2@rentalboil.com",
                    Email = "customer2@rentalboil.com",
                    FullName = "Dewi Lestari",
                    PhoneNumber = "083555555555",
                    Role = UserRole.Customer,
                    KtpVerified = false,
                    SimVerified = false,
                    MembershipTier = "Basic",
                    LoyaltyPoints = 0,
                    RegisteredAt = DateTime.UtcNow.AddDays(-5),
                    Language = "id"
                }, Password = "Customer123!", Role = "Customer" },

                new { User = new ApplicationUser
                {
                    UserName = "john@example.com",
                    Email = "john@example.com",
                    FullName = "John Smith",
                    PhoneNumber = "086666666666",
                    Role = UserRole.Customer,
                    KtpVerified = true,
                    SimVerified = true,
                    MembershipTier = "Gold",
                    LoyaltyPoints = 1500,
                    RegisteredAt = DateTime.UtcNow.AddDays(-10),
                    Language = "en"
                }, Password = "Customer123!", Role = "Customer" }
            };

            foreach (var u in users)
            {
                var result = await userManager.CreateAsync(u.User, u.Password);
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(u.User, u.Role);
            }
        }

        await context.SaveChangesAsync();

        // ---- Vehicles ----
        if (!context.Vehicles.Any())
        {
            var partner1 = await userManager.FindByEmailAsync("partner1@rentalboil.com");
            var partner2 = await userManager.FindByEmailAsync("partner2@rentalboil.com");

            var vehicles = new List<Vehicle>
            {
                new Vehicle
                {
                    Name = "Toyota Avanza 2024",
                    PlateNumber = "B 1234 AB",
                    Type = VehicleType.Car,
                    Brand = "Toyota",
                    Model = "Avanza",
                    Year = 2024,
                    Color = "Silver",
                    Transmission = TransmissionType.Automatic,
                    FuelType = FuelType.Petrol,
                    Capacity = 7,
                    LuggageCapacity = 200,
                    PricePerHour = 50_000,
                    PricePerDay = 400_000,
                    DynamicPriceMultiplier = 1.2m,
                    Description = "## Toyota Avanza 2024\n\nMobil keluarga terbaik dengan kapasitas 7 penumpang. Nyaman untuk perjalanan jauh maupun dalam kota.\n\n### Fitur:\n- AC Double Blower\n- Power Steering\n- Airbag\n- ABS Brakes\n- Bluetooth Audio",
                    Specifications = "{\"engine\":\"1.3L 4-cylinder\",\"power\":\"98 PS\",\"fuel_efficiency\":\"12 km/L\",\"doors\":5,\"seats\":7}",
                    Location = "Jl. Sudirman No. 123, Jakarta Pusat",
                    Latitude = -6.2088,
                    Longitude = 106.8456,
                    OwnerId = partner1!.Id,
                    IsAvailable = true,
                    IsVerified = true,
                    InsuranceAvailable = true,
                    InsuranceCostPerDay = 50_000,
                    RentalCount = 25,
                    AverageRating = 4.5,
                    ReviewCount = 18
                },
                new Vehicle
                {
                    Name = "Honda Brio 2024",
                    PlateNumber = "B 5678 CD",
                    Type = VehicleType.Car,
                    Brand = "Honda",
                    Model = "Brio",
                    Year = 2024,
                    Color = "Red",
                    Transmission = TransmissionType.Automatic,
                    FuelType = FuelType.Petrol,
                    Capacity = 5,
                    LuggageCapacity = 150,
                    PricePerHour = 40_000,
                    PricePerDay = 300_000,
                    Description = "## Honda Brio 2024\n\nCity car irit dan lincah. Cocok untuk perjalanan dalam kota.",
                    Specifications = "{\"engine\":\"1.2L 4-cylinder\",\"power\":\"90 PS\",\"fuel_efficiency\":\"14 km/L\",\"doors\":5,\"seats\":5}",
                    Location = "Jl. Thamrin No. 45, Jakarta Pusat",
                    Latitude = -6.1825,
                    Longitude = 106.8230,
                    OwnerId = partner1!.Id,
                    IsAvailable = true,
                    IsVerified = true,
                    InsuranceAvailable = true,
                    InsuranceCostPerDay = 35_000,
                    RentalCount = 42,
                    AverageRating = 4.7,
                    ReviewCount = 30
                },
                new Vehicle
                {
                    Name = "Honda CR-V 2024",
                    PlateNumber = "B 9012 EF",
                    Type = VehicleType.Car,
                    Brand = "Honda",
                    Model = "CR-V",
                    Year = 2024,
                    Color = "White",
                    Transmission = TransmissionType.Automatic,
                    FuelType = FuelType.Hybrid,
                    Capacity = 5,
                    LuggageCapacity = 500,
                    PricePerHour = 80_000,
                    PricePerDay = 650_000,
                    DynamicPriceMultiplier = 1.3m,
                    Description = "## Honda CR-V 2024 Hybrid\n\nSUV premium dengan teknologi hybrid hemat bahan bakar.",
                    Specifications = "{\"engine\":\"2.0L Hybrid\",\"power\":\"184 PS\",\"fuel_efficiency\":\"17 km/L\",\"doors\":5,\"seats\":5}",
                    Location = "Jl. Sudirman No. 123, Jakarta Pusat",
                    Latitude = -6.2088,
                    Longitude = 106.8456,
                    OwnerId = partner1!.Id,
                    IsAvailable = true,
                    IsVerified = true,
                    InsuranceAvailable = true,
                    InsuranceCostPerDay = 80_000,
                    RentalCount = 15,
                    AverageRating = 4.8,
                    ReviewCount = 12
                },
                new Vehicle
                {
                    Name = "Yamaha NMAX 2024",
                    PlateNumber = "B 3456 GH",
                    Type = VehicleType.Motorcycle,
                    Brand = "Yamaha",
                    Model = "NMAX",
                    Year = 2024,
                    Color = "Black",
                    Transmission = TransmissionType.Automatic,
                    FuelType = FuelType.Petrol,
                    Capacity = 2,
                    PricePerHour = 20_000,
                    PricePerDay = 150_000,
                    Description = "## Yamaha NMAX 2024\n\nMotor matic premium dengan bagasi luas dan nyaman dikendarai.",
                    Specifications = "{\"engine\":\"155cc\",\"power\":\"15 PS\",\"fuel_efficiency\":\"35 km/L\"}",
                    Location = "Jl. Sudirman No. 123, Jakarta Pusat",
                    Latitude = -6.2088,
                    Longitude = 106.8456,
                    OwnerId = partner1!.Id,
                    IsAvailable = true,
                    IsVerified = true,
                    InsuranceAvailable = true,
                    InsuranceCostPerDay = 15_000,
                    RentalCount = 55,
                    AverageRating = 4.6,
                    ReviewCount = 40
                },
                new Vehicle
                {
                    Name = "Honda PCX 160 2024",
                    PlateNumber = "B 7890 IJ",
                    Type = VehicleType.Motorcycle,
                    Brand = "Honda",
                    Model = "PCX 160",
                    Year = 2024,
                    Color = "Blue",
                    Transmission = TransmissionType.Automatic,
                    FuelType = FuelType.Petrol,
                    Capacity = 2,
                    PricePerHour = 25_000,
                    PricePerDay = 180_000,
                    Description = "## Honda PCX 160 2024\n\nMotor matic mewah dengan desain aerodinamis dan irit BBM.",
                    Specifications = "{\"engine\":\"160cc\",\"power\":\"16 PS\",\"fuel_efficiency\":\"40 km/L\"}",
                    Location = "Jl. Gatot Subroto No. 67, Jakarta Selatan",
                    Latitude = -6.2387,
                    Longitude = 106.8240,
                    OwnerId = partner2!.Id,
                    IsAvailable = true,
                    IsVerified = true,
                    InsuranceAvailable = false,
                    RentalCount = 38,
                    AverageRating = 4.4,
                    ReviewCount = 25
                },
                new Vehicle
                {
                    Name = "Toyota Innova Zenix 2024",
                    PlateNumber = "B 1122 KL",
                    Type = VehicleType.Car,
                    Brand = "Toyota",
                    Model = "Innova Zenix",
                    Year = 2024,
                    Color = "Black",
                    Transmission = TransmissionType.Automatic,
                    FuelType = FuelType.Hybrid,
                    Capacity = 7,
                    LuggageCapacity = 300,
                    PricePerHour = 90_000,
                    PricePerDay = 750_000,
                    DynamicPriceMultiplier = 1.25m,
                    Description = "## Toyota Innova Zenix 2024\n\nMPV premium hybrid dengan kenyamanan maksimal untuk 7 penumpang.",
                    Specifications = "{\"engine\":\"2.0L Hybrid\",\"power\":\"186 PS\",\"fuel_efficiency\":\"15 km/L\",\"doors\":5,\"seats\":7}",
                    Location = "Jl. Gatot Subroto No. 67, Jakarta Selatan",
                    Latitude = -6.2387,
                    Longitude = 106.8240,
                    OwnerId = partner2!.Id,
                    IsAvailable = true,
                    IsVerified = true,
                    InsuranceAvailable = true,
                    InsuranceCostPerDay = 90_000,
                    RentalCount = 10,
                    AverageRating = 4.9,
                    ReviewCount = 8
                },
                new Vehicle
                {
                    Name = "Suzuki Carry Pickup 2023",
                    PlateNumber = "B 3344 MN",
                    Type = VehicleType.Car,
                    Brand = "Suzuki",
                    Model = "Carry",
                    Year = 2023,
                    Color = "White",
                    Transmission = TransmissionType.Manual,
                    FuelType = FuelType.Petrol,
                    Capacity = 2,
                    PricePerHour = 35_000,
                    PricePerDay = 280_000,
                    Description = "## Suzuki Carry Pickup\n\nKendaraan niaga untuk angkutan barang. Cocok untuk pindahan.",
                    Specifications = "{\"engine\":\"1.5L\",\"power\":\"96 PS\",\"fuel_efficiency\":\"10 km/L\",\"doors\":2,\"seats\":2}",
                    Location = "Jl. Gatot Subroto No. 67, Jakarta Selatan",
                    Latitude = -6.2387,
                    Longitude = 106.8240,
                    OwnerId = partner2!.Id,
                    IsAvailable = true,
                    IsVerified = true,
                    InsuranceAvailable = false,
                    RentalCount = 20,
                    AverageRating = 4.2,
                    ReviewCount = 15
                },
                new Vehicle
                {
                    Name = "Vespa Sprint 2024",
                    PlateNumber = "B 5566 OP",
                    Type = VehicleType.Motorcycle,
                    Brand = "Vespa",
                    Model = "Sprint",
                    Year = 2024,
                    Color = "Red",
                    Transmission = TransmissionType.Automatic,
                    FuelType = FuelType.Petrol,
                    Capacity = 2,
                    PricePerHour = 30_000,
                    PricePerDay = 220_000,
                    DynamicPriceMultiplier = 1.5m,
                    Description = "## Vespa Sprint 2024\n\nSkuter ikonik Italia dengan gaya classy. Perfect untuk city riding.",
                    Specifications = "{\"engine\":\"150cc\",\"power\":\"12 PS\",\"fuel_efficiency\":\"38 km/L\"}",
                    Location = "Jl. Kemang Raya No. 88, Jakarta Selatan",
                    Latitude = -6.2608,
                    Longitude = 106.8101,
                    OwnerId = partner1!.Id,
                    IsAvailable = true,
                    IsVerified = true,
                    InsuranceAvailable = true,
                    InsuranceCostPerDay = 25_000,
                    RentalCount = 30,
                    AverageRating = 4.3,
                    ReviewCount = 22
                }
            };

            context.Vehicles.AddRange(vehicles);
            await context.SaveChangesAsync();

            // ---- Vehicle Photos (using placeholder images) ----
            var photos = new List<VehiclePhoto>();
            for (int i = 1; i <= 8; i++)
            {
                photos.Add(new VehiclePhoto
                {
                    VehicleId = i,
                    Url = $"/images/vehicles/vehicle{i}_main.jpg",
                    IsPrimary = true,
                    SortOrder = 1,
                    Caption = $"Main photo of vehicle {i}"
                });
                photos.Add(new VehiclePhoto
                {
                    VehicleId = i,
                    Url = $"/images/vehicles/vehicle{i}_2.jpg",
                    IsPrimary = false,
                    SortOrder = 2,
                    Caption = $"Additional photo of vehicle {i}"
                });
            }
            context.VehiclePhotos.AddRange(photos);
            await context.SaveChangesAsync();
        }

        // ---- Reviews ----
        if (!context.Reviews.Any())
        {
            var customer1 = await userManager.FindByEmailAsync("customer1@rentalboil.com");
            var john = await userManager.FindByEmailAsync("john@example.com");

            var reviews = new[]
            {
                new Review { VehicleId = 1, UserId = customer1!.Id, Rating = 5, Comment = "Mobil sangat bersih dan nyaman. AC dingin, mesin halus. Recommended!", IsVerified = true, CreatedAt = DateTime.UtcNow.AddDays(-5) },
                new Review { VehicleId = 1, UserId = john!.Id, Rating = 4, Comment = "Good family car, but fuel consumption could be better.", IsVerified = true, CreatedAt = DateTime.UtcNow.AddDays(-10) },
                new Review { VehicleId = 2, UserId = customer1!.Id, Rating = 5, Comment = "Irit banget! Cocok buat daily commute. Pasti sewa lagi.", IsVerified = true, CreatedAt = DateTime.UtcNow.AddDays(-3) },
                new Review { VehicleId = 2, UserId = john!.Id, Rating = 5, Comment = "Excellent fuel efficiency. Very easy to park in Jakarta.", IsVerified = true, CreatedAt = DateTime.UtcNow.AddDays(-7) },
                new Review { VehicleId = 3, UserId = customer1!.Id, Rating = 5, Comment = "Mobil mewah dengan performa mantap. Cocok buat road trip!", IsVerified = true, CreatedAt = DateTime.UtcNow.AddDays(-2) },
                new Review { VehicleId = 4, UserId = john!.Id, Rating = 4, Comment = "Smooth ride through Jakarta traffic. Great scooter.", IsVerified = true, CreatedAt = DateTime.UtcNow.AddDays(-6) },
                new Review { VehicleId = 5, UserId = customer1!.Id, Rating = 5, Comment = "PCX paling nyaman buat motor harian. Bagasi luas.", IsVerified = true, CreatedAt = DateTime.UtcNow.AddDays(-8) },
                new Review { VehicleId = 6, UserId = john!.Id, Rating = 5, Comment = "Very luxurious MPV. Family loved it! Will rent again.", IsVerified = true, CreatedAt = DateTime.UtcNow.AddDays(-1) },
            };

            context.Reviews.AddRange(reviews);
            await context.SaveChangesAsync();
        }

        // ---- Promotions ----
        if (!context.Promotions.Any())
        {
            var promotions = new[]
            {
                new Promotion
                {
                    Code = "WELCOME50", Description = "Diskon 50% untuk pengguna baru", DiscountType = "percentage",
                    DiscountValue = 50, MinTransaction = 100_000, MaxDiscount = 200_000,
                    StartDate = DateTime.UtcNow.AddDays(-30), EndDate = DateTime.UtcNow.AddDays(60),
                    UsageLimit = 1000, IsActive = true
                },
                new Promotion
                {
                    Code = "WEEKEND25", Description = "Diskon 25% untuk sewa weekend", DiscountType = "percentage",
                    DiscountValue = 25, MinTransaction = 200_000, MaxDiscount = 150_000,
                    StartDate = DateTime.UtcNow.AddDays(-7), EndDate = DateTime.UtcNow.AddDays(90),
                    UsageLimit = 500, IsActive = true
                },
                new Promotion
                {
                    Code = "CASHBACK100", Description = "Cashback Rp 100.000 untuk transaksi di atas 500rb", DiscountType = "fixed",
                    DiscountValue = 100_000, MinTransaction = 500_000,
                    StartDate = DateTime.UtcNow.AddDays(-15), EndDate = DateTime.UtcNow.AddDays(45),
                    IsActive = true, RequiredTier = "Gold"
                },
                new Promotion
                {
                    Code = "PLATINUM30", Description = "Diskon 30% untuk member Platinum", DiscountType = "percentage",
                    DiscountValue = 30, MinTransaction = 0, MaxDiscount = 500_000,
                    StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(180),
                    IsActive = true, RequiredTier = "Platinum"
                }
            };

            context.Promotions.AddRange(promotions);
            await context.SaveChangesAsync();
        }

        // ---- Bookings (Sample) ----
        if (!context.Bookings.Any())
        {
            var customer1 = await userManager.FindByEmailAsync("customer1@rentalboil.com");
            var john = await userManager.FindByEmailAsync("john@example.com");

            var bookings = new[]
            {
                new Booking
                {
                    BookingNumber = $"RB-{DateTime.UtcNow:yyyyMMdd}-0001",
                    VehicleId = 1, CustomerId = customer1!.Id,
                    Status = BookingStatus.Completed,
                    StartDate = DateTime.UtcNow.AddDays(-10), EndDate = DateTime.UtcNow.AddDays(-7),
                    DurationDays = 3, DurationHours = 72,
                    BasePrice = 1_200_000, InsuranceCost = 150_000, TotalPrice = 1_350_000,
                    PaymentStatus = PaymentStatus.Paid, PaymentMethod = PaymentMethod.BankTransfer,
                    CreatedAt = DateTime.UtcNow.AddDays(-12), PaidAt = DateTime.UtcNow.AddDays(-12)
                },
                new Booking
                {
                    BookingNumber = $"RB-{DateTime.UtcNow:yyyyMMdd}-0002",
                    VehicleId = 2, CustomerId = john!.Id,
                    Status = BookingStatus.Active,
                    StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(2),
                    DurationDays = 3, DurationHours = 72,
                    BasePrice = 900_000, InsuranceCost = 105_000, TotalPrice = 1_005_000,
                    PaymentStatus = PaymentStatus.Paid, PaymentMethod = PaymentMethod.QRIS,
                    CreatedAt = DateTime.UtcNow.AddDays(-3), PaidAt = DateTime.UtcNow.AddDays(-3)
                },
                new Booking
                {
                    BookingNumber = $"RB-{DateTime.UtcNow:yyyyMMdd}-0003",
                    VehicleId = 4, CustomerId = customer1!.Id,
                    Status = BookingStatus.Pending,
                    StartDate = DateTime.UtcNow.AddDays(2), EndDate = DateTime.UtcNow.AddDays(5),
                    DurationDays = 3, DurationHours = 72,
                    BasePrice = 450_000, InsuranceCost = 45_000, TotalPrice = 495_000,
                    PaymentStatus = PaymentStatus.Unpaid,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                }
            };

            context.Bookings.AddRange(bookings);
            await context.SaveChangesAsync();
        }

        // ---- FAQs ----
        if (!context.Faqs.Any())
        {
            var faqs = new[]
            {
                new Faq { Question = "Bagaimana cara menyewa kendaraan?", Answer = "1. Registrasi akun\n2. Cari kendaraan yang diinginkan\n3. Klik 'Sewa Sekarang'\n4. Pilih tanggal dan durasi\n5. Lakukan pembayaran\n6. Ambil kendaraan di lokasi yang ditentukan!", Category = "Booking", SortOrder = 1, Language = "id" },
                new Faq { Question = "Apa saja syarat menyewa?", Answer = "Anda perlu memiliki KTP dan SIM yang masih berlaku. Dokumen akan diverifikasi oleh tim kami.", Category = "Booking", SortOrder = 2, Language = "id" },
                new Faq { Question = "Bagaimana cara membatalkan booking?", Answer = "Pembatalan dapat dilakukan melalui halaman 'Pesanan Saya'. Pembatalan H-2 akan mendapat refund 100%, H-1 refund 50%, dan hari H tidak dapat dibatalkan.", Category = "Booking", SortOrder = 3, Language = "id" },
                new Faq { Question = "Apakah ada asuransi?", Answer = "Ya, kami menyediakan opsi asuransi untuk melindungi Anda selama masa sewa. Biaya asuransi bervariasi tergantung kendaraan.", Category = "Insurance", SortOrder = 4, Language = "id" },
                new Faq { Question = "How to rent a vehicle?", Answer = "1. Register an account\n2. Search for desired vehicle\n3. Click 'Rent Now'\n4. Select dates and duration\n5. Make payment\n6. Pick up vehicle!", Category = "Booking", SortOrder = 1, Language = "en" },
                new Faq { Question = "What are the rental requirements?", Answer = "You need a valid ID (KTP) and driver's license (SIM). Documents will be verified.", Category = "Booking", SortOrder = 2, Language = "en" },
                new Faq { Question = "Bagaimana cara menjadi partner?", Answer = "Klik 'Daftar Partner' di halaman utama. Isi formulir dan upload dokumen yang diperlukan. Tim kami akan memverifikasi dalam 1x24 jam.", Category = "Partner", SortOrder = 5, Language = "id" },
                new Faq { Question = "Metode pembayaran apa yang tersedia?", Answer = "Kami menerima: E-Wallet (GoPay, OVO, Dana), Kartu Kredit, Transfer Bank (BCA, Mandiri, BNI, BRI), dan QRIS.", Category = "Payment", SortOrder = 6, Language = "id" }
            };

            context.Faqs.AddRange(faqs);
            await context.SaveChangesAsync();
        }

        // ---- System Settings ----
        if (!context.SystemSettings.Any())
        {
            var settings = new[]
            {
                new SystemSetting { Key = "AppName", Value = "RentalBoil", Group = "General", Description = "Application name" },
                new SystemSetting { Key = "MaxRentalDays", Value = "30", Group = "Booking", Description = "Maximum rental duration in days" },
                new SystemSetting { Key = "MinRentalHours", Value = "6", Group = "Booking", Description = "Minimum rental duration in hours" },
                new SystemSetting { Key = "CancellationRefundH2", Value = "100", Group = "Booking", Description = "Refund percentage for cancellation 2+ days before" },
                new SystemSetting { Key = "CancellationRefundH1", Value = "50", Group = "Booking", Description = "Refund percentage for cancellation 1 day before" },
                new SystemSetting { Key = "PlatformFee", Value = "5", Group = "Finance", Description = "Platform fee percentage" },
                new SystemSetting { Key = "LoyaltyPointsPerBooking", Value = "100", Group = "Loyalty", Description = "Base loyalty points per booking" },
                new SystemSetting { Key = "GPSUpdateInterval", Value = "3", Group = "IoT", Description = "GPS update interval in seconds" },
                new SystemSetting { Key = "MaintenanceMode", Value = "false", Group = "General", Description = "Maintenance mode toggle" }
            };

            context.SystemSettings.AddRange(settings);
            await context.SaveChangesAsync();
        }
    }
}
