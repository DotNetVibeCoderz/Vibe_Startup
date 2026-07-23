namespace VibeWallet.Services;

/// <summary>
/// Implementation of notification service
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    public async Task SendTransactionNotificationAsync(Guid userId, string title, string message)
    {
        // In real app: push notification, email, in-app notification
        _logger.LogInformation("Transaction Notification -> User:{UserId} Title:{Title} Message:{Message}",
            userId, title, message);
        await Task.CompletedTask;
    }

    public async Task SendPromoNotificationAsync(Guid userId, string title, string message)
    {
        _logger.LogInformation("Promo Notification -> User:{UserId} Title:{Title} Message:{Message}",
            userId, title, message);
        await Task.CompletedTask;
    }

    public async Task SendSecurityAlertAsync(Guid userId, string title, string message)
    {
        _logger.LogWarning("Security Alert -> User:{UserId} Title:{Title} Message:{Message}",
            userId, title, message);
        await Task.CompletedTask;
    }
}
