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

/// <summary>
/// Stands in for the payer against the simulated provider.
///
/// Demos, the load simulator and the test suite need a charge to complete without a human
/// scanning a QR. This drives the *same* callback path a real provider uses — signed body,
/// signature verification, the lot — so exercising it proves the production path works
/// rather than bypassing it.
///
/// Only mapped outside Production, and only ever touches the simulated provider.
/// </summary>
public static class PaymentSandboxEndpoints
{
    public static IEndpointRouteBuilder MapPaymentSandboxEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/payments/sandbox")
            .WithTags("Payments · Sandbox")
            .RequireAuthorization();

        group.MapPost("/{orderId:guid}/settle", Settle)
            .WithSummary("Pretend the rider paid (simulated provider only)");

        group.MapPost("/{orderId:guid}/fail", Fail)
            .WithSummary("Pretend the payment was declined (simulated provider only)");

        return api;
    }

    private static Task<IResult> Settle(
        Guid orderId, HttpContext http, FastRideDbContext db,
        PaymentProviderRegistry registry, PaymentService payments, CancellationToken ct) =>
        AdvanceAsync(orderId, PaymentStatus.Completed, http, db, registry, payments, ct);

    private static Task<IResult> Fail(
        Guid orderId, HttpContext http, FastRideDbContext db,
        PaymentProviderRegistry registry, PaymentService payments, CancellationToken ct) =>
        AdvanceAsync(orderId, PaymentStatus.Failed, http, db, registry, payments, ct);

    private static async Task<IResult> AdvanceAsync(
        Guid orderId,
        PaymentStatus status,
        HttpContext http,
        FastRideDbContext db,
        PaymentProviderRegistry registry,
        PaymentService payments,
        CancellationToken ct)
    {
        var payment = await db.Payments
            .AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .Select(p => new
            {
                p.TransactionReference,
                p.ProviderReference,
                p.ProviderName,
                p.Amount,
                p.Order.RiderId,
                p.Order.DriverId
            })
            .FirstOrDefaultAsync(ct);

        if (payment is null) return Results.NotFound(new ApiError("NotFound", "Belum ada pembayaran untuk pesanan ini."));

        var caller = http.User.UserId();
        if (!http.User.IsAdmin() && caller != payment.RiderId && caller != payment.DriverId)
            return Results.Json(new ApiError("Forbidden", "Pesanan ini bukan milik kamu."), statusCode: StatusCodes.Status403Forbidden);

        if (!string.Equals(payment.ProviderName, "simulated", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new ApiError("Invalid", "Endpoint ini hanya untuk provider simulasi."));

        var resolved = await registry.ResolveByNameAsync("simulated", ct);

        if (resolved is not { Provider: SimulatedPaymentProvider simulated, Config: var config })
            return Results.BadRequest(new ApiError("Invalid", "Provider simulasi tidak aktif."));

        if (payment.ProviderReference is null)
            return Results.BadRequest(new ApiError("Invalid", "Pembayaran belum dikirim ke provider."));

        simulated.Advance(payment.ProviderReference, status, out _);

        // Go through the real callback path rather than writing the row directly — that is
        // the point of this endpoint.
        var (body, signature) = simulated.BuildCallback(
            payment.ProviderReference, payment.TransactionReference!, status, payment.Amount);

        var callback = simulated.ReadCallback(new PaymentCallbackContext(
            body,
            new Dictionary<string, string> { ["x-fastride-signature"] = signature },
            config.WebhookSecret));

        if (callback is null)
            return Results.Json(new ApiError("ServerError", "Callback simulasi gagal diverifikasi."), statusCode: 500);

        await payments.ApplyCallbackAsync(callback, ct);

        var result = await payments.GetStatusAsync(orderId, ct);
        return result.ToHttpResult();
    }
}
