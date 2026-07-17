using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WashUp.Models;

namespace WashUp.Data;

/// <summary>
/// Seeds the database with sample data including users, branches, orders, inventory, etc.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Skip if already seeded
        if (await context.Users.AnyAsync()) return;

        // --- Create Branches ---
        var branches = new[]
        {
            new Branch { Id = 1, Name = "WashUp Pusat - Jakarta", Address = "Jl. Sudirman No. 123, Jakarta Pusat", Phone = "021-555-0101", Email = "pusat@washup.id", Latitude = -6.2088, Longitude = 106.8456, IsActive = true },
            new Branch { Id = 2, Name = "WashUp Cabang - Bandung", Address = "Jl. Dago No. 45, Bandung", Phone = "022-555-0202", Email = "bandung@washup.id", Latitude = -6.9147, Longitude = 107.6098, IsActive = true },
            new Branch { Id = 3, Name = "WashUp Cabang - Surabaya", Address = "Jl. Tunjungan No. 78, Surabaya", Phone = "031-555-0303", Email = "surabaya@washup.id", Latitude = -7.2575, Longitude = 112.7521, IsActive = true }
        };
        context.Branches.AddRange(branches);
        await context.SaveChangesAsync();

        // --- Create Users ---
        var hasher = new PasswordHasher<ApplicationUser>();

        var owner = new ApplicationUser
        {
            Id = "user-owner",
            UserName = "owner@washup.id",
            NormalizedUserName = "OWNER@WASHUP.ID",
            Email = "owner@washup.id",
            NormalizedEmail = "OWNER@WASHUP.ID",
            FullName = "Budi Santoso",
            PhoneNumber = "0811-1111-0001",
            Address = "Jl. Menteng No. 1, Jakarta",
            BranchId = 1,
            MembershipTier = "Platinum",
            LoyaltyPoints = 5000,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddMonths(-12)
        };
        owner.PasswordHash = hasher.HashPassword(owner, "WashUp@2024");

        var admin = new ApplicationUser
        {
            Id = "user-admin",
            UserName = "admin@washup.id",
            NormalizedUserName = "ADMIN@WASHUP.ID",
            Email = "admin@washup.id",
            NormalizedEmail = "ADMIN@WASHUP.ID",
            FullName = "Siti Rahayu",
            PhoneNumber = "0811-1111-0002",
            Address = "Jl. Kebon Jeruk No. 5, Jakarta",
            BranchId = 1,
            MembershipTier = "Gold",
            LoyaltyPoints = 2500,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddMonths(-10)
        };
        admin.PasswordHash = hasher.HashPassword(admin, "WashUp@2024");

        var courier1 = new ApplicationUser
        {
            Id = "user-courier1",
            UserName = "kurir1@washup.id",
            NormalizedUserName = "KURIR1@WASHUP.ID",
            Email = "kurir1@washup.id",
            NormalizedEmail = "KURIR1@WASHUP.ID",
            FullName = "Agus Hermawan",
            PhoneNumber = "0811-1111-0003",
            Address = "Jl. Kemang No. 15, Jakarta",
            BranchId = 1,
            MembershipTier = "Silver",
            LoyaltyPoints = 800,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddMonths(-8)
        };
        courier1.PasswordHash = hasher.HashPassword(courier1, "WashUp@2024");

        var courier2 = new ApplicationUser
        {
            Id = "user-courier2",
            UserName = "kurir2@washup.id",
            NormalizedUserName = "KURIR2@WASHUP.ID",
            Email = "kurir2@washup.id",
            NormalizedEmail = "KURIR2@WASHUP.ID",
            FullName = "Dedi Kurniawan",
            PhoneNumber = "0811-1111-0004",
            Address = "Jl. Antapani No. 22, Bandung",
            BranchId = 2,
            MembershipTier = "Regular",
            LoyaltyPoints = 350,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddMonths(-6)
        };
        courier2.PasswordHash = hasher.HashPassword(courier2, "WashUp@2024");

        // Customer sample users
        var customers = new List<ApplicationUser>();
        var customerNames = new[] { "Rina Melati", "Andi Pratama", "Dewi Lestari", "Bambang Wibowo", "Citra Ayu", 
            "Eko Saputro", "Fitri Handayani", "Gunawan Wibisono", "Hana Permata", "Indra Gunawan",
            "Joko Susilo", "Kartika Sari", "Lutfi Hakim", "Maya Indah", "Nina Agustina" };
        
        for (int i = 0; i < customerNames.Length; i++)
        {
            var cust = new ApplicationUser
            {
                Id = $"user-cust{i + 1:D2}",
                UserName = $"pelanggan{i + 1}@email.com",
                NormalizedUserName = $"PELANGGAN{i + 1}@EMAIL.COM",
                Email = $"pelanggan{i + 1}@email.com",
                NormalizedEmail = $"PELANGGAN{i + 1}@EMAIL.COM",
                FullName = customerNames[i],
                PhoneNumber = $"0812-{1000 + i:D4}-{1000 + i:D4}",
                Address = $"Jl. Merdeka No. {i + 1}, Kota Contoh",
                BranchId = (i % 3) + 1,
                MembershipTier = i switch { < 3 => "Platinum", < 7 => "Gold", < 11 => "Silver", _ => "Regular" },
                LoyaltyPoints = new Random().Next(100, 3000),
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-new Random().Next(30, 365)),
                PreferredServiceType = i % 3 == 0 ? "Express" : (i % 2 == 0 ? "Kiloan" : "CuciKering"),
                PreferredDetergent = i % 2 == 0 ? "Standard" : "Hypoallergenic",
                PreferredFragrance = new[] { "Lavender", "Rose", "Fresh Linen", "Ocean Breeze", "Baby Powder" }[i % 5]
            };
            cust.PasswordHash = hasher.HashPassword(cust, "Pelanggan@123");
            customers.Add(cust);
        }

        var allUsers = new List<ApplicationUser> { owner, admin, courier1, courier2 };
        allUsers.AddRange(customers);
        context.Users.AddRange(allUsers);
        await context.SaveChangesAsync();

        // --- Assign Roles ---
        await userManager.AddToRoleAsync(owner, "Pemilik");
        await userManager.AddToRoleAsync(admin, "Admin");
        await userManager.AddToRoleAsync(courier1, "Kurir");
        await userManager.AddToRoleAsync(courier2, "Kurir");
        foreach (var cust in customers)
            await userManager.AddToRoleAsync(cust, "Pelanggan");

        // --- Create Staff Members ---
        var staffMembers = new[]
        {
            new StaffMember { FullName = "Budi Santoso", Position = "Owner", BranchId = 1, BaseSalary = 0, EmploymentType = "FullTime", HireDate = DateTime.UtcNow.AddMonths(-12), IsActive = true, UserId = "user-owner" },
            new StaffMember { FullName = "Siti Rahayu", Position = "Admin", BranchId = 1, BaseSalary = 6000000, EmploymentType = "FullTime", HireDate = DateTime.UtcNow.AddMonths(-10), IsActive = true, UserId = "user-admin" },
            new StaffMember { FullName = "Agus Hermawan", Position = "Kurir", BranchId = 1, BaseSalary = 4500000, EmploymentType = "FullTime", HireDate = DateTime.UtcNow.AddMonths(-8), IsActive = true, UserId = "user-courier1" },
            new StaffMember { FullName = "Dedi Kurniawan", Position = "Kurir", BranchId = 2, BaseSalary = 4300000, EmploymentType = "FullTime", HireDate = DateTime.UtcNow.AddMonths(-6), IsActive = true, UserId = "user-courier2" },
            new StaffMember { FullName = "Wati Sumarni", Position = "Operator", BranchId = 1, BaseSalary = 4000000, EmploymentType = "FullTime", HireDate = DateTime.UtcNow.AddMonths(-5), IsActive = true },
            new StaffMember { FullName = "Hendra Gunawan", Position = "Operator", BranchId = 2, BaseSalary = 3800000, EmploymentType = "FullTime", HireDate = DateTime.UtcNow.AddMonths(-4), IsActive = true },
            new StaffMember { FullName = "Susi Susanti", Position = "Operator", BranchId = 3, BaseSalary = 3900000, EmploymentType = "FullTime", HireDate = DateTime.UtcNow.AddMonths(-3), IsActive = true }
        };
        context.StaffMembers.AddRange(staffMembers);
        await context.SaveChangesAsync();

        // --- Create Staff Schedules ---
        foreach (var staff in staffMembers.Where(s => s.Position != "Owner"))
        {
            for (int d = 1; d <= 5; d++) // Mon-Fri
            {
                context.StaffSchedules.Add(new StaffSchedule
                {
                    StaffMemberId = staff.Id,
                    DayOfWeek = (DayOfWeek)d,
                    StartTime = new TimeSpan(8, 0, 0),
                    EndTime = new TimeSpan(17, 0, 0),
                    IsActive = true
                });
            }
        }
        await context.SaveChangesAsync();

        // --- Create Inventory Items ---
        var inventoryItems = new[]
        {
            new InventoryItem { Name = "Detergen Cair Premium", Category = "Detergent", Unit = "liter", CurrentStock = 45.5, MinimumStock = 10, MaximumStock = 100, BranchId = 1, Description = "Detergen cair untuk cuci premium" },
            new InventoryItem { Name = "Detergen Bubuk Standard", Category = "Detergent", Unit = "kg", CurrentStock = 32, MinimumStock = 8, MaximumStock = 80, BranchId = 1, Description = "Detergen bubuk standar" },
            new InventoryItem { Name = "Pewangi Lavender", Category = "Fragrance", Unit = "liter", CurrentStock = 12, MinimumStock = 5, MaximumStock = 50, BranchId = 1, Description = "Pewangi aroma lavender" },
            new InventoryItem { Name = "Pewangi Ocean Breeze", Category = "Fragrance", Unit = "liter", CurrentStock = 8.5, MinimumStock = 5, MaximumStock = 50, BranchId = 1, Description = "Pewangi aroma ocean breeze" },
            new InventoryItem { Name = "Plastik Laundry Besar", Category = "Plastic", Unit = "pcs", CurrentStock = 15, MinimumStock = 20, MaximumStock = 200, BranchId = 1, Description = "Plastik besar untuk packing" },
            new InventoryItem { Name = "Plastik Laundry Kecil", Category = "Plastic", Unit = "pcs", CurrentStock = 45, MinimumStock = 25, MaximumStock = 300, BranchId = 1, Description = "Plastik kecil untuk packing" },
            new InventoryItem { Name = "Hanger Plastik", Category = "Other", Unit = "pcs", CurrentStock = 200, MinimumStock = 50, MaximumStock = 500, BranchId = 1, Description = "Hanger plastik standar" },
            new InventoryItem { Name = "Pelicin Pakaian", Category = "Other", Unit = "liter", CurrentStock = 6, MinimumStock = 3, MaximumStock = 30, BranchId = 1, Description = "Cairan pelicin setrika" }
        };
        context.InventoryItems.AddRange(inventoryItems);
        await context.SaveChangesAsync();

        // --- Create IoT Devices ---
        var iotDevices = new[]
        {
            new IoTDevice { Name = "Mesin Cuci #1", DeviceType = "MesinCuci", DeviceId = "MC-001", BranchId = 1, IsSimulated = true, Status = "Running" },
            new IoTDevice { Name = "Mesin Cuci #2", DeviceType = "MesinCuci", DeviceId = "MC-002", BranchId = 1, IsSimulated = true, Status = "Running" },
            new IoTDevice { Name = "Mesin Cuci #3", DeviceType = "MesinCuci", DeviceId = "MC-003", BranchId = 1, IsSimulated = true, Status = "Offline" },
            new IoTDevice { Name = "Listrik Meter", DeviceType = "Listrik", DeviceId = "EL-001", BranchId = 1, IsSimulated = true, Status = "Online" },
            new IoTDevice { Name = "Air Meter", DeviceType = "Air", DeviceId = "WM-001", BranchId = 1, IsSimulated = true, Status = "Online" },
            new IoTDevice { Name = "Sensor Suhu Ruangan", DeviceType = "SensorSuhu", DeviceId = "TS-001", BranchId = 1, IsSimulated = true, Status = "Online" }
        };
        context.IoTDevices.AddRange(iotDevices);
        await context.SaveChangesAsync();

        // --- Create Sample Orders ---
        var random = new Random();
        var serviceTypes = new[] { "CuciKering", "Setrika", "Express", "Kiloan", "CuciLipat" };
        var statuses = new[] { "Diterima", "Dicuci", "Disetrika", "Selesai", "Dikirim" };
        var paymentStatuses = new[] { "Lunas", "BelumBayar", "DP" };
        var paymentMethods = new[] { "OVO", "GoPay", "Dana", "QRIS", "Transfer", "Cash" };

        for (int i = 0; i < 50; i++)
        {
            var customer = customers[random.Next(customers.Count)];
            var serviceType = serviceTypes[random.Next(serviceTypes.Length)];
            var weight = Math.Round(random.NextDouble() * 9 + 1, 1); // 1-10 kg
            var pricePerKg = serviceType switch
            {
                "Express" => 12000m,
                "Kiloan" => 7000m,
                "CuciKering" => 8000m,
                "Setrika" => 6000m,
                _ => 8000m
            };
            var subtotal = (decimal)weight * pricePerKg;
            var discount = customer.MembershipTier switch
            {
                "Platinum" => subtotal * 0.15m,
                "Gold" => subtotal * 0.10m,
                "Silver" => subtotal * 0.05m,
                _ => 0
            };
            var tax = (subtotal - discount) * 0.1m;
            var total = subtotal - discount + tax;
            var status = statuses[random.Next(statuses.Length)];
            var pStatus = status == "Selesai" || status == "Dikirim" ? "Lunas" : paymentStatuses[random.Next(2)];

            var order = new Order
            {
                OrderNumber = $"WO-{DateTime.UtcNow.AddDays(-i):yyyyMMdd}-{i + 1:D4}",
                UserId = customer.Id,
                BranchId = customer.BranchId,
                ServiceType = serviceType,
                WeightKg = weight,
                ItemCount = random.Next(3, 20),
                ItemDescription = $"Pakaian campuran - {random.Next(3, 15)} items",
                DetergentPreference = customer.PreferredDetergent,
                FragrancePreference = customer.PreferredFragrance,
                PricePerKg = pricePerKg,
                Subtotal = subtotal,
                Discount = discount,
                TaxAmount = tax,
                TotalAmount = total,
                Status = status,
                ReceivedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30)),
                EstimatedCompletion = DateTime.UtcNow.AddDays(random.Next(1, 5)),
                PaymentStatus = pStatus,
                PaymentMethod = pStatus == "Lunas" ? paymentMethods[random.Next(paymentMethods.Length)] : null,
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30)),
                Notes = random.Next(3) == 0 ? "Harap hati-hati dengan bahan sutra" : null
            };

            // Set status dates
            if (status == "Dicuci" || status == "Disetrika" || status == "Selesai" || status == "Dikirim")
                order.WashedAt = order.ReceivedAt?.AddHours(random.Next(2, 6));
            if (status == "Disetrika" || status == "Selesai" || status == "Dikirim")
                order.IronedAt = order.WashedAt?.AddHours(random.Next(1, 3));
            if (status == "Selesai" || status == "Dikirim")
                order.CompletedAt = order.IronedAt?.AddHours(random.Next(1, 2));
            if (status == "Dikirim")
                order.DeliveredAt = order.CompletedAt?.AddHours(random.Next(1, 4));

            context.Orders.Add(order);
        }
        await context.SaveChangesAsync();

        // --- Create Invoices for paid orders ---
        var paidOrders = context.Orders.Where(o => o.PaymentStatus == "Lunas").ToList();
        foreach (var order in paidOrders)
        {
            context.Invoices.Add(new Invoice
            {
                InvoiceNumber = $"INV-{order.CreatedAt:yyyyMMdd}-{order.Id:D4}",
                OrderId = order.Id,
                Subtotal = order.Subtotal,
                Discount = order.Discount,
                TaxAmount = order.TaxAmount,
                TotalAmount = order.TotalAmount,
                PaymentStatus = "Paid",
                PaymentMethod = order.PaymentMethod,
                PaidAt = order.CompletedAt ?? order.CreatedAt.AddDays(1),
                DueDate = order.CreatedAt.AddDays(7),
                CreatedAt = order.CreatedAt
            });
        }
        await context.SaveChangesAsync();

        // --- Create Financial Transactions (6 bulan, untuk grafik laporan) ---
        var expenseCategories = new[] { "Supplies", "Salary", "Utility", "Maintenance", "Other" };
        for (int m = 0; m < 6; m++)
        {
            var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-m);
            var daysInMonth = m == 0 ? Math.Max(1, DateTime.UtcNow.Day - 1) : DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
            var incomeCount = random.Next(15, 25);
            for (int i = 0; i < incomeCount; i++)
            {
                context.FinancialTransactions.Add(new FinancialTransaction
                {
                    TransactionType = "Income",
                    Category = "OrderPayment",
                    Description = "Pembayaran order laundry",
                    Amount = (decimal)(random.NextDouble() * 250000 + 30000),
                    BranchId = random.Next(1, 4),
                    TransactionDate = monthStart.AddDays(random.Next(daysInMonth)).AddHours(random.Next(8, 20)),
                    RecordedByUserId = "user-admin"
                });
            }
            var expenseCount = random.Next(6, 10);
            for (int i = 0; i < expenseCount; i++)
            {
                context.FinancialTransactions.Add(new FinancialTransaction
                {
                    TransactionType = "Expense",
                    Category = expenseCategories[random.Next(expenseCategories.Length)],
                    Description = "Pengeluaran operasional",
                    Amount = (decimal)(random.NextDouble() * 400000 + 50000),
                    BranchId = random.Next(1, 4),
                    TransactionDate = monthStart.AddDays(random.Next(daysInMonth)).AddHours(random.Next(8, 20)),
                    RecordedByUserId = "user-admin"
                });
            }
        }
        await context.SaveChangesAsync();

        // --- Create Reviews ---
        var completedOrders = context.Orders.Where(o => o.Status == "Selesai" || o.Status == "Dikirim").ToList();
        var reviewComments = new[]
        {
            "Hasil cucian bersih sekali! Wanginya tahan lama. Recommended!",
            "Pelayanan cepat dan rapi. Setrikaan rapi banget.",
            "Kurirnya ramah, datang tepat waktu. Laundry terbaik!",
            "Agak lama sedikit tapi hasilnya memuaskan.",
            "Bersih, rapi, wangi. Langganan terus nih!",
            "Harga terjangkau dengan kualitas premium. Suka!",
            "Packing rapi, tidak ada yang rusak atau tertukar.",
            "Express service-nya cepat banget, 6 jam udah beres!",
            "Kadang ada noda yang masih tersisa, tapi overall ok.",
            "Pelayanan customer service-nya baik banget, fast response!"
        };

        foreach (var order in completedOrders.Take(30))
        {
            context.Reviews.Add(new Review
            {
                OrderId = order.Id,
                UserId = order.UserId,
                Rating = random.Next(3, 6), // 3-5 stars
                Comment = reviewComments[random.Next(reviewComments.Length)],
                CreatedAt = order.CompletedAt?.AddDays(random.Next(1, 3)) ?? DateTime.UtcNow,
                IsVisible = true
            });
        }
        await context.SaveChangesAsync();

        // --- Create Notifications ---
        var notifications = new[]
        {
            new Notification { Type = "Promo", Title = "Promo Ramadhan! Diskon 20%", Message = "Nikmati diskon 20% untuk semua layanan laundry selama bulan Ramadhan. Gunakan kode: RAMADHAN20", Priority = "High", CreatedAt = DateTime.UtcNow.AddDays(-1) },
            new Notification { Type = "Announcement", Title = "WashUp Buka Cabang Baru di Surabaya!", Message = "Kami hadir di Surabaya! Sekarang melayani area Tunjungan dan sekitarnya.", Priority = "Normal", CreatedAt = DateTime.UtcNow.AddDays(-3) },
            new Notification { Type = "System", Title = "Pembaruan Aplikasi v2.0", Message = "Aplikasi WashUp telah diperbarui dengan fitur tracking real-time dan chatbot AI Mbok Inem!", Priority = "Normal", CreatedAt = DateTime.UtcNow.AddDays(-7) },
            new Notification { Type = "Promo", Title = "Paket Kiloan Hemat!", Message = "Cuci kiloan mulai dari Rp 7.000/kg! Gratis antar-jemput untuk area tertentu.", Priority = "High", CreatedAt = DateTime.UtcNow.AddDays(-2) }
        };
        context.Notifications.AddRange(notifications);
        await context.SaveChangesAsync();

        // --- Create Marketplace Listings ---
        var listings = new[]
        {
            new MarketplaceListing { BranchId = 1, Title = "WashUp Pusat - Cuci Kering Premium", Description = "Layanan cuci kering premium dengan detergen hypoallergenic. Cocok untuk kulit sensitif.", PricePerKg = 10000, ServiceArea = "Jakarta Pusat, Jakarta Selatan", Latitude = -6.2088, Longitude = 106.8456, IsFeatured = true, AverageRating = 4.7, ReviewCount = 120 },
            new MarketplaceListing { BranchId = 1, Title = "WashUp Pusat - Express 6 Jam", Description = "Layanan cuci express selesai dalam 6 jam. Tersedia untuk area Jakarta Pusat.", PricePerKg = 15000, ServiceArea = "Jakarta Pusat", IsFeatured = true, AverageRating = 4.5, ReviewCount = 85 },
            new MarketplaceListing { BranchId = 2, Title = "WashUp Bandung - Kiloan Hemat", Description = "Cuci kiloan murah meriah di Bandung. Hasil bersih, wangi, dan rapi.", PricePerKg = 7000, ServiceArea = "Bandung Utara, Dago, Cihampelas", IsFeatured = false, AverageRating = 4.3, ReviewCount = 56 },
            new MarketplaceListing { BranchId = 3, Title = "WashUp Surabaya - Setrika Rapi", Description = "Layanan setrika profesional. Pakaian rapi, lipatan presisi, wangi segar.", PricePerKg = 8000, ServiceArea = "Surabaya Pusat, Tunjungan", IsFeatured = false, AverageRating = 4.0, ReviewCount = 12 }
        };
        context.MarketplaceListings.AddRange(listings);
        await context.SaveChangesAsync();

        // --- Create Courier Assignments ---
        var deliveryOrders = context.Orders.Where(o => o.Status == "Dikirim").Take(8).ToList();
        foreach (var order in deliveryOrders)
        {
            context.CourierAssignments.Add(new CourierAssignment
            {
                OrderId = order.Id,
                StaffMemberId = order.BranchId == 1 ? 3 : 4, // Courier 1 or 2
                AssignmentType = "Delivery",
                Status = "Completed",
                AssignedAt = order.CompletedAt,
                StartedAt = order.CompletedAt?.AddMinutes(15),
                CompletedAt = order.DeliveredAt,
                DestinationLatitude = -6.2000 + random.NextDouble() * 0.05,
                DestinationLongitude = 106.8166 + random.NextDouble() * 0.05
            });
        }

        // Tugas aktif agar GPS simulator langsung ada yang digerakkan.
        // Tujuan sengaja 5–9 km dari cabang supaya pergerakan di peta terlihat lama (±15 menit).
        var activeOrders = context.Orders.Where(o => o.Status == "Selesai").Take(3).ToList();
        foreach (var (order, i) in activeOrders.Select((o, i) => (o, i)))
        {
            // Tujuan di sekitar cabang order sendiri agar rute masuk akal
            var origin = branches.FirstOrDefault(b => b.Id == order.BranchId) ?? branches[0];
            var angle = random.NextDouble() * Math.PI * 2;
            var distDeg = 0.05 + random.NextDouble() * 0.035; // ±5–9 km
            context.CourierAssignments.Add(new CourierAssignment
            {
                OrderId = order.Id,
                StaffMemberId = i % 2 == 0 ? 3 : 4,
                AssignmentType = i == 0 ? "Pickup" : "Delivery",
                Status = "Assigned",
                AssignedAt = DateTime.UtcNow,
                DestinationLatitude = (origin.Latitude ?? -6.2088) + Math.Sin(angle) * distDeg,
                DestinationLongitude = (origin.Longitude ?? 106.8456) + Math.Cos(angle) * distDeg
            });
        }
        await context.SaveChangesAsync();

        // --- Create Complaints ---
        var complaintSamples = new (string Category, string Subject, string Description, string Status)[]
        {
            ("Keterlambatan", "Order lewat estimasi 1 hari", "Order saya dijanjikan selesai kemarin tapi belum ada kabar.", "Diproses"),
            ("Kualitas", "Masih ada noda di kemeja putih", "Noda kopi di kemeja putih masih terlihat setelah dicuci.", "Selesai"),
            ("Kerusakan", "Kancing baju lepas", "Setelah laundry, dua kancing kemeja hilang.", "Diterima"),
            ("Kehilangan", "Satu pasang kaos kaki hilang", "Jumlah kaos kaki yang kembali kurang satu pasang.", "Diproses"),
            ("Lainnya", "Plastik packing sobek", "Packing sampai rumah dalam kondisi sobek, untung isi aman.", "Selesai")
        };
        foreach (var (sample, i) in complaintSamples.Select((s, i) => (s, i)))
        {
            var cust = customers[random.Next(customers.Count)];
            context.Complaints.Add(new Complaint
            {
                ComplaintNumber = $"CMP-{DateTime.UtcNow.AddDays(-i * 3):yyyyMMdd}-{1000 + i}",
                UserId = cust.Id,
                Category = sample.Category,
                Subject = sample.Subject,
                Description = sample.Description,
                Status = sample.Status,
                Priority = sample.Category == "Kehilangan" ? "High" : "Normal",
                Resolution = sample.Status == "Selesai" ? "Sudah kami proses ulang dan antar kembali. Mohon maaf atas ketidaknyamanannya." : null,
                ResolvedAt = sample.Status == "Selesai" ? DateTime.UtcNow.AddDays(-i) : null,
                CreatedAt = DateTime.UtcNow.AddDays(-i * 3)
            });
        }
        await context.SaveChangesAsync();

        // --- Create Stock Movements (riwayat & laporan pemakaian) ---
        foreach (var item in inventoryItems)
        {
            for (int i = 0; i < 4; i++)
            {
                context.StockMovements.Add(new StockMovement
                {
                    InventoryItemId = item.Id,
                    MovementType = i % 3 == 0 ? "In" : "Out",
                    Quantity = Math.Round(random.NextDouble() * (i % 3 == 0 ? 20 : 5) + 1, 1),
                    Notes = i % 3 == 0 ? "Pembelian dari supplier" : "Pemakaian operasional harian",
                    RecordedByUserId = "user-admin",
                    CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 28))
                });
            }
        }
        await context.SaveChangesAsync();

        // --- Create Tax Records ---
        for (int m = 1; m <= 3; m++)
        {
            var revenue = (decimal)(random.NextDouble() * 5000000 + 2000000);
            context.TaxRecords.Add(new TaxRecord
            {
                Month = DateTime.UtcNow.AddMonths(-m).Month,
                Year = DateTime.UtcNow.AddMonths(-m).Year,
                BranchId = 1,
                TotalRevenue = revenue,
                TaxableAmount = revenue * 0.8m,
                PphRate = 0.1m,
                PphAmount = revenue * 0.8m * 0.1m,
                PpnAmount = revenue * 0.11m,
                Status = m == 3 ? "Paid" : (m == 2 ? "Reported" : "Draft")
            });
        }
        await context.SaveChangesAsync();

        // --- Create Subscriptions ---
        foreach (var cust in customers.Take(5))
        {
            context.Subscriptions.Add(new Subscription
            {
                UserId = cust.Id,
                Tier = cust.MembershipTier!,
                PackageType = cust.MembershipTier == "Platinum" ? "Bulanan" : "Kiloan",
                StartDate = DateTime.UtcNow.AddMonths(-random.Next(1, 6)),
                IsActive = true,
                MaxOrdersPerMonth = cust.MembershipTier == "Platinum" ? 20 : 10,
                MaxWeightPerOrder = cust.MembershipTier == "Platinum" ? 10 : 7,
                DiscountPercentage = cust.MembershipTier switch { "Platinum" => 15, "Gold" => 10, "Silver" => 5, _ => 0 }
            });
        }
        await context.SaveChangesAsync();
    }
}
