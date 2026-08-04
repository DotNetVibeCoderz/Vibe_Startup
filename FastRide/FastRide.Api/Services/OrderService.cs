using FastRide.Data;
using FastRide.Shared.Common;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FastRide.Api.Services;

/// <summary>
/// Order lifecycle rules. Endpoints stay thin; the invariants that must hold no matter which
/// client calls (mobile, admin, simulator) live here.
/// </summary>
public sealed class OrderService(
    FastRideDbContext db,
    PricingService pricing,
    NotificationService notifications,
    PaymentService payments,
    ILogger<OrderService> logger)
{
    /// <summary>Which transitions are legal. Anything not listed is rejected with 409.</summary>
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        [OrderStatus.Requested] = [OrderStatus.Accepted, OrderStatus.Cancelled, OrderStatus.Expired],
        [OrderStatus.Accepted] = [OrderStatus.DriverArrived, OrderStatus.Started, OrderStatus.Cancelled],
        [OrderStatus.DriverArrived] = [OrderStatus.Started, OrderStatus.Cancelled],
        [OrderStatus.Started] = [OrderStatus.Completed, OrderStatus.Cancelled],
        [OrderStatus.Completed] = [],
        [OrderStatus.Cancelled] = [],
        [OrderStatus.Expired] = []
    };

    public static bool CanTransition(OrderStatus from, OrderStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public async Task<Result<CreateOrderResponse>> CreateAsync(CreateOrderRequest request, CancellationToken ct)
    {
        var rider = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == request.RiderId && u.Role == UserRole.Rider)
            .Select(u => new { u.Id, u.IsActive })
            .FirstOrDefaultAsync(ct);

        if (rider is null) return Result<CreateOrderResponse>.NotFound("Rider tidak ditemukan.");
        if (!rider.IsActive) return Result<CreateOrderResponse>.Forbidden("Akun rider sedang tidak aktif.");

        // One trip at a time — a rider with a trip in progress cannot book another.
        var hasOpenOrder = await db.Orders.AnyAsync(
            o => o.RiderId == request.RiderId &&
                 (o.Status == OrderStatus.Requested || o.Status == OrderStatus.Accepted ||
                  o.Status == OrderStatus.DriverArrived || o.Status == OrderStatus.Started),
            ct);

        if (hasOpenOrder)
            return Result<CreateOrderResponse>.Conflict("Masih ada perjalanan yang berjalan. Selesaikan dulu sebelum memesan lagi.");

        var quote = await pricing.QuoteAsync(new FareQuoteRequest(
            request.PickupLatitude, request.PickupLongitude,
            request.DropoffLatitude, request.DropoffLongitude,
            request.VehicleCategory, request.PromoCode, request.Stops), ct);

        var discount = 0m;
        string? appliedPromo = null;

        if (!string.IsNullOrWhiteSpace(request.PromoCode))
        {
            var evaluation = await pricing.EvaluatePromoAsync(
                request.PromoCode, quote.EstimatedFare, request.VehicleCategory, ct);

            // Only charge a redemption once the booking is actually going through.
            if (evaluation is { IsValid: true, Promo: not null } &&
                await pricing.TryConsumePromoAsync(evaluation.Promo.Id, ct))
            {
                discount = evaluation.Discount;
                appliedPromo = evaluation.Promo.Code;
            }
        }

        var fareConfig = await pricing.GetFareConfigAsync(request.VehicleCategory, ct);

        var order = new Order
        {
            Code = await GenerateCodeAsync(ct),
            RiderId = request.RiderId,
            PickupLatitude = request.PickupLatitude,
            PickupLongitude = request.PickupLongitude,
            PickupAddress = request.PickupAddress,
            DropoffLatitude = request.DropoffLatitude,
            DropoffLongitude = request.DropoffLongitude,
            DropoffAddress = request.DropoffAddress,
            DistanceKm = quote.DistanceKm,
            EstimatedDurationMinutes = quote.EstimatedDurationMinutes,
            EstimatedFare = quote.EstimatedFare,
            DiscountAmount = discount,
            FinalFare = quote.EstimatedFare - discount,
            PromoCode = appliedPromo,
            SurgeMultiplier = fareConfig.SurgeMultiplier,
            VehicleCategory = request.VehicleCategory,
            PaymentMethod = request.PaymentMethod,
            Status = OrderStatus.Requested
        };

        if (request.Stops is { Count: > 0 })
        {
            var sequence = 1;
            foreach (var stop in request.Stops)
            {
                order.Stops.Add(new TripStop
                {
                    OrderId = order.Id,
                    SequenceNumber = sequence++,
                    Latitude = stop.Latitude,
                    Longitude = stop.Longitude,
                    Address = stop.Address,
                    StopType = TripStopType.Waypoint
                });
            }
        }

        db.Orders.Add(order);
        await notifications.QueueAsync(order.RiderId, "Pesanan dibuat",
            $"Kami sedang mencari driver untuk trip {order.Code}.", NotificationType.OrderUpdate, order.Id);

        await db.SaveChangesAsync(ct);

        return Result<CreateOrderResponse>.Ok(new CreateOrderResponse(
            order.Id, order.Code, order.Status,
            order.EstimatedFare, order.DiscountAmount, order.FinalFare,
            order.DistanceKm, order.EstimatedDurationMinutes,
            appliedPromo, order.CreatedAt));
    }

    /// <summary>
    /// Assign a driver to an open order. The conditional UPDATE is what stops two drivers
    /// from accepting the same trip — the simulator hits this race constantly.
    /// </summary>
    public async Task<Result<OrderStatus>> AcceptAsync(Guid orderId, Guid driverUserId, CancellationToken ct)
    {
        var profile = await db.DriverProfiles.FirstOrDefaultAsync(p => p.UserId == driverUserId, ct);
        if (profile is null) return Result<OrderStatus>.NotFound("Profil driver tidak ditemukan.");

        if (!profile.IsDocumentVerified)
            return Result<OrderStatus>.Forbidden("Dokumen driver belum diverifikasi admin.");

        var alreadyBusy = await db.Orders.AnyAsync(
            o => o.DriverId == driverUserId &&
                 (o.Status == OrderStatus.Accepted || o.Status == OrderStatus.DriverArrived || o.Status == OrderStatus.Started),
            ct);

        if (alreadyBusy) return Result<OrderStatus>.Conflict("Driver masih mengerjakan perjalanan lain.");

        var now = DateTime.UtcNow;
        var claimed = await db.Orders
            .Where(o => o.Id == orderId && o.Status == OrderStatus.Requested && o.DriverId == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.DriverId, driverUserId)
                .SetProperty(o => o.Status, OrderStatus.Accepted)
                .SetProperty(o => o.AcceptedAt, now), ct);

        if (claimed == 0)
            return Result<OrderStatus>.Conflict("Pesanan sudah diambil driver lain atau dibatalkan.");

        profile.Status = DriverStatus.OnTrip;

        var order = await db.Orders.AsNoTracking().FirstAsync(o => o.Id == orderId, ct);
        await notifications.QueueAsync(order.RiderId, "Driver ditemukan",
            $"Driver sedang menuju titik jemput untuk trip {order.Code}.", NotificationType.OrderUpdate, order.Id);

        await db.SaveChangesAsync(ct);
        return Result<OrderStatus>.Ok(OrderStatus.Accepted);
    }

    /// <summary>Move a trip along its lifecycle (arrived → started → completed).</summary>
    public async Task<Result<OrderDetailResponse>> AdvanceAsync(
        Guid orderId, Guid driverUserId, OrderStatus target, CancellationToken ct)
    {
        var order = await db.Orders.Include(o => o.Stops).FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return Result<OrderDetailResponse>.NotFound("Pesanan tidak ditemukan.");
        if (order.DriverId != driverUserId) return Result<OrderDetailResponse>.Forbidden("Pesanan ini bukan milik driver tersebut.");

        if (!CanTransition(order.Status, target))
            return Result<OrderDetailResponse>.Conflict($"Tidak bisa mengubah status dari {order.Status} ke {target}.");

        var now = DateTime.UtcNow;
        order.Status = target;

        switch (target)
        {
            case OrderStatus.DriverArrived:
                order.ArrivedAt = now;
                await notifications.QueueAsync(order.RiderId, "Driver sudah tiba",
                    $"Driver menunggu di titik jemput untuk trip {order.Code}.", NotificationType.OrderUpdate, order.Id);
                break;

            case OrderStatus.Started:
                order.StartedAt = now;
                break;

            case OrderStatus.Completed:
                order.CompletedAt = now;
                await CompleteTripAsync(order, now, ct);
                break;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Order {Code} moved to {Status}.", order.Code, target);

        return await GetDetailAsync(orderId, ct);
    }

    /// <summary>Settle the trip: pay the driver, record the payment, notify the rider.</summary>
    private async Task CompleteTripAsync(Order order, DateTime now, CancellationToken ct)
    {
        var profile = await db.DriverProfiles.FirstOrDefaultAsync(p => p.UserId == order.DriverId, ct);
        if (profile is not null)
        {
            profile.Status = DriverStatus.Online;
            profile.TotalTrips++;
            profile.TotalEarnings += order.FinalFare;
        }

        // Cash settles here; anything else opens a charge the rider still has to complete.
        // The unique index on Payment.OrderId remains the real guard against a double charge.
        await payments.EnsureChargeForCompletedTripAsync(order, now, ct);

        var settledOnTheSpot = order.PaymentMethod == PaymentMethod.Cash;

        await notifications.QueueAsync(order.RiderId, "Perjalanan selesai",
            settledOnTheSpot
                ? $"Trip {order.Code} selesai. Total Rp {order.FinalFare:N0}. Beri rating untuk driver kamu."
                : $"Trip {order.Code} selesai. Selesaikan pembayaran Rp {order.FinalFare:N0} di aplikasi.",
            NotificationType.OrderUpdate, order.Id);
    }

    public async Task<Result<OrderDetailResponse>> CancelAsync(
        Guid orderId, Guid actorId, bool actorIsAdmin, string? reason, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return Result<OrderDetailResponse>.NotFound("Pesanan tidak ditemukan.");

        var isRider = order.RiderId == actorId;
        var isDriver = order.DriverId == actorId;
        if (!isRider && !isDriver && !actorIsAdmin)
            return Result<OrderDetailResponse>.Forbidden("Kamu tidak berhak membatalkan pesanan ini.");

        if (!CanTransition(order.Status, OrderStatus.Cancelled))
            return Result<OrderDetailResponse>.Conflict($"Pesanan berstatus {order.Status} tidak bisa dibatalkan.");

        var now = DateTime.UtcNow;
        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = now;
        order.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Dibatalkan pengguna" : reason.Trim();
        order.CancelledBy = isRider ? CancelledByParty.Rider : isDriver ? CancelledByParty.Driver : CancelledByParty.System;

        // Release the driver and hand back the promo redemption.
        if (order.DriverId is not null)
        {
            var profile = await db.DriverProfiles.FirstOrDefaultAsync(p => p.UserId == order.DriverId, ct);
            if (profile is not null && profile.Status == DriverStatus.OnTrip) profile.Status = DriverStatus.Online;
        }

        if (!string.IsNullOrWhiteSpace(order.PromoCode))
        {
            await db.Promos
                .Where(p => p.Code == order.PromoCode && p.UsageCount > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.UsageCount, p => p.UsageCount - 1), ct);
        }

        var notifyUser = isRider ? order.DriverId : order.RiderId;
        if (notifyUser is not null)
        {
            await notifications.QueueAsync(notifyUser.Value, "Perjalanan dibatalkan",
                $"Trip {order.Code} dibatalkan. {order.CancellationReason}", NotificationType.OrderUpdate, order.Id);
        }

        await db.SaveChangesAsync(ct);
        return await GetDetailAsync(orderId, ct);
    }

    /// <summary>
    /// Built from three flat queries rather than one nested projection: combining a collection
    /// (stops) with a correlated subquery (payment) compiles to SQL APPLY, which SQLite does
    /// not support. Three indexed lookups keep the endpoint portable across all four providers.
    /// </summary>
    public async Task<Result<OrderDetailResponse>> GetDetailAsync(Guid orderId, CancellationToken ct)
    {
        var detail = await db.Orders
            .AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new
            {
                Order = o,
                Rider = new OrderPartyResponse(o.Rider.Id, o.Rider.FullName, o.Rider.PhoneNumber, o.Rider.PhotoUrl, null, null, null),
                Driver = o.Driver == null
                    ? null
                    : new OrderPartyResponse(
                        o.Driver.Id, o.Driver.FullName, o.Driver.PhoneNumber, o.Driver.PhotoUrl,
                        o.Driver.DriverProfile!.Rating,
                        o.Driver.DriverProfile.VehicleType,
                        o.Driver.DriverProfile.VehiclePlate)
            })
            .FirstOrDefaultAsync(ct);

        if (detail is null) return Result<OrderDetailResponse>.NotFound("Pesanan tidak ditemukan.");

        var o = detail.Order;

        var stops = await db.TripStops
            .AsNoTracking()
            .Where(s => s.OrderId == orderId)
            .OrderBy(s => s.SequenceNumber)
            .Select(s => new TripStopResponse(s.Id, s.SequenceNumber, s.Latitude, s.Longitude, s.Address, s.StopType, s.ReachedAt))
            .ToListAsync(ct);

        var payment = await db.Payments
            .AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .Select(p => new PaymentResponse(
                p.Id, p.OrderId, o.Code, p.Amount, p.DiscountAmount,
                p.Method, p.Status, p.CreatedAt, p.CompletedAt, p.TransactionReference))
            .FirstOrDefaultAsync(ct);

        return Result<OrderDetailResponse>.Ok(new OrderDetailResponse(
            o.Id, o.Code, o.Status, detail.Rider, detail.Driver,
            o.PickupLatitude, o.PickupLongitude, o.PickupAddress,
            o.DropoffLatitude, o.DropoffLongitude, o.DropoffAddress,
            o.DistanceKm, o.EstimatedDurationMinutes,
            o.EstimatedFare, o.DiscountAmount, o.FinalFare, o.SurgeMultiplier, o.PromoCode,
            o.VehicleCategory, o.PaymentMethod,
            o.CreatedAt, o.AcceptedAt, o.ArrivedAt, o.StartedAt, o.CompletedAt, o.CancelledAt,
            o.CancellationReason, o.CancelledBy,
            o.RiderRating, o.DriverRating, o.ReviewComment,
            stops, payment));
    }

    /// <summary>Booking codes are short enough to read out loud, so collisions must be handled.</summary>
    private async Task<string> GenerateCodeAsync(CancellationToken ct)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no look-alike characters

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var code = "FR-" + new string(Enumerable.Range(0, 6)
                .Select(_ => alphabet[Random.Shared.Next(alphabet.Length)])
                .ToArray());

            if (!await db.Orders.AnyAsync(o => o.Code == code, ct)) return code;
        }

        return $"FR-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
    }
}
