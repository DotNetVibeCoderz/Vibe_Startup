using FastRide.Api.Security;
using FastRide.Api.Services;
using FastRide.Data;
using FastRide.Shared.Common;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FastRide.Api.Endpoints;

/// <summary>Booking, fare preview, order lookup, tracking and cancellation.</summary>
public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/orders").WithTags("Orders").RequireAuthorization();

        group.MapGet("/", ListOrders)
            .RequireAuthorization(Policies.AdminOnly)
            .WithSummary("Paged, filterable order list for the dashboard");

        group.MapGet("/export.csv", ExportOrders)
            .RequireAuthorization(Policies.AdminOnly)
            .WithSummary("Download the filtered order list as CSV");

        // Declared after /export.csv so the literal route wins over the {id} pattern.
        group.MapGet("/{id:guid}", GetOrder).WithSummary("Full order detail");
        group.MapGet("/{id:guid}/tracking", TrackOrder).WithSummary("Live driver position and ETA");

        group.MapPost("/", CreateOrder).WithSummary("Book a ride");
        group.MapPost("/quote", QuoteFare).WithSummary("Price a trip before booking");
        group.MapPost("/{id:guid}/cancel", CancelOrder).WithSummary("Cancel a trip");

        return api;
    }

    private static async Task<IResult> ListOrders(
        [AsParameters] OrderQuery query, FastRideDbContext db, CancellationToken ct)
    {
        var page = PageRequest.From(query.Page, query.Limit);
        var filtered = BuildQuery(db, query);

        var total = await filtered.CountAsync(ct);
        var data = await filtered
            .OrderByDescending(o => o.CreatedAt)
            .Skip(page.Skip)
            .Take(page.Limit)
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
            Page = page.Page,
            Limit = page.Limit,
            Data = data
        });
    }

    private static async Task<IResult> ExportOrders(
        [AsParameters] OrderQuery query, FastRideDbContext db, CancellationToken ct)
    {
        // Exports are capped: the dashboard offers a date filter for anything larger.
        var rows = await BuildQuery(db, query)
            .OrderByDescending(o => o.CreatedAt)
            .Take(10_000)
            .Select(o => new
            {
                o.Code,
                Rider = o.Rider.FullName,
                Driver = o.Driver != null ? o.Driver.FullName : null,
                o.PickupAddress,
                o.DropoffAddress,
                o.DistanceKm,
                o.VehicleCategory,
                o.PaymentMethod,
                o.Status,
                o.EstimatedFare,
                o.DiscountAmount,
                o.FinalFare,
                o.CreatedAt,
                o.CompletedAt
            })
            .ToListAsync(ct);

        var csv = CsvExporter.Build(rows,
            ("Kode", r => r.Code),
            ("Rider", r => r.Rider),
            ("Driver", r => r.Driver),
            ("Jemput", r => r.PickupAddress),
            ("Tujuan", r => r.DropoffAddress),
            ("Jarak (km)", r => r.DistanceKm),
            ("Kategori", r => r.VehicleCategory),
            ("Pembayaran", r => r.PaymentMethod),
            ("Status", r => r.Status),
            ("Estimasi", r => r.EstimatedFare),
            ("Diskon", r => r.DiscountAmount),
            ("Total", r => r.FinalFare),
            ("Dibuat", r => r.CreatedAt),
            ("Selesai", r => r.CompletedAt));

        return Results.File(csv, "text/csv", $"fastride-orders-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
    }

    private static async Task<IResult> GetOrder(
        Guid id, HttpContext http, OrderService orders, CancellationToken ct)
    {
        var result = await orders.GetDetailAsync(id, ct);
        if (!result.IsSuccess) return result.ToHttpResult();

        // Only the two people on the trip — or an admin — may read it.
        var order = result.Value!;
        var caller = http.User.UserId();
        if (!http.User.IsAdmin() && caller != order.Rider.Id && caller != order.Driver?.Id)
            return Results.Json(new ApiError("Forbidden", "Perjalanan ini bukan milik kamu."), statusCode: StatusCodes.Status403Forbidden);

        return Results.Ok(order);
    }

    private static async Task<IResult> TrackOrder(
        Guid id, HttpContext http, FastRideDbContext db, DispatchService dispatch, CancellationToken ct)
    {
        var parties = await db.Orders
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new { o.RiderId, o.DriverId })
            .FirstOrDefaultAsync(ct);

        if (parties is null) return Results.NotFound(new ApiError("NotFound", "Pesanan tidak ditemukan."));

        var caller = http.User.UserId();
        if (!http.User.IsAdmin() && caller != parties.RiderId && caller != parties.DriverId)
            return Results.Json(new ApiError("Forbidden", "Perjalanan ini bukan milik kamu."), statusCode: StatusCodes.Status403Forbidden);

        var tracking = await dispatch.GetTrackingAsync(id, ct);
        return tracking is null
            ? Results.NotFound(new ApiError("NotFound", "Pesanan tidak ditemukan."))
            : Results.Ok(tracking);
    }

    private static async Task<IResult> CreateOrder(
        CreateOrderRequest request, HttpContext http, OrderService orders, CancellationToken ct)
    {
        // A rider can only book for themselves; admins may book on someone's behalf.
        if (!http.User.CanAccess(request.RiderId))
            return Results.Json(new ApiError("Forbidden", "Kamu hanya bisa memesan untuk akun sendiri."), statusCode: StatusCodes.Status403Forbidden);

        var result = await orders.CreateAsync(request, ct);
        return result.IsSuccess
            ? result.ToCreatedResult($"/api/orders/{result.Value!.Id}")
            : result.ToHttpResult();
    }

    private static async Task<IResult> QuoteFare(
        FareQuoteRequest request, PricingService pricing, CancellationToken ct) =>
        Results.Ok(await pricing.QuoteAsync(request, ct));

    private static async Task<IResult> CancelOrder(
        Guid id, CancelOrderRequest request, HttpContext http, OrderService orders, CancellationToken ct)
    {
        var caller = http.User.UserId();
        if (caller is null) return Results.Unauthorized();

        var result = await orders.CancelAsync(id, caller.Value, http.User.IsAdmin(), request.Reason, ct);
        return result.ToHttpResult();
    }

    /// <summary>Shared filter used by both the list and the CSV export.</summary>
    private static IQueryable<Order> BuildQuery(FastRideDbContext db, OrderQuery query)
    {
        var orders = db.Orders.AsNoTracking();

        if (query.Status is { } status) orders = orders.Where(o => o.Status == status);
        if (query.VehicleCategory is { } category) orders = orders.Where(o => o.VehicleCategory == category);
        if (query.PaymentMethod is { } method) orders = orders.Where(o => o.PaymentMethod == method);
        if (query.RiderId is { } riderId) orders = orders.Where(o => o.RiderId == riderId);
        if (query.DriverId is { } driverId) orders = orders.Where(o => o.DriverId == driverId);
        if (query.From is { } from) orders = orders.Where(o => o.CreatedAt >= from);
        if (query.To is { } to) orders = orders.Where(o => o.CreatedAt <= to);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            orders = orders.Where(o =>
                o.Code.ToLower().Contains(term) ||
                o.Rider.FullName.ToLower().Contains(term) ||
                o.PickupAddress.ToLower().Contains(term) ||
                o.DropoffAddress.ToLower().Contains(term));
        }

        return orders;
    }
}

/// <summary>Query string of the order list, bound as one parameter object.</summary>
public sealed record OrderQuery(
    int? Page, int? Limit,
    OrderStatus? Status, VehicleCategory? VehicleCategory, PaymentMethod? PaymentMethod,
    Guid? RiderId, Guid? DriverId,
    DateTime? From, DateTime? To, string? Search);
