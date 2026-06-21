using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RentalBoil.Models;

namespace RentalBoil.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await context.Database.EnsureCreatedAsync();

        // ---- Roles ----
        var roles = new[] { "Admin", "Partner", "Customer" };
        foreach (var role in roles)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));

        // ---- Users ----
        if (!context.Users.Any())
        {
            var users = new (ApplicationUser User, string Password, string Role)[]
            {
                (new ApplicationUser { UserName = "admin@rentalboil.com", Email = "admin@rentalboil.com", FullName = "Super Admin", PhoneNumber = "081111111111", Role = UserRole.Admin, KtpVerified = true, SimVerified = true, MembershipTier = "Platinum", LoyaltyPoints = 5000, RegisteredAt = DateTime.UtcNow, Language = "id" }, "Admin123!", "Admin"),
                (new ApplicationUser { UserName = "partner1@rentalboil.com", Email = "partner1@rentalboil.com", FullName = "Budi Santoso", PhoneNumber = "082222222222", Role = UserRole.Partner, KtpVerified = true, SimVerified = true, MembershipTier = "Gold", LoyaltyPoints = 2000, RegisteredAt = DateTime.UtcNow.AddDays(-30), Language = "id" }, "Partner123!", "Partner"),
                (new ApplicationUser { UserName = "partner2@rentalboil.com", Email = "partner2@rentalboil.com", FullName = "Siti Rahayu", PhoneNumber = "082333333333", Role = UserRole.Partner, KtpVerified = true, SimVerified = true, RegisteredAt = DateTime.UtcNow.AddDays(-20), Language = "id" }, "Partner123!", "Partner"),
                (new ApplicationUser { UserName = "customer1@rentalboil.com", Email = "customer1@rentalboil.com", FullName = "Andi Pratama", PhoneNumber = "083444444444", Role = UserRole.Customer, KtpVerified = true, SimVerified = true, MembershipTier = "Silver", LoyaltyPoints = 500, RegisteredAt = DateTime.UtcNow.AddDays(-15), Language = "id" }, "Customer123!", "Customer"),
                (new ApplicationUser { UserName = "customer2@rentalboil.com", Email = "customer2@rentalboil.com", FullName = "Dewi Lestari", PhoneNumber = "083555555555", Role = UserRole.Customer, KtpVerified = false, SimVerified = false, MembershipTier = "Basic", LoyaltyPoints = 0, RegisteredAt = DateTime.UtcNow.AddDays(-5), Language = "id" }, "Customer123!", "Customer"),
                (new ApplicationUser { UserName = "john@example.com", Email = "john@example.com", FullName = "John Smith", PhoneNumber = "086666666666", Role = UserRole.Customer, KtpVerified = true, SimVerified = true, MembershipTier = "Gold", LoyaltyPoints = 1500, RegisteredAt = DateTime.UtcNow.AddDays(-10), Language = "en" }, "Customer123!", "Customer")
            };
            foreach (var u in users) { var r = await userManager.CreateAsync(u.User, u.Password); if (r.Succeeded) await userManager.AddToRoleAsync(u.User, u.Role); }
        }
        await context.SaveChangesAsync();

        // ---- Vehicles ----
        if (!context.Vehicles.Any())
        {
            var p1 = await userManager.FindByEmailAsync("partner1@rentalboil.com");
            var p2 = await userManager.FindByEmailAsync("partner2@rentalboil.com");

            var vehicles = new[]
            {
                new Vehicle { Name = "Toyota Avanza 2024", PlateNumber = "B 1234 AB", Type = VehicleType.Car, Brand = "Toyota", Model = "Avanza", Year = 2024, Color = "Silver", Transmission = TransmissionType.Automatic, FuelType = FuelType.Petrol, Capacity = 7, LuggageCapacity = 200, PricePerHour = 50_000, PricePerDay = 400_000, DynamicPriceMultiplier = 1.2m, Description = "Mobil keluarga terbaik dengan kapasitas 7 penumpang.", Location = "Jl. Sudirman No. 123, Jakarta Pusat", Latitude = -6.2088, Longitude = 106.8456, OwnerId = p1!.Id, IsAvailable = true, IsVerified = true, InsuranceAvailable = true, InsuranceCostPerDay = 50_000, RentalCount = 25, AverageRating = 4.5, ReviewCount = 18 },
                new Vehicle { Name = "Honda Brio 2024", PlateNumber = "B 5678 CD", Type = VehicleType.Car, Brand = "Honda", Model = "Brio", Year = 2024, Color = "Red", Transmission = TransmissionType.Automatic, FuelType = FuelType.Petrol, Capacity = 5, LuggageCapacity = 150, PricePerHour = 40_000, PricePerDay = 300_000, Description = "City car irit dan lincah.", Location = "Jl. Thamrin No. 45, Jakarta Pusat", Latitude = -6.1825, Longitude = 106.8230, OwnerId = p1!.Id, IsAvailable = true, IsVerified = true, InsuranceAvailable = true, InsuranceCostPerDay = 35_000, RentalCount = 42, AverageRating = 4.7, ReviewCount = 30 },
                new Vehicle { Name = "Honda CR-V 2024", PlateNumber = "B 9012 EF", Type = VehicleType.Car, Brand = "Honda", Model = "CR-V", Year = 2024, Color = "White", Transmission = TransmissionType.Automatic, FuelType = FuelType.Hybrid, Capacity = 5, LuggageCapacity = 500, PricePerHour = 80_000, PricePerDay = 650_000, DynamicPriceMultiplier = 1.3m, Description = "SUV premium hybrid.", Location = "Jl. Sudirman No. 123, Jakarta Pusat", Latitude = -6.2088, Longitude = 106.8456, OwnerId = p1!.Id, IsAvailable = true, IsVerified = true, InsuranceAvailable = true, InsuranceCostPerDay = 80_000, RentalCount = 15, AverageRating = 4.8, ReviewCount = 12 },
                new Vehicle { Name = "Yamaha NMAX 2024", PlateNumber = "B 3456 GH", Type = VehicleType.Motorcycle, Brand = "Yamaha", Model = "NMAX", Year = 2024, Color = "Black", Transmission = TransmissionType.Automatic, FuelType = FuelType.Petrol, Capacity = 2, PricePerHour = 20_000, PricePerDay = 150_000, Description = "Motor matic premium.", Location = "Jl. Sudirman No. 123, Jakarta Pusat", Latitude = -6.2088, Longitude = 106.8456, OwnerId = p1!.Id, IsAvailable = true, IsVerified = true, InsuranceAvailable = true, InsuranceCostPerDay = 15_000, RentalCount = 55, AverageRating = 4.6, ReviewCount = 40 },
                new Vehicle { Name = "Honda PCX 160 2024", PlateNumber = "B 7890 IJ", Type = VehicleType.Motorcycle, Brand = "Honda", Model = "PCX 160", Year = 2024, Color = "Blue", Transmission = TransmissionType.Automatic, FuelType = FuelType.Petrol, Capacity = 2, PricePerHour = 25_000, PricePerDay = 180_000, Description = "Motor matic mewah.", Location = "Jl. Gatot Subroto No. 67, Jakarta Selatan", Latitude = -6.2387, Longitude = 106.8240, OwnerId = p2!.Id, IsAvailable = true, IsVerified = true, InsuranceAvailable = false, RentalCount = 38, AverageRating = 4.4, ReviewCount = 25 },
                new Vehicle { Name = "Toyota Innova Zenix 2024", PlateNumber = "B 1122 KL", Type = VehicleType.Car, Brand = "Toyota", Model = "Innova Zenix", Year = 2024, Color = "Black", Transmission = TransmissionType.Automatic, FuelType = FuelType.Hybrid, Capacity = 7, LuggageCapacity = 300, PricePerHour = 90_000, PricePerDay = 750_000, DynamicPriceMultiplier = 1.25m, Description = "MPV premium hybrid.", Location = "Jl. Gatot Subroto No. 67, Jakarta Selatan", Latitude = -6.2387, Longitude = 106.8240, OwnerId = p2!.Id, IsAvailable = true, IsVerified = true, InsuranceAvailable = true, InsuranceCostPerDay = 90_000, RentalCount = 10, AverageRating = 4.9, ReviewCount = 8 },
                new Vehicle { Name = "Suzuki Carry Pickup 2023", PlateNumber = "B 3344 MN", Type = VehicleType.Car, Brand = "Suzuki", Model = "Carry", Year = 2023, Color = "White", Transmission = TransmissionType.Manual, FuelType = FuelType.Petrol, Capacity = 2, PricePerHour = 35_000, PricePerDay = 280_000, Description = "Kendaraan niaga.", Location = "Jl. Gatot Subroto No. 67, Jakarta Selatan", Latitude = -6.2387, Longitude = 106.8240, OwnerId = p2!.Id, IsAvailable = true, IsVerified = true, InsuranceAvailable = false, RentalCount = 20, AverageRating = 4.2, ReviewCount = 15 },
                new Vehicle { Name = "Vespa Sprint 2024", PlateNumber = "B 5566 OP", Type = VehicleType.Motorcycle, Brand = "Vespa", Model = "Sprint", Year = 2024, Color = "Red", Transmission = TransmissionType.Automatic, FuelType = FuelType.Petrol, Capacity = 2, PricePerHour = 30_000, PricePerDay = 220_000, DynamicPriceMultiplier = 1.5m, Description = "Skuter ikonik Italia.", Location = "Jl. Kemang Raya No. 88, Jakarta Selatan", Latitude = -6.2608, Longitude = 106.8101, OwnerId = p1!.Id, IsAvailable = true, IsVerified = true, InsuranceAvailable = true, InsuranceCostPerDay = 25_000, RentalCount = 30, AverageRating = 4.3, ReviewCount = 22 }
            };

            context.Vehicles.AddRange(vehicles);
            await context.SaveChangesAsync();

            // NOTE: Tidak ada seeding foto placeholder. Partner bisa upload foto sendiri via /partner/vehicles.
            // Halaman kendaraan akan menampilkan emoji 🚗/🏍️ jika belum ada foto.
        }

        // ---- Reviews ----
        if (!context.Reviews.Any())
        {
            var c1 = await userManager.FindByEmailAsync("customer1@rentalboil.com");
            var j = await userManager.FindByEmailAsync("john@example.com");
            context.Reviews.AddRange(
                new Review { VehicleId = 1, UserId = c1!.Id, Rating = 5, Comment = "Mobil sangat bersih dan nyaman. Recommended!", IsVerified = true, CreatedAt = DateTime.UtcNow.AddDays(-5) },
                new Review { VehicleId = 1, UserId = j!.Id, Rating = 4, Comment = "Good family car.", IsVerified = true, CreatedAt = DateTime.UtcNow.AddDays(-10) },
                new Review { VehicleId = 2, UserId = c1!.Id, Rating = 5, Comment = "Irit banget!", IsVerified = true, CreatedAt = DateTime.UtcNow.AddDays(-3) },
                new Review { VehicleId = 2, UserId = j!.Id, Rating = 5, Comment = "Excellent fuel efficiency.", IsVerified = true, CreatedAt = DateTime.UtcNow.AddDays(-7) },
                new Review { VehicleId = 3, UserId = c1!.Id, Rating = 5, Comment = "Mobil mewah performa mantap!", IsVerified = true, CreatedAt = DateTime.UtcNow.AddDays(-2) },
                new Review { VehicleId = 4, UserId = j!.Id, Rating = 4, Comment = "Smooth ride.", IsVerified = true, CreatedAt = DateTime.UtcNow.AddDays(-6) },
                new Review { VehicleId = 5, UserId = c1!.Id, Rating = 5, Comment = "PCX paling nyaman.", IsVerified = true, CreatedAt = DateTime.UtcNow.AddDays(-8) },
                new Review { VehicleId = 6, UserId = j!.Id, Rating = 5, Comment = "Very luxurious MPV.", IsVerified = true, CreatedAt = DateTime.UtcNow.AddDays(-1) }
            );
            await context.SaveChangesAsync();
        }

        // ---- Promotions ----
        if (!context.Promotions.Any())
        {
            context.Promotions.AddRange(
                new Promotion { Code = "WELCOME50", Description = "Diskon 50% untuk pengguna baru", DiscountType = "percentage", DiscountValue = 50, MinTransaction = 100_000, MaxDiscount = 200_000, StartDate = DateTime.UtcNow.AddDays(-30), EndDate = DateTime.UtcNow.AddDays(60), UsageLimit = 1000, IsActive = true },
                new Promotion { Code = "WEEKEND25", Description = "Diskon 25% untuk sewa weekend", DiscountType = "percentage", DiscountValue = 25, MinTransaction = 200_000, MaxDiscount = 150_000, StartDate = DateTime.UtcNow.AddDays(-7), EndDate = DateTime.UtcNow.AddDays(90), UsageLimit = 500, IsActive = true },
                new Promotion { Code = "CASHBACK100", Description = "Cashback Rp 100.000", DiscountType = "fixed", DiscountValue = 100_000, MinTransaction = 500_000, StartDate = DateTime.UtcNow.AddDays(-15), EndDate = DateTime.UtcNow.AddDays(45), IsActive = true, RequiredTier = "Gold" },
                new Promotion { Code = "PLATINUM30", Description = "Diskon 30% untuk member Platinum", DiscountType = "percentage", DiscountValue = 30, MinTransaction = 0, MaxDiscount = 500_000, StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(180), IsActive = true, RequiredTier = "Platinum" }
            );
            await context.SaveChangesAsync();
        }

        // ---- Bookings ----
        if (!context.Bookings.Any())
        {
            var c1 = await userManager.FindByEmailAsync("customer1@rentalboil.com");
            var j = await userManager.FindByEmailAsync("john@example.com");
            context.Bookings.AddRange(
                new Booking { BookingNumber = $"RB-{DateTime.UtcNow:yyyyMMdd}-0001", VehicleId = 1, CustomerId = c1!.Id, Status = BookingStatus.Completed, StartDate = DateTime.UtcNow.AddDays(-10), EndDate = DateTime.UtcNow.AddDays(-7), DurationDays = 3, DurationHours = 72, BasePrice = 1_200_000, InsuranceCost = 150_000, TotalPrice = 1_350_000, PaymentStatus = PaymentStatus.Paid, PaymentMethod = PaymentMethod.BankTransfer, CreatedAt = DateTime.UtcNow.AddDays(-12), PaidAt = DateTime.UtcNow.AddDays(-12) },
                new Booking { BookingNumber = $"RB-{DateTime.UtcNow:yyyyMMdd}-0002", VehicleId = 2, CustomerId = j!.Id, Status = BookingStatus.Active, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow.AddDays(2), DurationDays = 3, DurationHours = 72, BasePrice = 900_000, InsuranceCost = 105_000, TotalPrice = 1_005_000, PaymentStatus = PaymentStatus.Paid, PaymentMethod = PaymentMethod.QRIS, CreatedAt = DateTime.UtcNow.AddDays(-3), PaidAt = DateTime.UtcNow.AddDays(-3) },
                new Booking { BookingNumber = $"RB-{DateTime.UtcNow:yyyyMMdd}-0003", VehicleId = 4, CustomerId = c1!.Id, Status = BookingStatus.Pending, StartDate = DateTime.UtcNow.AddDays(2), EndDate = DateTime.UtcNow.AddDays(5), DurationDays = 3, DurationHours = 72, BasePrice = 450_000, InsuranceCost = 45_000, TotalPrice = 495_000, PaymentStatus = PaymentStatus.Unpaid, CreatedAt = DateTime.UtcNow.AddDays(-1) }
            );
            await context.SaveChangesAsync();
        }

        // ---- FAQs ----
        if (!context.Faqs.Any())
        {
            context.Faqs.AddRange(
                new Faq { Question = "Bagaimana cara menyewa kendaraan?", Answer = "1. Registrasi\n2. Cari kendaraan\n3. Booking\n4. Bayar\n5. Ambil kendaraan!", Category = "Booking", SortOrder = 1, Language = "id" },
                new Faq { Question = "Apa saja syarat menyewa?", Answer = "KTP dan SIM yang masih berlaku.", Category = "Booking", SortOrder = 2, Language = "id" },
                new Faq { Question = "Bagaimana cara membatalkan booking?", Answer = "Melalui halaman Pesanan Saya. H-2 refund 100%, H-1 refund 50%.", Category = "Booking", SortOrder = 3, Language = "id" },
                new Faq { Question = "Apakah ada asuransi?", Answer = "Ya, tersedia opsi asuransi untuk melindungi Anda.", Category = "Insurance", SortOrder = 4, Language = "id" },
                new Faq { Question = "How to rent a vehicle?", Answer = "1. Register\n2. Search\n3. Book\n4. Pay\n5. Pick up!", Category = "Booking", SortOrder = 1, Language = "en" },
                new Faq { Question = "What are the rental requirements?", Answer = "Valid ID (KTP) and driver's license (SIM).", Category = "Booking", SortOrder = 2, Language = "en" },
                new Faq { Question = "Bagaimana cara menjadi partner?", Answer = "Klik Daftar Partner, isi formulir, upload dokumen.", Category = "Partner", SortOrder = 5, Language = "id" },
                new Faq { Question = "Metode pembayaran apa yang tersedia?", Answer = "E-Wallet, Kartu Kredit, Transfer Bank, QRIS.", Category = "Payment", SortOrder = 6, Language = "id" }
            );
            await context.SaveChangesAsync();
        }

        // ---- System Settings ----
        if (!context.SystemSettings.Any())
        {
            context.SystemSettings.AddRange(
                new SystemSetting { Key = "AppName", Value = "RentalBoil", Group = "General", Description = "Application name" },
                new SystemSetting { Key = "MaxRentalDays", Value = "30", Group = "Booking", Description = "Maximum rental duration in days" },
                new SystemSetting { Key = "MinRentalHours", Value = "6", Group = "Booking", Description = "Minimum rental duration in hours" },
                new SystemSetting { Key = "CancellationRefundH2", Value = "100", Group = "Booking", Description = "Refund % for cancellation 2+ days before" },
                new SystemSetting { Key = "CancellationRefundH1", Value = "50", Group = "Booking", Description = "Refund % for cancellation 1 day before" },
                new SystemSetting { Key = "PlatformFee", Value = "5", Group = "Finance", Description = "Platform fee percentage" },
                new SystemSetting { Key = "LoyaltyPointsPerBooking", Value = "100", Group = "Loyalty", Description = "Base loyalty points per booking" },
                new SystemSetting { Key = "GPSUpdateInterval", Value = "3", Group = "IoT", Description = "GPS update interval in seconds" },
                new SystemSetting { Key = "MaintenanceMode", Value = "false", Group = "General", Description = "Maintenance mode toggle" }
            );
            await context.SaveChangesAsync();
        }
    }
}
