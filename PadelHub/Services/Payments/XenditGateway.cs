using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PadelHub.Models;

namespace PadelHub.Services.Payments;

/// <summary>
/// Xendit lewat Invoice API (https://api.xendit.co/v2/invoices).
///
/// Autentikasi memakai HTTP Basic dengan secret key sebagai username dan
/// password kosong. Notifikasi diverifikasi lewat header "x-callback-token"
/// yang harus sama persis dengan token di dashboard Xendit.
/// </summary>
public class XenditGateway : IPaymentGateway
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<XenditGateway> _logger;

    public XenditGateway(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<XenditGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public string Key => PaymentProviders.Xendit;
    public string DisplayName => "Xendit";
    public string Description => "Virtual account, e-wallet (OVO, DANA, LinkAja), QRIS, kartu kredit, dan retail.";
    public string Mark => "XD";
    public bool RedirectsToProvider => true;

    private IConfigurationSection Section => _config.GetSection("Payments:Providers:Xendit");
    private string SecretKey => Section["SecretKey"] ?? "";
    private string CallbackToken => Section["CallbackToken"] ?? "";
    private string BaseUrl => (Section["BaseUrl"] ?? "https://api.xendit.co").TrimEnd('/');

    public bool IsEnabled => Section.GetValue("Enabled", false) && !string.IsNullOrWhiteSpace(SecretKey);

    public bool IsSandbox => SecretKey.StartsWith("xnd_development", StringComparison.OrdinalIgnoreCase);

    public async Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
            return CheckoutSession.Failed("Xendit belum aktif. Isi secret key di appsettings.json.");

        var payload = new Dictionary<string, object?>
        {
            ["external_id"] = request.ExternalId,
            ["amount"] = decimal.Round(request.Amount, 0, MidpointRounding.AwayFromZero),
            ["description"] = request.Description,
            ["currency"] = "IDR",
            ["invoice_duration"] = Section.GetValue("InvoiceDurationSeconds", 86400),
            ["success_redirect_url"] = request.SuccessUrl,
            ["failure_redirect_url"] = request.FailureUrl,
        };

        if (!string.IsNullOrWhiteSpace(request.CustomerEmail))
            payload["payer_email"] = request.CustomerEmail;

        var customer = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(request.CustomerName)) customer["given_names"] = request.CustomerName;
        if (!string.IsNullOrWhiteSpace(request.CustomerEmail)) customer["email"] = request.CustomerEmail;
        if (!string.IsNullOrWhiteSpace(request.CustomerPhone)) customer["mobile_number"] = request.CustomerPhone;
        if (customer.Count > 0) payload["customer"] = customer;

        try
        {
            var http = _httpClientFactory.CreateClient("Xendit");
            using var message = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v2/invoices")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            message.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{SecretKey}:")));

            var response = await http.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Xendit menolak pembuatan invoice {ExternalId}: {Status} {Body}",
                    request.ExternalId, (int)response.StatusCode, body);
                return CheckoutSession.Failed($"Xendit menolak permintaan ({(int)response.StatusCode}). {ReadErrorMessage(body)}");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            return new CheckoutSession
            {
                Success = true,
                CheckoutUrl = root.TryGetProperty("invoice_url", out var url) ? url.GetString() : null,
                ProviderReference = root.TryGetProperty("id", out var id) ? id.GetString() : null,
                ExpiresAt = root.TryGetProperty("expiry_date", out var exp) && exp.TryGetDateTime(out var expiry)
                    ? expiry.ToUniversalTime()
                    : null,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gagal menghubungi Xendit untuk {ExternalId}", request.ExternalId);
            return CheckoutSession.Failed("Tidak bisa menghubungi Xendit. Coba lagi beberapa saat.");
        }
    }

    public GatewayCallback ParseCallback(string requestBody, IHeaderDictionary headers)
    {
        var token = headers["x-callback-token"].ToString();
        if (string.IsNullOrWhiteSpace(CallbackToken) || !FixedTimeEquals(token, CallbackToken))
        {
            _logger.LogWarning("Notifikasi Xendit ditolak: callback token tidak cocok.");
            return GatewayCallback.Invalid("Callback token tidak cocok.");
        }

        try
        {
            using var doc = JsonDocument.Parse(requestBody);
            var root = doc.RootElement;

            var status = root.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";

            return new GatewayCallback
            {
                Valid = true,
                ExternalId = root.TryGetProperty("external_id", out var ext) ? ext.GetString() : null,
                ProviderReference = root.TryGetProperty("id", out var id) ? id.GetString() : null,
                Status = MapStatus(status),
                Amount = root.TryGetProperty("paid_amount", out var paid) && paid.TryGetDecimal(out var amount)
                    ? amount
                    : root.TryGetProperty("amount", out var amt) && amt.TryGetDecimal(out var fallback) ? fallback : null,
                Method = root.TryGetProperty("payment_channel", out var channel) ? channel.GetString()
                    : root.TryGetProperty("payment_method", out var method) ? method.GetString() : null,
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Notifikasi Xendit bukan JSON yang valid.");
            return GatewayCallback.Invalid("Isi notifikasi tidak bisa dibaca.");
        }
    }

    /// <summary>PAID/SETTLED = lunas, EXPIRED = kedaluwarsa, sisanya tetap menunggu.</summary>
    private static string MapStatus(string xenditStatus) => xenditStatus.ToUpperInvariant() switch
    {
        "PAID" or "SETTLED" => PaymentStatuses.Paid,
        "EXPIRED" => PaymentStatuses.Expired,
        _ => PaymentStatuses.Pending,
    };

    private static string ReadErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
        }
        catch (JsonException)
        {
            return "";
        }
    }

    /// <summary>Perbandingan waktu-tetap supaya token tidak bisa ditebak lewat timing.</summary>
    private static bool FixedTimeEquals(string a, string b)
    {
        var left = Encoding.UTF8.GetBytes(a);
        var right = Encoding.UTF8.GetBytes(b);
        return left.Length == right.Length &&
               System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(left, right);
    }
}
