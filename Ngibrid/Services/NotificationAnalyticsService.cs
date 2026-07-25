using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Ngibrid.Data;
using Ngibrid.Models;

namespace Ngibrid.Services;

/// <summary>
/// Notification service - email, SMS, push, in-app.
/// External channels are attempted only when credentials are configured; otherwise the
/// notification is still recorded in-app so nothing is silently lost.
/// </summary>
public class NotificationService
{
    private readonly NgibridDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(NgibridDbContext db, IHttpClientFactory httpFactory,
        IConfiguration config, ILogger<NotificationService> logger)
    { _db = db; _httpFactory = httpFactory; _config = config; _logger = logger; }

    /// <summary>
    /// Send notification to a user
    /// </summary>
    public async Task<Notification> SendAsync(long userId, string title, string message, 
        string type = "INFO", string? actionUrl = null, string channel = "WEB")
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            ActionUrl = actionUrl,
            Channel = channel,
            IsRead = false
        };
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();
        return notification;
    }

    /// <summary>
    /// Fan a notification out to every requested channel. In-app is always written; email/SMS/push
    /// are attempted when configured and their failures are logged, not thrown, so a delivery
    /// status update is never rolled back by an SMTP outage.
    /// </summary>
    public async Task<Notification> SendMultiChannelAsync(long userId, string title, string message,
        string type = "INFO", string? actionUrl = null, params string[] channels)
    {
        var notification = await SendAsync(userId, title, message, type, actionUrl);
        if (channels.Length == 0) return notification;

        var user = await _db.Users.FindAsync(userId);
        if (user == null) return notification;

        foreach (var channel in channels)
        {
            try
            {
                switch (channel.ToUpperInvariant())
                {
                    case "EMAIL" when !string.IsNullOrEmpty(user.Email):
                        await SendEmailAsync(user.Email, title, message);
                        break;
                    case "SMS" when !string.IsNullOrEmpty(user.PhoneNumber):
                        await SendSmsAsync(user.PhoneNumber, $"{title}: {message}");
                        break;
                    case "PUSH":
                        await SendPushAsync(userId, title, message);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Channel {Channel} failed for user {UserId}", channel, userId);
            }
        }

        return notification;
    }

    /// <summary>
    /// Send order status update notification
    /// </summary>
    public async Task NotifyOrderStatusChangeAsync(long orderId, string status, long userId)
    {
        var statusMessages = new Dictionary<string, string>
        {
            ["PICKED_UP"] = "Paket Anda telah dijemput dan sedang dalam perjalanan ke warehouse.",
            ["IN_TRANSIT"] = "Paket Anda sedang dalam perjalanan ke tujuan.",
            ["OUT_FOR_DELIVERY"] = "Paket Anda sedang diantar oleh kurir. Mohon tunggu di lokasi.",
            ["DELIVERED"] = "Paket Anda telah berhasil dikirim! Terima kasih telah menggunakan Ngibrid.",
            ["FAILED"] = "Pengiriman gagal. Tim kami akan menghubungi Anda segera.",
            ["RETURNED"] = "Paket dikembalikan ke pengirim."
        };

        var message = statusMessages.GetValueOrDefault(status, $"Status pesanan diperbarui: {status}");
        await SendAsync(userId, $"Update Order #{orderId}", message, "INFO", $"/orders/{orderId}");
    }

    public async Task<List<Notification>> GetUserNotificationsAsync(long userId, bool unreadOnly = false)
    {
        var query = _db.Notifications.Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);
        return await query.OrderByDescending(n => n.CreatedAt).Take(50).ToListAsync();
    }

    public async Task MarkAsReadAsync(long notificationId)
    {
        var n = await _db.Notifications.FindAsync(notificationId);
        if (n != null) { n.IsRead = true; n.ReadAt = DateTime.UtcNow; await _db.SaveChangesAsync(); }
    }

    public async Task MarkAllAsReadAsync(long userId)
    {
        var unread = await _db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
        foreach (var n in unread) { n.IsRead = true; n.ReadAt = DateTime.UtcNow; }
        if (unread.Count > 0) await _db.SaveChangesAsync();
    }

    public async Task<int> GetUnreadCountAsync(long userId) =>
        await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

    /// <summary>
    /// Send an email over SMTP. Returns false (without throwing) when SMTP isn't configured,
    /// so callers can treat email as best-effort.
    /// </summary>
    public async Task<bool> SendEmailAsync(string to, string subject, string body)
    {
        var host = _config["Notification:Email:SmtpServer"];
        var username = _config["Notification:Email:Username"];
        var password = _config["Notification:Email:Password"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogInformation("SMTP not configured; skipped email to {To} ({Subject})", to, subject);
            return false;
        }

        try
        {
            using var client = new SmtpClient(host, _config.GetValue("Notification:Email:SmtpPort", 587))
            {
                EnableSsl = _config.GetValue("Notification:Email:EnableSsl", true),
                Credentials = new NetworkCredential(username, password)
            };

            using var mail = new MailMessage
            {
                From = new MailAddress(
                    _config["Notification:Email:FromAddress"] ?? username,
                    _config["Notification:Email:FromName"] ?? "Ngibrid Logistics"),
                Subject = subject,
                Body = $"<p>{WebUtility.HtmlEncode(body)}</p><hr/><small>Ngibrid Logistics</small>",
                IsBodyHtml = true
            };
            mail.To.Add(to);

            await client.SendMailAsync(mail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send email to {To}", to);
            return false;
        }
    }

    /// <summary>Send an SMS through the configured provider (Twilio).</summary>
    public async Task<bool> SendSmsAsync(string toNumber, string message)
    {
        var sid = _config["Notification:SMS:AccountSid"];
        var token = _config["Notification:SMS:AuthToken"];
        var from = _config["Notification:SMS:FromNumber"];

        if (string.IsNullOrWhiteSpace(sid) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(from))
        {
            _logger.LogInformation("SMS not configured; skipped message to {To}", toNumber);
            return false;
        }

        var client = _httpFactory.CreateClient("Default");
        var auth = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{sid}:{token}"));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = toNumber,
            ["From"] = from,
            ["Body"] = message
        });

        var response = await client.PostAsync($"https://api.twilio.com/2010-04-01/Accounts/{sid}/Messages.json", form);
        if (!response.IsSuccessStatusCode)
            _logger.LogWarning("SMS send failed with {Status}", response.StatusCode);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Send a push notification via Firebase Cloud Messaging.</summary>
    public async Task<bool> SendPushAsync(long userId, string title, string message)
    {
        var serverKey = _config["Notification:PushNotification:FirebaseServerKey"];
        if (string.IsNullOrWhiteSpace(serverKey))
        {
            _logger.LogInformation("FCM not configured; skipped push for user {UserId}", userId);
            return false;
        }

        var client = _httpFactory.CreateClient("Default");
        client.DefaultRequestHeaders.Add("Authorization", $"key={serverKey}");

        var endpoint = _config["Notification:PushNotification:FcmEndpoint"] ?? "https://fcm.googleapis.com/fcm/send";
        var response = await client.PostAsJsonAsync(endpoint, new
        {
            to = $"/topics/user_{userId}",
            notification = new { title, body = message }
        });

        return response.IsSuccessStatusCode;
    }
}

/// <summary>
/// Analytics service for business intelligence
/// </summary>
public class AnalyticsService
{
    private readonly NgibridDbContext _db;

    public AnalyticsService(NgibridDbContext db) { _db = db; }

    /// <summary>
    /// Get delivery volume by period
    /// </summary>
    public async Task<Dictionary<string, int>> GetDeliveryVolumeAsync(string periodType = "DAILY", int days = 30)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        var data = await _db.Orders
            .Where(o => o.CreatedAt >= since)
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();
        return data.ToDictionary(d => d.Date.ToString("yyyy-MM-dd"), d => d.Count);
    }

    /// <summary>
    /// Get revenue summary
    /// </summary>
    public async Task<RevenueSummary> GetRevenueSummaryAsync(int days = 30)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        var orders = await _db.Orders
            .Where(o => o.CreatedAt >= since)
            .ToListAsync();
        return new RevenueSummary
        {
            TotalRevenue = orders.Sum(o => o.TotalAmount),
            TotalOrders = orders.Count,
            AvgOrderValue = orders.Count > 0 ? orders.Average(o => o.TotalAmount) : 0,
            DeliveredCount = orders.Count(o => o.Status == "DELIVERED")
        };
    }

    /// <summary>
    /// Get SLA compliance
    /// </summary>
    public async Task<double> GetSlaComplianceAsync(int days = 30)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        var delivered = await _db.Orders
            .Where(o => o.Status == "DELIVERED" && o.ActualDeliveryDate != null && o.CreatedAt >= since)
            .ToListAsync();
        if (delivered.Count == 0) return 100;
        var onTime = delivered.Count(o => 
            o.ActualDeliveryDate <= o.EstimatedDeliveryDate?.AddHours(2));
        return Math.Round((double)onTime / delivered.Count * 100, 1);
    }

    /// <summary>
    /// Live order-status breakdown for the dashboard pie chart.
    /// </summary>
    public async Task<Dictionary<string, int>> GetStatusBreakdownAsync(int days = 30)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        var data = await _db.Orders
            .Where(o => o.CreatedAt >= since && !o.IsDeleted)
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        return data.OrderByDescending(d => d.Count).ToDictionary(d => d.Status, d => d.Count);
    }

    /// <summary>
    /// Recent operational events, merged from status history, pickups, and support tickets
    /// so the activity feed reflects what actually happened.
    /// </summary>
    public async Task<List<ActivityEntry>> GetRecentActivityAsync(int take = 12)
    {
        var statusEvents = await _db.OrderStatusHistories
            .Include(h => h.Order)
            .OrderByDescending(h => h.CreatedAt)
            .Take(take)
            .Select(h => new ActivityEntry
            {
                Timestamp = h.CreatedAt,
                Description = h.Notes ?? $"Status pesanan menjadi {h.Status}",
                Status = h.Status,
                Details = h.Order != null ? h.Order.TrackingNumber ?? h.Order.OrderNumber : $"Order #{h.OrderId}"
            })
            .ToListAsync();

        var pickups = await _db.PickupRequests
            .Include(p => p.Customer)
            .OrderByDescending(p => p.CreatedAt)
            .Take(take / 3 + 1)
            .Select(p => new ActivityEntry
            {
                Timestamp = p.CreatedAt,
                Description = "Permintaan pickup baru",
                Status = p.Status,
                Details = p.Customer != null ? p.Customer.FullName : p.RequestNumber
            })
            .ToListAsync();

        var tickets = await _db.SupportTickets
            .OrderByDescending(t => t.CreatedAt)
            .Take(take / 3 + 1)
            .Select(t => new ActivityEntry
            {
                Timestamp = t.CreatedAt,
                Description = $"Tiket support: {t.Subject}",
                Status = t.Status,
                Details = t.TicketNumber
            })
            .ToListAsync();

        return statusEvents.Concat(pickups).Concat(tickets)
            .OrderByDescending(a => a.Timestamp)
            .Take(take)
            .ToList();
    }

    /// <summary>Operational counters for the dashboard tiles.</summary>
    public async Task<OperationalSnapshot> GetOperationalSnapshotAsync()
    {
        var today = DateTime.UtcNow.Date;
        return new OperationalSnapshot
        {
            OrdersToday = await _db.Orders.CountAsync(o => o.CreatedAt >= today && !o.IsDeleted),
            InTransit = await _db.Orders.CountAsync(o => o.Status == "IN_TRANSIT" || o.Status == "OUT_FOR_DELIVERY"),
            PendingPickups = await _db.PickupRequests.CountAsync(p => p.Status == "REQUESTED" && !p.IsDeleted),
            OpenTickets = await _db.SupportTickets.CountAsync(t => t.Status == "OPEN"),
            CouriersOnDelivery = await _db.CourierProfiles.CountAsync(c => c.Status == "ON_DELIVERY"),
            CouriersAvailable = await _db.CourierProfiles.CountAsync(c => c.Status == "AVAILABLE"),
            WarehouseUtilization = await GetWarehouseUtilizationAsync()
        };
    }

    private async Task<double> GetWarehouseUtilizationAsync()
    {
        var warehouses = await _db.Warehouses.Where(w => !w.IsDeleted).ToListAsync();
        var total = warehouses.Sum(w => w.TotalCapacityM3);
        if (total <= 0) return 0;
        return Math.Round(warehouses.Sum(w => w.UsedCapacityM3) / total * 100, 1);
    }

    /// <summary>Total carbon emission and offset cost across recent orders (green logistics reporting).</summary>
    public async Task<(double TotalGram, double AvgGramPerOrder, int EcoOrders)> GetEmissionSummaryAsync(int days = 30)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        var orders = await _db.Orders
            .Where(o => o.CreatedAt >= since && !o.IsDeleted && o.CarbonEmissionGram != null)
            .Select(o => new { o.CarbonEmissionGram, o.IsEcoDelivery })
            .ToListAsync();

        if (orders.Count == 0) return (0, 0, 0);
        var total = orders.Sum(o => o.CarbonEmissionGram ?? 0);
        return (Math.Round(total, 1), Math.Round(total / orders.Count, 1), orders.Count(o => o.IsEcoDelivery));
    }

    /// <summary>
    /// Get courier performance
    /// </summary>
    public async Task<List<CourierPerformance>> GetCourierPerformanceAsync()
    {
        // Counted from the order table rather than read off CourierProfile.TotalDeliveries:
        // those denormalised columns are only ever right if every code path that moves an order
        // remembers to increment them, and nothing does — so the panel read 0 for every courier.
        var stats = await _db.Orders
            .Where(o => o.AssignedCourierId != null && !o.IsDeleted)
            .GroupBy(o => o.AssignedCourierId!.Value)
            .Select(g => new
            {
                UserId = g.Key,
                Total = g.Count(),
                Delivered = g.Count(o => o.Status == "DELIVERED"),
            })
            .ToListAsync();

        var profiles = await _db.CourierProfiles
            .Where(c => !c.IsDeleted)
            .Select(c => new { c.Id, c.UserId, Name = c.User!.FullName, c.Rating })
            .ToListAsync();

        return profiles
            .Select(c =>
            {
                var s = stats.FirstOrDefault(x => x.UserId == c.UserId);
                var total = s?.Total ?? 0;
                var ok = s?.Delivered ?? 0;
                return new CourierPerformance
                {
                    CourierId = c.Id,
                    Name = c.Name,
                    TotalDeliveries = total,
                    SuccessfulDeliveries = ok,
                    Rating = c.Rating,
                    SuccessRate = total > 0 ? Math.Round((double)ok / total * 100, 1) : 0,
                };
            })
            .OrderByDescending(c => c.SuccessRate)
            .ThenByDescending(c => c.TotalDeliveries)
            .Take(20)
            .ToList();
    }
}

public class RevenueSummary
{
    public decimal TotalRevenue { get; set; }
    public int TotalOrders { get; set; }
    public decimal AvgOrderValue { get; set; }
    public int DeliveredCount { get; set; }
}

public class CourierPerformance
{
    public long CourierId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalDeliveries { get; set; }
    public int SuccessfulDeliveries { get; set; }
    public double Rating { get; set; }
    public double SuccessRate { get; set; }
}

public class ActivityEntry
{
    public DateTime Timestamp { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;

    /// <summary>Human-friendly relative time, e.g. "5 menit lalu".</summary>
    public string RelativeTime
    {
        get
        {
            var delta = DateTime.UtcNow - Timestamp;
            if (delta.TotalMinutes < 1) return "Baru saja";
            if (delta.TotalMinutes < 60) return $"{(int)delta.TotalMinutes} menit lalu";
            if (delta.TotalHours < 24) return $"{(int)delta.TotalHours} jam lalu";
            if (delta.TotalDays < 30) return $"{(int)delta.TotalDays} hari lalu";
            return Timestamp.ToString("dd MMM yyyy");
        }
    }

    public string BadgeClass => Status switch
    {
        "DELIVERED" or "RESOLVED" or "CLOSED" => "badge-success",
        "IN_TRANSIT" or "OUT_FOR_DELIVERY" or "ASSIGNED" => "badge-info",
        "PICKED_UP" or "REQUESTED" or "OPEN" => "badge-warning",
        "FAILED" or "RETURNED" or "CANCELLED" => "badge-danger",
        _ => "badge-default"
    };
}

public class OperationalSnapshot
{
    public int OrdersToday { get; set; }
    public int InTransit { get; set; }
    public int PendingPickups { get; set; }
    public int OpenTickets { get; set; }
    public int CouriersOnDelivery { get; set; }
    public int CouriersAvailable { get; set; }
    public double WarehouseUtilization { get; set; }
}
