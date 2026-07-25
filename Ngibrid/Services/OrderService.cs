using Microsoft.EntityFrameworkCore;
using Ngibrid.Data;
using Ngibrid.Models;

namespace Ngibrid.Services;

/// <summary>
/// Core order management service
/// </summary>
public class OrderService
{
    private readonly NgibridDbContext _db;
    private readonly AuditService _audit;
    private readonly DynamicPricingService _pricing;
    private readonly GreenLogisticsService _green;
    private readonly IConfiguration _config;
    private readonly ILogger<OrderService> _logger;
    private readonly IServiceProvider _services;

    public OrderService(NgibridDbContext db, AuditService audit,
        DynamicPricingService pricing, GreenLogisticsService green,
        IConfiguration config, ILogger<OrderService> logger, IServiceProvider services)
    {
        _db = db;
        _audit = audit;
        _pricing = pricing;
        _green = green;
        _config = config;
        _logger = logger;
        _services = services;
    }

    public async Task<Order> CreateOrderAsync(Order order)
    {
        order.OrderNumber = await GenerateOrderNumberAsync();
        order.TrackingNumber = $"NGB{DateTime.UtcNow:yyMMdd}{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

        // Volumetric weight uses the industry divisor of 6000 (cm³ per kg); carriers bill on
        // whichever of actual/volumetric weight is greater.
        order.VolumetricWeight = Math.Round(order.LengthCm * order.WidthCm * order.HeightCm / 6000.0, 2);
        var chargeableWeight = Math.Max(order.WeightKg, order.VolumetricWeight);

        var priceResult = await _pricing.CalculatePriceAsync(
            order.SenderCity ?? "", order.RecipientCity ?? "", chargeableWeight, order.ServiceType,
            order.SenderProvince, order.RecipientProvince);

        order.BasePrice = priceResult.BasePrice;
        var runningTotal = priceResult.BasePrice;

        if (order.HasInsurance)
        {
            // Premium is a rate on declared value; fall back to the freight-based estimate when
            // the sender didn't declare one.
            var insuranceRate = _config.GetValue<decimal>("Shipment:InsuranceRate", 0.02m);
            order.InsuranceFee = order.DeclaredValue.HasValue
                ? Math.Round(order.DeclaredValue.Value * insuranceRate, 0)
                : priceResult.InsuranceFee;
            runningTotal += order.InsuranceFee;
        }
        else
        {
            order.InsuranceFee = 0;
        }

        var taxRate = _config.GetValue<decimal>("Shipment:TaxRate", 0.11m);
        order.TaxAmount = Math.Round(runningTotal * taxRate, 0);
        order.TotalAmount = runningTotal + order.TaxAmount;

        order.IsEcoDelivery = order.IsEcoDelivery || order.ServiceType == "ECO";
        order.CarbonEmissionGram = await _green.EstimateEmissionAsync(order);

        if (order.EstimatedDeliveryDate == null)
            order.EstimatedDeliveryDate = EstimateDelivery(order.ServiceType);

        order.BarcodeData = order.TrackingNumber;
        order.QrCodeUrl = $"/api/v1/labels/{order.TrackingNumber}/qr";

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Persist the opening history row in the same unit of work as the save below.
        _db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            Status = order.Status,
            Notes = "Pesanan dibuat dan menunggu penjemputan."
        });
        await _db.SaveChangesAsync();

        await _audit.LogAsync("CREATE", "Order", order.Id, notes: $"Order {order.OrderNumber} created");

        return order;
    }

    /// <summary>
    /// Daily sequence number. Counting on the indexed CreatedAt range (rather than o.CreatedAt.Date)
    /// keeps this translatable to SQL on every supported provider.
    /// </summary>
    private async Task<string> GenerateOrderNumberAsync()
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var count = await _db.Orders.CountAsync(o => o.CreatedAt >= today && o.CreatedAt < tomorrow);
        return $"NGB-{today:yyyyMMdd}-{(count + 1):D4}";
    }

    private static DateTime EstimateDelivery(string serviceType)
    {
        var days = serviceType.ToUpperInvariant() switch
        {
            "SAMEDAY" => 0,
            "EXP" => 1,
            "ECO" => 5,
            _ => 3
        };

        var eta = DateTime.UtcNow.AddDays(days);
        while (eta.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            eta = eta.AddDays(1);
        return eta;
    }

    /// <summary>
    /// Move an order to a new status, recording history, GPS breadcrumb, customer notification,
    /// loyalty points and marketplace push-back as applicable.
    /// </summary>
    public async Task<Order> UpdateStatusAsync(long orderId, string newStatus, string? notes = null,
        double? latitude = null, double? longitude = null, string? updatedBy = null)
    {
        var order = await _db.Orders.FindAsync(orderId)
            ?? throw new KeyNotFoundException($"Order {orderId} not found");

        var oldStatus = order.Status;
        if (oldStatus == newStatus) return order;

        order.Status = newStatus;

        if (newStatus == "DELIVERED") order.ActualDeliveryDate = DateTime.UtcNow;
        if (newStatus == "PICKED_UP") order.PickupDate = DateTime.UtcNow;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = updatedBy ?? "System";

        _db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = orderId,
            Status = newStatus,
            Notes = notes ?? DefaultStatusNote(newStatus),
            Latitude = latitude,
            Longitude = longitude,
            UpdatedByUserId = updatedBy
        });

        if (latitude.HasValue && longitude.HasValue)
        {
            _db.ShipmentTrackings.Add(new ShipmentTracking
            {
                OrderId = orderId,
                Latitude = latitude.Value,
                Longitude = longitude.Value,
                EventType = "STATUS_CHANGE",
                LocationDescription = notes
            });
        }

        await _db.SaveChangesAsync();
        await _audit.LogAsync("UPDATE_STATUS", "Order", orderId, "Status", oldStatus, newStatus, notes);

        await RunPostStatusHooksAsync(order, newStatus);

        return order;
    }

    /// <summary>
    /// Side effects of a status change. Each hook is isolated: a failing notification or a
    /// marketplace timeout must not roll back the status the customer can already see.
    /// </summary>
    private async Task RunPostStatusHooksAsync(Order order, string newStatus)
    {
        try
        {
            var notifications = _services.GetRequiredService<NotificationService>();
            await notifications.NotifyOrderStatusChangeAsync(order.Id, newStatus, order.CustomerId);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Notification hook failed for order {Id}", order.Id); }

        if (newStatus == "DELIVERED")
        {
            try
            {
                var loyalty = _services.GetRequiredService<LoyaltyService>();
                await loyalty.EarnForOrderAsync(order.Id);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Loyalty hook failed for order {Id}", order.Id); }

            try
            {
                var compliance = _services.GetRequiredService<ComplianceService>();
                await compliance.RecordTaxAsync(order.Id);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Tax hook failed for order {Id}", order.Id); }
        }

        if (!string.IsNullOrEmpty(order.ExternalOrderId))
        {
            try
            {
                var marketplace = _services.GetRequiredService<MarketplaceService>();
                await marketplace.PushStatusAsync(order.Id, newStatus);
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Marketplace push failed for order {Id}", order.Id); }
        }
    }

    private static string DefaultStatusNote(string status) => status switch
    {
        "CREATED" => "Pesanan telah dibuat dan menunggu penjemputan.",
        "PICKED_UP" => "Paket telah dijemput oleh kurir.",
        "IN_TRANSIT" => "Paket dalam perjalanan ke kota tujuan.",
        "AT_WAREHOUSE" => "Paket tiba di warehouse sortir.",
        "OUT_FOR_DELIVERY" => "Paket sedang diantar ke alamat penerima.",
        "DELIVERED" => "Paket telah diterima oleh penerima.",
        "FAILED" => "Pengiriman gagal, penerima tidak ditemukan.",
        "RETURNED" => "Paket dikembalikan ke pengirim.",
        "CANCELLED" => "Pesanan dibatalkan.",
        _ => $"Status diperbarui menjadi {status}."
    };

    public async Task<Order?> GetOrderAsync(long id) =>
        await _db.Orders.Include(o => o.StatusHistory).Include(o => o.Trackings)
            .Include(o => o.Payment).Include(o => o.Invoice).Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == id);

    public async Task<Order?> GetByTrackingNumberAsync(string trackingNumber) =>
        await _db.Orders.Include(o => o.StatusHistory).Include(o => o.Trackings)
            .FirstOrDefaultAsync(o => o.TrackingNumber == trackingNumber);

    /// <summary>
    /// Payment is included because the list needs to know whether an order is still payable —
    /// it is a 1:1 navigation, so this is one extra join, not a per-row query.
    /// </summary>
    public async Task<List<Order>> GetCustomerOrdersAsync(long customerId, int page = 1, int pageSize = 20) =>
        await _db.Orders.Where(o => o.CustomerId == customerId && !o.IsDeleted)
            .Include(o => o.Payment)
            .OrderByDescending(o => o.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

    /// <summary>
    /// Orders across all customers — the admin/ops view. Supports the search and status
    /// filters used by the orders page so filtering happens in SQL, not in the browser.
    /// </summary>
    public async Task<List<Order>> GetOrdersAsync(string? search = null, string? status = null,
        int page = 1, int pageSize = 50)
    {
        var query = _db.Orders.Where(o => !o.IsDeleted);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(o => o.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(o =>
                o.OrderNumber.Contains(term) ||
                (o.TrackingNumber != null && o.TrackingNumber.Contains(term)) ||
                o.RecipientName.Contains(term) ||
                o.SenderName.Contains(term));
        }

        return await query
            .Include(o => o.Payment)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> CountOrdersAsync(string? search = null, string? status = null)
    {
        var query = _db.Orders.Where(o => !o.IsDeleted);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(o => o.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(o =>
                o.OrderNumber.Contains(term) ||
                (o.TrackingNumber != null && o.TrackingNumber.Contains(term)) ||
                o.RecipientName.Contains(term) ||
                o.SenderName.Contains(term));
        }
        return await query.CountAsync();
    }

    public async Task<List<Order>> GetCourierOrdersAsync(long courierId) =>
        await _db.Orders.Where(o => o.AssignedCourierId == courierId && o.Status != "DELIVERED")
            .OrderBy(o => o.EstimatedDeliveryDate).ToListAsync();

    /// <summary>Soft-delete: orders are financial records, so rows are retained.</summary>
    public async Task CancelOrderAsync(long orderId, string reason)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order == null) return;
        await UpdateStatusAsync(orderId, "CANCELLED", reason);
    }
}
