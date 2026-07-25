using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ngibrid.Data;
using Ngibrid.Models;

namespace Ngibrid.Services;

/// <summary>
/// Seeds the database with sample data for development and testing
/// </summary>
public class DataSeeder
{
    private readonly NgibridDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IConfiguration _config;

    public DataSeeder(NgibridDbContext db, UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager, IConfiguration config)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
        _config = config;
    }

    /// <summary>Home cities for the demo customers — all names the city master table resolves.</summary>
    private static readonly string[] CustomerCities =
    {
        "Kota Jakarta Selatan", "Kota Bandung", "Kota Surabaya", "Kota Semarang", "Kota Medan",
        "Kota Yogyakarta", "Kota Bekasi", "Kota Denpasar", "Kota Makassar", "Kota Tangerang"
    };

    public async Task SeedAsync()
    {
        // Only seed if no users exist
        if (await _db.Users.AnyAsync()) return;

        // ─── Roles ───
        var roles = new[] { "Admin", "Manager", "Customer", "Courier", "WarehouseStaff" };
        foreach (var roleName in roles)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
                await _roleManager.CreateAsync(new ApplicationRole { Name = roleName, Description = $"{roleName} role" });
        }

        // ─── Users ───
        var admin = await CreateUserAsync("admin@ngibrid.com", "Admin123!", "Admin Ngibrid", "Admin", "08123456789");
        await _userManager.AddToRoleAsync(admin, "Admin");

        var manager = await CreateUserAsync("manager@ngibrid.com", "Manager123!", "Budi Manager", "Manager", "08123456780");
        await _userManager.AddToRoleAsync(manager, "Manager");

        // Courier users
        var courier1 = await CreateUserAsync("courier1@ngibrid.com", "Courier123!", "Andi Kurir", "Courier", "08123456781");
        var courier2 = await CreateUserAsync("courier2@ngibrid.com", "Courier123!", "Bambang Kurir", "Courier", "08123456782");
        var courier3 = await CreateUserAsync("courier3@ngibrid.com", "Courier123!", "Cici Kurir", "Courier", "08123456783");
        await _userManager.AddToRoleAsync(courier1, "Courier");
        await _userManager.AddToRoleAsync(courier2, "Courier");
        await _userManager.AddToRoleAsync(courier3, "Courier");

        // Customer users
        var customers = new List<ApplicationUser>();
        string[] customerNames = { "Dewi Lestari", "Eko Pratama", "Fitri Ananda", "Gilang Ramadhan", 
            "Hana Safira", "Irfan Kurnia", "Jessica Tan", "Kevin Hartono", "Lia Permata", "Maya Indah" };
        
        for (int i = 0; i < customerNames.Length; i++)
        {
            // Spread the customers over real cities so pickup requests land in different places.
            var cust = await CreateUserAsync($"customer{i + 1}@ngibrid.com", "Customer123!", 
                customerNames[i], "Customer", $"0812{i:D4}{i:D4}", CustomerCities[i % CustomerCities.Length]);
            await _userManager.AddToRoleAsync(cust, "Customer");
            customers.Add(cust);
        }

        // Warehouse staff
        var staff = await CreateUserAsync("staff@ngibrid.com", "Staff123!", "Rudi Gudang", "WarehouseStaff", "08123456790");
        await _userManager.AddToRoleAsync(staff, "WarehouseStaff");

        // ─── Courier Profiles ───
        var courierProfiles = new List<CourierProfile>
        {
            // Coordinates seeded so the fleet map has something to plot before the GPS simulator
            // starts moving anyone; the simulator overwrites them once an order is in transit.
            new() { UserId = courier1.Id, CourierId = "CID-001", VehicleType = "MOTORCYCLE",
                VehiclePlateNumber = "B 1234 XYZ", MaxLoadKg = 20, ServiceArea = "[\"JKT-PUS\",\"JKT-SEL\"]", Rating = 4.8,
                Status = "ON_DELIVERY", IsAvailable = false,
                CurrentLatitude = -6.1944, CurrentLongitude = 106.8229, LastLocationUpdate = DateTime.UtcNow },
            new() { UserId = courier2.Id, CourierId = "CID-002", VehicleType = "CAR",
                VehiclePlateNumber = "B 5678 ABC", MaxLoadKg = 100, ServiceArea = "[\"JKT-TIM\",\"JKT-UTA\"]", Rating = 4.5,
                CurrentLatitude = -6.2250, CurrentLongitude = 106.9004, LastLocationUpdate = DateTime.UtcNow },
            new() { UserId = courier3.Id, CourierId = "CID-003", VehicleType = "MOTORCYCLE",
                VehiclePlateNumber = "B 9012 DEF", MaxLoadKg = 20, ServiceArea = "[\"JKT-BAR\",\"TNG\"]", Rating = 4.9,
                CurrentLatitude = -6.1783, CurrentLongitude = 106.7314, LastLocationUpdate = DateTime.UtcNow }
        };
        _db.CourierProfiles.AddRange(courierProfiles);
        await _db.SaveChangesAsync();

        // ─── Warehouses ───
        var warehouses = new List<Warehouse>
        {
            new() { Name = "Warehouse Jakarta Pusat", Code = "WH-JKT-01", City = "Jakarta Pusat", 
                Province = "DKI Jakarta", Latitude = -6.2088, Longitude = 106.8456, TotalCapacityM3 = 5000, UsedCapacityM3 = 1500 },
            new() { Name = "Warehouse Jakarta Timur", Code = "WH-JKT-02", City = "Jakarta Timur", 
                Province = "DKI Jakarta", Latitude = -6.2251, Longitude = 106.9004, TotalCapacityM3 = 3000, UsedCapacityM3 = 800 },
            new() { Name = "Warehouse Bandung", Code = "WH-BDG-01", City = "Bandung", 
                Province = "Jawa Barat", Latitude = -6.9175, Longitude = 107.6191, TotalCapacityM3 = 4000, UsedCapacityM3 = 1200 },
            new() { Name = "Warehouse Surabaya", Code = "WH-SBY-01", City = "Surabaya", 
                Province = "Jawa Timur", Latitude = -7.2575, Longitude = 112.7521, TotalCapacityM3 = 3500, UsedCapacityM3 = 900 }
        };
        _db.Warehouses.AddRange(warehouses);
        await _db.SaveChangesAsync();

        // ─── Warehouse Sections ───
        foreach (var wh in warehouses)
        {
            _db.WarehouseSections.AddRange(
                new() { WarehouseId = wh.Id, Name = "General Storage", SectionType = "GENERAL", CapacityM3 = wh.TotalCapacityM3 * 0.5 },
                new() { WarehouseId = wh.Id, Name = "Cold Storage", SectionType = "COLD", CapacityM3 = wh.TotalCapacityM3 * 0.2 },
                new() { WarehouseId = wh.Id, Name = "High Value", SectionType = "HIGH_VALUE", CapacityM3 = wh.TotalCapacityM3 * 0.15 },
                new() { WarehouseId = wh.Id, Name = "Bulk Storage", SectionType = "BULK", CapacityM3 = wh.TotalCapacityM3 * 0.15 }
            );
        }
        await _db.SaveChangesAsync();

        // ─── Orders ───
        // Routes are drawn from the city master table rather than a hardcoded list of names, so
        // every seeded order carries a province and resolves to real coordinates — the distances,
        // tariffs, and route maps built on top of them are then genuine numbers.
        // The pool is the largest kota rather than all 514 areas: the demand-per-city breakdown and
        // the forecaster need enough orders per destination to mean anything.
        string[] hubs =
        {
            "Jakarta Pusat", "Jakarta Selatan", "Jakarta Timur", "Jakarta Barat", "Jakarta Utara",
            "Bandung", "Surabaya", "Medan", "Semarang", "Yogyakarta", "Bekasi", "Depok",
            "Tangerang", "Tangerang Selatan", "Bogor", "Makassar", "Palembang", "Denpasar",
            "Malang", "Balikpapan", "Pekanbaru", "Padang", "Banjarmasin", "Manado", "Samarinda",
            "Batam", "Bandar Lampung", "Pontianak", "Surakarta", "Cirebon"
        };
        var cityIndex = await _db.Cities.AsNoTracking()
            .Where(c => c.Type == "KOTA")
            .ToListAsync();
        var cities = hubs
            .Select(h => cityIndex.FirstOrDefault(c => c.Name == h))
            .Where(c => c != null)
            .Select(c => c!)
            .ToArray();
        if (cities.Length == 0)
            cities = cityIndex.Take(10).ToArray(); // master data missing: still produce something
        var statuses = new[] { "DELIVERED", "IN_TRANSIT", "OUT_FOR_DELIVERY", "CREATED", "PICKED_UP", "DELIVERED", "IN_TRANSIT" };
        var rng = new Random(42);
        var orders = new List<Order>();

        // 120 days of history with a day-of-week shape and two promo peaks. A flat series would
        // make the volume chart a straight line and give the forecaster nothing to detect —
        // seasonal indices and peak-season detection only mean something against real variation.
        // Older orders are always DELIVERED; only the last few days carry in-flight statuses.
        var orderSeq = 0;
        for (int daysAgo = 119; daysAgo >= 0; daysAgo--)
        {
            var day = DateTime.UtcNow.Date.AddDays(-daysAgo);
            var dowFactor = day.DayOfWeek switch
            {
                DayOfWeek.Sunday => 0.3,
                DayOfWeek.Saturday => 0.7,
                DayOfWeek.Monday => 1.35,
                DayOfWeek.Friday => 1.25,
                _ => 1.0,
            };
            // Two campaign spikes inside the window (Harbolnas-style).
            var peakFactor = (daysAgo is >= 40 and <= 46) || (daysAgo is >= 12 and <= 16) ? 1.9 : 1.0;
            var dailyCount = Math.Max(0, (int)Math.Round(3 * dowFactor * peakFactor) + rng.Next(-1, 2));

            for (int k = 0; k < dailyCount; k++)
            {
            var i = orderSeq++;
            var createdAt = day.AddHours(rng.Next(7, 20)).AddMinutes(rng.Next(60));

            var senderIdx = rng.Next(customers.Count);
            var weight = Math.Round(rng.NextDouble() * 20 + 0.5, 1);

            // Origin and destination are never the same city, so no seeded order prices as an
            // intra-city run and the route map always has a leg to draw.
            var fromIdx = rng.Next(cities.Length);
            var toIdx = (fromIdx + 1 + rng.Next(Math.Max(cities.Length - 1, 1))) % cities.Length;
            var from = cities[fromIdx];
            var to = cities[toIdx];

            // Tariff and emissions are derived from the real distance between the two seats, using
            // the same zone model DynamicPricingService applies. Random amounts unrelated to the
            // route made the revenue chart pure noise and every invoice internally inconsistent.
            var service = new[] { "REG", "EXP", "ECO" }[i % 3];
            var (basePrice, distanceKm) = EstimateSeedTariff(from, to, weight, service);

            var order = new Order
            {
                OrderNumber = $"NGB-{createdAt:yyyyMMdd}-{(i + 1):D4}",
                CustomerId = customers[senderIdx].Id,
                SenderName = customers[senderIdx].FullName,
                SenderPhone = customers[senderIdx].PhoneNumber ?? "0812000000",
                SenderAddress = $"Jl. Sudirman No.{i + 1}",
                SenderCity = from.FullName,
                SenderProvince = from.Province,
                RecipientName = customers[(senderIdx + 1) % customers.Count].FullName,
                RecipientPhone = customers[(senderIdx + 1) % customers.Count].PhoneNumber ?? "0812000001",
                RecipientAddress = $"Jl. Thamrin No.{i * 10}",
                RecipientCity = to.FullName,
                RecipientProvince = to.Province,
                PackageDescription = $"Paket sample #{i + 1} - {new[] { "Elektronik", "Pakaian", "Makanan", "Buku", "Kosmetik" }[i % 5]}",
                WeightKg = weight,
                LengthCm = rng.Next(10, 50),
                WidthCm = rng.Next(10, 50),
                HeightCm = rng.Next(5, 30),
                VolumetricWeight = Math.Round(weight * 1.2, 1),
                ServiceType = service,
                TrackingNumber = $"NGB{createdAt:yyMMdd}{i:D4}{rng.Next(1000, 9999)}",
                Status = daysAgo > 5 ? "DELIVERED" : statuses[i % statuses.Length],
                BasePrice = basePrice,
                TaxAmount = Math.Round(basePrice * 0.11m, 0),
                TotalAmount = basePrice + Math.Round(basePrice * 0.11m, 0),
                Currency = "IDR",
                EstimatedDeliveryDate = createdAt.AddDays(rng.Next(1, 7)),
                AssignedCourierId = courierProfiles[i % 3].UserId,
                CarbonEmissionGram = Math.Round(distanceKm * 150 * (Math.Max(weight, 0.5) / 10.0), 2),
                IsEcoDelivery = i % 7 == 0,
                CreatedAt = createdAt
            };
            orders.Add(order);
            }
        }
        _db.Orders.AddRange(orders);
        await _db.SaveChangesAsync();

        // ─── Order Status History ───
        foreach (var order in orders)
        {
            var statusFlow = GetStatusFlow(order.Status, order.CreatedAt, rng);
            foreach (var (status, ts) in statusFlow)
            {
                _db.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OrderId = order.Id,
                    Status = status,
                    Notes = GetStatusNote(status),
                    CreatedAt = ts
                });
            }

            // SLA is measured from ActualDeliveryDate against the estimate; without it every
            // delivered order was skipped and the dashboard reported a flat, meaningless 100%.
            if (order.Status == "DELIVERED")
            {
                var lateBy = rng.Next(100) < 88 ? -rng.Next(2, 36) : rng.Next(4, 48);
                order.ActualDeliveryDate = (order.EstimatedDeliveryDate ?? order.CreatedAt.AddDays(3))
                    .AddHours(lateBy);
            }
        }
        await _db.SaveChangesAsync();

        // ─── Delivery schedules for today ───
        // Without these the route optimiser (and its map) has nothing to plan on a fresh install
        // and the courier page just says "no deliveries scheduled today".
        var todaysOrders = orders
            .Where(o => o.Status is "OUT_FOR_DELIVERY" or "IN_TRANSIT" or "PICKED_UP")
            .ToList();
        var sequence = new Dictionary<long, int>();
        foreach (var order in todaysOrders)
        {
            var profile = courierProfiles.FirstOrDefault(c => c.UserId == order.AssignedCourierId);
            if (profile == null) continue;
            sequence[profile.Id] = sequence.GetValueOrDefault(profile.Id) + 1;
            _db.DeliverySchedules.Add(new DeliverySchedule
            {
                CourierProfileId = profile.Id,
                OrderId = order.Id,
                ScheduledDate = DateTime.UtcNow.Date,
                EstimatedDeliveryTime = DateTime.UtcNow.Date.AddHours(8 + sequence[profile.Id] % 9),
                SequenceNumber = sequence[profile.Id],
                Status = order.Status == "PICKED_UP" ? "SCHEDULED" : "IN_PROGRESS",
                EstimatedDistanceKm = Math.Round(rng.NextDouble() * 18 + 2, 1),
            });
        }
        await _db.SaveChangesAsync();

        // ─── Payments & Invoices ───
        foreach (var order in orders.Take(20))
        {
            _db.Payments.Add(new Payment
            {
                OrderId = order.Id,
                PaymentNumber = $"PAY-{order.CreatedAt:yyyyMMdd}-{rng.Next(100000, 999999)}",
                PaymentMethod = new[] { "BANK_TRANSFER", "E_WALLET", "COD", "CREDIT_CARD" }[rng.Next(4)],
                PaymentChannel = new[] { "BCA", "GoPay", "OVO", "Mandiri" }[rng.Next(4)],
                Amount = order.TotalAmount,
                TotalAmount = order.TotalAmount,
                Status = order.Status == "DELIVERED" ? "PAID" : "PENDING",
                PaidAt = order.Status == "DELIVERED" ? order.CreatedAt.AddHours(rng.Next(1, 24)) : null
            });

            _db.Invoices.Add(new Invoice
            {
                OrderId = order.Id,
                InvoiceNumber = $"INV-{order.CreatedAt:yyyyMMdd}-{rng.Next(100000, 999999)}",
                InvoiceDate = order.CreatedAt,
                DueDate = order.CreatedAt.AddDays(14),
                SubTotal = order.BasePrice,
                TaxAmount = order.TaxAmount,
                InsuranceFee = order.InsuranceFee,
                TotalAmount = order.TotalAmount,
                Status = order.Status == "DELIVERED" ? "PAID" : "UNPAID"
            });
        }
        await _db.SaveChangesAsync();

        // ─── System Configurations ───
        var configs = new List<SystemConfiguration>
        {
            new() { Key = "CompanyName", Value = "Ngibrid Logistics", Category = "General" },
            new() { Key = "TaxRate", Value = "0.11", Category = "Finance" },
            new() { Key = "DefaultCurrency", Value = "IDR", Category = "Finance" },
            new() { Key = "MaxPackageWeight", Value = "50", Category = "Shipping" },
            new() { Key = "InsuranceRate", Value = "0.02", Category = "Finance" },
            new() { Key = "SlaDeliveryHours", Value = "48", Category = "Shipping" },
            new() { Key = "SupportEmail", Value = "support@ngibrid.com", Category = "General" },
            new() { Key = "SupportPhone", Value = "021-5555-1234", Category = "General" }
        };
        _db.SystemConfigurations.AddRange(configs);
        await _db.SaveChangesAsync();

        // ─── Analytics Sample Data ───
        for (int d = 0; d < 30; d++)
        {
            _db.AnalyticsData.Add(new AnalyticsData
            {
                Period = DateTime.UtcNow.AddDays(-d).Date,
                MetricName = "OrderCount",
                MetricValue = rng.Next(50, 200),
                PeriodType = "DAILY"
            });
            _db.AnalyticsData.Add(new AnalyticsData
            {
                Period = DateTime.UtcNow.AddDays(-d).Date,
                MetricName = "Revenue",
                MetricValue = rng.Next(5000000, 20000000),
                PeriodType = "DAILY"
            });
        }
        await _db.SaveChangesAsync();

        // ─── Notifications ───
        foreach (var cust in customers.Take(5))
        {
            _db.Notifications.AddRange(
                new Notification { UserId = cust.Id, Title = "Selamat Datang!", Message = "Selamat datang di Ngibrid Logistics! Gunakan kode WELCOME10 untuk diskon 10%.", Type = "SUCCESS", Channel = "WEB" },
                new Notification { UserId = cust.Id, Title = "Paket Dikirim", Message = "Paket Anda sedang dalam perjalanan. Track dengan nomor resi Anda.", Type = "INFO", Channel = "WEB" }
            );
        }
        await _db.SaveChangesAsync();

        await SeedInventoryAsync(warehouses, rng);
        await SeedIntegrationsAsync();
        await SeedPartnersAsync();
        await SeedLockersAsync();
        await SeedSupportAndPickupsAsync(customers, rng);
        await SeedLoyaltyAsync(customers, orders);
        await SeedComplianceAsync(orders, rng);
    }

    /// <summary>Inventory across sections, including cold-storage items the IoT simulator drives.</summary>
    private async Task SeedInventoryAsync(List<Warehouse> warehouses, Random rng)
    {
        var sections = await _db.WarehouseSections.ToListAsync();
        var products = new[]
        {
            ("Kaos Katun Premium", "PCS", 0.3, false), ("Sepatu Running", "PCS", 0.9, false),
            ("Kopi Arabika 250g", "KG", 0.25, false), ("Vaksin Cold Chain", "BOX", 2.0, true),
            ("Daging Beku 1kg", "KG", 1.0, true), ("Susu UHT Karton", "BOX", 12.0, true),
            ("Powerbank 20000mAh", "PCS", 0.4, false), ("Buku Novel", "PCS", 0.35, false),
            ("Laptop Gaming", "PCS", 2.4, false), ("Obat Herbal", "BOX", 0.6, false)
        };

        var items = new List<InventoryItem>();
        foreach (var warehouse in warehouses)
        {
            var warehouseSections = sections.Where(s => s.WarehouseId == warehouse.Id).ToList();
            if (warehouseSections.Count == 0) continue;

            for (var i = 0; i < 12; i++)
            {
                var (name, unit, weight, needsCold) = products[rng.Next(products.Length)];
                var section = needsCold
                    ? warehouseSections.FirstOrDefault(s => s.SectionType == "COLD") ?? warehouseSections[0]
                    : warehouseSections[rng.Next(warehouseSections.Count)];

                items.Add(new InventoryItem
                {
                    WarehouseId = warehouse.Id,
                    SectionId = section.Id,
                    Sku = $"SKU-{warehouse.Code[^2..]}-{i + 1:D3}",
                    Name = name,
                    Description = $"{name} — stok gudang {warehouse.City}",
                    Quantity = rng.Next(5, 300),
                    Unit = unit,
                    WeightKg = weight,
                    VolumeM3 = Math.Round(rng.NextDouble() * 0.5 + 0.01, 3),
                    RfidTag = $"RFID-{Guid.NewGuid().ToString("N")[..10].ToUpper()}",
                    Barcode = $"899{rng.Next(1000000, 9999999)}",
                    ShelfLocation = $"{(char)('A' + rng.Next(6))}-{rng.Next(1, 20):D2}-{rng.Next(1, 6)}",
                    BatchNumber = $"BATCH-{DateTime.UtcNow:yyyyMM}-{rng.Next(100, 999)}",
                    ReceivedAt = DateTime.UtcNow.AddDays(-rng.Next(1, 60)),
                    ExpiryDate = needsCold ? DateTime.UtcNow.AddDays(rng.Next(10, 120)) : null,
                    CurrentTemperature = needsCold ? Math.Round(2 + rng.NextDouble() * 4, 1) : null,
                    CurrentHumidity = needsCold ? Math.Round(45 + rng.NextDouble() * 30, 1) : null,
                    LastSensorReading = needsCold ? DateTime.UtcNow : null,
                    Status = "STORED"
                });
            }
        }

        _db.InventoryItems.AddRange(items);
        await _db.SaveChangesAsync();

        // Movement history so the inventory ledger isn't empty.
        foreach (var item in items.Take(40))
        {
            _db.InventoryMovements.Add(new InventoryMovement
            {
                InventoryItemId = item.Id,
                MovementType = "IN",
                Quantity = item.Quantity,
                BalanceAfter = item.Quantity,
                Notes = "Penerimaan awal stok",
                CreatedAt = item.ReceivedAt ?? DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();
    }

    private async Task SeedIntegrationsAsync()
    {
        _db.Integrations.AddRange(
            new Integration
            {
                Name = "Toko Gadget Jaya", Platform = "TOKOPEDIA", IntegrationType = "MARKETPLACE",
                ShopId = "TKP-889120", Endpoint = _config["Integration:Marketplace:Tokopedia:Endpoint"],
                ApiKey = _config["Integration:Marketplace:Tokopedia:ApiKey"],
                IsEnabled = true, AutoSync = true, SyncIntervalMinutes = 30
            },
            new Integration
            {
                Name = "Fashion Store ID", Platform = "SHOPEE", IntegrationType = "MARKETPLACE",
                ShopId = "SHP-445201", Endpoint = _config["Integration:Marketplace:Shopee:Endpoint"],
                ApiKey = _config["Integration:Marketplace:Shopee:ApiKey"],
                IsEnabled = true, AutoSync = false, SyncIntervalMinutes = 60
            },
            new Integration
            {
                Name = "SAP ERP Internal", Platform = "ERP", IntegrationType = "ERP",
                IsEnabled = false, SyncIntervalMinutes = 120
            },
            new Integration
            {
                Name = "HubSpot CRM", Platform = "CRM", IntegrationType = "CRM",
                IsEnabled = false, SyncIntervalMinutes = 240
            });

        await _db.SaveChangesAsync();
    }

    private async Task SeedPartnersAsync()
    {
        _db.LogisticsPartners.AddRange(
            new LogisticsPartner
            {
                Name = "Pos Indonesia", Code = "POS", PartnerType = "DOMESTIC",
                CoverageAreas = "[\"*\"]", BaseRatePerKg = 9000, HandoverFee = 3000,
                EstimatedDaysMin = 3, EstimatedDaysMax = 7, SupportsCod = true, Rating = 4.1
            },
            new LogisticsPartner
            {
                Name = "Lion Parcel", Code = "LNP", PartnerType = "DOMESTIC",
                CoverageAreas = "[\"Jakarta\",\"Bandung\",\"Surabaya\",\"Semarang\",\"Medan\",\"Yogyakarta\"]",
                BaseRatePerKg = 11000, HandoverFee = 2500,
                EstimatedDaysMin = 2, EstimatedDaysMax = 5, SupportsCod = true, Rating = 4.3
            },
            new LogisticsPartner
            {
                Name = "Last-Mile Bandung", Code = "LMB", PartnerType = "LAST_MILE",
                CoverageAreas = "[\"Bandung\",\"Cimahi\"]", BaseRatePerKg = 7000, HandoverFee = 1500,
                EstimatedDaysMin = 1, EstimatedDaysMax = 2, SupportsCod = false, Rating = 4.6
            },
            new LogisticsPartner
            {
                Name = "DHL Express", Code = "DHL", PartnerType = "CROSS_BORDER",
                CoverageCountries = "[\"SG\",\"MY\",\"TH\",\"AU\",\"US\",\"JP\",\"CN\",\"GB\"]",
                BaseRatePerKg = 145000, HandoverFee = 55000,
                EstimatedDaysMin = 3, EstimatedDaysMax = 8, SupportsInsurance = true, Rating = 4.8
            },
            new LogisticsPartner
            {
                Name = "FedEx International", Code = "FDX", PartnerType = "CROSS_BORDER",
                CoverageCountries = "[\"SG\",\"MY\",\"US\",\"NL\",\"DE\"]",
                BaseRatePerKg = 158000, HandoverFee = 60000,
                EstimatedDaysMin = 2, EstimatedDaysMax = 6, SupportsInsurance = true, Rating = 4.7
            });

        await _db.SaveChangesAsync();
    }

    private async Task SeedLockersAsync()
    {
        var lockerSpecs = new[]
        {
            ("Locker Stasiun Gambir", "LKR-JKT-001", "Jakarta Pusat", -6.1766, 106.8306),
            ("Locker Mall Kelapa Gading", "LKR-JKT-002", "Jakarta Utara", -6.1588, 106.9065),
            ("Locker Stasiun Bandung", "LKR-BDG-001", "Bandung", -6.9145, 107.6021),
            ("Locker Tunjungan Plaza", "LKR-SBY-001", "Surabaya", -7.2624, 112.7387)
        };

        var rng = new Random(7);
        foreach (var (name, code, city, lat, lng) in lockerSpecs)
        {
            var locker = new SmartLocker
            {
                Name = name, Code = code, City = city,
                Address = $"{name}, {city}",
                Latitude = lat, Longitude = lng,
                Status = "ONLINE",
                BatteryPercent = Math.Round(60 + rng.NextDouble() * 40, 1),
                TemperatureCelsius = Math.Round(24 + rng.NextDouble() * 5, 1),
                LastHeartbeat = DateTime.UtcNow
            };
            _db.SmartLockers.Add(locker);
            await _db.SaveChangesAsync();

            // 12 doors: a mix of sizes, a few already occupied.
            var sizes = new[] { "S", "S", "S", "M", "M", "M", "M", "L", "L", "L", "XL", "XL" };
            for (var i = 0; i < sizes.Length; i++)
            {
                var occupied = rng.NextDouble() < 0.25;
                _db.LockerCompartments.Add(new LockerCompartment
                {
                    SmartLockerId = locker.Id,
                    CompartmentNumber = $"{(char)('A' + i / 4)}{i % 4 + 1}",
                    Size = sizes[i],
                    Status = occupied ? "OCCUPIED" : "EMPTY",
                    AccessPin = occupied ? rng.Next(100000, 999999).ToString() : null,
                    OccupiedAt = occupied ? DateTime.UtcNow.AddHours(-rng.Next(1, 40)) : null,
                    ExpiresAt = occupied ? DateTime.UtcNow.AddHours(rng.Next(5, 60)) : null
                });
            }
            await _db.SaveChangesAsync();
        }
    }

    private async Task SeedSupportAndPickupsAsync(List<ApplicationUser> customers, Random rng)
    {
        var ticketSpecs = new[]
        {
            ("Paket belum sampai padahal status terkirim", "COMPLAINT", "HIGH", "OPEN"),
            ("Kemasan rusak saat diterima", "DAMAGE", "URGENT", "IN_PROGRESS"),
            ("Minta refund ongkir karena keterlambatan", "REFUND", "NORMAL", "OPEN"),
            ("Bagaimana cara klaim asuransi?", "GENERAL", "LOW", "RESOLVED"),
            ("Paket hilang setelah 2 minggu", "LOST_PACKAGE", "URGENT", "IN_PROGRESS")
        };

        for (var i = 0; i < ticketSpecs.Length; i++)
        {
            var (subject, category, priority, status) = ticketSpecs[i];
            var customer = customers[i % customers.Count];

            var ticket = new SupportTicket
            {
                UserId = customer.Id,
                TicketNumber = $"TKT-{DateTime.UtcNow:yyyyMMdd}-{rng.Next(100000, 999999)}",
                Subject = subject,
                Category = category,
                Priority = priority,
                Status = status,
                ResolvedAt = status == "RESOLVED" ? DateTime.UtcNow.AddDays(-1) : null,
                CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(1, 14))
            };
            _db.SupportTickets.Add(ticket);
            await _db.SaveChangesAsync();

            _db.SupportMessages.AddRange(
                new SupportMessage
                {
                    SupportTicketId = ticket.Id, SenderId = customer.Id, SenderType = "CUSTOMER",
                    Message = subject, CreatedAt = ticket.CreatedAt
                },
                new SupportMessage
                {
                    SupportTicketId = ticket.Id, SenderId = 1, SenderType = "AGENT",
                    Message = "Terima kasih atas laporannya. Tim kami sedang menindaklanjuti kasus Anda.",
                    CreatedAt = ticket.CreatedAt.AddHours(2)
                });
        }
        await _db.SaveChangesAsync();

        var slots = new[] { "MORNING", "AFTERNOON", "EVENING" };
        var statuses = new[] { "REQUESTED", "REQUESTED", "ASSIGNED", "PICKED_UP" };

        for (var i = 0; i < 8; i++)
        {
            var customer = customers[i % customers.Count];
            var status = statuses[i % statuses.Length];

            _db.PickupRequests.Add(new PickupRequest
            {
                CustomerId = customer.Id,
                RequestNumber = $"PKP-{DateTime.UtcNow:yyyyMMdd}-{rng.Next(100000, 999999)}",
                PickupAddress = $"Jl. Melati No.{rng.Next(1, 150)}, {customer.City}",
                PickupCity = customer.City ?? "Kota Jakarta Pusat",
                PickupProvince = CityCoordinates.ProvinceOf(customer.City) ?? "DKI Jakarta",
                PickupPostalCode = $"{rng.Next(10000, 19999)}",
                RequestedPickupDate = DateTime.UtcNow.AddDays(rng.Next(-3, 4)),
                PreferredTimeSlot = slots[i % slots.Length],
                EstimatedPackageCount = rng.Next(1, 8),
                EstimatedWeightKg = Math.Round(rng.NextDouble() * 15 + 1, 1),
                SpecialInstructions = i % 3 == 0 ? "Hubungi satpam lobby saat tiba." : null,
                Status = status,
                ActualPickupTime = status == "PICKED_UP" ? DateTime.UtcNow.AddHours(-rng.Next(2, 40)) : null,
                CreatedAt = DateTime.UtcNow.AddDays(-rng.Next(0, 7))
            });
        }
        await _db.SaveChangesAsync();
    }

    /// <summary>Loyalty ledger consistent with each user's seeded point balance.</summary>
    private async Task SeedLoyaltyAsync(List<ApplicationUser> customers, List<Order> orders)
    {
        foreach (var customer in customers)
        {
            var customerOrders = orders
                .Where(o => o.CustomerId == customer.Id && o.Status == "DELIVERED")
                .Take(5)
                .ToList();
            if (customerOrders.Count == 0) continue;

            var balance = 0;
            foreach (var order in customerOrders)
            {
                var points = Math.Max((int)Math.Floor(order.TotalAmount * 0.0001m), 1);
                balance += points;

                _db.LoyaltyTransactions.Add(new LoyaltyTransaction
                {
                    UserId = customer.Id,
                    OrderId = order.Id,
                    TransactionType = "EARN",
                    Points = points,
                    BalanceAfter = balance,
                    Description = $"Poin dari pengiriman {order.OrderNumber}",
                    ExpiresAt = DateTime.UtcNow.AddYears(1),
                    CreatedAt = order.CreatedAt.AddHours(6)
                });
            }

            // Keep the balance column in step with the ledger.
            customer.LoyaltyPoints = balance;
        }

        await _db.SaveChangesAsync();
    }

    private async Task SeedComplianceAsync(List<Order> orders, Random rng)
    {
        var taxRate = _config.GetValue<decimal>("Shipment:TaxRate", 0.11m);

        foreach (var order in orders.Where(o => o.Status == "DELIVERED").Take(15))
        {
            var taxable = order.BasePrice;
            _db.TaxRecords.Add(new TaxRecord
            {
                OrderId = order.Id,
                TaxNumber = $"TAX-{order.CreatedAt:yyyyMMdd}-{rng.Next(100000, 999999)}",
                TaxType = "PPN",
                TaxableAmount = taxable,
                TaxRate = taxRate,
                TaxAmount = Math.Round(taxable * taxRate, 0),
                Currency = "IDR",
                Period = new DateTime(order.CreatedAt.Year, order.CreatedAt.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                Status = "RECORDED",
                CreatedAt = order.CreatedAt
            });
        }
        await _db.SaveChangesAsync();

        // Two cross-border declarations with their full document sets.
        var crossBorderOrders = orders.Take(2).ToList();
        var destinations = new[] { ("SG", "EXPORT", 250m), ("MY", "EXPORT", 480m) };

        for (var i = 0; i < crossBorderOrders.Count && i < destinations.Length; i++)
        {
            var order = crossBorderOrders[i];
            var (country, type, value) = destinations[i];

            var declaration = new CustomsDeclaration
            {
                OrderId = order.Id,
                DeclarationNumber = $"CUS-{DateTime.UtcNow:yyyyMMdd}-{rng.Next(100000, 999999)}",
                DeclarationType = type,
                OriginCountry = "ID",
                DestinationCountry = country,
                HsCode = "6109.10.00",
                GoodsDescription = order.PackageDescription,
                DeclaredValue = value,
                Currency = "USD",
                DutyAmount = Math.Round(value * 0.075m, 2),
                VatAmount = Math.Round(value * 1.075m * 0.11m, 2),
                Incoterm = "DAP",
                Status = i == 0 ? "SUBMITTED" : "DRAFT",
                SubmittedAt = i == 0 ? DateTime.UtcNow.AddDays(-2) : null
            };
            _db.CustomsDeclarations.Add(declaration);
            await _db.SaveChangesAsync();

            foreach (var docType in ComplianceService.RequiredDocuments(type))
            {
                _db.ComplianceDocuments.Add(new ComplianceDocument
                {
                    CustomsDeclarationId = declaration.Id,
                    DocumentType = docType,
                    DocumentNumber = $"{docType[..2]}-{DateTime.UtcNow:yyyyMMdd}-{rng.Next(10000, 99999)}",
                    Status = "ISSUED"
                });
            }
        }
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// <paramref name="city"/> must be a name the city master table knows, because the pickup form
    /// and the courier's routing resolve it to coordinates. A bare "Jakarta" does not qualify —
    /// DKI is five kota — so the default names one of them.
    /// </summary>
    private async Task<ApplicationUser> CreateUserAsync(string email, string password, string fullName, 
        string userType, string phone, string city = "Kota Jakarta Pusat")
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            UserType = userType,
            PhoneNumber = phone,
            Address = "Jl. Contoh Alamat No. 123",
            City = city,
            EmailConfirmed = true,
            IsActive = true,
            LoyaltyPoints = new Random().Next(0, 500)
        };
        await _userManager.CreateAsync(user, password);
        return user;
    }

    /// <summary>
    /// Tariff for a seeded order, mirroring <see cref="DynamicPricingService"/>'s zone model:
    /// a flat first-kilogram fare that grows with distance, plus ~60% of it per extra kilogram,
    /// scaled by service level. Returns the road distance too, so emissions stay consistent with it.
    ///
    /// Deliberately a local copy rather than a call into the pricing service: that one also applies
    /// live demand and time-of-day multipliers read from the clock, which would price a seeded order
    /// from four months ago as if it were placed right now.
    /// </summary>
    private static (decimal BasePrice, double DistanceKm) EstimateSeedTariff(
        City from, City to, double weightKg, string service)
    {
        var straight = RouteOptimizationService.HaversineDistance(
            from.Latitude, from.Longitude, to.Latitude, to.Longitude);
        var distance = Math.Max(straight * (straight <= 600 ? 1.3 : 1.12), 10);

        var firstKg = 9000m + (decimal)distance * 22m;
        var chargeable = (decimal)Math.Max(Math.Ceiling(weightKg), 1);
        var price = firstKg + Math.Round(firstKg * 0.6m, 0) * (chargeable - 1);

        price *= service switch
        {
            "EXP" => 1.8m,
            "SAMEDAY" => 3.0m,
            "ECO" => 0.8m,
            _ => 1.0m
        };

        return (Math.Round(price, 0), Math.Round(distance, 1));
    }

    /// <summary>
    /// Status history for one order. <paramref name="baseTime"/> is the order's own creation time —
    /// anchoring every order to "48 hours ago" instead put the history of a three-month-old order
    /// in the future relative to the order itself.
    /// </summary>
    private List<(string Status, DateTime Timestamp)> GetStatusFlow(string finalStatus, DateTime baseTime, Random rng)
    {
        var flow = new List<(string, DateTime)>();
        var statusOrder = new[] { "CREATED", "PICKED_UP", "IN_TRANSIT", "AT_WAREHOUSE", "OUT_FOR_DELIVERY", "DELIVERED" };

        foreach (var status in statusOrder)
        {
            flow.Add((status, baseTime));
            baseTime = baseTime.AddHours(4 + rng.Next(1, 8));
            if (status == finalStatus) break;
        }
        return flow;
    }

    private string GetStatusNote(string status) => status switch
    {
        "CREATED" => "Pesanan telah dibuat dan menunggu penjemputan.",
        "PICKED_UP" => "Paket telah dijemput oleh kurir.",
        "IN_TRANSIT" => "Paket dalam perjalanan ke kota tujuan.",
        "AT_WAREHOUSE" => "Paket telah tiba di warehouse sortir.",
        "OUT_FOR_DELIVERY" => "Paket sedang diantar ke alamat penerima.",
        "DELIVERED" => "Paket telah diterima oleh penerima.",
        "FAILED" => "Pengiriman gagal, penerima tidak ditemukan.",
        "RETURNED" => "Paket dikembalikan ke pengirim.",
        _ => "Status diperbarui."
    };
}
