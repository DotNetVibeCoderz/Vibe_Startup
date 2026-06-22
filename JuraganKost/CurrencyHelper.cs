using System.Globalization;

namespace JuraganKost;

/// <summary>
/// Helper untuk formatting Rupiah dan culture Indonesia.
/// </summary>
public static class CurrencyHelper
{
    private static readonly CultureInfo _idCulture = new("id-ID");

    /// <summary>Format decimal sebagai Rupiah: Rp1.500.000</summary>
    public static string ToRupiah(this decimal amount) => amount.ToString("C0", _idCulture);

    /// <summary>Format decimal? sebagai Rupiah, return "-" jika null</summary>
    public static string ToRupiah(this decimal? amount) => amount.HasValue ? amount.Value.ToString("C0", _idCulture) : "-";

    /// <summary>Format double sebagai Rupiah</summary>
    public static string ToRupiah(this double amount) => ((decimal)amount).ToString("C0", _idCulture);
}
