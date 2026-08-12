using PadelHub.Models;

namespace PadelHub.Services.Payments;

/// <summary>
/// Titik pencarian provider pembayaran. Halaman dan endpoint webhook memakai
/// registry ini, bukan implementasi konkret, sehingga provider baru cukup
/// didaftarkan sekali di Program.cs.
/// </summary>
public class PaymentGatewayRegistry
{
    private readonly IReadOnlyList<IPaymentGateway> _gateways;
    private readonly IConfiguration _config;

    public PaymentGatewayRegistry(IEnumerable<IPaymentGateway> gateways, IConfiguration config)
    {
        _gateways = gateways.ToList();
        _config = config;
    }

    /// <summary>Semua provider yang terpasang, termasuk yang belum dikonfigurasi.</summary>
    public IReadOnlyList<IPaymentGateway> All => _gateways;

    /// <summary>Provider yang aktif dan kredensialnya lengkap.</summary>
    public IReadOnlyList<IPaymentGateway> Enabled => _gateways.Where(g => g.IsEnabled).ToList();

    public IPaymentGateway? Find(string? key) =>
        string.IsNullOrWhiteSpace(key)
            ? null
            : _gateways.FirstOrDefault(g => string.Equals(g.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Provider bawaan: dari "Payments:DefaultProvider" jika aktif, kalau tidak
    /// provider aktif pertama, dan pada akhirnya transfer manual.
    /// </summary>
    public IPaymentGateway Default
    {
        get
        {
            var configured = Find(_config["Payments:DefaultProvider"]);
            if (configured is { IsEnabled: true }) return configured;

            return Enabled.FirstOrDefault()
                ?? Find(PaymentProviders.Manual)
                ?? _gateways.First();
        }
    }

    /// <summary>
    /// Alamat publik aplikasi untuk URL kembali dan webhook. Diambil dari
    /// "Payments:BaseUrl" bila diisi (wajib saat di balik tunnel/reverse proxy),
    /// selain itu memakai host permintaan saat ini.
    /// </summary>
    public string ResolveBaseUrl(string requestBaseUrl)
    {
        var configured = _config["Payments:BaseUrl"];
        return string.IsNullOrWhiteSpace(configured) ? requestBaseUrl.TrimEnd('/') : configured.TrimEnd('/');
    }
}
