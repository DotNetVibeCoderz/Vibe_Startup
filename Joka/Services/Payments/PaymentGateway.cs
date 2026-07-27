// Payment gateway integration (F-1).
//
// The important design point: the gateway is chosen at runtime from
// Payment:DefaultGateway, but a gateway with no credentials is never selected.
// StubGateway takes over instead and settles instantly, which is exactly what
// the app did before this file existed - so a fresh clone still works end to end
// without anyone signing up for Midtrans.
//
// Money is only ever marked paid from two places: StubGateway (demo) or a
// webhook signed by the provider (Program.cs). The browser never gets to tell us
// a payment succeeded, because a customer can edit whatever the browser posts.
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Joka.Services.Payments;

public record ChargeRequest(
    string OrderId,
    decimal Amount,
    string PaymentMethod,
    string? CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string ItemName,
    string ReturnUrl);

/// <param name="Settled">
/// True only when the money is already ours - the stub gateway. A real gateway
/// returns false here and settles later through its webhook.
/// </param>
public record ChargeResult(
    bool Success,
    string Message,
    string? GatewayTransactionId = null,
    string? PaymentUrl = null,
    bool Settled = false);

public interface IPaymentGateway
{
    /// <summary>Provider name written to PaymentTransaction.PaymentGateway.</summary>
    string Name { get; }

    /// <summary>False when the keys are missing, which keeps it out of the factory.</summary>
    bool IsConfigured { get; }

    Task<ChargeResult> CreateChargeAsync(ChargeRequest request, CancellationToken ct = default);

    /// <summary>Asks the provider what really happened. Returns null if unknown.</summary>
    Task<string?> GetStatusAsync(string orderId, CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
// Stub - no external call, settles immediately
// ---------------------------------------------------------------------------
public class StubGateway : IPaymentGateway
{
    public string Name => "Simulasi";
    public bool IsConfigured => true;

    public Task<ChargeResult> CreateChargeAsync(ChargeRequest request, CancellationToken ct = default) =>
        Task.FromResult(new ChargeResult(
            true,
            "Pembayaran disimulasikan - tidak ada gateway sungguhan yang dikonfigurasi.",
            GatewayTransactionId: $"SIM-{request.OrderId}",
            PaymentUrl: null,
            Settled: true));

    public Task<string?> GetStatusAsync(string orderId, CancellationToken ct = default) =>
        Task.FromResult<string?>("Completed");
}

// ---------------------------------------------------------------------------
// Midtrans Snap
// ---------------------------------------------------------------------------
public class MidtransGateway : IPaymentGateway
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly ILogger<MidtransGateway> _log;

    public MidtransGateway(IHttpClientFactory http, IConfiguration config, ILogger<MidtransGateway> log)
    {
        _http = http;
        _config = config;
        _log = log;
    }

    public string Name => "Midtrans";

    private string ServerKey => _config["Payment:Gateways:Midtrans:ServerKey"] ?? "";

    private bool IsProduction =>
        bool.TryParse(_config["Payment:Gateways:Midtrans:IsProduction"], out var p) && p;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ServerKey);

    private string SnapUrl => IsProduction
        ? "https://app.midtrans.com/snap/v1/transactions"
        : "https://app.sandbox.midtrans.com/snap/v1/transactions";

    private string ApiBase => IsProduction
        ? "https://api.midtrans.com"
        : "https://api.sandbox.midtrans.com";

    /// <summary>Midtrans uses HTTP Basic with the server key as username and an empty password.</summary>
    private AuthenticationHeaderValue AuthHeader() =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ServerKey}:")));

    public async Task<ChargeResult> CreateChargeAsync(ChargeRequest request, CancellationToken ct = default)
    {
        try
        {
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            var payload = new
            {
                transaction_details = new
                {
                    order_id = request.OrderId,
                    // Snap only accepts whole rupiah.
                    gross_amount = (long)Math.Round(request.Amount, MidpointRounding.AwayFromZero)
                },
                item_details = new[]
                {
                    new
                    {
                        id = request.OrderId,
                        price = (long)Math.Round(request.Amount, MidpointRounding.AwayFromZero),
                        quantity = 1,
                        name = Truncate(request.ItemName, 50)
                    }
                },
                customer_details = new
                {
                    first_name = request.CustomerName ?? "Pelanggan Joka",
                    email = request.CustomerEmail,
                    phone = request.CustomerPhone
                },
                callbacks = new { finish = request.ReturnUrl },
                enabled_payments = EnabledPayments(request.PaymentMethod)
            };

            using var message = new HttpRequestMessage(HttpMethod.Post, SnapUrl)
            {
                Content = JsonContent.Create(payload)
            };
            message.Headers.Authorization = AuthHeader();
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await client.SendAsync(message, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("Midtrans menolak charge {OrderId}: {Status} {Body}",
                    request.OrderId, (int)response.StatusCode, body);
                return new(false, $"Midtrans menolak transaksi ({(int)response.StatusCode}).");
            }

            using var json = JsonDocument.Parse(body);
            var token = json.RootElement.TryGetProperty("token", out var t) ? t.GetString() : null;
            var redirect = json.RootElement.TryGetProperty("redirect_url", out var r) ? r.GetString() : null;

            if (string.IsNullOrEmpty(redirect))
                return new(false, "Midtrans tidak mengembalikan redirect_url.");

            return new(true, "Lanjutkan pembayaran di halaman Midtrans.", token, redirect);
        }
        catch (Exception ex)
        {
            // A gateway outage must not take the checkout page down with it.
            _log.LogError(ex, "Gagal menghubungi Midtrans untuk {OrderId}", request.OrderId);
            return new(false, "Tidak bisa menghubungi Midtrans. Coba lagi sebentar.");
        }
    }

    public async Task<string?> GetStatusAsync(string orderId, CancellationToken ct = default)
    {
        try
        {
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            using var message = new HttpRequestMessage(HttpMethod.Get, $"{ApiBase}/v2/{orderId}/status");
            message.Headers.Authorization = AuthHeader();

            var response = await client.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode) return null;

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var status = json.RootElement.TryGetProperty("transaction_status", out var s) ? s.GetString() : null;
            var fraud = json.RootElement.TryGetProperty("fraud_status", out var f) ? f.GetString() : null;

            return MapStatus(status, fraud);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Gagal membaca status Midtrans untuk {OrderId}", orderId);
            return null;
        }
    }

    /// <summary>Midtrans vocabulary translated to the app's PaymentTransaction.Status values.</summary>
    public static string MapStatus(string? transactionStatus, string? fraudStatus) => transactionStatus switch
    {
        "capture" => fraudStatus == "challenge" ? "Processing" : "Completed",
        "settlement" => "Completed",
        "pending" => "Pending",
        "deny" or "cancel" or "expire" or "failure" => "Failed",
        "refund" or "partial_refund" => "Refunded",
        _ => "Pending"
    };

    /// <summary>
    /// Verifies the notification really came from Midtrans:
    /// SHA512(order_id + status_code + gross_amount + ServerKey).
    /// Without this check anyone who knows an order id could mark it paid.
    /// </summary>
    public bool VerifySignature(string orderId, string statusCode, string grossAmount, string signatureKey)
    {
        var raw = $"{orderId}{statusCode}{grossAmount}{ServerKey}";
        var hash = Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(hash),
            Encoding.UTF8.GetBytes(signatureKey.ToLowerInvariant()));
    }

    /// <summary>Narrows the Snap page to the channel the customer already picked.</summary>
    private static string[]? EnabledPayments(string method) => method?.ToLowerInvariant() switch
    {
        "banktransfer" or "transfer" => new[] { "bca_va", "bni_va", "bri_va", "permata_va", "other_va" },
        "ewallet" => new[] { "gopay", "shopeepay" },
        "qris" => new[] { "qris", "gopay" },
        "creditcard" or "card" => new[] { "credit_card" },
        _ => null   // biarkan Midtrans menampilkan semua kanal
    };

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}

// ---------------------------------------------------------------------------
// Xendit Invoice
// ---------------------------------------------------------------------------
public class XenditGateway : IPaymentGateway
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly ILogger<XenditGateway> _log;

    public XenditGateway(IHttpClientFactory http, IConfiguration config, ILogger<XenditGateway> log)
    {
        _http = http;
        _config = config;
        _log = log;
    }

    public string Name => "Xendit";

    private string ApiKey => _config["Payment:Gateways:Xendit:ApiKey"] ?? "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    private const string InvoiceUrl = "https://api.xendit.co/v2/invoices";

    private AuthenticationHeaderValue AuthHeader() =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ApiKey}:")));

    public async Task<ChargeResult> CreateChargeAsync(ChargeRequest request, CancellationToken ct = default)
    {
        try
        {
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            var payload = new
            {
                external_id = request.OrderId,
                amount = Math.Round(request.Amount, 0, MidpointRounding.AwayFromZero),
                currency = "IDR",
                description = request.ItemName,
                payer_email = request.CustomerEmail,
                success_redirect_url = request.ReturnUrl,
                failure_redirect_url = request.ReturnUrl,
                invoice_duration = 3600,
                customer = new
                {
                    given_names = request.CustomerName ?? "Pelanggan Joka",
                    email = request.CustomerEmail,
                    mobile_number = request.CustomerPhone
                }
            };

            using var message = new HttpRequestMessage(HttpMethod.Post, InvoiceUrl)
            {
                Content = JsonContent.Create(payload)
            };
            message.Headers.Authorization = AuthHeader();

            var response = await client.SendAsync(message, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning("Xendit menolak invoice {OrderId}: {Status} {Body}",
                    request.OrderId, (int)response.StatusCode, body);
                return new(false, $"Xendit menolak transaksi ({(int)response.StatusCode}).");
            }

            using var json = JsonDocument.Parse(body);
            var id = json.RootElement.TryGetProperty("id", out var i) ? i.GetString() : null;
            var url = json.RootElement.TryGetProperty("invoice_url", out var u) ? u.GetString() : null;

            if (string.IsNullOrEmpty(url))
                return new(false, "Xendit tidak mengembalikan invoice_url.");

            return new(true, "Lanjutkan pembayaran di halaman Xendit.", id, url);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Gagal menghubungi Xendit untuk {OrderId}", request.OrderId);
            return new(false, "Tidak bisa menghubungi Xendit. Coba lagi sebentar.");
        }
    }

    public async Task<string?> GetStatusAsync(string orderId, CancellationToken ct = default)
    {
        try
        {
            var client = _http.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            using var message = new HttpRequestMessage(
                HttpMethod.Get, $"https://api.xendit.co/v2/invoices?external_id={Uri.EscapeDataString(orderId)}");
            message.Headers.Authorization = AuthHeader();

            var response = await client.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode) return null;

            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() == 0)
                return null;

            var status = json.RootElement[0].TryGetProperty("status", out var s) ? s.GetString() : null;
            return MapStatus(status);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Gagal membaca status Xendit untuk {OrderId}", orderId);
            return null;
        }
    }

    public static string MapStatus(string? status) => status?.ToUpperInvariant() switch
    {
        "PAID" or "SETTLED" => "Completed",
        "PENDING" => "Pending",
        "EXPIRED" => "Failed",
        _ => "Pending"
    };

    /// <summary>
    /// Xendit sends a static token in the x-callback-token header rather than a
    /// per-message signature. Compared in fixed time all the same.
    /// </summary>
    public bool VerifyCallbackToken(string? provided)
    {
        var expected = _config["Payment:Gateways:Xendit:CallbackToken"] ?? "";

        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(provided))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(provided));
    }
}

// ---------------------------------------------------------------------------
// Factory
// ---------------------------------------------------------------------------
public class PaymentGatewayFactory
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<PaymentGatewayFactory> _log;

    public PaymentGatewayFactory(IServiceProvider services, IConfiguration config, ILogger<PaymentGatewayFactory> log)
    {
        _services = services;
        _config = config;
        _log = log;
    }

    /// <summary>
    /// Returns the configured gateway, or the stub when its credentials are
    /// missing. Never returns a half-configured real gateway - that would fail
    /// on the customer's screen instead of here.
    /// </summary>
    public IPaymentGateway Create()
    {
        var requested = _config["Payment:DefaultGateway"] ?? "Midtrans";

        IPaymentGateway? gateway = requested.ToLowerInvariant() switch
        {
            "midtrans" => _services.GetRequiredService<MidtransGateway>(),
            "xendit" => _services.GetRequiredService<XenditGateway>(),
            "simulasi" or "stub" or "none" => _services.GetRequiredService<StubGateway>(),
            _ => null
        };

        if (gateway is null)
        {
            _log.LogWarning("Payment:DefaultGateway '{Requested}' tidak dikenal, memakai simulasi.", requested);
            return _services.GetRequiredService<StubGateway>();
        }

        if (!gateway.IsConfigured)
        {
            _log.LogInformation("{Gateway} belum punya kredensial, memakai simulasi.", gateway.Name);
            return _services.GetRequiredService<StubGateway>();
        }

        return gateway;
    }

    /// <summary>What the admin Settings screen shows, without leaking the keys.</summary>
    public string ActiveProvider => Create().Name;

    public bool IsLive => Create() is not StubGateway;
}
