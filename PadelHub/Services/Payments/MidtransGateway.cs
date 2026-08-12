using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PadelHub.Models;

namespace PadelHub.Services.Payments;

/// <summary>
/// Midtrans lewat Snap API (/snap/v1/transactions).
///
/// Autentikasi memakai HTTP Basic dengan server key sebagai username dan
/// password kosong. Notifikasi diverifikasi dengan signature_key:
/// SHA-512(order_id + status_code + gross_amount + ServerKey).
/// </summary>
public class MidtransGateway : IPaymentGateway
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<MidtransGateway> _logger;

    public MidtransGateway(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<MidtransGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
    }

    public string Key => PaymentProviders.Midtrans;
    public string DisplayName => "Midtrans";
    public string Description => "GoPay, QRIS, virtual account, kartu kredit, Akulaku, dan gerai retail.";
    public string Mark => "MT";
    public bool RedirectsToProvider => true;

    private IConfigurationSection Section => _config.GetSection("Payments:Providers:Midtrans");
    private string ServerKey => Section["ServerKey"] ?? "";
    private bool IsProduction => Section.GetValue("IsProduction", false);

    private string SnapBaseUrl => IsProduction
        ? "https://app.midtrans.com"
        : "https://app.sandbox.midtrans.com";

    public bool IsEnabled => Section.GetValue("Enabled", false) && !string.IsNullOrWhiteSpace(ServerKey);
    public bool IsSandbox => !IsProduction;

    public async Task<CheckoutSession> CreateCheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
            return CheckoutSession.Failed("Midtrans belum aktif. Isi server key di appsettings.json.");

        // Midtrans hanya menerima bilangan bulat untuk IDR.
        var grossAmount = (long)decimal.Round(request.Amount, 0, MidpointRounding.AwayFromZero);

        var payload = new Dictionary<string, object?>
        {
            ["transaction_details"] = new Dictionary<string, object?>
            {
                ["order_id"] = request.ExternalId,
                ["gross_amount"] = grossAmount,
            },
            ["item_details"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = request.ExternalId,
                    ["name"] = Truncate(request.Description, 50),
                    ["price"] = grossAmount,
                    ["quantity"] = 1,
                }
            },
            ["customer_details"] = new Dictionary<string, object?>
            {
                ["first_name"] = request.CustomerName,
                ["email"] = request.CustomerEmail,
                ["phone"] = request.CustomerPhone,
            },
            ["callbacks"] = new Dictionary<string, object?>
            {
                ["finish"] = request.SuccessUrl,
                ["error"] = request.FailureUrl,
            },
        };

        try
        {
            var http = _httpClientFactory.CreateClient("Midtrans");
            using var message = new HttpRequestMessage(HttpMethod.Post, $"{SnapBaseUrl}/snap/v1/transactions")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            message.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ServerKey}:")));
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await http.SendAsync(message, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Midtrans menolak order {OrderId}: {Status} {Body}",
                    request.ExternalId, (int)response.StatusCode, body);
                return CheckoutSession.Failed($"Midtrans menolak permintaan ({(int)response.StatusCode}). {ReadErrorMessage(body)}");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            return new CheckoutSession
            {
                Success = true,
                CheckoutUrl = root.TryGetProperty("redirect_url", out var url) ? url.GetString() : null,
                ProviderReference = root.TryGetProperty("token", out var token) ? token.GetString() : null,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gagal menghubungi Midtrans untuk {OrderId}", request.ExternalId);
            return CheckoutSession.Failed("Tidak bisa menghubungi Midtrans. Coba lagi beberapa saat.");
        }
    }

    public GatewayCallback ParseCallback(string requestBody, IHeaderDictionary headers)
    {
        try
        {
            using var doc = JsonDocument.Parse(requestBody);
            var root = doc.RootElement;

            var orderId = root.TryGetProperty("order_id", out var o) ? o.GetString() ?? "" : "";
            var statusCode = root.TryGetProperty("status_code", out var sc) ? sc.GetString() ?? "" : "";
            var grossAmount = root.TryGetProperty("gross_amount", out var ga) ? ga.GetString() ?? "" : "";
            var signature = root.TryGetProperty("signature_key", out var sig) ? sig.GetString() ?? "" : "";

            var expected = ComputeSignature(orderId, statusCode, grossAmount, ServerKey);
            if (!FixedTimeEquals(signature, expected))
            {
                _logger.LogWarning("Notifikasi Midtrans untuk {OrderId} ditolak: signature tidak cocok.", orderId);
                return GatewayCallback.Invalid("Signature tidak cocok.");
            }

            var transactionStatus = root.TryGetProperty("transaction_status", out var ts) ? ts.GetString() ?? "" : "";
            var fraudStatus = root.TryGetProperty("fraud_status", out var fs) ? fs.GetString() ?? "" : "";

            return new GatewayCallback
            {
                Valid = true,
                ExternalId = orderId,
                ProviderReference = root.TryGetProperty("transaction_id", out var tid) ? tid.GetString() : null,
                Status = MapStatus(transactionStatus, fraudStatus),
                Amount = decimal.TryParse(grossAmount, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var amount) ? amount : null,
                Method = root.TryGetProperty("payment_type", out var pt) ? pt.GetString() : null,
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Notifikasi Midtrans bukan JSON yang valid.");
            return GatewayCallback.Invalid("Isi notifikasi tidak bisa dibaca.");
        }
    }

    /// <summary>
    /// capture menunggu hasil pemeriksaan fraud; hanya "accept" yang dianggap lunas.
    /// </summary>
    private static string MapStatus(string transactionStatus, string fraudStatus) => transactionStatus.ToLowerInvariant() switch
    {
        "settlement" => PaymentStatuses.Paid,
        "capture" => fraudStatus.Equals("accept", StringComparison.OrdinalIgnoreCase)
            ? PaymentStatuses.Paid
            : PaymentStatuses.Pending,
        "pending" => PaymentStatuses.Pending,
        "expire" => PaymentStatuses.Expired,
        "cancel" or "deny" => PaymentStatuses.Cancelled,
        "failure" => PaymentStatuses.Failed,
        _ => PaymentStatuses.Pending,
    };

    private static string ComputeSignature(string orderId, string statusCode, string grossAmount, string serverKey)
    {
        var raw = $"{orderId}{statusCode}{grossAmount}{serverKey}";
        var hash = SHA512.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var left = Encoding.UTF8.GetBytes(a);
        var right = Encoding.UTF8.GetBytes(b);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    private static string ReadErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error_messages", out var errors) && errors.ValueKind == JsonValueKind.Array)
                return string.Join(" ", errors.EnumerateArray().Select(e => e.GetString()));
        }
        catch (JsonException) { }
        return "";
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
