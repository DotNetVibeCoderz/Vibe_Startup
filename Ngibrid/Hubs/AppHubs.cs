using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Ngibrid.Services;

namespace Ngibrid.Hubs;

/// <summary>
/// Real-time tracking hub for GPS updates
/// </summary>
[Authorize]
public class TrackingHub : Hub
{
    private readonly TrackingService _tracking;
    private readonly ILogger<TrackingHub> _logger;

    public TrackingHub(TrackingService tracking, ILogger<TrackingHub> logger)
    {
        _tracking = tracking;
        _logger = logger;
    }

    /// <summary>
    /// Join a tracking room for a specific order.
    /// Groups are keyed by order id so joins and broadcasts land in the same room — clients that
    /// only know the tracking number are resolved to the order id here.
    /// </summary>
    public async Task JoinTracking(string trackingNumber)
    {
        var orderId = await _tracking.ResolveOrderIdAsync(trackingNumber);
        if (orderId is null)
        {
            _logger.LogWarning("Client {Id} tried to track unknown number {Tracking}",
                Context.ConnectionId, trackingNumber);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(orderId.Value));
        _logger.LogInformation("Client {Id} joined tracking for order {OrderId}", Context.ConnectionId, orderId);
    }

    /// <summary>
    /// Leave tracking room
    /// </summary>
    public async Task LeaveTracking(string trackingNumber)
    {
        var orderId = await _tracking.ResolveOrderIdAsync(trackingNumber);
        if (orderId is null) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(orderId.Value));
    }

    internal static string GroupName(long orderId) => $"tracking-{orderId}";

    /// <summary>
    /// Send GPS position update (called by courier app)
    /// </summary>
    public async Task SendPosition(long orderId, double latitude, double longitude, 
        double? speed = null, string? description = null)
    {
        var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var courierId = userId != null ? long.Parse(userId) : (long?)null;
        
        var point = await _tracking.AddTrackingPointAsync(orderId, latitude, longitude,
            speed, null, description, courierId);

        await Clients.Group(GroupName(orderId)).SendAsync("PositionUpdated", new
        {
            orderId,
            latitude,
            longitude,
            speed,
            description,
            timestamp = point.Timestamp
        });
    }
}

/// <summary>
/// Chat hub for real-time messaging with Mas Supri
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly ChatBotService _chatBot;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(ChatBotService chatBot, ILogger<ChatHub> logger)
    {
        _chatBot = chatBot;
        _logger = logger;
    }

    /// <summary>
    /// Join a chat session
    /// </summary>
    public async Task JoinSession(long sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat-{sessionId}");
    }

    /// <summary>
    /// Send a message to the chatbot
    /// </summary>
    public async Task SendMessage(long sessionId, string message, string? attachmentsJson = null)
    {
        var userId = GetUserId();
        _logger.LogInformation("Chat message from user {UserId} in session {Session}", userId, sessionId);

        // Acknowledge receipt
        await Clients.Caller.SendAsync("MessageSent", new { role = "user", content = message });

        // Get AI response
        var response = await _chatBot.SendMessageAsync(sessionId, message, attachmentsJson);

        // Send response to all in session
        await Clients.Group($"chat-{sessionId}").SendAsync("MessageReceived", new
        {
            role = "assistant",
            content = response.Content,
            messageId = response.Id,
            createdAt = response.CreatedAt
        });
    }

    /// <summary>
    /// Typing indicator
    /// </summary>
    public async Task Typing(long sessionId)
    {
        await Clients.OthersInGroup($"chat-{sessionId}").SendAsync("UserTyping", GetUserId());
    }

    private long GetUserId()
    {
        var claim = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim != null ? long.Parse(claim.Value) : 0;
    }
}

/// <summary>
/// Notification hub for real-time notifications
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly NotificationService _notificationService;

    public NotificationHub(NotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId != null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Get unread notification count
    /// </summary>
    public async Task GetUnreadCount()
    {
        var userId = GetUserId();
        var notifications = await _notificationService.GetUserNotificationsAsync(userId, unreadOnly: true);
        await Clients.Caller.SendAsync("UnreadCount", notifications.Count);
    }

    private long GetUserId()
    {
        var claim = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim != null ? long.Parse(claim.Value) : 0;
    }
}

/// <summary>
/// Courier hub for real-time courier management
/// </summary>
[Authorize]
public class CourierHub : Hub
{
    private readonly CourierService _courierService;
    private readonly GpsSimulatorService _gpsSim;

    public CourierHub(CourierService courierService, GpsSimulatorService gpsSim)
    {
        _courierService = courierService;
        _gpsSim = gpsSim;
    }

    /// <summary>
    /// Courier updates their location
    /// </summary>
    public async Task UpdateLocation(double latitude, double longitude)
    {
        var userId = GetUserId();
        await _courierService.UpdateLocationAsync(userId, latitude, longitude);
        await Clients.Group("dispatchers").SendAsync("CourierLocationUpdated", new
        {
            courierId = userId,
            latitude,
            longitude,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Courier updates their status
    /// </summary>
    public async Task UpdateStatus(string status)
    {
        var userId = GetUserId();
        await _courierService.UpdateStatusAsync(userId, status);
        await Clients.Group("dispatchers").SendAsync("CourierStatusChanged", new
        {
            courierId = userId,
            status,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Join dispatcher monitoring room
    /// </summary>
    public async Task JoinDispatcher()
    {
        if (Context.User?.IsInRole("Admin") == true || Context.User?.IsInRole("Manager") == true)
            await Groups.AddToGroupAsync(Context.ConnectionId, "dispatchers");
    }

    private long GetUserId()
    {
        var claim = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim != null ? long.Parse(claim.Value) : 0;
    }
}
