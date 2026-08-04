using FastRide.Api.Security;
using FastRide.Data;
using FastRide.Shared.Common;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FastRide.Api.Endpoints;

/// <summary>Rider directory for the dashboard, plus the rider app's home and trip list.</summary>
public static class RiderEndpoints
{
    public static IEndpointRouteBuilder MapRiderEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/riders", ListRiders)
            .WithTags("Riders")
            .RequireAuthorization(Policies.AdminOnly);

        var mobile = api.MapGroup("/mobile/rider/{userId:guid}")
            .WithTags("Mobile · Rider")
            .RequireAuthorization();

        mobile.MapGet("/home", RiderHome);
        mobile.MapGet("/trips", RiderTrips).WithSummary("Paged trip history");

        return api;
    }

    private static async Task<IResult> ListRiders(
        int? page, int? limit, string? search, FastRideDbContext db, CancellationToken ct)
    {
        var paging = PageRequest.From(page, limit, 20);
        var riders = db.Users.AsNoTracking().Where(u => u.Role == UserRole.Rider);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            riders = riders.Where(u =>
                u.FullName.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term) ||
                u.PhoneNumber.Contains(term));
        }

        var total = await riders.CountAsync(ct);
        var data = await riders
            .OrderByDescending(u => u.CreatedAt)
            .Skip(paging.Skip)
            .Take(paging.Limit)
            .Select(u => new RiderListItem(
                u.Id, u.FullName, u.Email, u.PhoneNumber, u.PhotoUrl,
                u.IsVerified, u.IsActive, u.CreatedAt,
                u.RiderOrders.Count(o => o.Status == OrderStatus.Completed),
                u.RiderOrders.Where(o => o.Status == OrderStatus.Completed).Sum(o => o.FinalFare)))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<RiderListItem>
        {
            Total = total,
            Page = paging.Page,
            Limit = paging.Limit,
            Data = data
        });
    }

    private static async Task<IResult> RiderHome(
        Guid userId, HttpContext http, FastRideDbContext db, CancellationToken ct)
    {
        if (!http.User.CanAccess(userId)) return Forbidden();

        var rider = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId && u.Role == UserRole.Rider)
            .Select(u => new { u.Id, u.FullName, u.PhotoUrl })
            .FirstOrDefaultAsync(ct);

        if (rider is null) return Results.NotFound(new ApiError("NotFound", "Rider tidak ditemukan."));

        var totals = await db.Orders
            .AsNoTracking()
            .Where(o => o.RiderId == userId && o.Status == OrderStatus.Completed)
            .GroupBy(_ => 1)
            .Select(g => new { Trips = g.Count(), Spent = g.Sum(o => o.FinalFare) })
            .FirstOrDefaultAsync(ct);

        var active = await db.Orders
            .AsNoTracking()
            .Where(o => o.RiderId == userId &&
                        (o.Status == OrderStatus.Requested || o.Status == OrderStatus.Accepted ||
                         o.Status == OrderStatus.DriverArrived || o.Status == OrderStatus.Started))
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderListItem(
                o.Id, o.Code, o.RiderId, o.Rider.FullName, o.DriverId,
                o.Driver != null ? o.Driver.FullName : null,
                o.PickupAddress, o.DropoffAddress, o.DistanceKm, o.EstimatedDurationMinutes,
                o.EstimatedFare, o.FinalFare, o.DiscountAmount,
                o.VehicleCategory, o.PaymentMethod, o.Status, o.CreatedAt, o.CompletedAt))
            .FirstOrDefaultAsync(ct);

        var recent = await db.Orders
            .AsNoTracking()
            .Where(o => o.RiderId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .Select(o => new RecentTripItem(
                o.Id, o.Code, o.Driver != null ? o.Driver.FullName : null,
                o.PickupAddress, o.DropoffAddress, o.FinalFare, o.Status, o.CreatedAt, o.DriverRating))
            .ToListAsync(ct);

        var unread = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

        return Results.Ok(new RiderHomeResponse(
            rider.Id, rider.FullName, rider.PhotoUrl,
            totals?.Trips ?? 0, totals?.Spent ?? 0m, unread,
            active, recent));
    }

    /// <summary>
    /// The rider app has always called this route; until now it did not exist and every
    /// "My Trips" screen silently failed.
    /// </summary>
    private static async Task<IResult> RiderTrips(
        Guid userId, int? page, int? limit, OrderStatus? status,
        HttpContext http, FastRideDbContext db, CancellationToken ct)
    {
        if (!http.User.CanAccess(userId)) return Forbidden();

        var paging = PageRequest.From(page, limit, 20);
        var trips = db.Orders.AsNoTracking().Where(o => o.RiderId == userId);

        if (status is { } wanted) trips = trips.Where(o => o.Status == wanted);

        var total = await trips.CountAsync(ct);
        var data = await trips
            .OrderByDescending(o => o.CreatedAt)
            .Skip(paging.Skip)
            .Take(paging.Limit)
            .Select(o => new OrderListItem(
                o.Id, o.Code, o.RiderId, o.Rider.FullName, o.DriverId,
                o.Driver != null ? o.Driver.FullName : null,
                o.PickupAddress, o.DropoffAddress, o.DistanceKm, o.EstimatedDurationMinutes,
                o.EstimatedFare, o.FinalFare, o.DiscountAmount,
                o.VehicleCategory, o.PaymentMethod, o.Status, o.CreatedAt, o.CompletedAt))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<OrderListItem>
        {
            Total = total,
            Page = paging.Page,
            Limit = paging.Limit,
            Data = data
        });
    }

    private static IResult Forbidden() =>
        Results.Json(new ApiError("Forbidden", "Kamu tidak berhak mengakses data rider lain."), statusCode: StatusCodes.Status403Forbidden);
}
