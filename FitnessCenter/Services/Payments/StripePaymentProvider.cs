using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FitnessCenter.Models;

namespace FitnessCenter.Services.Payments;

/// <summary>
/// Stripe Checkout — dipakai untuk pembayaran kartu internasional.
/// Memanggil REST API langsung (form-encoded) sehingga tidak menambah dependensi.
///
/// Dokumentasi: https://docs.stripe.com/api/checkout/sessions/create
/// </summary>
public class StripePaymentProvider : IPaymentProvider
{
    private const string ApiBase = "https://api.stripe.com";

    /// <summary>Mata uang tanpa pecahan — nominal dikirim apa adanya, bukan dikali 100.</summary>
    private static readonly HashSet<string> ZeroDecimalCurrencies =
        new(StringComparer.OrdinalIgnoreCase) { "idr", "jpy", "krw", "vnd", "clp", "isk", "bif", "djf", "gnf", "kmf", "mga", "pyg", "rwf", "ugx", "vuv", "xaf", "xof", "xpf" };

    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<StripePaymentProvider> _logger;

    public StripePaymentProvider(IConfiguration config, IHttpClientFactory httpFactory, ILogger<StripePaymentProvider> logger)
    {
        _config = config;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public PaymentGatewayProvider Key => PaymentGatewayProvider.Stripe;
    public string DisplayName => "Stripe";
    public string Description => "Checkout kartu internasional — Visa, Mastercard, Amex, dan dompet digital global.";
    public bool IsRedirectBased => true;
    public string SetupHint => "Isi PaymentGateway:Stripe:SecretKey dan WebhookSecret (whsec_…).";

    public IReadOnlyList<string> Channels { get; } =
        new[] { "Visa", "Mastercard", "American Express", "Apple Pay", "Google Pay" };

    private string SecretKey => _config.GetValue<string>("PaymentGateway:Stripe:SecretKey") ?? "";
    private string WebhookSecret => _config.GetValue<string>("PaymentGateway:Stripe:WebhookSecret") ?? "";
    private string Currency => (_config.GetValue<string>("PaymentGateway:Stripe:Currency") ?? "idr").ToLowerInvariant();

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SecretKey);

    private HttpClient CreateClient()
    {
        var client = _httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        client.BaseAddress = new Uri(ApiBase);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SecretKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    /// <summary>Mengubah rupiah ke satuan terkecil sesuai aturan mata uang Stripe.</summary>
    private long ToMinorUnits(decimal amount) =>
        ZeroDecimalCurrencies.Contains(Currency)
            ? (long)Math.Round(amount, MidpointRounding.AwayFromZero)
            : (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);

    public async Task<PaymentChargeResult> CreateChargeAsync(PaymentChargeRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured) return PaymentChargeResult.Fail("Stripe belum dikonfigurasi. " + SetupHint);

        var expiresAt = DateTime.UtcNow.Add(request.Lifetime < TimeSpan.FromMinutes(30)
            ? TimeSpan.FromMinutes(30)      // Stripe menolak masa berlaku di bawah 30 menit
            : request.Lifetime);

        var form = new List<KeyValuePair<string, string>>
        {
            new("mode", "payment"),
            new("client_reference_id", request.InvoiceNumber),
            new("success_url", request.SuccessUrl ?? "https://localhost/payments"),
            new("cancel_url", request.FailureUrl ?? request.SuccessUrl ?? "https://localhost/payments"),
            new("expires_at", new DateTimeOffset(expiresAt).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new("line_items[0][quantity]", "1"),
            new("line_items[0][price_data][currency]", Currency),
            new("line_items[0][price_data][unit_amount]", ToMinorUnits(request.Amount).ToString(CultureInfo.InvariantCulture)),
            new("line_items[0][price_data][product_data][name]", request.Description),
            new("metadata[invoice_number]", request.InvoiceNumber)
        };

        if (!string.IsNullOrWhiteSpace(request.CustomerEmail))
            form.Add(new("customer_email", request.CustomerEmail));

        try
        {
            using var client = CreateClient();
            using var content = new FormUrlEncodedContent(form);
            using var response = await client.PostAsync("/v1/checkout/sessions", content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Stripe menolak sesi checkout {Invoice}: {Status} {Body}",
                    request.InvoiceNumber, (int)response.StatusCode, body);
                return PaymentChargeResult.Fail($"Stripe menolak tagihan ({(int)response.StatusCode}): {ReadError(body)}");
            }

            using var doc = JsonDocument.Parse(body);
            var id = GetString(doc.RootElement, "id");
            var url = GetString(doc.RootElement, "url");

            if (string.IsNullOrWhiteSpace(url))
                return PaymentChargeResult.Fail("Stripe tidak mengembalikan halaman checkout.");

            return new PaymentChargeResult
            {
                Success = true,
                Reference = id,
                PaymentUrl = url,
                ExpiresAt = expiresAt,
                RawStatus = "open",
                Message = "Halaman checkout Stripe siap dibuka."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gagal membuat sesi Stripe untuk {Invoice}", request.InvoiceNumber);
            return PaymentChargeResult.Fail($"Tidak bisa menghubungi Stripe: {ex.Message}");
        }
    }

    public async Task<PaymentStatusResult> GetStatusAsync(string reference, CancellationToken ct = default)
    {
        if (!IsConfigured) return PaymentStatusResult.NotFound("Stripe belum dikonfigurasi.");

        try
        {
            using var client = CreateClient();
            using var response = await client.GetAsync($"/v1/checkout/sessions/{Uri.EscapeDataString(reference)}", ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                return PaymentStatusResult.NotFound($"Stripe tidak menemukan sesi {reference}.");

            using var doc = JsonDocument.Parse(body);
            var sessionStatus = GetString(doc.RootElement, "status");         // open | complete | expired
            var paymentStatus = GetString(doc.RootElement, "payment_status"); // paid | unpaid | no_payment_required

            return new PaymentStatusResult
            {
                Found = true,
                Status = MapStatus(sessionStatus, paymentStatus),
                RawStatus = $"{sessionStatus}/{paymentStatus}",
                Channel = "card",
                Message = $"Status Stripe: {sessionStatus} ({paymentStatus})"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gagal membaca status Stripe {Reference}", reference);
            return PaymentStatusResult.NotFound($"Tidak bisa menghubungi Stripe: {ex.Message}");
        }
    }

    public Task<PaymentWebhookResult> HandleWebhookAsync(PaymentWebhookContext context, CancellationToken ct = default)
    {
        if (!IsConfigured) return Task.FromResult(PaymentWebhookResult.Reject("Stripe belum dikonfigurasi."));
        if (string.IsNullOrWhiteSpace(WebhookSecret))
            return Task.FromResult(PaymentWebhookResult.Reject(
                "PaymentGateway:Stripe:WebhookSecret belum diisi, callback ditolak."));

        var signatureHeader = context.Header("Stripe-Signature");
        if (string.IsNullOrWhiteSpace(signatureHeader) || !VerifySignature(context.Body, signatureHeader))
        {
            _logger.LogWarning("Tanda tangan webhook Stripe tidak cocok.");
            return Task.FromResult(PaymentWebhookResult.Reject("Tanda tangan webhook tidak cocok."));
        }

        try
        {
            using var doc = JsonDocument.Parse(context.Body);
            var eventType = GetString(doc.RootElement, "type");

            if (!doc.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("object", out var obj))
                return Task.FromResult(PaymentWebhookResult.Reject("Webhook Stripe tanpa data.object."));

            var invoiceNumber = GetString(obj, "client_reference_id");
            if (string.IsNullOrWhiteSpace(invoiceNumber) &&
                obj.TryGetProperty("metadata", out var meta))
                invoiceNumber = GetString(meta, "invoice_number");

            if (string.IsNullOrWhiteSpace(invoiceNumber))
                return Task.FromResult(PaymentWebhookResult.Ignore("Event Stripe tanpa nomor invoice, dilewati."));

            PaymentStatus? mapped = eventType switch
            {
                "checkout.session.completed" or "checkout.session.async_payment_succeeded" => PaymentStatus.Completed,
                "checkout.session.async_payment_failed" => PaymentStatus.Failed,
                "checkout.session.expired" => PaymentStatus.Cancelled,
                "charge.refunded" => PaymentStatus.Refunded,
                _ => null
            };

            if (mapped is null)
                return Task.FromResult(PaymentWebhookResult.Ignore($"Event {eventType} tidak dipakai."));

            var status = mapped.Value;

            // Sesi selesai tapi pembayaran belum lunas (misal metode asinkron) tetap Pending.
            if (status == PaymentStatus.Completed &&
                GetString(obj, "payment_status") is string ps &&
                !string.Equals(ps, "paid", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(ps, "no_payment_required", StringComparison.OrdinalIgnoreCase))
            {
                status = PaymentStatus.Pending;
            }

            return Task.FromResult(new PaymentWebhookResult
            {
                Verified = true,
                InvoiceNumber = invoiceNumber,
                Reference = GetString(obj, "id"),
                Status = status,
                RawStatus = eventType,
                Channel = "card",
                Message = $"Stripe mengirim event {eventType}."
            });
        }
        catch (JsonException)
        {
            return Task.FromResult(PaymentWebhookResult.Reject("Webhook Stripe bukan JSON yang sah."));
        }
    }

    /// <summary>
    /// Memverifikasi header Stripe-Signature: "t=…,v1=…".
    /// Tanda tangan dihitung HMAC-SHA256 atas "{timestamp}.{body}".
    /// </summary>
    private bool VerifySignature(string body, string header)
    {
        string? timestamp = null;
        var candidates = new List<string>();

        foreach (var part in header.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0].Trim() == "t") timestamp = kv[1].Trim();
            else if (kv[0].Trim() == "v1") candidates.Add(kv[1].Trim());
        }

        if (timestamp is null || candidates.Count == 0) return false;

        // Tolak callback yang terlalu tua untuk mencegah replay.
        if (long.TryParse(timestamp, out var unix))
        {
            var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(unix);
            if (age > TimeSpan.FromMinutes(5) || age < TimeSpan.FromMinutes(-5)) return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(WebhookSecret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{body}")))
            .ToLowerInvariant();

        return candidates.Any(c => CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(c.ToLowerInvariant())));
    }

    private static PaymentStatus MapStatus(string? sessionStatus, string? paymentStatus)
    {
        if (string.Equals(paymentStatus, "paid", StringComparison.OrdinalIgnoreCase)) return PaymentStatus.Completed;
        return sessionStatus switch
        {
            "expired" => PaymentStatus.Cancelled,
            "complete" => PaymentStatus.Completed,
            _ => PaymentStatus.Pending
        };
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var el) && el.ValueKind != JsonValueKind.Null ? el.ToString() : null;

    private static string ReadError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
                return GetString(err, "message") ?? "permintaan ditolak";
        }
        catch { /* biarkan body mentah yang tampil */ }
        return body.Length > 200 ? body[..200] : body;
    }
}
