using System.Security.Claims;
using Comblang.Services.Chat;
using Microsoft.AspNetCore.SignalR;

namespace Comblang.Hubs;

/// <summary>
/// SignalR hub for real-time direct chat messaging between matched users.
/// Requires authentication — extracts user identity from JWT/cookie claims.
/// </summary>
public class ChatHub : Hub
{
    private readonly ChatService _chatService;
    private readonly SignalRConnectionManager _connectionManager;
    private readonly IHubContext<NotificationHub> _notificationHubContext;

    public ChatHub(
        ChatService chatService,
        SignalRConnectionManager connectionManager,
        IHubContext<NotificationHub> notificationHubContext)
    {
        _chatService = chatService;
        _connectionManager = connectionManager;
        _notificationHubContext = notificationHubContext;
    }

    /// <summary>
    /// Called when a client connects. Registers the connection, extracts the user ID
    /// from claims, and adds the connection to a user-scoped group for targeted messaging.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        Guid? userId = GetUserIdFromContext();
        if (userId.HasValue)
        {
            _connectionManager.AddConnection(userId.Value, Context.ConnectionId);

            // Join a user-specific group so messages can be sent via Groups
            string groupName = $"chat_{userId.Value}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects. Removes the connection from tracking.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _connectionManager.RemoveConnection(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Sends a direct message to a matched user.
    /// Persists the message to the database, then broadcasts to the receiver's group
    /// and sends a notification via NotificationHub.
    /// </summary>
    public async Task SendMessage(Guid receiverId, string content, string messageType = "Text", string? mediaUrl = null, string? mediaName = null)
    {
        Guid? senderId = GetUserIdFromContext();
        if (!senderId.HasValue)
        {
            await Clients.Caller.SendAsync("MessageSent", new { success = false, error = "Unauthenticated" });
            return;
        }

        try
        {
            // Persist to database via ChatService
            Models.Message message = await _chatService.SendMessageAsync(
                senderId.Value,
                receiverId,
                content,
                messageType,
                mediaUrl);

            object messagePayload = new
            {
                id = message.Id,
                senderId = message.SenderId,
                receiverId = message.ReceiverId,
                content = message.Content,
                messageType = message.MessageType,
                mediaUrl = message.MediaUrl,
                mediaName = mediaName,
                sentAt = message.SentAt,
                isRead = message.IsRead
            };

            // Broadcast to the receiver's group (all their devices)
            string receiverGroup = $"chat_{receiverId}";
            await Clients.Group(receiverGroup).SendAsync("ReceiveMessage", messagePayload);

            // Also notify via NotificationHub
            await _notificationHubContext.Clients.Group($"notification_{receiverId}")
                .SendAsync("NewMessage", new
                {
                    senderId = senderId.Value,
                    content = content.Length > 50 ? string.Concat(content.AsSpan(0, 50), "...") : content,
                    messageType = messageType,
                    sentAt = message.SentAt
                });

            // Confirm to the sender
            await Clients.Caller.SendAsync("MessageSent", new { success = true, messageId = message.Id });
        }
        catch (InvalidOperationException ex)
        {
            await Clients.Caller.SendAsync("MessageSent", new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Notifies the receiver that the sender is currently typing.
    /// </summary>
    public async Task Typing(Guid receiverId)
    {
        Guid? senderId = GetUserIdFromContext();
        if (!senderId.HasValue) return;

        string receiverGroup = $"chat_{receiverId}";
        await Clients.Group(receiverGroup).SendAsync("UserTyping", new
        {
            senderId = senderId.Value,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Marks all messages from the given sender to the current user as read.
    /// Then notifies the sender that their messages have been read.
    /// </summary>
    public async Task MarkRead(Guid senderId)
    {
        Guid? readerId = GetUserIdFromContext();
        if (!readerId.HasValue) return;

        try
        {
            await _chatService.MarkMessagesAsReadAsync(readerId.Value, senderId);

            // Notify the original sender that their messages were read
            string senderGroup = $"chat_{senderId}";
            await Clients.Group(senderGroup).SendAsync("MessagesRead", new
            {
                readBy = readerId.Value,
                timestamp = DateTime.UtcNow
            });

            await Clients.Caller.SendAsync("MarkReadComplete", new { success = true });
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("MarkReadComplete", new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Extracts the authenticated user's Guid from the JWT/cookie claim.
    /// Uses ClaimTypes.NameIdentifier as the primary lookup.
    /// </summary>
    private Guid? GetUserIdFromContext()
    {
        System.Security.Claims.Claim? userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirst("sub")
            ?? Context.User?.FindFirst("userId");

        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out Guid userId))
            return userId;

        return null;
    }
}
