using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ngibrid.Data;
using Ngibrid.Models;

namespace Ngibrid.Services;

/// <summary>
/// Marketplace / ERP / CRM integration. Pulls external orders in and pushes status back out.
///
/// When an integration has a configured endpoint + API key the real HTTP call is made; otherwise the
/// sync runs against a deterministic local generator so the flow is exercisable without live credentials.
/// Either way the same import path, dedupe rule, and sync log are used.
/// </summary>
public class MarketplaceService
{
    private readonly NgibridDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly OrderService _orders;
    private readonly AuditService _audit;
    private readonly ILogger<MarketplaceService> _logger;

    public MarketplaceService(NgibridDbContext db, IHttpClientFactory http, OrderService orders,
        AuditService audit, ILogger<MarketplaceService> logger)
    {
        _db = db; _http = http; _orders = orders; _audit = audit; _logger = logger;
    }

    public async Task<List<Integration>> GetIntegrationsAsync() =>
        await _db.Integrations.Where(i => !i.IsDeleted).OrderBy(i => i.Platform).ToListAsync();

    public async Task<Integration> SaveIntegrationAsync(Integration integration)
    {
        if (integration.Id == 0) _db.Integrations.Add(integration);
        else _db.Integrations.Update(integration);
        await _db.SaveChangesAsync();
        return integration;
    }

    public async Task<List<IntegrationSyncLog>> GetSyncLogsAsync(long? integrationId = null, int take = 50)
    {
        var query = _db.IntegrationSyncLogs.AsQueryable();
        if (integrationId.HasValue) query = query.Where(l => l.IntegrationId == integrationId);
        return await query.OrderByDescending(l => l.CreatedAt).Take(take).ToListAsync();
    }

    /// <summary>
    /// Import orders from an external platform. Orders already imported (matched on ExternalOrderId)
    /// are skipped, so a re-run is safe.
    /// </summary>
    public async Task<IntegrationSyncLog> SyncOrdersAsync(long integrationId)
    {
        var integration = await _db.Integrations.FindAsync(integrationId)
            ?? throw new KeyNotFoundException($"Integration {integrationId} not found");

        var sw = Stopwatch.StartNew();
        var log = new IntegrationSyncLog { IntegrationId = integrationId, Direction = "INBOUND" };

        try
        {
            var external = await FetchExternalOrdersAsync(integration);
            var imported = 0;
            var failed = 0;

            foreach (var ext in external)
            {
                try
                {
                    var exists = await _db.Orders.AnyAsync(o => o.ExternalOrderId == ext.ExternalOrderId);
                    if (exists) continue;

                    var customerId = await ResolveCustomerIdAsync();
                    var order = new Order
                    {
                        CustomerId = customerId,
                        ExternalOrderId = ext.ExternalOrderId,
                        SenderName = ext.SenderName,
                        SenderPhone = ext.SenderPhone,
                        SenderAddress = ext.SenderAddress,
                        // Marketplaces send a city string and nothing else, so the province is
                        // recovered from the master table — the tariff depends on it.
                        SenderCity = CityCoordinates.CanonicalName(ext.SenderProvince, ext.SenderCity) ?? ext.SenderCity,
                        SenderProvince = ext.SenderProvince ?? CityCoordinates.ProvinceOf(ext.SenderCity),
                        RecipientName = ext.RecipientName,
                        RecipientPhone = ext.RecipientPhone,
                        RecipientAddress = ext.RecipientAddress,
                        RecipientCity = CityCoordinates.CanonicalName(ext.RecipientProvince, ext.RecipientCity) ?? ext.RecipientCity,
                        RecipientProvince = ext.RecipientProvince ?? CityCoordinates.ProvinceOf(ext.RecipientCity),
                        PackageDescription = ext.PackageDescription,
                        WeightKg = ext.WeightKg,
                        LengthCm = ext.LengthCm,
                        WidthCm = ext.WidthCm,
                        HeightCm = ext.HeightCm,
                        ServiceType = ext.ServiceType,
                        Currency = "IDR"
                    };

                    await _orders.CreateOrderAsync(order);
                    imported++;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogWarning(ex, "Failed to import external order {Id}", ext.ExternalOrderId);
                }
            }

            integration.LastSyncAt = DateTime.UtcNow;
            integration.LastSyncStatus = failed == 0 ? "OK" : "FAILED";
            integration.LastSyncMessage = $"{imported} imported, {failed} failed, {external.Count - imported - failed} already present";
            integration.TotalOrdersImported += imported;

            log.Status = failed == 0 ? "OK" : "PARTIAL";
            log.RecordsProcessed = imported;
            log.RecordsFailed = failed;
            log.Message = integration.LastSyncMessage;
        }
        catch (Exception ex)
        {
            integration.LastSyncAt = DateTime.UtcNow;
            integration.LastSyncStatus = "FAILED";
            integration.LastSyncMessage = ex.Message;
            log.Status = "FAILED";
            log.Message = ex.Message;
            _logger.LogError(ex, "Sync failed for integration {Id}", integrationId);
        }

        log.DurationMs = (int)sw.ElapsedMilliseconds;
        _db.IntegrationSyncLogs.Add(log);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("SYNC", "Integration", integrationId, notes: log.Message);
        return log;
    }

    /// <summary>
    /// Push a status change back to the marketplace so the buyer sees it on the platform.
    /// </summary>
    public async Task<bool> PushStatusAsync(long orderId, string status)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order?.ExternalOrderId == null) return false;

        var integration = await _db.Integrations
            .FirstOrDefaultAsync(i => i.IsEnabled && !i.IsDeleted);
        if (integration == null) return false;

        var log = new IntegrationSyncLog
        {
            IntegrationId = integration.Id,
            Direction = "OUTBOUND",
            RecordsProcessed = 1,
            Message = $"Order {order.OrderNumber} → {status}"
        };

        if (!string.IsNullOrEmpty(integration.Endpoint) && !string.IsNullOrEmpty(integration.ApiKey))
        {
            try
            {
                var client = _http.CreateClient("Default");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {integration.ApiKey}");
                var body = new
                {
                    order_id = order.ExternalOrderId,
                    logistics_status = status,
                    tracking_number = order.TrackingNumber,
                    updated_at = DateTime.UtcNow
                };
                var response = await client.PostAsJsonAsync($"{integration.Endpoint.TrimEnd('/')}/order/status", body);
                log.Status = response.IsSuccessStatusCode ? "OK" : "FAILED";
                if (!response.IsSuccessStatusCode) log.Message += $" — HTTP {(int)response.StatusCode}";
            }
            catch (Exception ex)
            {
                log.Status = "FAILED";
                log.Message += $" — {ex.Message}";
            }
        }
        else
        {
            log.Status = "OK";
            log.Message += " (no endpoint configured — recorded locally)";
        }

        _db.IntegrationSyncLogs.Add(log);
        await _db.SaveChangesAsync();
        return log.Status == "OK";
    }

    private async Task<List<ExternalOrderDto>> FetchExternalOrdersAsync(Integration integration)
    {
        if (!string.IsNullOrEmpty(integration.Endpoint) && !string.IsNullOrEmpty(integration.ApiKey))
        {
            var client = _http.CreateClient("Default");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {integration.ApiKey}");
            var url = $"{integration.Endpoint.TrimEnd('/')}/orders?shop_id={integration.ShopId}&status=ready_to_ship";

            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
            return ParseExternalOrders(payload, integration.Platform);
        }

        return GenerateSampleExternalOrders(integration);
    }

    /// <summary>
    /// Tokopedia and Shopee both wrap the order list in a data envelope but name the fields differently.
    /// Unknown shapes yield an empty list rather than throwing, so one odd payload doesn't fail the run.
    /// </summary>
    private static List<ExternalOrderDto> ParseExternalOrders(JsonElement payload, string platform)
    {
        var result = new List<ExternalOrderDto>();

        if (!payload.TryGetProperty("data", out var data)) return result;
        var list = data.ValueKind == JsonValueKind.Array
            ? data
            : data.TryGetProperty("orders", out var nested) ? nested : default;
        if (list.ValueKind != JsonValueKind.Array) return result;

        foreach (var item in list.EnumerateArray())
        {
            string? Get(params string[] names)
            {
                foreach (var n in names)
                    if (item.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
                        return v.GetString();
                return null;
            }

            double GetNum(string name, double fallback)
                => item.TryGetProperty(name, out var v) && v.TryGetDouble(out var d) ? d : fallback;

            var id = Get("order_id", "ordersn", "id");
            if (string.IsNullOrEmpty(id)) continue;

            result.Add(new ExternalOrderDto
            {
                ExternalOrderId = $"{platform}-{id}",
                SenderName = Get("shop_name", "seller_name") ?? platform,
                SenderPhone = Get("shop_phone", "seller_phone") ?? "0800000000",
                SenderAddress = Get("shop_address", "pickup_address") ?? "-",
                SenderCity = Get("shop_city", "pickup_city") ?? "Jakarta",
                SenderProvince = Get("shop_province", "pickup_province"),
                RecipientName = Get("recipient_name", "buyer_name") ?? "-",
                RecipientPhone = Get("recipient_phone", "buyer_phone") ?? "0800000001",
                RecipientAddress = Get("recipient_address", "shipping_address") ?? "-",
                RecipientCity = Get("recipient_city", "shipping_city") ?? "Jakarta",
                RecipientProvince = Get("recipient_province", "shipping_province"),
                PackageDescription = Get("product_name", "item_name") ?? "Marketplace order",
                WeightKg = GetNum("weight", 1),
                LengthCm = GetNum("length", 20),
                WidthCm = GetNum("width", 15),
                HeightCm = GetNum("height", 10),
                ServiceType = Get("service_type") ?? "REG"
            });
        }

        return result;
    }

    /// <summary>
    /// Deterministic sample feed used when no live credentials are configured. Seeded from the
    /// integration id and the current date so repeated syncs on the same day are idempotent.
    /// </summary>
    private static List<ExternalOrderDto> GenerateSampleExternalOrders(Integration integration)
    {
        var rng = new Random((int)(integration.Id * 1000 + DateTime.UtcNow.DayOfYear));
        // Names match the city master table, so imported samples price and route like real orders.
        var cities = new (string City, string Province)[]
        {
            ("Kota Bandung", "Jawa Barat"), ("Kota Surabaya", "Jawa Timur"),
            ("Kota Semarang", "Jawa Tengah"), ("Kota Medan", "Sumatera Utara"),
            ("Kota Yogyakarta", "DI Yogyakarta"), ("Kota Denpasar", "Bali"),
            ("Kota Makassar", "Sulawesi Selatan"), ("Kota Balikpapan", "Kalimantan Timur"),
            ("Kabupaten Bekasi", "Jawa Barat"), ("Kota Palembang", "Sumatera Selatan")
        };
        var products = new[] { "Case HP", "Sepatu Sneakers", "Kopi 250gr", "Powerbank", "Buku Novel", "Tas Ransel" };
        var buyers = new[] { "Rina Wijaya", "Doni Saputra", "Sari Melati", "Agus Priyanto", "Nina Kartika" };

        var count = rng.Next(2, 6);
        var result = new List<ExternalOrderDto>();

        for (var i = 0; i < count; i++)
        {
            var buyer = buyers[rng.Next(buyers.Length)];
            var destination = cities[rng.Next(cities.Length)];
            result.Add(new ExternalOrderDto
            {
                ExternalOrderId = $"{integration.Platform}-{DateTime.UtcNow:yyyyMMdd}-{integration.Id}-{i:D3}",
                SenderName = integration.Name,
                SenderPhone = "0811000000",
                SenderAddress = $"Gudang Seller {integration.Platform}",
                SenderCity = "Kota Jakarta Pusat",
                SenderProvince = "DKI Jakarta",
                RecipientName = buyer,
                RecipientPhone = $"0812{rng.Next(1000000, 9999999)}",
                RecipientAddress = $"Jl. Merdeka No.{rng.Next(1, 200)}",
                RecipientCity = destination.City,
                RecipientProvince = destination.Province,
                PackageDescription = products[rng.Next(products.Length)],
                WeightKg = Math.Round(rng.NextDouble() * 4 + 0.3, 1),
                LengthCm = rng.Next(10, 40),
                WidthCm = rng.Next(10, 30),
                HeightCm = rng.Next(5, 25),
                ServiceType = new[] { "REG", "EXP", "ECO" }[rng.Next(3)]
            });
        }

        return result;
    }

    /// <summary>Marketplace orders are booked against the first customer account available.</summary>
    private async Task<long> ResolveCustomerIdAsync()
    {
        var customer = await _db.Users
            .Where(u => u.UserType == "Customer")
            .OrderBy(u => u.Id)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();
        if (customer != 0) return customer;
        return await _db.Users.OrderBy(u => u.Id).Select(u => u.Id).FirstAsync();
    }

    public class ExternalOrderDto
    {
        public string ExternalOrderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderPhone { get; set; } = string.Empty;
        public string SenderAddress { get; set; } = string.Empty;
        public string SenderCity { get; set; } = string.Empty;
        public string? SenderProvince { get; set; }
        public string RecipientName { get; set; } = string.Empty;
        public string RecipientPhone { get; set; } = string.Empty;
        public string RecipientAddress { get; set; } = string.Empty;
        public string RecipientCity { get; set; } = string.Empty;
        public string? RecipientProvince { get; set; }
        public string PackageDescription { get; set; } = string.Empty;
        public double WeightKg { get; set; } = 1;
        public double LengthCm { get; set; } = 20;
        public double WidthCm { get; set; } = 15;
        public double HeightCm { get; set; } = 10;
        public string ServiceType { get; set; } = "REG";
    }
}

/// <summary>
/// Third-party logistics: partner selection, handover, and cross-border legs.
/// </summary>
public class PartnerLogisticsService
{
    private readonly NgibridDbContext _db;
    private readonly AuditService _audit;

    public PartnerLogisticsService(NgibridDbContext db, AuditService audit)
    { _db = db; _audit = audit; }

    public async Task<List<LogisticsPartner>> GetPartnersAsync(bool activeOnly = true)
    {
        var query = _db.LogisticsPartners.Where(p => !p.IsDeleted);
        if (activeOnly) query = query.Where(p => p.IsActive);
        return await query.OrderBy(p => p.Name).ToListAsync();
    }

    public async Task<LogisticsPartner> SavePartnerAsync(LogisticsPartner partner)
    {
        if (partner.Id == 0) _db.LogisticsPartners.Add(partner);
        else _db.LogisticsPartners.Update(partner);
        await _db.SaveChangesAsync();
        return partner;
    }

    /// <summary>
    /// Rank partners that can serve a destination, cheapest first.
    /// Domestic destinations use the city coverage list; ISO country codes route to cross-border partners.
    /// </summary>
    public async Task<List<PartnerQuote>> GetQuotesAsync(string destination, double weightKg, bool crossBorder = false)
    {
        var partners = await GetPartnersAsync();
        var quotes = new List<PartnerQuote>();

        foreach (var p in partners)
        {
            if (crossBorder && p.PartnerType != "CROSS_BORDER") continue;
            if (!crossBorder && p.PartnerType == "CROSS_BORDER") continue;

            if (!ServesDestination(p, destination, crossBorder)) continue;

            var cost = Math.Round(p.BaseRatePerKg * (decimal)Math.Max(weightKg, 1) + p.HandoverFee, 0);
            quotes.Add(new PartnerQuote
            {
                PartnerId = p.Id,
                PartnerName = p.Name,
                PartnerCode = p.Code,
                Cost = cost,
                EstimatedDaysMin = p.EstimatedDaysMin,
                EstimatedDaysMax = p.EstimatedDaysMax,
                SupportsCod = p.SupportsCod,
                Rating = p.Rating
            });
        }

        return quotes.OrderBy(q => q.Cost).ToList();
    }

    private static bool ServesDestination(LogisticsPartner partner, string destination, bool crossBorder)
    {
        var raw = crossBorder ? partner.CoverageCountries : partner.CoverageAreas;
        if (string.IsNullOrWhiteSpace(raw)) return true; // no restriction recorded

        try
        {
            var areas = JsonSerializer.Deserialize<List<string>>(raw) ?? new List<string>();
            if (areas.Count == 0) return true;
            return areas.Any(a =>
                a.Equals("*", StringComparison.Ordinal) ||
                a.Contains(destination, StringComparison.OrdinalIgnoreCase) ||
                destination.Contains(a, StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return true;
        }
    }

    public async Task<PartnerShipment> HandoverAsync(long orderId, long partnerId, bool crossBorder = false,
        string? destinationCountry = null)
    {
        var order = await _db.Orders.FindAsync(orderId)
            ?? throw new KeyNotFoundException($"Order {orderId} not found");
        var partner = await _db.LogisticsPartners.FindAsync(partnerId)
            ?? throw new KeyNotFoundException($"Partner {partnerId} not found");

        var shipment = new PartnerShipment
        {
            OrderId = orderId,
            LogisticsPartnerId = partnerId,
            HandoverNumber = $"HOV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
            PartnerTrackingNumber = $"{partner.Code}{DateTime.UtcNow:yyMMdd}{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
            PartnerCost = Math.Round(partner.BaseRatePerKg * (decimal)Math.Max(order.WeightKg, 1) + partner.HandoverFee, 0),
            IsCrossBorder = crossBorder,
            DestinationCountry = destinationCountry,
            Status = "HANDED_OVER"
        };

        _db.PartnerShipments.Add(shipment);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("HANDOVER", "Order", orderId,
            notes: $"Handed over to {partner.Name} ({shipment.PartnerTrackingNumber})");
        return shipment;
    }

    public async Task<List<PartnerShipment>> GetShipmentsAsync(int take = 100) =>
        await _db.PartnerShipments
            .Include(s => s.Partner)
            .Include(s => s.Order)
            .OrderByDescending(s => s.HandoverAt)
            .Take(take)
            .ToListAsync();

    public async Task UpdateShipmentStatusAsync(long shipmentId, string status)
    {
        var shipment = await _db.PartnerShipments.FindAsync(shipmentId);
        if (shipment == null) return;
        shipment.Status = status;
        if (status is "DELIVERED" or "RETURNED" or "FAILED") shipment.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public class PartnerQuote
    {
        public long PartnerId { get; set; }
        public string PartnerName { get; set; } = string.Empty;
        public string PartnerCode { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public int EstimatedDaysMin { get; set; }
        public int EstimatedDaysMax { get; set; }
        public bool SupportsCod { get; set; }
        public double Rating { get; set; }
    }
}

/// <summary>
/// Smart locker network — reserve a compartment, issue a collection PIN, release on pickup.
/// </summary>
public class SmartLockerService
{
    private readonly NgibridDbContext _db;
    private readonly NotificationService _notifications;

    public SmartLockerService(NgibridDbContext db, NotificationService notifications)
    { _db = db; _notifications = notifications; }

    public async Task<List<SmartLocker>> GetLockersAsync(string? city = null)
    {
        var query = _db.SmartLockers.Include(l => l.Compartments).Where(l => !l.IsDeleted);
        if (!string.IsNullOrWhiteSpace(city)) query = query.Where(l => l.City.Contains(city));
        return await query.OrderBy(l => l.City).ThenBy(l => l.Name).ToListAsync();
    }

    public async Task<SmartLocker?> GetLockerAsync(long id) =>
        await _db.SmartLockers.Include(l => l.Compartments!).ThenInclude(c => c.Order)
            .FirstOrDefaultAsync(l => l.Id == id);

    /// <summary>
    /// Reserve the smallest free compartment that fits the parcel and issue a 6-digit collection PIN.
    /// Returns null when the locker is offline or full.
    /// </summary>
    public async Task<LockerCompartment?> AssignCompartmentAsync(long lockerId, long orderId, string size = "M",
        int holdHours = 72)
    {
        var locker = await _db.SmartLockers.Include(l => l.Compartments)
            .FirstOrDefaultAsync(l => l.Id == lockerId);
        if (locker == null || locker.Status != "ONLINE") return null;

        var sizeOrder = new[] { "S", "M", "L", "XL" };
        var minIndex = Math.Max(Array.IndexOf(sizeOrder, size.ToUpperInvariant()), 0);

        var compartment = locker.Compartments?
            .Where(c => c.Status == "EMPTY")
            .Where(c => Array.IndexOf(sizeOrder, c.Size) >= minIndex)
            .OrderBy(c => Array.IndexOf(sizeOrder, c.Size))
            .FirstOrDefault();
        if (compartment == null) return null;

        compartment.Status = "OCCUPIED";
        compartment.OrderId = orderId;
        compartment.AccessPin = Random.Shared.Next(100000, 999999).ToString();
        compartment.OccupiedAt = DateTime.UtcNow;
        compartment.ExpiresAt = DateTime.UtcNow.AddHours(holdHours);
        compartment.CollectedAt = null;
        await _db.SaveChangesAsync();

        var order = await _db.Orders.FindAsync(orderId);
        if (order != null)
        {
            await _notifications.SendAsync(order.CustomerId,
                "Paket siap diambil di Smart Locker",
                $"Paket {order.TrackingNumber} menunggu di {locker.Name}, loker {compartment.CompartmentNumber}. " +
                $"PIN: {compartment.AccessPin}. Ambil sebelum {compartment.ExpiresAt:dd MMM HH:mm}.",
                "SUCCESS", $"/locker");
        }

        return compartment;
    }

    /// <summary>Open a compartment with the recipient's PIN. Returns false when the PIN doesn't match.</summary>
    public async Task<bool> CollectAsync(long compartmentId, string pin)
    {
        var compartment = await _db.LockerCompartments.FindAsync(compartmentId);
        if (compartment == null || compartment.Status != "OCCUPIED") return false;
        if (!string.Equals(compartment.AccessPin, pin, StringComparison.Ordinal)) return false;

        compartment.Status = "EMPTY";
        compartment.CollectedAt = DateTime.UtcNow;
        compartment.AccessPin = null;
        compartment.OrderId = null;
        compartment.ExpiresAt = null;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<LockerStats> GetStatsAsync()
    {
        var compartments = await _db.LockerCompartments.ToListAsync();
        return new LockerStats
        {
            TotalLockers = await _db.SmartLockers.CountAsync(l => !l.IsDeleted),
            OnlineLockers = await _db.SmartLockers.CountAsync(l => !l.IsDeleted && l.Status == "ONLINE"),
            TotalCompartments = compartments.Count,
            OccupiedCompartments = compartments.Count(c => c.Status == "OCCUPIED"),
            ExpiredParcels = compartments.Count(c => c.Status == "OCCUPIED" && c.ExpiresAt < DateTime.UtcNow)
        };
    }

    public class LockerStats
    {
        public int TotalLockers { get; set; }
        public int OnlineLockers { get; set; }
        public int TotalCompartments { get; set; }
        public int OccupiedCompartments { get; set; }
        public int ExpiredParcels { get; set; }
        public double UtilizationPercent => TotalCompartments > 0
            ? Math.Round((double)OccupiedCompartments / TotalCompartments * 100, 1) : 0;
    }
}
