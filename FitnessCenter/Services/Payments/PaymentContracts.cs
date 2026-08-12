using FitnessCenter.Models;

namespace FitnessCenter.Services.Payments;

/// <summary>
/// Permintaan tagih ke payment gateway. Dibuat oleh <see cref="PaymentGatewayService"/>
/// dari entitas Payment, sehingga provider tidak perlu menyentuh database.
/// </summary>
public record PaymentChargeRequest
{
    public required string InvoiceNumber { get; init; }
    public required decimal Amount { get; init; }
    public string Description { get; init; } = "Pembayaran FitnessCenter";

    public string CustomerName { get; init; } = "Member";
    public string? CustomerEmail { get; init; }
    public string? CustomerPhone { get; init; }

    /// <summary>Alamat kembali setelah pembayar selesai di halaman provider.</summary>
    public string? SuccessUrl { get; init; }
    public string? FailureUrl { get; init; }

    /// <summary>Kanal yang diminta pembayar, misal "gopay". Diabaikan jika provider tidak mendukung.</summary>
    public string? PreferredChannel { get; init; }

    /// <summary>Berapa lama halaman bayar berlaku.</summary>
    public TimeSpan Lifetime { get; init; } = TimeSpan.FromHours(24);
}

/// <summary>Hasil pembuatan tagihan di provider.</summary>
public record PaymentChargeResult
{
    public bool Success { get; init; }

    /// <summary>ID transaksi di sisi provider.</summary>
    public string? Reference { get; init; }

    /// <summary>Halaman bayar. Kosong untuk provider manual.</summary>
    public string? PaymentUrl { get; init; }

    public DateTime? ExpiresAt { get; init; }
    public string? RawStatus { get; init; }

    /// <summary>Pesan siap tampil untuk pengguna.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Petunjuk pembayaran manual — hanya diisi provider Manual.</summary>
    public IReadOnlyList<string> Instructions { get; init; } = Array.Empty<string>();

    public static PaymentChargeResult Fail(string message) => new() { Success = false, Message = message };
}

/// <summary>Status terkini sebuah transaksi menurut provider.</summary>
public record PaymentStatusResult
{
    public bool Found { get; init; }
    public PaymentStatus Status { get; init; } = PaymentStatus.Pending;
    public string? RawStatus { get; init; }
    public string? Channel { get; init; }
    public string Message { get; init; } = string.Empty;

    public static PaymentStatusResult NotFound(string message) => new() { Found = false, Message = message };
}

/// <summary>Isi callback mentah dari provider, sebelum diverifikasi.</summary>
public record PaymentWebhookContext
{
    public required string Body { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public string? Header(string name) =>
        Headers.TryGetValue(name, out var value) ? value : null;
}

/// <summary>Hasil pembacaan callback: sudah diverifikasi tanda tangannya atau ditolak.</summary>
public record PaymentWebhookResult
{
    /// <summary>Tanda tangan atau token callback cocok.</summary>
    public bool Verified { get; init; }

    /// <summary>Nomor invoice milik aplikasi, dipakai untuk mencari Payment.</summary>
    public string? InvoiceNumber { get; init; }

    public string? Reference { get; init; }
    public PaymentStatus Status { get; init; } = PaymentStatus.Pending;
    public string? RawStatus { get; init; }
    public string? Channel { get; init; }
    public string Message { get; init; } = string.Empty;

    /// <summary>Callback sah tapi tidak mengubah status (misal event yang tidak dipakai).</summary>
    public bool Ignored { get; init; }

    public static PaymentWebhookResult Reject(string message) => new() { Verified = false, Message = message };
    public static PaymentWebhookResult Ignore(string message) => new() { Verified = true, Ignored = true, Message = message };
}

/// <summary>Ringkasan provider untuk ditampilkan di UI.</summary>
public record PaymentProviderInfo
{
    public required PaymentGatewayProvider Key { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public bool IsConfigured { get; init; }
    public bool IsRedirectBased { get; init; }
    public bool IsDefault { get; init; }
    public IReadOnlyList<string> Channels { get; init; } = Array.Empty<string>();

    /// <summary>Apa yang perlu diisi di appsettings.json agar provider ini hidup.</summary>
    public string SetupHint { get; init; } = string.Empty;
}
