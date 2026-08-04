using FastRide.Api.Payments;
using FastRide.Data;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using FastRide.Shared.Payments;
using Microsoft.EntityFrameworkCore;

namespace FastRide.Api.Services;

/// <summary>
/// Owns the money side of a trip.
///
/// One payment row per order — the unique index on <c>Payment.OrderId</c> is what makes a
/// double charge impossible — and that row is a *state machine*, not a receipt. Starting a
/// charge, retrying after a failure, and applying a provider callback all converge on it.
/// </summary>
public sealed class PaymentService(
    FastRideDbContext db,
    PaymentProviderRegistry registry,
    NotificationService notifications,
    ILogger<PaymentService> logger,
    TimeProvider clock)
{
    /// <summary>
    /// Start (or restart) a charge for an order.
    ///
    /// Safe to call repeatedly: an already-settled order returns its payment untouched, and
    /// a charge still awaiting the payer returns the same QR rather than issuing a second one.
    /// </summary>
    public async Task<Result<PaymentResponse>> ChargeAsync(
        Guid orderId, PaymentMethod method, EWalletChannel walletChannel, decimal amountOverride, CancellationToken ct)
    {
        var order = await db.Orders
            .Include(o => o.Rider)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (order is null) return Result<PaymentResponse>.NotFound("Pesanan tidak ditemukan.");

        if (order.Status is OrderStatus.Cancelled or OrderStatus.Expired)
            return Result<PaymentResponse>.Conflict("Pesanan yang dibatalkan tidak bisa dibayar.");

        var payment = await db.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId, ct);

        if (payment is { IsSettled: true })
            return Result<PaymentResponse>.Ok(ToResponse(payment, order.Code));

        // A live charge the payer has not acted on yet is still good — reissuing would leave
        // a second QR payable at the provider for the same trip.
        //
        // The test is AwaitingPayment specifically, not IsInFlight: a Pending row is one that
        // was created when the driver finished the trip but never sent to a provider, so it
        // has no payload and must still be chargeable. Treating that as "already in flight"
        // left riders holding an order they could never pay.
        var hasLiveCharge = payment is
        {
            Status: PaymentStatus.AwaitingPayment,
            ProviderReference: not null
        };

        if (hasLiveCharge && payment!.Method == method && !HasExpired(payment))
            return Result<PaymentResponse>.Ok(ToResponse(payment, order.Code));

        var provider = await registry.ResolveAsync(method, ct);

        if (provider is null)
            return Result<PaymentResponse>.Invalid($"Metode pembayaran {method} sedang tidak tersedia.");

        var amount = amountOverride > 0 ? amountOverride : order.FinalFare;
        var now = clock.GetUtcNow().UtcDateTime;

        var configs = await registry.GetConfigsAsync(ct);
        var expiryMinutes = configs
            .FirstOrDefault(config => string.Equals(config.Name, provider.Name, StringComparison.OrdinalIgnoreCase))
            ?.ChargeExpiryMinutes ?? 15;

        if (payment is null)
        {
            payment = new Payment
            {
                OrderId = order.Id,
                DiscountAmount = order.DiscountAmount,
                CreatedAt = now
            };

            db.Payments.Add(payment);
        }

        // Reset the row for this attempt. Keeping one row means a failed charge can be retried
        // without ever risking two settled payments for the same trip.
        payment.Amount = amount;
        payment.Method = method;
        payment.WalletChannel = walletChannel;
        payment.Status = PaymentStatus.Pending;
        payment.ProviderName = provider.Name;
        payment.ProviderReference = null;
        payment.PaymentPayload = null;
        payment.FailureReason = null;
        payment.CompletedAt = null;
        payment.AttemptCount++;
        payment.TransactionReference ??= BuildReference(now);
        payment.ExpiresAt = now.AddMinutes(expiryMinutes);

        var result = await provider.ChargeAsync(new PaymentChargeRequest(
            payment.Id,
            payment.TransactionReference!,
            amount,
            method,
            walletChannel,
            order.Code,
            order.Rider.FullName,
            order.Rider.Email,
            order.Rider.PhoneNumber,
            payment.ExpiresAt.Value), ct);

        if (!result.Success)
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = result.Error;

            await db.SaveChangesAsync(ct);

            logger.LogWarning("Charge for order {Code} rejected by {Provider}: {Error}",
                order.Code, provider.Name, result.Error);

            return Result<PaymentResponse>.Invalid(result.Error ?? "Pembayaran ditolak provider.");
        }

        payment.Status = result.Status;
        payment.ProviderReference = result.ProviderReference;
        payment.PaymentPayload = result.Payload;
        if (result.ExpiresAt is { } expiresAt) payment.ExpiresAt = expiresAt;

        if (result.Status == PaymentStatus.Completed)
            await SettleAsync(order, payment, now, ct);

        await db.SaveChangesAsync(ct);

        return Result<PaymentResponse>.Ok(ToResponse(payment, order.Code));
    }

    /// <summary>
    /// Where a payment stands. Asks the provider when the local row is still waiting, so a
    /// lost callback cannot strand a trip that the payer actually paid for.
    /// </summary>
    public async Task<Result<PaymentResponse>> GetStatusAsync(Guid orderId, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null) return Result<PaymentResponse>.NotFound("Pesanan tidak ditemukan.");

        var payment = await db.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId, ct);
        if (payment is null) return Result<PaymentResponse>.NotFound("Belum ada pembayaran untuk pesanan ini.");

        if (payment.IsInFlight && payment.ProviderReference is not null && payment.ProviderName is not null)
        {
            var resolved = await registry.ResolveByNameAsync(payment.ProviderName, ct);

            if (resolved is { } entry)
            {
                var status = await entry.Provider.QueryAsync(
                    new PaymentQueryRequest(payment.ProviderReference, payment.TransactionReference ?? string.Empty), ct);

                if (status.Error is null && status.Status != payment.Status)
                    await ApplyStatusAsync(order, payment, status.Status, null, ct);
            }
        }

        // Expiry is decided locally too, so a provider that never reports it cannot leave a
        // dead QR looking payable in the app.
        if (payment.IsInFlight && HasExpired(payment))
            await ApplyStatusAsync(order, payment, PaymentStatus.Expired, "Waktu pembayaran habis.", ct);

        await db.SaveChangesAsync(ct);

        return Result<PaymentResponse>.Ok(ToResponse(payment, order.Code));
    }

    /// <summary>
    /// Apply a verified provider callback.
    ///
    /// Idempotent by design: providers retry callbacks, and the same "paid" message may
    /// arrive several times. Re-applying a status the row already has changes nothing.
    /// </summary>
    public async Task<bool> ApplyCallbackAsync(PaymentCallback callback, CancellationToken ct)
    {
        var payment = await db.Payments
            .FirstOrDefaultAsync(p => p.TransactionReference == callback.Reference, ct);

        if (payment is null)
        {
            logger.LogWarning("Callback for unknown reference {Reference}.", callback.Reference);
            return false;
        }

        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == payment.OrderId, ct);
        if (order is null) return false;

        // A settled payment is final. A late "expired" must not un-pay a completed trip.
        if (payment.IsSettled && callback.Status != PaymentStatus.Refunded)
        {
            payment.LastCallbackAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
            return true;
        }

        // The amount the provider says it took must match what we asked for.
        if (callback.Status == PaymentStatus.Completed &&
            callback.Amount is { } paid &&
            Math.Abs(paid - payment.Amount) > 1m)
        {
            logger.LogError(
                "Callback for {Reference} reports Rp {Paid} but the charge was Rp {Expected}; refusing to settle.",
                callback.Reference, paid, payment.Amount);

            return false;
        }

        if (callback.ProviderReference is not null) payment.ProviderReference = callback.ProviderReference;
        payment.LastCallbackAt = clock.GetUtcNow().UtcDateTime;

        await ApplyStatusAsync(order, payment, callback.Status, callback.FailureReason, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Payment {Reference} moved to {Status} by callback.", callback.Reference, callback.Status);
        return true;
    }

    /// <summary>
    /// Record the payment for a trip the driver just finished.
    ///
    /// Cash settles on the spot. Anything else opens a charge the rider still has to complete,
    /// so the trip closes but the payment stays in flight.
    /// </summary>
    public async Task EnsureChargeForCompletedTripAsync(Order order, DateTime now, CancellationToken ct)
    {
        var existing = await db.Payments.FirstOrDefaultAsync(p => p.OrderId == order.Id, ct);
        if (existing is not null) return;

        var provider = await registry.ResolveAsync(order.PaymentMethod, ct);

        var payment = new Payment
        {
            OrderId = order.Id,
            Amount = order.FinalFare,
            DiscountAmount = order.DiscountAmount,
            Method = order.PaymentMethod,
            CreatedAt = now,
            TransactionReference = BuildReference(now),
            ProviderName = provider?.Name,
            AttemptCount = 1
        };

        // Settled outside the app — the driver has the cash in hand.
        if (provider is { SettlesImmediately: true })
        {
            payment.Status = PaymentStatus.Completed;
            payment.CompletedAt = now;
            payment.ProviderReference = payment.TransactionReference;
        }
        else
        {
            // The rider pays from the app; the trip does not wait for it.
            payment.Status = PaymentStatus.Pending;
            payment.ExpiresAt = now.AddMinutes(15);
        }

        db.Payments.Add(payment);
    }

    // ─────────────────────────── internals ───────────────────────────

    private async Task ApplyStatusAsync(
        Order order, Payment payment, PaymentStatus status, string? failureReason, CancellationToken ct)
    {
        if (payment.Status == status) return;

        var now = clock.GetUtcNow().UtcDateTime;
        payment.Status = status;

        switch (status)
        {
            case PaymentStatus.Completed:
                await SettleAsync(order, payment, now, ct);
                break;

            case PaymentStatus.Failed or PaymentStatus.Expired:
                payment.FailureReason = failureReason ?? "Pembayaran tidak selesai.";

                await notifications.QueueAsync(order.RiderId, "Pembayaran belum berhasil",
                    $"Pembayaran trip {order.Code} belum selesai. Coba lagi dari aplikasi.",
                    NotificationType.Payment, order.Id);
                break;

            case PaymentStatus.Refunded:
                payment.FailureReason = failureReason;

                await notifications.QueueAsync(order.RiderId, "Dana dikembalikan",
                    $"Pembayaran trip {order.Code} telah direfund.",
                    NotificationType.Payment, order.Id);
                break;
        }
    }

    private async Task SettleAsync(Order order, Payment payment, DateTime now, CancellationToken ct)
    {
        payment.Status = PaymentStatus.Completed;
        payment.CompletedAt = now;
        payment.FailureReason = null;

        // Money for a trip still in progress also closes the trip out.
        if (order.Status == OrderStatus.Started)
        {
            order.Status = OrderStatus.Completed;
            order.CompletedAt = now;

            var profile = await db.DriverProfiles.FirstOrDefaultAsync(p => p.UserId == order.DriverId, ct);

            if (profile is not null)
            {
                profile.Status = DriverStatus.Online;
                profile.TotalTrips++;
                profile.TotalEarnings += payment.Amount;
            }
        }

        await notifications.QueueAsync(order.RiderId, "Pembayaran berhasil",
            $"Pembayaran trip {order.Code} sebesar Rp {payment.Amount:N0} sudah diterima.",
            NotificationType.Payment, order.Id);
    }

    private bool HasExpired(Payment payment) =>
        payment.ExpiresAt is { } expiresAt && clock.GetUtcNow().UtcDateTime > expiresAt;

    private static string BuildReference(DateTime now) =>
        $"TRX-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

    internal static PaymentResponse ToResponse(Payment payment, string? orderCode) => new(
        payment.Id,
        payment.OrderId,
        orderCode,
        payment.Amount,
        payment.DiscountAmount,
        payment.Method,
        payment.Status,
        payment.CreatedAt,
        payment.CompletedAt,
        payment.TransactionReference,
        payment.WalletChannel,
        payment.ProviderName,
        payment.PaymentPayload,
        payment.ExpiresAt,
        payment.FailureReason,
        // Only a live QRIS charge needs a rendered code; anything else would be wasted bytes
        // on a mobile connection.
        payment is { Method: PaymentMethod.Qris, PaymentPayload: not null } && payment.IsInFlight
            ? QrCodeRenderer.ToDataUri(payment.PaymentPayload)
            : null);
}
