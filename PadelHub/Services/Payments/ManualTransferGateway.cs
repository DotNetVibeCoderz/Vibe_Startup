using PadelHub.Models;

namespace PadelHub.Services.Payments;

/// <summary>
/// Transfer bank manual: tidak ada panggilan ke pihak ketiga. Pengguna melihat
/// nomor rekening klub, lalu operator memverifikasi pembayaran di halaman
/// Keuangan. Ini juga jaring pengaman ketika provider lain belum dikonfigurasi.
/// </summary>
public class ManualTransferGateway : IPaymentGateway
{
    private readonly IConfiguration _config;

    public ManualTransferGateway(IConfiguration config) => _config = config;

    public string Key => PaymentProviders.Manual;
    public string DisplayName => Section["DisplayName"] ?? "Transfer bank manual";
    public string Description => $"Transfer ke {BankName} a.n. {AccountHolder}, lalu tunggu verifikasi operator.";
    public string Mark => "TF";
    public bool RedirectsToProvider => false;
    public bool IsSandbox => false;

    private IConfigurationSection Section => _config.GetSection("Payments:Providers:Manual");

    public bool IsEnabled => Section.GetValue("Enabled", true);

    public string BankName => Section["BankName"] ?? "-";
    public string AccountNumber => Section["AccountNumber"] ?? "-";
    public string AccountHolder => Section["AccountHolder"] ?? "-";

    public Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        // Tidak ada halaman bayar eksternal: instruksi transfer ditampilkan
        // langsung di halaman checkout PadelHub.
        return Task.FromResult(new CheckoutSession
        {
            Success = true,
            CheckoutUrl = null,
            ProviderReference = request.ExternalId,
        });
    }

    public GatewayCallback ParseCallback(string requestBody, IHeaderDictionary headers) =>
        GatewayCallback.Invalid("Transfer manual tidak menerima notifikasi otomatis.");
}
