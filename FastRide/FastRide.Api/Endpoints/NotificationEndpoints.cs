using FastRide.Api.Security;
using FastRide.Data;
using FastRide.Shared.Common;
using FastRide.Shared.DTOs;
using Microsoft.EntityFrameworkCore;

namespace FastRide.Api.Endpoints;

/// <summary>In-app notification inbox.</summary>
public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/notifications").WithTags("Notifications").RequireAuthorization();

        group.MapGet("/{userId:guid}", List);
        group.MapGet("/{userId:guid}/unread-count", UnreadCount);
        group.MapPut("/{userId:guid}/read-all", MarkAllRead);
        group.MapPut("/{id:guid}/read", MarkRead);

        return api;
    }

    private static async Task<IResult> List(
        Guid userId, int? page, int? limit, bool? unreadOnly,
        HttpContext http, FastRideDbContext db, CancellationToken ct)
    {
        if (!http.User.CanAccess(userId)) return Forbidden();

        var paging = PageRequest.From(page, limit, 30);
        var notifications = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);

        if (unreadOnly == true) notifications = notifications.Where(n => !n.IsRead);

        var total = await notifications.CountAsync(ct);
        var data = await notifications
            .OrderByDescending(n => n.CreatedAt)
            .Skip(paging.Skip)
            .Take(paging.Limit)
            .Select(n => new NotificationResponse(n.Id, n.Title, n.Message, n.Type, n.IsRead, n.CreatedAt, n.OrderId))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<NotificationResponse>
        {
            Total = total,
            Page = paging.Page,
            Limit = paging.Limit,
            Data = data
        });
    }

    private static async Task<IResult> UnreadCount(
        Guid userId, HttpContext http, FastRideDbContext db, CancellationToken ct)
    {
        if (!http.User.CanAccess(userId)) return Forbidden();

        var unread = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);
        return Results.Ok(new UnreadCountResponse(unread));
    }

    private static async Task<IResult> MarkAllRead(
        Guid userId, HttpContext http, FastRideDbContext db, CancellationToken ct)
    {
        if (!http.User.CanAccess(userId)) return Forbidden();

        var now = DateTime.UtcNow;
        var affected = await db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, now), ct);

        return Results.Ok(new MessageResponse($"{affected} notifikasi ditandai sudah dibaca."));
    }

    private static async Task<IResult> MarkRead(Guid id, HttpContext http, FastRideDbContext db, CancellationToken ct)
    {
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id, ct);
        if (notification is null) return Results.NotFound(new ApiError("NotFound", "Notifikasi tidak ditemukan."));
        if (!http.User.CanAccess(notification.UserId)) return Forbidden();

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new NotificationResponse(
            notification.Id, notification.Title, notification.Message,
            notification.Type, notification.IsRead, notification.CreatedAt, notification.OrderId));
    }

    private static IResult Forbidden() =>
        Results.Json(new ApiError("Forbidden", "Kamu tidak berhak membaca notifikasi pengguna lain."), statusCode: StatusCodes.Status403Forbidden);
}
