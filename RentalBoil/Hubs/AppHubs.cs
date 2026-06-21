using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace RentalBoil.Hubs;

/// <summary>
/// SignalR Hub untuk notifikasi real-time
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private static readonly Dictionary<string, string> _userConnections = new();

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            _userConnections[userId] = Context.ConnectionId;
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            _userConnections.Remove(userId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Kirim notifikasi ke user spesifik
    /// </summary>
    public async Task SendNotification(string userId, string title, string message, string type = "info", string? link = null)
    {
        if (_userConnections.TryGetValue(userId, out var connectionId))
        {
            await Clients.Client(connectionId).SendAsync("ReceiveNotification", new
            {
                Title = title,
                Message = message,
                Type = type,
                Link = link,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Broadcast ke semua user
    /// </summary>
    public async Task BroadcastNotification(string title, string message, string type = "info")
    {
        await Clients.All.SendAsync("ReceiveNotification", new
        {
            Title = title,
            Message = message,
            Type = type,
            CreatedAt = DateTime.UtcNow
        });
    }
}

/// <summary>
/// SignalR Hub untuk Chat real-time
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private static readonly Dictionary<string, string> _userConnections = new();

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            _userConnections[userId] = Context.ConnectionId;
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrWhiteSpace(userId))
            _userConnections.Remove(userId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Kirim pesan chat
    /// </summary>
    public async Task SendMessage(string receiverId, string message, int? bookingId = null)
    {
        var senderId = Context.UserIdentifier;
        if (_userConnections.TryGetValue(receiverId, out var receiverConnection))
        {
            await Clients.Client(receiverConnection).SendAsync("ReceiveMessage", new
            {
                SenderId = senderId,
                Message = message,
                BookingId = bookingId,
                SentAt = DateTime.UtcNow
            });
        }

        // Echo back to sender
        await Clients.Caller.SendAsync("MessageSent", new
        {
            ReceiverId = receiverId,
            Message = message,
            BookingId = bookingId,
            SentAt = DateTime.UtcNow
        });
    }
}

/// <summary>
/// SignalR Hub untuk GPS real-time tracking
/// </summary>
[Authorize]
public class GpsHub : Hub
{
    /// <summary>
    /// Subscribe ke GPS tracking untuk kendaraan tertentu
    /// </summary>
    public async Task SubscribeToVehicle(int vehicleId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"vehicle_{vehicleId}");
    }

    /// <summary>
    /// Unsubscribe dari GPS tracking
    /// </summary>
    public async Task UnsubscribeFromVehicle(int vehicleId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"vehicle_{vehicleId}");
    }

    /// <summary>
    /// Kirim update GPS (dipanggil dari server)
    /// </summary>
    public async Task SendGpsUpdate(int vehicleId, double latitude, double longitude, 
        double speed, double heading, string lockStatus, string engineStatus, string motionStatus)
    {
        await Clients.Group($"vehicle_{vehicleId}").SendAsync("GpsUpdate", new
        {
            VehicleId = vehicleId,
            Latitude = latitude,
            Longitude = longitude,
            Speed = speed,
            Heading = heading,
            LockStatus = lockStatus,
            EngineStatus = engineStatus,
            MotionStatus = motionStatus,
            UpdatedAt = DateTime.UtcNow
        });
    }
}
