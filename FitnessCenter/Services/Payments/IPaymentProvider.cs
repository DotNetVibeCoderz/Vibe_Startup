using FitnessCenter.Models;

namespace FitnessCenter.Services.Payments;

/// <summary>
/// Kontrak satu payment gateway. Mengikuti pola yang sama dengan IStorageProvider:
/// implementasi dipilih lewat konfigurasi, dan aplikasi hanya bicara ke antarmuka ini.
///
/// Implementasi tidak menyentuh database — seluruh penyimpanan ditangani
/// <see cref="PaymentGatewayService"/>.
/// </summary>
public interface IPaymentProvider
{
    PaymentGatewayProvider Key { get; }

    /// <summary>Nama yang dibaca pengguna, misal "Midtrans Snap".</summary>
    string DisplayName { get; }

    /// <summary>Satu kalimat penjelas untuk pemilih metode bayar.</summary>
    string Description { get; }

    /// <summary>Kunci API sudah terisi sehingga provider siap dipakai.</summary>
    bool IsConfigured { get; }

    /// <summary>Pembayar diarahkan ke halaman milik provider.</summary>
    bool IsRedirectBased { get; }

    /// <summary>Metode bayar yang ditawarkan, untuk ditampilkan sebagai keterangan.</summary>
    IReadOnlyList<string> Channels { get; }

    /// <summary>Yang perlu diisi di appsettings.json bila provider belum aktif.</summary>
    string SetupHint { get; }

    /// <summary>Membuat tagihan di sisi provider dan mengembalikan halaman bayarnya.</summary>
    Task<PaymentChargeResult> CreateChargeAsync(PaymentChargeRequest request, CancellationToken ct = default);

    /// <summary>Menanyakan status transaksi ke provider.</summary>
    Task<PaymentStatusResult> GetStatusAsync(string reference, CancellationToken ct = default);

    /// <summary>Memverifikasi dan membaca callback dari provider.</summary>
    Task<PaymentWebhookResult> HandleWebhookAsync(PaymentWebhookContext context, CancellationToken ct = default);
}
