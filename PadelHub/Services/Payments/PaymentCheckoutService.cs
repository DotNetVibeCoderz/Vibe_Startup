using Microsoft.EntityFrameworkCore;
using PadelHub.Data;
using PadelHub.Models;

namespace PadelHub.Services.Payments;

/// <summary>
/// Menjembatani entitas Payment dengan provider: membuat sesi checkout dan
/// menerapkan notifikasi yang masuk. Semua perubahan status pembayaran yang
/// berasal dari provider melewati kelas ini.
/// </summary>
public class PaymentCheckoutService
{
    private readonly AppDbContext _db;
    private readonly PaymentGatewayRegistry _registry;
    private readonly ILogger<PaymentCheckoutService> _logger;

    public PaymentCheckoutService(AppDbContext db, PaymentGatewayRegistry registry, ILogger<PaymentCheckoutService> logger)
    {
        _db = db;
        _registry = registry;
        _logger = logger;
    }

    /// <summary>
    /// Membuat sesi pembayaran baru pada provider terpilih dan menyimpan
    /// referensinya di Payment.
    /// </summary>
    public async Task<CheckoutSession> StartAsync(int paymentId, string providerKey, string baseUrl,
        CancellationToken cancellationToken = default)
    {
        var payment = await _db.Payments
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);

        if (payment is null)
            return CheckoutSession.Failed("Tagihan tidak ditemukan.");

        if (PaymentStatuses.IsPaid(payment.Status))
            return CheckoutSession.Failed("Tagihan ini sudah lunas.");

        var gateway = _registry.Find(providerKey);
        if (gateway is null)
            return CheckoutSession.Failed($"Provider \"{providerKey}\" tidak dikenal.");

        if (!gateway.IsEnabled)
            return CheckoutSession.Failed($"{gateway.DisplayName} belum aktif. Aktifkan di appsettings.json.");

        // ID unik per percobaan: Midtrans menolak order_id yang dipakai ulang.
        var externalId = $"PDH-{payment.Id}-{DateTime.UtcNow:yyMMddHHmmss}";
        var description = string.IsNullOrWhiteSpace(payment.Notes)
            ? $"Pembayaran PadelHub #{payment.Id}"
            : payment.Notes!;

        var request = new CheckoutRequest
        {
            ExternalId = externalId,
            Amount = payment.Amount,
            Description = description,
            CustomerName = payment.User?.FullName,
            CustomerEmail = payment.User?.Email,
            CustomerPhone = payment.User?.PhoneNumber,
            SuccessUrl = $"{baseUrl}/finance/checkout/{payment.Id}?state=success",
            FailureUrl = $"{baseUrl}/finance/checkout/{payment.Id}?state=failed",
        };

        var session = await gateway.CreateCheckoutAsync(request, cancellationToken);
        if (!session.Success) return session;

        payment.Provider = gateway.Key;
        payment.ExternalId = externalId;
        payment.ProviderReference = session.ProviderReference;
        payment.CheckoutUrl = session.CheckoutUrl;
        payment.ExpiresAt = session.ExpiresAt;
        payment.PaymentMethod = gateway.Key;
        if (string.IsNullOrWhiteSpace(payment.TransactionId))
            payment.TransactionId = externalId;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Sesi pembayaran {ExternalId} dibuat di {Provider} untuk tagihan {PaymentId}.",
            externalId, gateway.Key, payment.Id);

        return session;
    }

    /// <summary>
    /// Menerapkan notifikasi provider yang sudah diverifikasi. Mengembalikan
    /// false bila tagihan tidak ditemukan atau nominalnya tidak cocok — pada
    /// kasus itu status sengaja tidak diubah.
    /// </summary>
    public async Task<bool> ApplyCallbackAsync(GatewayCallback callback, string rawBody, string providerKey,
        CancellationToken cancellationToken = default)
    {
        if (!callback.Valid || string.IsNullOrWhiteSpace(callback.ExternalId))
            return false;

        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.ExternalId == callback.ExternalId, cancellationToken);

        if (payment is null)
        {
            _logger.LogWarning("Notifikasi {Provider} untuk {ExternalId} diabaikan: tagihan tidak ditemukan.",
                providerKey, callback.ExternalId);
            return false;
        }

        payment.CallbackPayload = rawBody.Length > 4000 ? rawBody[..4000] : rawBody;
        payment.Provider = providerKey;

        if (callback.Status == PaymentStatuses.Paid)
        {
            // Jangan pernah melunasi tagihan berdasarkan nominal yang tidak cocok.
            if (callback.Amount is { } paidAmount && Math.Abs(paidAmount - payment.Amount) > 0.5m)
            {
                _logger.LogError("Notifikasi {Provider} untuk {ExternalId} ditolak: nominal {Paid} ≠ tagihan {Expected}.",
                    providerKey, callback.ExternalId, paidAmount, payment.Amount);
                await _db.SaveChangesAsync(cancellationToken);
                return false;
            }

            payment.Status = PaymentStatuses.Paid;
            payment.PaidAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(callback.Method))
                payment.PaymentMethod = callback.Method!;

            // Reservasi terkait ikut terkonfirmasi begitu pembayarannya lunas.
            if (payment.ReservationId is { } reservationId)
            {
                var reservation = await _db.Reservations
                    .FirstOrDefaultAsync(r => r.Id == reservationId, cancellationToken);
                if (reservation is not null && reservation.Status == "Pending")
                    reservation.Status = "Confirmed";
            }

            _logger.LogInformation("Tagihan {PaymentId} lunas lewat {Provider}.", payment.Id, providerKey);
        }
        else if (callback.Status is PaymentStatuses.Expired or PaymentStatuses.Cancelled or PaymentStatuses.Failed)
        {
            payment.Status = callback.Status;
            _logger.LogInformation("Tagihan {PaymentId} berstatus {Status} dari {Provider}.",
                payment.Id, callback.Status, providerKey);
        }

        if (!string.IsNullOrWhiteSpace(callback.ProviderReference))
            payment.ProviderReference = callback.ProviderReference;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
