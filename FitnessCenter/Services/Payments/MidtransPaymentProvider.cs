using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FitnessCenter.Models;

namespace FitnessCenter.Services.Payments;

/// <summary>
/// Midtrans Snap — halaman bayar serbaguna untuk pasar Indonesia
/// (GoPay, QRIS, virtual account, kartu kredit, gerai retail).
///
/// Dokumentasi: https://docs.midtrans.com/reference/snap-api
/// </summary>
public class MidtransPaymentProvider : IPaymentProvider
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<MidtransPaymentProvider> _logger;

    public MidtransPaymentProvider(IConfiguration config, IHttpClientFactory httpFactory, ILogger<MidtransPaymentProvider> logger)
    {
        _config = config;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public PaymentGatewayProvider Key => PaymentGatewayProvider.Midtrans;
    public string DisplayName => "Midtrans";
    public string Description => "Halaman Snap: GoPay, QRIS, virtual account, kartu, dan gerai retail.";
    public bool IsRedirectBased => true;
    public string SetupHint => "Isi PaymentGateway:Midtrans:ServerKey (dan ClientKey untuk Snap embed).";

    public IReadOnlyList<string> Channels { get; } =
        new[] { "GoPay", "QRIS", "ShopeePay", "Virtual account", "Kartu kredit", "Indomaret/Alfamart" };

    private string ServerKey => _config.GetValue<string>("PaymentGateway:Midtrans:ServerKey") ?? "";
    private bool IsProduction => _config.GetValue<bool>("PaymentGateway:Midtrans:IsProduction");

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ServerKey);

    private string SnapUrl => IsProduction
        ? "https://app.midtrans.com/snap/v1/transactions"
        : "https://app.sandbox.midtrans.com/snap/v1/transactions";

    private string ApiBase => IsProduction
        ? "https://api.midtrans.com"
        : "https://api.sandbox.midtrans.com";

    private HttpClient CreateClient()
    {
        var client = _httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(ServerKey + ":"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public async Task<PaymentChargeResult> CreateChargeAsync(PaymentChargeRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured) return PaymentChargeResult.Fail("Midtrans belum dikonfigurasi. " + SetupHint);

        // Midtrans menolak gross_amount berdesimal untuk IDR.
        var gross = (long)Math.Round(request.Amount, MidpointRounding.AwayFromZero);

        var nameParts = request.CustomerName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        var payload = new Dictionary<string, object?>
        {
            ["transaction_details"] = new Dictionary<string, object?>
            {
                ["order_id"] = request.InvoiceNumber,
                ["gross_amount"] = gross
            },
            ["item_details"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["id"] = request.InvoiceNumber,
                    ["price"] = gross,
                    ["quantity"] = 1,
                    ["name"] = Truncate(request.Description, 50)
                }
            },
            ["customer_details"] = new Dictionary<string, object?>
            {
                ["first_name"] = nameParts.Length > 0 ? Truncate(nameParts[0], 20) : "Member",
                ["last_name"] = nameParts.Length > 1 ? Truncate(nameParts[1], 20) : "",
                ["email"] = request.CustomerEmail,
                ["phone"] = request.CustomerPhone
            },
            ["expiry"] = new Dictionary<string, object?>
            {
                ["unit"] = "minute",
                ["duration"] = (int)request.Lifetime.TotalMinutes
            }
        };

        if (!string.IsNullOrWhiteSpace(request.SuccessUrl))
            payload["callbacks"] = new Dictionary<string, object?> { ["finish"] = request.SuccessUrl };

        if (!string.IsNullOrWhiteSpace(request.PreferredChannel))
            payload["enabled_payments"] = new[] { request.PreferredChannel };

        try
        {
            using var client = CreateClient();
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(SnapUrl, content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Midtrans menolak tagihan {Invoice}: {Status} {Body}",
                    request.InvoiceNumber, (int)response.StatusCode, body);
                return PaymentChargeResult.Fail($"Midtrans menolak tagihan ({(int)response.StatusCode}): {ReadError(body)}");
            }

            using var doc = JsonDocument.Parse(body);
            var redirect = doc.RootElement.TryGetProperty("redirect_url", out var r) ? r.GetString() : null;

            if (string.IsNullOrWhiteSpace(redirect))
                return PaymentChargeResult.Fail("Midtrans tidak mengembalikan halaman bayar.");

            return new PaymentChargeResult
            {
                Success = true,
                Reference = request.InvoiceNumber,   // Midtrans memakai order_id sebagai kunci
                PaymentUrl = redirect,
                ExpiresAt = DateTime.UtcNow.Add(request.Lifetime),
                RawStatus = "pending",
                Message = "Halaman pembayaran Midtrans siap dibuka."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gagal membuat tagihan Midtrans untuk {Invoice}", request.InvoiceNumber);
            return PaymentChargeResult.Fail($"Tidak bisa menghubungi Midtrans: {ex.Message}");
        }
    }

    public async Task<PaymentStatusResult> GetStatusAsync(string reference, CancellationToken ct = default)
    {
        if (!IsConfigured) return PaymentStatusResult.NotFound("Midtrans belum dikonfigurasi.");

        try
        {
            using var client = CreateClient();
            using var response = await client.GetAsync($"{ApiBase}/v2/{Uri.EscapeDataString(reference)}/status", ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                return PaymentStatusResult.NotFound($"Midtrans tidak menemukan transaksi {reference}.");

            using var doc = JsonDocument.Parse(body);
            var raw = doc.RootElement.TryGetProperty("transaction_status", out var s) ? s.GetString() : null;
            var fraud = doc.RootElement.TryGetProperty("fraud_status", out var f) ? f.GetString() : null;
            var channel = doc.RootElement.TryGetProperty("payment_type", out var p) ? p.GetString() : null;

            return new PaymentStatusResult
            {
                Found = true,
                Status = MapStatus(raw, fraud),
                RawStatus = raw,
                Channel = channel,
                Message = $"Status Midtrans: {raw}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gagal membaca status Midtrans {Reference}", reference);
            return PaymentStatusResult.NotFound($"Tidak bisa menghubungi Midtrans: {ex.Message}");
        }
    }

    public Task<PaymentWebhookResult> HandleWebhookAsync(PaymentWebhookContext context, CancellationToken ct = default)
    {
        if (!IsConfigured) return Task.FromResult(PaymentWebhookResult.Reject("Midtrans belum dikonfigurasi."));

        try
        {
            using var doc = JsonDocument.Parse(context.Body);
            var root = doc.RootElement;

            var orderId = GetString(root, "order_id");
            var statusCode = GetString(root, "status_code");
            var grossAmount = GetString(root, "gross_amount");
            var signature = GetString(root, "signature_key");
            var raw = GetString(root, "transaction_status");
            var fraud = GetString(root, "fraud_status");
            var channel = GetString(root, "payment_type");

            if (orderId is null || statusCode is null || grossAmount is null || signature is null)
                return Task.FromResult(PaymentWebhookResult.Reject("Callback Midtrans tidak lengkap."));

            // signature_key = SHA512(order_id + status_code + gross_amount + ServerKey)
            var expected = Convert.ToHexString(
                SHA512.HashData(Encoding.UTF8.GetBytes(orderId + statusCode + grossAmount + ServerKey)))
                .ToLowerInvariant();

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(expected),
                    Encoding.UTF8.GetBytes(signature.ToLowerInvariant())))
            {
                _logger.LogWarning("Tanda tangan callback Midtrans tidak cocok untuk {OrderId}", orderId);
                return Task.FromResult(PaymentWebhookResult.Reject("Tanda tangan callback tidak cocok."));
            }

            return Task.FromResult(new PaymentWebhookResult
            {
                Verified = true,
                InvoiceNumber = orderId,
                Reference = orderId,
                Status = MapStatus(raw, fraud),
                RawStatus = raw,
                Channel = channel,
                Message = $"Midtrans melaporkan status {raw}."
            });
        }
        catch (JsonException)
        {
            return Task.FromResult(PaymentWebhookResult.Reject("Callback Midtrans bukan JSON yang sah."));
        }
    }

    /// <summary>Memetakan transaction_status Midtrans ke status internal.</summary>
    private static PaymentStatus MapStatus(string? raw, string? fraud) => raw switch
    {
        "capture" => string.Equals(fraud, "challenge", StringComparison.OrdinalIgnoreCase)
            ? PaymentStatus.Confirmed
            : PaymentStatus.Completed,
        "settlement" => PaymentStatus.Completed,
        "pending" => PaymentStatus.Pending,
        "deny" or "failure" => PaymentStatus.Failed,
        "cancel" or "expire" => PaymentStatus.Cancelled,
        "refund" or "partial_refund" => PaymentStatus.Refunded,
        _ => PaymentStatus.Pending
    };

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) ? el.ToString() : null;

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];

    private static string ReadError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error_messages", out var errors) && errors.ValueKind == JsonValueKind.Array)
                return string.Join("; ", errors.EnumerateArray().Select(e => e.GetString()));
        }
        catch { /* biarkan body mentah yang tampil */ }
        return Truncate(body, 200);
    }
}
