namespace PadelHub.Models;

/// <summary>
/// Kunci provider pembayaran. Nilainya dipakai apa adanya sebagai nama seksi
/// di appsettings.json ("Payments:Providers:Xendit") dan disimpan di kolom
/// Payment.Provider.
/// </summary>
public static class PaymentProviders
{
    public const string Manual = "Manual";
    public const string Xendit = "Xendit";
    public const string Midtrans = "Midtrans";
}

/// <summary>
/// Status pembayaran.
///
/// Catatan historis: data lama memakai "Success" untuk pembayaran lunas,
/// sedangkan alur kasir memakai "Confirmed". "Confirmed" adalah nilai kanonik;
/// gunakan <see cref="IsPaid"/> saat membaca supaya keduanya ikut terhitung.
/// </summary>
public static class PaymentStatuses
{
    public const string Pending = "Pending";
    public const string Paid = "Confirmed";
    public const string Cancelled = "Cancelled";
    public const string Expired = "Expired";
    public const string Failed = "Failed";
    public const string Refunded = "Refunded";

    /// <summary>Nilai lama yang tetap dihitung sebagai lunas.</summary>
    public const string LegacyPaid = "Success";

    public static bool IsPaid(string? status) =>
        string.Equals(status, Paid, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, LegacyPaid, StringComparison.OrdinalIgnoreCase);

    public static bool IsOpen(string? status) =>
        string.Equals(status, Pending, StringComparison.OrdinalIgnoreCase);
}
