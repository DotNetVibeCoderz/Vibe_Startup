using FastRide.Api.Security;
using FastRide.Api.Services;
using FastRide.Data;
using FastRide.Shared.Common;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FastRide.Api.Endpoints;

/// <summary>Driver directory, GPS tracking, and everything the driver app calls.</summary>
public static class DriverEndpoints
{
    public static IEndpointRouteBuilder MapDriverEndpoints(this IEndpointRouteBuilder api)
    {
        var directory = api.MapGroup("/drivers").WithTags("Drivers").RequireAuthorization();

        directory.MapGet("/", ListDrivers).RequireAuthorization(Policies.AdminOnly);
        directory.MapGet("/nearby", NearbyDrivers).WithSummary("Online drivers around a point");

        var mobile = api.MapGroup("/mobile/driver/{userId:guid}")
            .WithTags("Mobile · Driver")
            .RequireAuthorization();

        mobile.MapGet("/home", DriverHome);
        mobile.MapGet("/earnings", DriverEarnings);
        mobile.MapGet("/orders/available", AvailableOrders);
        mobile.MapPut("/location", UpdateLocation);
        mobile.MapPut("/status", SetStatus);
        mobile.MapPut("/toggle-online", ToggleOnline);
        mobile.MapPut("/accept-order", AcceptOrder);
        mobile.MapPut("/arrive-order", ArriveOrder);
        mobile.MapPut("/start-order", StartOrder);
        mobile.MapPut("/complete-order", CompleteOrder);

        return api;
    }

    private static async Task<IResult> ListDrivers(
        int? page, int? limit, string? search, DriverStatus? status, bool? verified,
        FastRideDbContext db, CancellationToken ct)
    {
        var paging = PageRequest.From(page, limit, 20);

        var drivers = db.Users.AsNoTracking().Where(u => u.Role == UserRole.Driver && u.DriverProfile != null);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            drivers = drivers.Where(u =>
                u.FullName.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term) ||
                u.DriverProfile!.VehiclePlate.ToLower().Contains(term));
        }

        if (status is { } driverStatus) drivers = drivers.Where(u => u.DriverProfile!.Status == driverStatus);
        if (verified is { } isVerified) drivers = drivers.Where(u => u.DriverProfile!.IsDocumentVerified == isVerified);

        var total = await drivers.CountAsync(ct);
        var data = await drivers
            .OrderByDescending(u => u.DriverProfile!.Rating)
            .Skip(paging.Skip)
            .Take(paging.Limit)
            .Select(u => new DriverListItem(
                u.Id, u.FullName, u.Email, u.PhoneNumber, u.PhotoUrl,
                u.DriverProfile!.Status, u.DriverProfile.Rating, u.DriverProfile.TotalTrips,
                u.DriverProfile.TotalEarnings, u.DriverProfile.VehicleType, u.DriverProfile.VehiclePlate,
                u.DriverProfile.VehicleCategory, u.DriverProfile.CurrentLatitude, u.DriverProfile.CurrentLongitude,
                u.DriverProfile.LocationUpdatedAt, u.DriverProfile.IsDocumentVerified, u.IsActive))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<DriverListItem>
        {
            Total = total,
            Page = paging.Page,
            Limit = paging.Limit,
            Data = data
        });
    }

    private static async Task<IResult> NearbyDrivers(
        double lat, double lng, double? radiusKm, VehicleCategory? category, int? limit,
        DispatchService dispatch, CancellationToken ct)
    {
        var drivers = await dispatch.FindNearbyDriversAsync(
            lat, lng,
            Math.Clamp(radiusKm ?? 5, 0.5, 50),
            category,
            Math.Clamp(limit ?? 20, 1, 100),
            ct);

        return Results.Ok(drivers);
    }

    private static async Task<IResult> DriverHome(
        Guid userId, HttpContext http, FastRideDbContext db,
        DispatchService dispatch, OrderService orders, CancellationToken ct)
    {
        if (!http.User.CanAccess(userId)) return Forbidden();

        var driver = await db.Users
            .AsNoTracking()
            .Include(u => u.DriverProfile)
            .FirstOrDefaultAsync(u => u.Id == userId && u.Role == UserRole.Driver, ct);

        if (driver?.DriverProfile is null) return NotFound("Driver tidak ditemukan.");

        var today = DateTime.UtcNow.Date;

        // One aggregate query instead of a count and a sum.
        var todayTotals = await db.Orders
            .AsNoTracking()
            .Where(o => o.DriverId == userId && o.Status == OrderStatus.Completed && o.CompletedAt >= today)
            .GroupBy(_ => 1)
            .Select(g => new { Trips = g.Count(), Earnings = g.Sum(o => o.FinalFare) })
            .FirstOrDefaultAsync(ct);

        var activeTripId = await db.Orders
            .AsNoTracking()
            .Where(o => o.DriverId == userId &&
                        (o.Status == OrderStatus.Accepted || o.Status == OrderStatus.DriverArrived || o.Status == OrderStatus.Started))
            .Select(o => (Guid?)o.Id)
            .FirstOrDefaultAsync(ct);

        var activeTrip = activeTripId is null
            ? null
            : (await orders.GetDetailAsync(activeTripId.Value, ct)).Value;

        // A driver on a trip is not shown new offers.
        var incoming = driver.DriverProfile.Status == DriverStatus.Online && activeTrip is null
            ? await dispatch.FindOpenOrdersForDriverAsync(driver.DriverProfile, 12, 8, ct)
            : [];

        var recent = await db.Orders
            .AsNoTracking()
            .Where(o => o.DriverId == userId && o.Status == OrderStatus.Completed)
            .OrderByDescending(o => o.CompletedAt)
            .Take(10)
            .Select(o => new RecentTripItem(
                o.Id, o.Code, o.Rider.FullName, o.PickupAddress, o.DropoffAddress,
                o.FinalFare, o.Status, o.CreatedAt, o.DriverRating))
            .ToListAsync(ct);

        var unread = await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

        return Results.Ok(new DriverHomeResponse(
            userId, driver.FullName, driver.PhotoUrl,
            driver.DriverProfile.Status is DriverStatus.Online or DriverStatus.OnTrip,
            driver.DriverProfile.IsDocumentVerified,
            todayTotals?.Earnings ?? 0m, todayTotals?.Trips ?? 0,
            driver.DriverProfile.Rating, unread,
            activeTrip, incoming, recent));
    }

    private static async Task<IResult> DriverEarnings(
        Guid userId, string? period, HttpContext http, FastRideDbContext db, CancellationToken ct)
    {
        if (!http.User.CanAccess(userId)) return Forbidden();

        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekStart = today.AddDays(-7);
        var monthStart = today.AddDays(-30);

        // Pull the 30-day window once, then slice it in memory — three separate aggregate
        // queries over the same rows was the slow part of the old implementation.
        var completed = await db.Orders
            .AsNoTracking()
            .Where(o => o.DriverId == userId && o.Status == OrderStatus.Completed && o.CompletedAt >= monthStart)
            .Select(o => new { o.FinalFare, CompletedAt = o.CompletedAt!.Value })
            .ToListAsync(ct);

        var lifetime = await db.DriverProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.TotalEarnings)
            .FirstOrDefaultAsync(ct);

        var todayTrips = completed.Count(o => o.CompletedAt >= today);
        var weekTrips = completed.Count(o => o.CompletedAt >= weekStart);

        var daily = completed
            .GroupBy(o => o.CompletedAt.Date)
            .Select(g => new DailyEarningItem(g.Key, g.Sum(o => o.FinalFare), g.Count()))
            .OrderByDescending(d => d.Date)
            .ToList();

        return Results.Ok(new DriverEarningsResponse(
            completed.Where(o => o.CompletedAt >= today).Sum(o => o.FinalFare),
            completed.Where(o => o.CompletedAt >= weekStart).Sum(o => o.FinalFare),
            completed.Sum(o => o.FinalFare),
            todayTrips, weekTrips, completed.Count,
            completed.Count == 0 ? 0m : Math.Round(completed.Sum(o => o.FinalFare) / completed.Count, 0),
            lifetime,
            daily));
    }

    private static async Task<IResult> AvailableOrders(
        Guid userId, double? radiusKm, int? limit, HttpContext http,
        FastRideDbContext db, DispatchService dispatch, CancellationToken ct)
    {
        if (!http.User.CanAccess(userId)) return Forbidden();

        var profile = await db.DriverProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null) return NotFound("Profil driver tidak ditemukan.");

        var offers = await dispatch.FindOpenOrdersForDriverAsync(
            profile, Math.Clamp(radiusKm ?? 12, 1, 50), Math.Clamp(limit ?? 10, 1, 50), ct);

        return Results.Ok(offers);
    }

    /// <summary>GPS ping from the driver app. Also mirrored onto any trip in progress.</summary>
    private static async Task<IResult> UpdateLocation(
        Guid userId, UpdateLocationRequest request, HttpContext http, FastRideDbContext db, CancellationToken ct)
    {
        if (!http.User.CanAccess(userId)) return Forbidden();

        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
            return Results.BadRequest(new ApiError("Invalid", "Koordinat di luar jangkauan."));

        var now = DateTime.UtcNow;
        var updated = await db.DriverProfiles
            .Where(p => p.UserId == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.CurrentLatitude, request.Latitude)
                .SetProperty(p => p.CurrentLongitude, request.Longitude)
                .SetProperty(p => p.Heading, request.Heading)
                .SetProperty(p => p.LocationUpdatedAt, now), ct);

        if (updated == 0) return NotFound("Profil driver tidak ditemukan.");

        await db.Orders
            .Where(o => o.DriverId == userId &&
                        (o.Status == OrderStatus.Accepted || o.Status == OrderStatus.DriverArrived || o.Status == OrderStatus.Started))
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.DriverLatitude, request.Latitude)
                .SetProperty(o => o.DriverLongitude, request.Longitude), ct);

        return Results.Ok(new MessageResponse("Lokasi diperbarui."));
    }

    private static async Task<IResult> SetStatus(
        Guid userId, SetDriverStatusRequest request, HttpContext http, FastRideDbContext db, CancellationToken ct)
    {
        if (!http.User.CanAccess(userId)) return Forbidden();

        var profile = await db.DriverProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null) return NotFound("Profil driver tidak ditemukan.");

        if (request.Status == DriverStatus.Online && !profile.IsDocumentVerified)
            return Results.Json(new ApiError("Forbidden", "Dokumen kamu belum diverifikasi admin."), statusCode: StatusCodes.Status403Forbidden);

        // Going offline mid-trip would strand the rider.
        if (profile.Status == DriverStatus.OnTrip && request.Status != DriverStatus.OnTrip)
        {
            var onTrip = await db.Orders.AnyAsync(
                o => o.DriverId == userId &&
                     (o.Status == OrderStatus.Accepted || o.Status == OrderStatus.DriverArrived || o.Status == OrderStatus.Started),
                ct);

            if (onTrip) return Results.Conflict(new ApiError("Conflict", "Selesaikan perjalanan yang sedang berjalan dulu."));
        }

        profile.Status = request.Status;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new DriverStatusResponse(profile.Status, profile.Status is DriverStatus.Online or DriverStatus.OnTrip));
    }

    private static async Task<IResult> ToggleOnline(
        Guid userId, HttpContext http, FastRideDbContext db, CancellationToken ct)
    {
        if (!http.User.CanAccess(userId)) return Forbidden();

        var profile = await db.DriverProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null) return NotFound("Profil driver tidak ditemukan.");

        var target = profile.Status == DriverStatus.Online ? DriverStatus.Offline : DriverStatus.Online;
        return await SetStatus(userId, new SetDriverStatusRequest(target), http, db, ct);
    }

    private static async Task<IResult> AcceptOrder(
        Guid userId, AcceptOrderRequest request, HttpContext http, OrderService orders, CancellationToken ct)
    {
        if (!http.User.CanAccess(userId)) return Forbidden();

        var result = await orders.AcceptAsync(request.OrderId, userId, ct);
        return result.IsSuccess
            ? Results.Ok(new { request.OrderId, Status = result.Value })
            : result.ToHttpResult();
    }

    private static Task<IResult> ArriveOrder(
        Guid userId, AcceptOrderRequest request, HttpContext http, OrderService orders, CancellationToken ct) =>
        Advance(userId, request, http, orders, OrderStatus.DriverArrived, ct);

    private static Task<IResult> StartOrder(
        Guid userId, AcceptOrderRequest request, HttpContext http, OrderService orders, CancellationToken ct) =>
        Advance(userId, request, http, orders, OrderStatus.Started, ct);

    private static Task<IResult> CompleteOrder(
        Guid userId, AcceptOrderRequest request, HttpContext http, OrderService orders, CancellationToken ct) =>
        Advance(userId, request, http, orders, OrderStatus.Completed, ct);

    private static async Task<IResult> Advance(
        Guid userId, AcceptOrderRequest request, HttpContext http,
        OrderService orders, OrderStatus target, CancellationToken ct)
    {
        if (!http.User.CanAccess(userId)) return Forbidden();

        var result = await orders.AdvanceAsync(request.OrderId, userId, target, ct);
        return result.ToHttpResult();
    }

    private static IResult NotFound(string message) => Results.NotFound(new ApiError("NotFound", message));
    private static IResult Forbidden() =>
        Results.Json(new ApiError("Forbidden", "Kamu tidak berhak mengakses data driver lain."), statusCode: StatusCodes.Status403Forbidden);
}
