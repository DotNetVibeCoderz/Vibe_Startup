using FastRide.Api.Payments;
using FastRide.Api.Security;
using FastRide.Api.Services;
using FastRide.Data;
using FastRide.Shared.Common;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using FastRide.Shared.Payments;
using Microsoft.EntityFrameworkCore;

namespace FastRide.Api.Endpoints;

/// <summary>Payments, provider callbacks, and two-way reviews.</summary>
public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder api)
    {
        var payments = api.MapGroup("/payments").WithTags("Payments").RequireAuthorization();

        payments.MapGet("/", ListPayments).RequireAuthorization(Policies.AdminOnly);
        payments.MapGet("/methods", AvailableMethods).WithSummary("Payment methods currently switched on");
        payments.MapGet("/{id:guid}", GetPayment);
        payments.MapGet("/order/{orderId:guid}", GetForOrder).WithSummary("Payment state of an order");
        payments.MapPost("/", Charge).WithSummary("Start or retry a charge (idempotent)");

        // Providers post here. Anonymous by necessity — a PSP has no user token — but every
        // request is verified against the provider's own signature before it is trusted, and
        // rate limited so an unauthenticated endpoint cannot be hammered.
        api.MapPost("/payments/webhook/{provider}", Webhook)
            .WithTags("Payments")
            .AllowAnonymous()
            .RequireRateLimiting("webhook")
            .WithSummary("Provider callback (signature verified)");

        var reviews = api.MapGroup("/reviews").WithTags("Reviews").RequireAuthorization();

        reviews.MapPost("/", SubmitReview);
        reviews.MapGet("/user/{userId:guid}", ReviewsForUser).AllowAnonymous();

        return api;
    }

    private static async Task<IResult> ListPayments(
        int? page, int? limit, PaymentStatus? status, PaymentMethod? method,
        DateTime? from, DateTime? to, FastRideDbContext db, CancellationToken ct)
    {
        var paging = PageRequest.From(page, limit);
        var payments = db.Payments.AsNoTracking();

        if (status is { } paymentStatus) payments = payments.Where(p => p.Status == paymentStatus);
        if (method is { } paymentMethod) payments = payments.Where(p => p.Method == paymentMethod);
        if (from is { } start) payments = payments.Where(p => p.CreatedAt >= start);
        if (to is { } end) payments = payments.Where(p => p.CreatedAt <= end);

        var total = await payments.CountAsync(ct);
        var data = await payments
            .OrderByDescending(p => p.CreatedAt)
            .Skip(paging.Skip)
            .Take(paging.Limit)
            .Select(p => new PaymentResponse(
                p.Id, p.OrderId, p.Order.Code, p.Amount, p.DiscountAmount,
                p.Method, p.Status, p.CreatedAt, p.CompletedAt, p.TransactionReference,
                p.WalletChannel, p.ProviderName,
                // The payload is only useful to the payer; the admin list does not need it.
                null, p.ExpiresAt, p.FailureReason))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<PaymentResponse>
        {
            Total = total,
            Page = paging.Page,
            Limit = paging.Limit,
            Data = data
        });
    }

    private static async Task<IResult> AvailableMethods(PaymentProviderRegistry registry, CancellationToken ct)
    {
        var methods = await registry.GetAvailableMethodsAsync(ct);

        var options = methods
            .Select(method => new PaymentMethodOption(
                method,
                Display.Label(method),
                Display.Icon(method),
                // Cash needs nothing from the rider; everything else routes through a provider.
                method != PaymentMethod.Cash))
            .ToList();

        return Results.Ok(new AvailablePaymentMethodsResponse(options));
    }

    private static async Task<IResult> GetPayment(Guid id, HttpContext http, FastRideDbContext db, CancellationToken ct)
    {
        var record = await db.Payments
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new { Payment = p, p.Order.Code, p.Order.RiderId, p.Order.DriverId })
            .FirstOrDefaultAsync(ct);

        if (record is null) return Results.NotFound(new ApiError("NotFound", "Pembayaran tidak ditemukan."));

        var caller = http.User.UserId();
        if (!http.User.IsAdmin() && caller != record.RiderId && caller != record.DriverId)
            return Forbidden("Pembayaran ini bukan milik kamu.");

        return Results.Ok(PaymentService.ToResponse(record.Payment, record.Code));
    }

    private static async Task<IResult> GetForOrder(
        Guid orderId, HttpContext http, FastRideDbContext db, PaymentService payments, CancellationToken ct)
    {
        if (!await CanAccessOrderAsync(orderId, http, db, ct)) return Forbidden("Pesanan ini bukan milik kamu.");

        var result = await payments.GetStatusAsync(orderId, ct);
        return result.ToHttpResult();
    }

    /// <summary>
    /// Start a charge, or retry one that failed.
    ///
    /// Idempotent in both directions: a settled order returns its payment untouched, and a
    /// charge still awaiting the payer returns the same QR instead of issuing a second one.
    /// </summary>
    private static async Task<IResult> Charge(
        PaymentRequest request, HttpContext http, FastRideDbContext db, PaymentService payments, CancellationToken ct)
    {
        if (!await CanAccessOrderAsync(request.OrderId, http, db, ct))
            return Forbidden("Pesanan ini bukan milik kamu.");

        var result = await payments.ChargeAsync(
            request.OrderId, request.Method, request.WalletChannel, request.Amount, ct);

        return result.ToHttpResult();
    }

    /// <summary>
    /// Provider callback.
    ///
    /// Always answers 200 once the signature checks out, even when the payment is unknown —
    /// providers retry anything else, and a retry storm over a reference we will never
    /// recognise helps nobody. A bad signature gets 401 and is logged.
    /// </summary>
    private static async Task<IResult> Webhook(
        string provider,
        HttpRequest request,
        PaymentProviderRegistry registry,
        PaymentService payments,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("FastRide.Payments.Webhook");

        var resolved = await registry.ResolveByNameAsync(provider, ct);

        if (resolved is not { } entry)
        {
            logger.LogWarning("Callback for unknown or disabled provider {Provider}.", provider);
            return Results.NotFound(new ApiError("NotFound", "Provider tidak dikenal."));
        }

        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(ct);

        var headers = request.Headers.ToDictionary(
            header => header.Key.ToLowerInvariant(),
            header => header.Value.ToString(),
            StringComparer.Ordinal);

        var callback = entry.Provider.ReadCallback(
            new PaymentCallbackContext(body, headers, entry.Config.WebhookSecret));

        if (callback is null)
        {
            // ReadCallback already logged why. Never echo the reason back — that would help
            // someone tune a forgery.
            return Results.Json(
                new ApiError("Unauthorized", "Callback tidak bisa diverifikasi."),
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var applied = await payments.ApplyCallbackAsync(callback, ct);

        return Results.Ok(new MessageResponse(applied ? "Callback diterima." : "Callback diabaikan."));
    }

    // ─────────────────────────── reviews ───────────────────────────

    private static async Task<IResult> SubmitReview(
        SubmitReviewRequest request, HttpContext http, FastRideDbContext db, CancellationToken ct)
    {
        if (!http.User.CanAccess(request.ReviewerId))
            return Forbidden("Kamu hanya bisa memberi ulasan atas nama sendiri.");

        if (request.Rating is < 1 or > 5)
            return Results.BadRequest(new ApiError("Invalid", "Rating harus antara 1 dan 5."));

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == request.OrderId, ct);
        if (order is null) return Results.NotFound(new ApiError("NotFound", "Pesanan tidak ditemukan."));

        if (order.Status != OrderStatus.Completed)
            return Results.Conflict(new ApiError("Conflict", "Ulasan hanya bisa diberikan setelah perjalanan selesai."));

        if (request.ReviewerId != order.RiderId && request.ReviewerId != order.DriverId)
            return Forbidden("Kamu tidak ikut dalam perjalanan ini.");

        if (await db.Reviews.AnyAsync(r => r.OrderId == order.Id && r.ReviewerId == request.ReviewerId, ct))
            return Results.Conflict(new ApiError("Conflict", "Kamu sudah memberi ulasan untuk perjalanan ini."));

        db.Reviews.Add(new Review
        {
            OrderId = order.Id,
            ReviewerId = request.ReviewerId,
            TargetUserId = request.TargetUserId,
            Rating = request.Rating,
            Comment = request.Comment
        });

        if (request.ReviewerId == order.RiderId)
        {
            order.DriverRating = request.Rating;
            order.ReviewComment = request.Comment;
        }
        else
        {
            order.RiderRating = request.Rating;
        }

        // Keep the driver's headline rating in step with the reviews behind it.
        var driverProfile = await db.DriverProfiles.FirstOrDefaultAsync(p => p.UserId == request.TargetUserId, ct);
        if (driverProfile is not null)
        {
            var totals = await db.Reviews
                .Where(r => r.TargetUserId == request.TargetUserId)
                .GroupBy(_ => 1)
                .Select(g => new { Sum = g.Sum(r => r.Rating), Count = g.Count() })
                .FirstOrDefaultAsync(ct);

            var sum = (totals?.Sum ?? 0) + request.Rating;
            var count = (totals?.Count ?? 0) + 1;

            driverProfile.Rating = Math.Round((double)sum / count, 2);
            driverProfile.RatingCount = count;
        }

        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/reviews/user/{request.TargetUserId}",
            new MessageResponse("Terima kasih atas ulasannya."));
    }

    private static async Task<IResult> ReviewsForUser(
        Guid userId, int? page, int? limit, FastRideDbContext db, CancellationToken ct)
    {
        var paging = PageRequest.From(page, limit, 10);
        var reviews = db.Reviews.AsNoTracking().Where(r => r.TargetUserId == userId);

        var total = await reviews.CountAsync(ct);

        // Joined rather than pulled through correlated subqueries, which SQLite would have to
        // express as APPLY.
        var data = await reviews
            .OrderByDescending(r => r.CreatedAt)
            .Skip(paging.Skip)
            .Take(paging.Limit)
            .Join(db.Users.AsNoTracking(),
                review => review.ReviewerId,
                reviewer => reviewer.Id,
                (review, reviewer) => new ReviewResponse(
                    review.Id, review.OrderId, reviewer.FullName, reviewer.PhotoUrl,
                    review.Rating, review.Comment, review.CreatedAt))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<ReviewResponse>
        {
            Total = total,
            Page = paging.Page,
            Limit = paging.Limit,
            Data = data
        });
    }

    // ─────────────────────────── helpers ───────────────────────────

    private static async Task<bool> CanAccessOrderAsync(
        Guid orderId, HttpContext http, FastRideDbContext db, CancellationToken ct)
    {
        if (http.User.IsAdmin()) return true;

        var parties = await db.Orders
            .AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new { o.RiderId, o.DriverId })
            .FirstOrDefaultAsync(ct);

        // A missing order is reported as NotFound further in, not blocked here.
        if (parties is null) return true;

        var caller = http.User.UserId();
        return caller == parties.RiderId || caller == parties.DriverId;
    }

    private static IResult Forbidden(string message) =>
        Results.Json(new ApiError("Forbidden", message), statusCode: StatusCodes.Status403Forbidden);
}
