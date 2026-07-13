using Microsoft.AspNetCore.SignalR;
using PCHub.Shared.DTOs;
using PCHub.Shared.Enums;

namespace PCHub.Shared.Services;

/// <summary>
/// SignalR Hub untuk notifikasi real-time ke semua client (Admin Web & WPF Client).
/// Support: broadcast notifikasi, update status PC, update billing timer.
/// </summary>
public class NotificationHub : Hub
{
    // Client methods (dipanggil dari server):
    // - ReceiveNotification(title, message, type)
    // - PcStatusChanged(pcId, newStatus)
    // - BillingTimerUpdate(billingId, elapsedSeconds, cost)
    // - SessionExpiring(minutesRemaining)

    /// <summary>Kirim notifikasi ke semua client</summary>
    public async Task SendNotificationToAll(string title, string message, string type)
    {
        await Clients.All.SendAsync("ReceiveNotification", title, message, type);
    }

    /// <summary>Kirim notifikasi ke user spesifik</summary>
    public async Task SendNotificationToUser(string userId, string title, string message)
    {
        await Clients.User(userId).SendAsync("ReceiveNotification", title, message, "Info");
    }

    /// <summary>Broadcast perubahan status PC</summary>
    public async Task NotifyPcStatusChanged(Guid pcId, string newStatus)
    {
        await Clients.All.SendAsync("PcStatusChanged", pcId.ToString(), newStatus);
    }

    /// <summary>Update timer billing ke client</summary>
    public async Task UpdateBillingTimer(Guid billingId, int elapsedSeconds, decimal cost)
    {
        await Clients.All.SendAsync("BillingTimerUpdate", billingId.ToString(), elapsedSeconds, cost);
    }

    /// <summary>Peringatan sesi akan habis</summary>
    public async Task SessionExpiringWarning(Guid userId, int minutesRemaining)
    {
        await Clients.All.SendAsync("SessionExpiring", userId.ToString(), minutesRemaining);
    }

    public override async Task OnConnectedAsync()
    {
        var userName = Context.User?.Identity?.Name ?? Context.ConnectionId;
        System.Diagnostics.Debug.WriteLine($"✅ SignalR client connected: {userName}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        System.Diagnostics.Debug.WriteLine($"❌ SignalR client disconnected: {Context.ConnectionId}");
        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Service wrapper untuk memudahkan pengiriman notifikasi via SignalR.
/// </summary>
public class SignalRNotificationService
{
    private readonly IHubContext<NotificationHub>? _hubContext;

    public SignalRNotificationService(IHubContext<NotificationHub>? hubContext = null)
    {
        _hubContext = hubContext;
    }

    public async Task BroadcastAsync(string title, string message, NotificationType type)
    {
        if (_hubContext == null) return;
        try
        {
            await _hubContext.Clients.All.SendAsync("ReceiveNotification", title, message, type.ToString());
        }
        catch { /* Hub not available */ }
    }

    public async Task SendToUserAsync(string userId, string title, string message)
    {
        if (_hubContext == null) return;
        try
        {
            await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification", title, message, "Info");
        }
        catch { }
    }

    public async Task NotifyPcStatusAsync(Guid pcId, PcStatus status)
    {
        if (_hubContext == null) return;
        try
        {
            await _hubContext.Clients.All.SendAsync("PcStatusChanged", pcId.ToString(), status.ToString());
        }
        catch { }
    }
}
