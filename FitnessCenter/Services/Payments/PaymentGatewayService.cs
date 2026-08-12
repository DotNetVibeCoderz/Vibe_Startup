using Microsoft.EntityFrameworkCore;
using FitnessCenter.Data;
using FitnessCenter.Models;

namespace FitnessCenter.Services.Payments;

/// <summary>
/// Fasad payment gateway — satu-satunya tempat yang menulis hasil pembayaran ke database.
///
/// Pola yang dipakai sama dengan StorageService: provider dipilih lewat konfigurasi,
/// dan bila provider yang diminta belum siap, permintaan jatuh ke pembayaran manual
/// sehingga member tetap punya jalan membayar.
/// </summary>
public class PaymentGatewayService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly NotificationService _notifications;
    private readonly ILogger<PaymentGatewayService> _logger;
    private readonly Dictionary<PaymentGatewayProvider, IPaymentProvider> _providers;

    public PaymentGatewayService(
        AppDbContext db,
        IConfiguration config,
        IEnumerable<IPaymentProvider> providers,
        NotificationService notifications,
        ILogger<PaymentGatewayService> logger)
    {
        _db = db;
        _config = config;
        _notifications = notifications;
        _logger = logger;
        _providers = providers.ToDictionary(p => p.Key);
    }

    /// <summary>Provider bawaan dari appsettings.json, jatuh ke Manual bila tidak dikenal.</summary>
    public PaymentGatewayProvider DefaultProvider
    {
        get
        {
            var name = _config.GetValue<string>("PaymentGateway:DefaultProvider");
            return Enum.TryParse<PaymentGatewayProvider>(name, ignoreCase: true, out var parsed)
                ? parsed
                : PaymentGatewayProvider.Manual;
        }
    }

    /// <summary>Alamat publik aplikasi, dipakai untuk redirect balik dan URL callback.</summary>
    public string BaseUrl =>
        (_config.GetValue<string>("PaymentGateway:BaseUrl") ?? "https://localhost:7042").TrimEnd('/');

    /// <summary>Semua provider terdaftar beserta status kesiapannya.</summary>
    public IReadOnlyList<PaymentProviderInfo> GetProviders() =>
        _providers.Values
            .OrderBy(p => p.Key)
            .Select(p => new PaymentProviderInfo
            {
                Key = p.Key,
                DisplayName = p.DisplayName,
                Description = p.Description,
                IsConfigured = p.IsConfigured,
                IsRedirectBased = p.IsRedirectBased,
                IsDefault = p.Key == DefaultProvider,
                Channels = p.Channels,
                SetupHint = p.SetupHint
            })
            .ToList();

    /// <summary>Provider yang siap dipakai member untuk membayar sekarang.</summary>
    public IReadOnlyList<PaymentProviderInfo> GetAvailableProviders() =>
        GetProviders().Where(p => p.IsConfigured).ToList();

    /// <summary>URL callback yang perlu didaftarkan di dashboard tiap provider.</summary>
    public string WebhookUrlFor(PaymentGatewayProvider provider) =>
        $"{BaseUrl}/api/v1/payments/webhook/{provider.ToString().ToLowerInvariant()}";

    private IPaymentProvider Resolve(PaymentGatewayProvider requested)
    {
        if (_providers.TryGetValue(requested, out var provider) && provider.IsConfigured)
            return provider;

        if (requested != PaymentGatewayProvider.Manual)
            _logger.LogWarning("Provider {Provider} belum siap, dialihkan ke pembayaran manual.", requested);

        return _providers[PaymentGatewayProvider.Manual];
    }

    // ==================== MEMULAI PEMBAYARAN ====================

    /// <summary>
    /// Membuat tagihan di provider terpilih dan menyimpan tautan bayarnya.
    /// Tagihan yang tautannya masih hidup dipakai ulang, supaya satu invoice
    /// tidak menghasilkan banyak transaksi di sisi provider.
    /// </summary>
    public async Task<(bool ok, string message, Payment? payment)> StartPaymentAsync(
        int paymentId,
        PaymentGatewayProvider requested,
        string? requestedByUserId = null,
        string? preferredChannel = null,
        CancellationToken ct = default)
    {
        var payment = await _db.Payments.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (payment == null) return (false, "Tagihan tidak ditemukan.", null);

        if (requestedByUserId != null && payment.UserId != requestedByUserId)
            return (false, "Tagihan ini bukan milik kamu.", null);

        if (payment.Status is PaymentStatus.Completed)
            return (false, "Tagihan ini sudah lunas.", payment);

        if (payment.Status is PaymentStatus.Cancelled or PaymentStatus.Refunded)
            return (false, $"Tagihan berstatus {payment.Status} dan tidak bisa dibayar.", payment);

        // Tautan yang masih berlaku dari provider yang sama dipakai lagi.
        if (payment.Gateway == requested && payment.HasLivePaymentUrl)
            return (true, "Melanjutkan ke halaman bayar yang sudah dibuat.", payment);

        var provider = Resolve(requested);

        var request = new PaymentChargeRequest
        {
            InvoiceNumber = payment.InvoiceNumber,
            Amount = payment.Amount,
            Description = payment.Description ?? $"Invoice {payment.InvoiceNumber}",
            CustomerName = payment.User?.FullName ?? "Member",
            CustomerEmail = payment.User?.Email,
            CustomerPhone = payment.User?.PhoneNumber,
            SuccessUrl = $"{BaseUrl}/payments?paid={Uri.EscapeDataString(payment.InvoiceNumber)}",
            FailureUrl = $"{BaseUrl}/payments?failed={Uri.EscapeDataString(payment.InvoiceNumber)}",
            PreferredChannel = preferredChannel,
            Lifetime = TimeSpan.FromHours(_config.GetValue<int?>("PaymentGateway:InvoiceLifetimeHours") ?? 24)
        };

        var result = await provider.CreateChargeAsync(request, ct);
        if (!result.Success) return (false, result.Message, payment);

        payment.Gateway = provider.Key;
        payment.GatewayReference = result.Reference;
        payment.PaymentUrl = result.PaymentUrl;
        payment.GatewayStatus = result.RawStatus;
        payment.PaymentUrlExpiresAt = result.ExpiresAt;
        payment.LastSyncedAt = DateTime.UtcNow;

        // Metode dicatat agar laporan keuangan tetap konsisten dengan kanal yang dipakai.
        payment.Method = provider.Key switch
        {
            PaymentGatewayProvider.Stripe => PaymentMethod.CreditCard,
            PaymentGatewayProvider.Midtrans or PaymentGatewayProvider.Xendit => PaymentMethod.EWallet,
            _ => PaymentMethod.BankTransfer
        };

        await _db.SaveChangesAsync(ct);

        var note = provider.Key == requested
            ? result.Message
            : $"{result.Message} Provider {requested} belum aktif, jadi dipakai pembayaran manual.";

        return (true, note, payment);
    }

    /// <summary>Petunjuk transfer manual untuk sebuah tagihan.</summary>
    public async Task<IReadOnlyList<string>> GetManualInstructionsAsync(Payment payment, CancellationToken ct = default)
    {
        var manual = _providers[PaymentGatewayProvider.Manual];
        var result = await manual.CreateChargeAsync(new PaymentChargeRequest
        {
            InvoiceNumber = payment.InvoiceNumber,
            Amount = payment.Amount,
            Description = payment.Description ?? "Pembayaran membership"
        }, ct);
        return result.Instructions;
    }

    // ==================== MENYAMAKAN STATUS ====================

    /// <summary>Menanyakan status terkini ke provider lalu menyimpannya.</summary>
    public async Task<(bool ok, string message)> SyncStatusAsync(int paymentId, CancellationToken ct = default)
    {
        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (payment == null) return (false, "Tagihan tidak ditemukan.");

        if (payment.Gateway == PaymentGatewayProvider.Manual)
            return (false, "Pembayaran manual diverifikasi admin, bukan lewat provider.");

        if (string.IsNullOrWhiteSpace(payment.GatewayReference))
            return (false, "Tagihan ini belum pernah dikirim ke provider.");

        if (!_providers.TryGetValue(payment.Gateway, out var provider) || !provider.IsConfigured)
            return (false, $"Provider {payment.Gateway} sedang tidak aktif.");

        var status = await provider.GetStatusAsync(payment.GatewayReference, ct);
        if (!status.Found) return (false, status.Message);

        var changed = await ApplyStatusAsync(payment, status.Status, status.RawStatus, status.Channel, ct);

        return (true, changed
            ? $"Status diperbarui menjadi {payment.Status}."
            : $"Status belum berubah — masih {payment.Status}.");
    }

    // ==================== CALLBACK PROVIDER ====================

    /// <summary>
    /// Memproses callback dari provider. Dipanggil endpoint webhook.
    /// Mengembalikan kode HTTP yang pantas agar provider tahu perlu mengulang atau tidak.
    /// </summary>
    public async Task<(int statusCode, string message)> ProcessWebhookAsync(
        string providerKey,
        string body,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<PaymentGatewayProvider>(providerKey, ignoreCase: true, out var key) ||
            !_providers.TryGetValue(key, out var provider))
        {
            return (404, $"Provider '{providerKey}' tidak dikenal.");
        }

        var result = await provider.HandleWebhookAsync(new PaymentWebhookContext
        {
            Body = body,
            Headers = headers
        }, ct);

        if (!result.Verified)
        {
            _logger.LogWarning("Callback {Provider} ditolak: {Message}", key, result.Message);
            return (401, result.Message);
        }

        if (result.Ignored) return (200, result.Message);

        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.InvoiceNumber == result.InvoiceNumber, ct);

        if (payment == null)
        {
            _logger.LogWarning("Callback {Provider} menyebut invoice {Invoice} yang tidak ada.", key, result.InvoiceNumber);
            // 200 supaya provider berhenti mengulang callback untuk invoice yang memang tidak ada.
            return (200, $"Invoice {result.InvoiceNumber} tidak ditemukan, callback diabaikan.");
        }

        if (!string.IsNullOrWhiteSpace(result.Reference)) payment.GatewayReference = result.Reference;
        payment.Gateway = key;

        await ApplyStatusAsync(payment, result.Status, result.RawStatus, result.Channel, ct);

        _logger.LogInformation("Callback {Provider} menetapkan {Invoice} menjadi {Status}.",
            key, payment.InvoiceNumber, payment.Status);

        return (200, $"Invoice {payment.InvoiceNumber} diperbarui menjadi {payment.Status}.");
    }

    // ==================== PENYIMPANAN STATUS ====================

    /// <summary>
    /// Menerapkan status dari provider ke entitas Payment.
    /// Tagihan yang sudah lunas tidak diturunkan lagi oleh callback yang datang terlambat.
    /// </summary>
    private async Task<bool> ApplyStatusAsync(
        Payment payment,
        PaymentStatus incoming,
        string? rawStatus,
        string? channel,
        CancellationToken ct)
    {
        payment.GatewayStatus = rawStatus;
        payment.LastSyncedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(channel)) payment.PaymentChannel = channel;

        var wasCompleted = payment.Status == PaymentStatus.Completed;
        var changed = false;

        // Lunas hanya boleh berubah menjadi Refunded.
        if (wasCompleted && incoming != PaymentStatus.Refunded)
        {
            await _db.SaveChangesAsync(ct);
            return false;
        }

        if (payment.Status != incoming)
        {
            payment.Status = incoming;
            changed = true;

            if (incoming == PaymentStatus.Completed)
            {
                payment.PaidAt = DateTime.UtcNow;
                payment.TransactionId ??= payment.GatewayReference;
            }
        }

        await _db.SaveChangesAsync(ct);

        if (changed && incoming == PaymentStatus.Completed && !wasCompleted)
            await NotifyPaidAsync(payment, ct);

        return changed;
    }

    private async Task NotifyPaidAsync(Payment payment, CancellationToken ct)
    {
        try
        {
            await _notifications.SendAsync(
                payment.UserId,
                "Pembayaran diterima",
                $"Invoice {payment.InvoiceNumber} sebesar Rp {payment.Amount:N0} sudah lunas lewat {payment.Gateway}.",
                NotificationType.PaymentReminder,
                "/payments");
        }
        catch (Exception ex)
        {
            // Notifikasi gagal tidak boleh membatalkan pembayaran yang sudah tercatat lunas.
            _logger.LogWarning(ex, "Gagal mengirim notifikasi lunas untuk {Invoice}", payment.InvoiceNumber);
        }
    }
}
