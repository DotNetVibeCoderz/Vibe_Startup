using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FitnessCenter.Models;

namespace FitnessCenter.Services.Payments;

/// <summary>
/// Xendit Invoice — halaman tagihan yang menampung e-wallet, virtual account,
/// QRIS, kartu, dan gerai retail dalam satu tautan.
///
/// Dokumentasi: https://developers.xendit.co/api-reference/#create-invoice
/// </summary>
public class XenditPaymentProvider : IPaymentProvider
{
    private const string ApiBase = "https://api.xendit.co";

    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<XenditPaymentProvider> _logger;

    public XenditPaymentProvider(IConfiguration config, IHttpClientFactory httpFactory, ILogger<XenditPaymentProvider> logger)
    {
        _config = config;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public PaymentGatewayProvider Key => PaymentGatewayProvider.Xendit;
    public string DisplayName => "Xendit";
    public string Description => "Tautan invoice: OVO, DANA, LinkAja, QRIS, virtual account, dan kartu.";
    public bool IsRedirectBased => true;
    public string SetupHint => "Isi PaymentGateway:Xendit:ApiKey dan CallbackToken (untuk memverifikasi callback).";

    public IReadOnlyList<string> Channels { get; } =
        new[] { "OVO", "DANA", "LinkAja", "QRIS", "Virtual account", "Kartu kredit", "Retail outlet" };

    private string ApiKey => _config.GetValue<string>("PaymentGateway:Xendit:ApiKey") ?? "";
    private string CallbackToken => _config.GetValue<string>("PaymentGateway:Xendit:CallbackToken") ?? "";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    private HttpClient CreateClient()
    {
        var client = _httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        client.BaseAddress = new Uri(ApiBase);
        // Xendit memakai Basic auth: secret key sebagai username, password kosong.
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(ApiKey + ":"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public async Task<PaymentChargeResult> CreateChargeAsync(PaymentChargeRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured) return PaymentChargeResult.Fail("Xendit belum dikonfigurasi. " + SetupHint);

        var payload = new Dictionary<string, object?>
        {
            ["external_id"] = request.InvoiceNumber,
            ["amount"] = Math.Round(request.Amount, MidpointRounding.AwayFromZero),
            ["currency"] = "IDR",
            ["description"] = request.Description,
            ["invoice_duration"] = (int)request.Lifetime.TotalSeconds,
            ["customer"] = new Dictionary<string, object?>
            {
                ["given_names"] = request.CustomerName,
                ["email"] = request.CustomerEmail,
                ["mobile_number"] = request.CustomerPhone
            },
            ["success_redirect_url"] = request.SuccessUrl,
            ["failure_redirect_url"] = request.FailureUrl
        };

        if (!string.IsNullOrWhiteSpace(request.CustomerEmail))
            payload["payer_email"] = request.CustomerEmail;

        if (!string.IsNullOrWhiteSpace(request.PreferredChannel))
            payload["payment_methods"] = new[] { request.PreferredChannel.ToUpperInvariant() };

        try
        {
            using var client = CreateClient();
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("/v2/invoices", content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Xendit menolak tagihan {Invoice}: {Status} {Body}",
                    request.InvoiceNumber, (int)response.StatusCode, body);
                return PaymentChargeResult.Fail($"Xendit menolak tagihan ({(int)response.StatusCode}): {ReadError(body)}");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var id = GetString(root, "id");
            var url = GetString(root, "invoice_url");
            var status = GetString(root, "status");

            if (string.IsNullOrWhiteSpace(url))
                return PaymentChargeResult.Fail("Xendit tidak mengembalikan tautan invoice.");

            DateTime? expiry = null;
            if (root.TryGetProperty("expiry_date", out var exp) &&
                DateTime.TryParse(exp.GetString(), null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsed))
                expiry = parsed;

            return new PaymentChargeResult
            {
                Success = true,
                Reference = id,
                PaymentUrl = url,
                ExpiresAt = expiry ?? DateTime.UtcNow.Add(request.Lifetime),
                RawStatus = status,
                Message = "Tautan invoice Xendit siap dibuka."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gagal membuat invoice Xendit untuk {Invoice}", request.InvoiceNumber);
            return PaymentChargeResult.Fail($"Tidak bisa menghubungi Xendit: {ex.Message}");
        }
    }

    public async Task<PaymentStatusResult> GetStatusAsync(string reference, CancellationToken ct = default)
    {
        if (!IsConfigured) return PaymentStatusResult.NotFound("Xendit belum dikonfigurasi.");

        try
        {
            using var client = CreateClient();
            using var response = await client.GetAsync($"/v2/invoices/{Uri.EscapeDataString(reference)}", ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                return PaymentStatusResult.NotFound($"Xendit tidak menemukan invoice {reference}.");

            using var doc = JsonDocument.Parse(body);
            var raw = GetString(doc.RootElement, "status");
            var channel = GetString(doc.RootElement, "payment_channel")
                       ?? GetString(doc.RootElement, "payment_method");

            return new PaymentStatusResult
            {
                Found = true,
                Status = MapStatus(raw),
                RawStatus = raw,
                Channel = channel,
                Message = $"Status Xendit: {raw}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gagal membaca status Xendit {Reference}", reference);
            return PaymentStatusResult.NotFound($"Tidak bisa menghubungi Xendit: {ex.Message}");
        }
    }

    public Task<PaymentWebhookResult> HandleWebhookAsync(PaymentWebhookContext context, CancellationToken ct = default)
    {
        if (!IsConfigured) return Task.FromResult(PaymentWebhookResult.Reject("Xendit belum dikonfigurasi."));

        // Xendit memverifikasi callback lewat header x-callback-token, bukan tanda tangan isi.
        if (string.IsNullOrWhiteSpace(CallbackToken))
            return Task.FromResult(PaymentWebhookResult.Reject(
                "PaymentGateway:Xendit:CallbackToken belum diisi, callback ditolak."));

        var token = context.Header("x-callback-token");
        if (string.IsNullOrWhiteSpace(token) ||
            !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(CallbackToken)))
        {
            _logger.LogWarning("Token callback Xendit tidak cocok.");
            return Task.FromResult(PaymentWebhookResult.Reject("Token callback tidak cocok."));
        }

        try
        {
            using var doc = JsonDocument.Parse(context.Body);
            var root = doc.RootElement;

            var externalId = GetString(root, "external_id");
            var id = GetString(root, "id");
            var raw = GetString(root, "status");
            var channel = GetString(root, "payment_channel") ?? GetString(root, "payment_method");

            if (string.IsNullOrWhiteSpace(externalId))
                return Task.FromResult(PaymentWebhookResult.Reject("Callback Xendit tanpa external_id."));

            return Task.FromResult(new PaymentWebhookResult
            {
                Verified = true,
                InvoiceNumber = externalId,
                Reference = id,
                Status = MapStatus(raw),
                RawStatus = raw,
                Channel = channel,
                Message = $"Xendit melaporkan status {raw}."
            });
        }
        catch (JsonException)
        {
            return Task.FromResult(PaymentWebhookResult.Reject("Callback Xendit bukan JSON yang sah."));
        }
    }

    private static PaymentStatus MapStatus(string? raw) => raw?.ToUpperInvariant() switch
    {
        "PAID" or "SETTLED" => PaymentStatus.Completed,
        "PENDING" => PaymentStatus.Pending,
        "EXPIRED" => PaymentStatus.Cancelled,
        "FAILED" => PaymentStatus.Failed,
        _ => PaymentStatus.Pending
    };

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.ValueKind != JsonValueKind.Null ? el.ToString() : null;

    private static string ReadError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var message = GetString(doc.RootElement, "message");
            var code = GetString(doc.RootElement, "error_code");
            if (message != null) return code != null ? $"{code} — {message}" : message;
        }
        catch { /* biarkan body mentah yang tampil */ }
        return body.Length > 200 ? body[..200] : body;
    }
}
