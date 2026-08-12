using PadelHub.Models;

namespace PadelHub.Services.Payments;

/// <summary>
/// Permintaan pembuatan sesi pembayaran ke provider.
/// </summary>
public record CheckoutRequest
{
    public required string ExternalId { get; init; }
    public required decimal Amount { get; init; }
    public required string Description { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerEmail { get; init; }
    public string? CustomerPhone { get; init; }

    /// <summary>URL tujuan setelah pembayaran berhasil.</summary>
    public required string SuccessUrl { get; init; }

    /// <summary>URL tujuan setelah pembayaran gagal atau dibatalkan.</summary>
    public required string FailureUrl { get; init; }
}

/// <summary>
/// Hasil pembuatan sesi pembayaran.
/// </summary>
public record CheckoutSession
{
    public bool Success { get; init; }

    /// <summary>Halaman bayar provider. Kosong untuk transfer manual.</summary>
    public string? CheckoutUrl { get; init; }

    public string? ProviderReference { get; init; }
    public DateTime? ExpiresAt { get; init; }

    /// <summary>Pesan siap tampil untuk pengguna saat gagal.</summary>
    public string? Error { get; init; }

    public static CheckoutSession Failed(string error) => new() { Success = false, Error = error };
}

/// <summary>
/// Notifikasi (webhook) yang sudah diverifikasi dan diterjemahkan ke istilah PadelHub.
/// </summary>
public record GatewayCallback
{
    public bool Valid { get; init; }
    public string? ExternalId { get; init; }
    public string? ProviderReference { get; init; }

    /// <summary>Status hasil pemetaan; lihat <see cref="PaymentStatuses"/>.</summary>
    public string Status { get; init; } = PaymentStatuses.Pending;

    public decimal? Amount { get; init; }

    /// <summary>Kanal pembayaran yang dipakai pembeli, misal "BCA_VA" atau "gopay".</summary>
    public string? Method { get; init; }

    public string? Error { get; init; }

    public static GatewayCallback Invalid(string error) => new() { Valid = false, Error = error };
}

/// <summary>
/// Kontrak satu provider pembayaran. Menambah provider baru cukup dengan
/// membuat implementasi baru lalu mendaftarkannya di Program.cs — pemanggil
/// (halaman checkout, webhook, PaymentService) tidak perlu berubah.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Kunci unik, sama dengan nama seksi di appsettings.json.</summary>
    string Key { get; }

    /// <summary>Nama yang dilihat pengguna.</summary>
    string DisplayName { get; }

    /// <summary>Penjelasan singkat metode yang tersedia.</summary>
    string Description { get; }

    /// <summary>Inisial 2–3 huruf untuk penanda visual.</summary>
    string Mark { get; }

    /// <summary>Aktif di appsettings dan kredensialnya lengkap.</summary>
    bool IsEnabled { get; }

    /// <summary>Berjalan di lingkungan uji coba provider.</summary>
    bool IsSandbox { get; }

    /// <summary>Provider mengarahkan pengguna ke halaman bayar miliknya.</summary>
    bool RedirectsToProvider { get; }

    Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Memverifikasi keaslian notifikasi lalu menerjemahkannya. Implementasi
    /// wajib menolak payload yang tidak lolos verifikasi tanda tangan/token.
    /// </summary>
    GatewayCallback ParseCallback(string requestBody, IHeaderDictionary headers);
}
