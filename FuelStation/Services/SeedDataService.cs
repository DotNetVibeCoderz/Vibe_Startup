using FuelStation.Data;
using FuelStation.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FuelStation.Services;

/// <summary>
/// Seeds the database with sample data for demo and testing
/// </summary>
public class SeedDataService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ILogger<SeedDataService> _logger;

    public SeedDataService(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<SeedDataService> logger)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            // Ensure database is created
            await _db.Database.EnsureCreatedAsync();

            await SeedRolesAsync();
            await SeedUsersAsync();
            await SeedFuelStationsAsync();
            await SeedFuelProductsAsync();
            await SeedTanksAndPumpsAsync();
            await SeedCustomersAsync();
            await SeedEmployeesAsync();
            await SeedNonFuelProductsAsync();
            await SeedTransactionsAsync();
            await SeedFeedbacksAsync();

            _logger.LogInformation("Database seeded successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding database");
        }
    }

    private async Task SeedRolesAsync()
    {
        if (await _roleManager.Roles.AnyAsync()) return;

        var roles = new[]
        {
            new ApplicationRole { Name = "Admin", Description = "Full access administrator" },
            new ApplicationRole { Name = "Supervisor", Description = "Shift supervisor with reporting access" },
            new ApplicationRole { Name = "Operator", Description = "Front-line operator for transactions" },
            new ApplicationRole { Name = "Customer", Description = "Registered customer" }
        };

        foreach (var role in roles)
            await _roleManager.CreateAsync(role);
    }

    private async Task SeedUsersAsync()
    {
        if (await _userManager.Users.AnyAsync()) return;

        var admin = new ApplicationUser
        {
            UserName = "admin@fuelstation.com",
            Email = "admin@fuelstation.com",
            FullName = "Administrator",
            IsActive = true,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(admin, "Admin123!");
        await _userManager.AddToRoleAsync(admin, "Admin");

        var supervisor = new ApplicationUser
        {
            UserName = "supervisor@fuelstation.com",
            Email = "supervisor@fuelstation.com",
            FullName = "Budi Santoso",
            IsActive = true,
            EmailConfirmed = true
        };
        await _userManager.CreateAsync(supervisor, "Super123!");
        await _userManager.AddToRoleAsync(supervisor, "Supervisor");

        // Create operators
        for (int i = 1; i <= 5; i++)
        {
            var op = new ApplicationUser
            {
                UserName = $"operator{i}@fuelstation.com",
                Email = $"operator{i}@fuelstation.com",
                FullName = $"Operator {i}",
                EmployeeCode = $"OP-{i:D3}",
                IsActive = true,
                EmailConfirmed = true
            };
            await _userManager.CreateAsync(op, "Oper123!");
            await _userManager.AddToRoleAsync(op, "Operator");
        }
    }

    private async Task SeedFuelStationsAsync()
    {
        if (await _db.FuelStations.AnyAsync()) return;

        var stations = new[]
        {
            new FuelStationLocation
            {
                Id = Guid.NewGuid(), Name = "SPBU Mini - Cabang Utama",
                Code = "SPBU-001", Address = "Jl. Raya Utama No. 123, Jakarta Pusat",
                Phone = "021-5550101", Latitude = -6.2088, Longitude = 106.8456, IsActive = true
            },
            new FuelStationLocation
            {
                Id = Guid.NewGuid(), Name = "SPBU Mini - Cabang Selatan",
                Code = "SPBU-002", Address = "Jl. Raya Selatan No. 45, Jakarta Selatan",
                Phone = "021-5550202", Latitude = -6.2608, Longitude = 106.8156, IsActive = true
            },
            new FuelStationLocation
            {
                Id = Guid.NewGuid(), Name = "SPBU Mini - Cabang Timur",
                Code = "SPBU-003", Address = "Jl. Raya Timur No. 78, Bekasi",
                Phone = "021-5550303", Latitude = -6.2349, Longitude = 107.0001, IsActive = true
            }
        };

        _db.FuelStations.AddRange(stations);
        await _db.SaveChangesAsync();
    }

    private async Task SeedFuelProductsAsync()
    {
        if (await _db.FuelProducts.AnyAsync()) return;

        var products = new[]
        {
            new FuelProduct { Id = Guid.NewGuid(), Name = "Pertalite", Code = "PLT", FuelType = "Gasoline",
                PricePerLiter = 10000m, CostPerLiter = 8500m, OctaneRating = "90", ColorCode = "#4CAF50", IsActive = true },
            new FuelProduct { Id = Guid.NewGuid(), Name = "Pertamax", Code = "PTX", FuelType = "Gasoline",
                PricePerLiter = 12950m, CostPerLiter = 11000m, OctaneRating = "92", ColorCode = "#2196F3", IsActive = true },
            new FuelProduct { Id = Guid.NewGuid(), Name = "Pertamax Turbo", Code = "PTT", FuelType = "Gasoline",
                PricePerLiter = 14400m, CostPerLiter = 12500m, OctaneRating = "98", ColorCode = "#F44336", IsActive = true },
            new FuelProduct { Id = Guid.NewGuid(), Name = "Bio Solar", Code = "BSL", FuelType = "Diesel",
                PricePerLiter = 6800m, CostPerLiter = 5500m, OctaneRating = null, ColorCode = "#FF9800", IsActive = true },
            new FuelProduct { Id = Guid.NewGuid(), Name = "Dexlite", Code = "DXL", FuelType = "Diesel",
                PricePerLiter = 14500m, CostPerLiter = 12500m, OctaneRating = null, ColorCode = "#9C27B0", IsActive = true }
        };

        _db.FuelProducts.AddRange(products);
        await _db.SaveChangesAsync();
    }

    private async Task SeedTanksAndPumpsAsync()
    {
        if (await _db.Tanks.AnyAsync()) return;

        var stations = await _db.FuelStations.ToListAsync();
        var products = await _db.FuelProducts.ToListAsync();
        var rng = new Random(42);

        foreach (var station in stations)
        {
            foreach (var product in products.Take(3))
            {
                var tank = new Tank
                {
                    Id = Guid.NewGuid(),
                    Name = $"Tank {product.Code} - {station.Code}",
                    TankNumber = $"TNK-{station.Code}-{product.Code}",
                    FuelStationId = station.Id,
                    FuelProductId = product.Id,
                    CapacityLiters = 20000m,
                    CurrentVolumeLiters = (decimal)(rng.NextDouble() * 15000 + 5000),
                    MinThresholdLiters = 2000m,
                    SensorId = $"SENSOR-{Guid.NewGuid():N}".Substring(0, 8),
                    IsActive = true
                };
                _db.Tanks.Add(tank);

                // Add 2 pumps per tank
                for (int p = 1; p <= 2; p++)
                {
                    _db.FuelPumps.Add(new FuelPump
                    {
                        Id = Guid.NewGuid(),
                        PumpNumber = $"P{p}",
                        FuelStationId = station.Id,
                        TankId = tank.Id,
                        IsActive = true
                    });
                }
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task SeedCustomersAsync()
    {
        if (await _db.Customers.AnyAsync()) return;

        var customers = new[]
        {
            new Customer { Id = Guid.NewGuid(), Name = "Andi Wijaya", MemberCode = "MEM-001", Phone = "0812-3456-7890",
                Email = "andi@email.com", LoyaltyPoints = 1500, TotalSpent = 25000000m, VisitCount = 120,
                MemberSince = DateTime.UtcNow.AddMonths(-12), MembershipTier = "Gold" },
            new Customer { Id = Guid.NewGuid(), Name = "Siti Nurhaliza", MemberCode = "MEM-002", Phone = "0813-4567-8901",
                Email = "siti@email.com", LoyaltyPoints = 800, TotalSpent = 12000000m, VisitCount = 60,
                MemberSince = DateTime.UtcNow.AddMonths(-6), MembershipTier = "Silver" },
            new Customer { Id = Guid.NewGuid(), Name = "Bambang Hermanto", MemberCode = "MEM-003", Phone = "0814-5678-9012",
                Email = "bambang@email.com", LoyaltyPoints = 2500, TotalSpent = 45000000m, VisitCount = 200,
                MemberSince = DateTime.UtcNow.AddMonths(-24), MembershipTier = "Platinum" },
            new Customer { Id = Guid.NewGuid(), Name = "Dewi Lestari", MemberCode = "MEM-004", Phone = "0815-6789-0123",
                Email = "dewi@email.com", LoyaltyPoints = 300, TotalSpent = 3500000m, VisitCount = 15,
                MemberSince = DateTime.UtcNow.AddMonths(-2), MembershipTier = "Regular" },
            new Customer { Id = Guid.NewGuid(), Name = "Eko Prasetyo", MemberCode = "MEM-005", Phone = "0816-7890-1234",
                Email = "eko@email.com", LoyaltyPoints = 1200, TotalSpent = 18000000m, VisitCount = 85,
                MemberSince = DateTime.UtcNow.AddMonths(-9), MembershipTier = "Gold" }
        };

        _db.Customers.AddRange(customers);
        await _db.SaveChangesAsync();
    }

    private async Task SeedEmployeesAsync()
    {
        if (await _db.Employees.AnyAsync()) return;

        var stations = await _db.FuelStations.ToListAsync();
        var employees = new List<Employee>();

        var names = new[] { "Ahmad Fauzi", "Rini Purwanti", "Hendra Gunawan", "Maya Anggraini", "Rudi Hartono" };
        for (int i = 0; i < names.Length; i++)
        {
            employees.Add(new Employee
            {
                Id = Guid.NewGuid(),
                Name = names[i],
                EmployeeCode = $"EMP-{i + 1:D3}",
                Phone = $"081{i + 7}-0000-111{i}",
                Email = $"emp{i + 1}@fuelstation.com",
                Role = "Operator",
                FuelStationId = stations[i % stations.Count].Id,
                IsActive = true
            });
        }

        // Add supervisor
        employees.Add(new Employee
        {
            Id = Guid.NewGuid(),
            Name = "Budi Santoso",
            EmployeeCode = "EMP-006",
            Phone = "0812-0000-0000",
            Email = "supervisor@fuelstation.com",
            Role = "Supervisor",
            FuelStationId = stations[0].Id,
            IsActive = true
        });

        _db.Employees.AddRange(employees);
        await _db.SaveChangesAsync();
    }

    private async Task SeedNonFuelProductsAsync()
    {
        if (await _db.NonFuelProducts.AnyAsync()) return;

        var products = new[]
        {
            new NonFuelProduct { Id = Guid.NewGuid(), Name = "Oli Mesin 1L", SKU = "OIL-001", Category = "Oil",
                Price = 55000m, Cost = 40000m, StockQuantity = 50, MinStockThreshold = 10,
                Description = "Oli mesin berkualitas untuk kendaraan roda empat" },
            new NonFuelProduct { Id = Guid.NewGuid(), Name = "Air Mineral 600ml", SKU = "DRINK-001", Category = "Beverage",
                Price = 5000m, Cost = 3500m, StockQuantity = 200, MinStockThreshold = 20,
                Description = "Air mineral segar dalam kemasan" },
            new NonFuelProduct { Id = Guid.NewGuid(), Name = "Pengharum Mobil", SKU = "ACC-001", Category = "Accessories",
                Price = 25000m, Cost = 15000m, StockQuantity = 30, MinStockThreshold = 5,
                Description = "Pengharum mobil tahan lama" },
            new NonFuelProduct { Id = Guid.NewGuid(), Name = "Snack Ringan", SKU = "SNACK-001", Category = "Snacks",
                Price = 8000m, Cost = 5000m, StockQuantity = 100, MinStockThreshold = 15,
                Description = "Cemilan ringan untuk perjalanan" },
            new NonFuelProduct { Id = Guid.NewGuid(), Name = "Wipermobil", SKU = "ACC-002", Category = "Accessories",
                Price = 45000m, Cost = 30000m, StockQuantity = 25, MinStockThreshold = 5,
                Description = "Wiper karet berkualitas" }
        };

        _db.NonFuelProducts.AddRange(products);
        await _db.SaveChangesAsync();
    }

    private async Task SeedTransactionsAsync()
    {
        if (await _db.Transactions.AnyAsync()) return;

        var stations = await _db.FuelStations.ToListAsync();
        var products = await _db.FuelProducts.ToListAsync();
        var customers = await _db.Customers.ToListAsync();
        var pumps = await _db.FuelPumps.ToListAsync();
        var rng = new Random(42);

        var paymentMethods = new[] { "Cash", "QRIS", "EWallet", "DebitCard", "BankTransfer" };
        var transactions = new List<Transaction>();

        // Generate 50 sample transactions across the last 30 days
        for (int i = 0; i < 50; i++)
        {
            var station = stations[rng.Next(stations.Count)];
            var product = products[rng.Next(products.Count)];
            var liters = Math.Round((decimal)(rng.NextDouble() * 40 + 5), 2);
            var pricePerLiter = product.PricePerLiter;
            var subtotal = liters * pricePerLiter;
            var discount = rng.Next(3) == 0 ? Math.Round(subtotal * 0.05m, 2) : 0;
            var total = subtotal - discount;
            var daysAgo = rng.Next(0, 30);
            var hoursAgo = rng.Next(0, 23);
            var minsAgo = rng.Next(0, 59);

            var tx = new Transaction
            {
                Id = Guid.NewGuid(),
                TransactionNumber = $"TRX-{DateTime.UtcNow:yyyyMMdd}-{i + 1:D4}",
                TransactionDate = DateTime.UtcNow.AddDays(-daysAgo).AddHours(-hoursAgo).AddMinutes(-minsAgo),
                FuelStationId = station.Id,
                PumpId = pumps.Where(p => p.FuelStationId == station.Id).OrderBy(_ => rng.Next()).First().Id,
                CustomerId = rng.Next(2) == 0 ? customers[rng.Next(customers.Count)].Id : null,
                TotalAmount = subtotal,
                Discount = discount,
                GrandTotal = total,
                PaymentMethod = paymentMethods[rng.Next(paymentMethods.Length)],
                Status = "Completed",
                PaymentReference = $"PAY-{Guid.NewGuid():N}".Substring(0, 10)
            };

            tx.TransactionDetails.Add(new TransactionDetail
            {
                Id = Guid.NewGuid(),
                TransactionId = tx.Id,
                FuelProductId = product.Id,
                Liters = liters,
                PricePerLiter = pricePerLiter,
                Subtotal = subtotal
            });

            transactions.Add(tx);
        }

        _db.Transactions.AddRange(transactions);
        await _db.SaveChangesAsync();
    }

    private async Task SeedFeedbacksAsync()
    {
        if (await _db.Feedbacks.AnyAsync()) return;

        var customers = await _db.Customers.ToListAsync();
        var rng = new Random(42);

        var comments = new[]
        {
            "Pelayanan sangat ramah dan cepat!",
            "SPBU bersih dan nyaman.",
            "Harga kompetitif, recommended!",
            "Pompa kadang error, tolong diperbaiki.",
            "Petugasnya sopan dan membantu.",
            "Antrian terlalu lama saat jam sibuk.",
            "Toilet bersih, bagus!",
            "Stok BBM selalu tersedia."
        };

        for (int i = 0; i < 10; i++)
        {
            _db.Feedbacks.Add(new Feedback
            {
                Id = Guid.NewGuid(),
                CustomerId = customers[rng.Next(customers.Count)].Id,
                Rating = rng.Next(3, 6), // 3-5
                Comment = comments[rng.Next(comments.Length)],
                CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(0, 30))
            });
        }

        await _db.SaveChangesAsync();
    }
}
