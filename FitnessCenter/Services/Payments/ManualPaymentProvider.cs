using FitnessCenter.Models;

namespace FitnessCenter.Services.Payments;

/// <summary>
/// Pembayaran manual: transfer bank atau tunai di resepsionis, lalu diverifikasi admin.
/// Ini alur bawaan aplikasi dan selalu tersedia, termasuk saat semua gateway mati.
/// </summary>
public class ManualPaymentProvider : IPaymentProvider
{
    private readonly IConfiguration _config;

    public ManualPaymentProvider(IConfiguration config) => _config = config;

    public PaymentGatewayProvider Key => PaymentGatewayProvider.Manual;
    public string DisplayName => "Transfer manual";
    public string Description => "Transfer ke rekening gym atau bayar tunai di resepsionis, lalu dikonfirmasi admin.";
    public bool IsConfigured => true;
    public bool IsRedirectBased => false;
    public string SetupHint => "Isi PaymentGateway:Manual (BankName, AccountNumber, AccountHolder).";

    public IReadOnlyList<string> Channels { get; } = new[] { "Transfer bank", "Tunai di tempat", "QRIS statis" };

    private string Bank => _config.GetValue<string>("PaymentGateway:Manual:BankName") ?? "BCA";
    private string Account => _config.GetValue<string>("PaymentGateway:Manual:AccountNumber") ?? "—";
    private string Holder => _config.GetValue<string>("PaymentGateway:Manual:AccountHolder") ?? "FitnessCenter";

    public Task<PaymentChargeResult> CreateChargeAsync(PaymentChargeRequest request, CancellationToken ct = default)
    {
        var instructions = new List<string>
        {
            $"Transfer Rp {request.Amount:N0} ke {Bank} {Account} a.n. {Holder}.",
            $"Cantumkan nomor invoice {request.InvoiceNumber} pada berita transfer.",
            "Klik \"Saya sudah bayar\" dan isi nomor referensi transfer.",
            "Admin memverifikasi paling lambat 1×24 jam kerja."
        };

        var extra = _config.GetValue<string>("PaymentGateway:Manual:Instructions");
        if (!string.IsNullOrWhiteSpace(extra)) instructions.Add(extra);

        return Task.FromResult(new PaymentChargeResult
        {
            Success = true,
            Reference = request.InvoiceNumber,
            PaymentUrl = null,
            RawStatus = "manual_pending",
            ExpiresAt = DateTime.UtcNow.Add(request.Lifetime),
            Message = "Tagihan siap dibayar lewat transfer manual.",
            Instructions = instructions
        });
    }

    /// <summary>Tidak ada sistem luar yang bisa ditanya — status ditentukan verifikasi admin.</summary>
    public Task<PaymentStatusResult> GetStatusAsync(string reference, CancellationToken ct = default) =>
        Task.FromResult(new PaymentStatusResult
        {
            Found = true,
            Status = PaymentStatus.Pending,
            RawStatus = "manual_pending",
            Message = "Pembayaran manual menunggu verifikasi admin."
        });

    public Task<PaymentWebhookResult> HandleWebhookAsync(PaymentWebhookContext context, CancellationToken ct = default) =>
        Task.FromResult(PaymentWebhookResult.Reject("Provider manual tidak menerima callback."));
}
