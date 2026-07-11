using Microsoft.EntityFrameworkCore;
using SmartDrive.Data;
using SmartDrive.Models.Entities;
using System.Collections.Concurrent;

namespace SmartDrive.Services;

/// <summary>
/// Background service untuk simulasi GPS kendaraan saat latihan
/// Berjalan di thread terpisah, bisa di start/stop
/// </summary>
public class GpsSimulatorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GpsSimulatorService> _logger;
    
    // Track active simulations: BookingId -> CancellationTokenSource
    private static readonly ConcurrentDictionary<int, CancellationTokenSource> _activeSimulations = new();

    // Simulate Jakarta area coordinates
    private static readonly (double lat, double lng)[] _jakartaRoute = new[]
    {
        (-6.2088, 106.8456), // Monas
        (-6.2185, 106.8025), // GBK
        (-6.2250, 106.8200), // Senayan
        (-6.2400, 106.8300), // Blok M
        (-6.2000, 106.8500), // Menteng
        (-6.1750, 106.8270), // Gambir
    };

    public GpsSimulatorService(IServiceProvider serviceProvider, ILogger<GpsSimulatorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Start GPS simulation for a booking
    /// </summary>
    public bool StartSimulation(int bookingId, double startLat, double startLng)
    {
        if (_activeSimulations.ContainsKey(bookingId))
            return false; // Already running

        var cts = new CancellationTokenSource();
        _activeSimulations[bookingId] = cts;

        _ = Task.Run(() => RunSimulationAsync(bookingId, startLat, startLng, cts.Token));

        _logger.LogInformation("GPS simulation started for booking {BookingId}", bookingId);
        return true;
    }

    /// <summary>
    /// Stop GPS simulation for a booking
    /// </summary>
    public bool StopSimulation(int bookingId)
    {
        if (_activeSimulations.TryRemove(bookingId, out var cts))
        {
            cts.Cancel();
            _logger.LogInformation("GPS simulation stopped for booking {BookingId}", bookingId);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Get all active simulation booking IDs
    /// </summary>
    public List<int> GetActiveSimulations()
    {
        return _activeSimulations.Keys.ToList();
    }

    /// <summary>
    /// Check if simulation is running for a booking
    /// </summary>
    public bool IsSimulationRunning(int bookingId)
    {
        return _activeSimulations.ContainsKey(bookingId);
    }

    private async Task RunSimulationAsync(int bookingId, double startLat, double startLng, CancellationToken token)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SmartDriveDbContext>();

            int routeIndex = 0;
            
            while (!token.IsCancellationRequested)
            {
                // Pick a point from the route, with some randomness
                var basePoint = _jakartaRoute[routeIndex % _jakartaRoute.Length];
                var lat = basePoint.lat + (Random.Shared.NextDouble() - 0.5) * 0.005;
                var lng = basePoint.lng + (Random.Shared.NextDouble() - 0.5) * 0.005;
                var speed = 10 + Random.Shared.NextDouble() * 40; // 10-50 km/h
                var heading = Random.Shared.NextDouble() * 360;

                // Save GPS data
                var gpsData = new GpsTrackingData
                {
                    BookingId = bookingId,
                    Latitude = lat,
                    Longitude = lng,
                    Speed = speed,
                    Heading = heading,
                    Timestamp = DateTime.UtcNow,
                    IsSimulated = true,
                    DeviceId = $"SIM-{bookingId}"
                };

                context.GpsTrackingData.Add(gpsData);
                await context.SaveChangesAsync(token);

                routeIndex++;
                await Task.Delay(5000, token); // Update every 5 seconds
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("GPS simulation cancelled for booking {BookingId}", bookingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GPS simulation for booking {BookingId}", bookingId);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Keep the service alive
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}

/// <summary>
/// Service untuk notifikasi
/// </summary>
public class NotificationService
{
    private readonly SmartDriveDbContext _context;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(SmartDriveDbContext context, ILogger<NotificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Kirim notifikasi ke user
    /// </summary>
    public async Task SendNotificationAsync(string userId, string title, string message, 
        Models.Enums.NotificationType type = Models.Enums.NotificationType.GeneralInfo, string? actionUrl = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            ActionUrl = actionUrl,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Notification sent to {UserId}: {Title}", userId, title);
    }

    /// <summary>
    /// Get unread notifications count
    /// </summary>
    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    /// <summary>
    /// Get notifications for user
    /// </summary>
    public async Task<List<Notification>> GetNotificationsAsync(string userId, int take = 20)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    /// <summary>
    /// Mark notification as read
    /// </summary>
    public async Task MarkAsReadAsync(int notificationId)
    {
        var notification = await _context.Notifications.FindAsync(notificationId);
        if (notification != null && !notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Mark all notifications as read
    /// </summary>
    public async Task MarkAllAsReadAsync(string userId)
    {
        var unread = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
    }
}
