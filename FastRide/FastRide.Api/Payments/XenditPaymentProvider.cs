using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FastRide.Shared.Models;
using FastRide.Shared.Payments;

namespace FastRide.Api.Payments;

/// <summary>
/// Xendit, via its QR Code and e-wallet APIs.
///
/// Docs: https://developers.xendit.co
///
/// Auth is HTTP Basic with the secret key as the username. Callbacks are authenticated by a
/// static token in the <c>x-callback-token</c> header rather than a per-message signature,
/// so the comparison must still be constant-time and the token must never be logged.
/// </summary>
public sealed class XenditPaymentProvider(
    PaymentProviderConfig config,
    IHttpClientFactory httpClientFactory,
    ILogger<XenditPaymentProvider> logger) : IPaymentProvider
{
    private const string DefaultUrl = "https://api.xendit.co";

    public string Name => "xendit";

    public string DisplayName => "Xendit";

    public IReadOnlyCollection<PaymentMethod> SupportedMethods { get; } =
    [
        PaymentMethod.Qris,
        PaymentMethod.EWallet,
        PaymentMethod.VirtualAccount,
        PaymentMethod.BankTransfer
    ];

    public bool SettlesImmediately => false;

    private string BaseUrl => (config.BaseUrl ?? DefaultUrl).TrimEnd('/');

    public async Task<PaymentChargeResult> ChargeAsync(PaymentChargeRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(config.ServerKey))
            return PaymentChargeResult.Rejected("Secret key Xendit belum diisi.");

        var (path, payload) = request.Method switch
        {
            PaymentMethod.Qris => ("/qr_codes", QrPayload(request)),
            PaymentMethod.EWallet => ("/ewallets/charges", WalletPayload(request)),
            _ => ("/callback_virtual_accounts", VirtualAccountPayload(request))
        };

        try
        {
            using var client = CreateClient();
            using var message = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            // Xendit deduplicates on this header, so a retried request cannot double-charge.
            message.Headers.TryAddWithoutValidation("Idempotency-key", request.Reference);

            // The QR API is versioned by header.
            if (request.Method == PaymentMethod.Qris)
                message.Headers.TryAddWithoutValidation("api-version", "2022-07-31");

            using var response = await client.SendAsync(message, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Xendit charge for {Reference} failed with {Status}.", request.Reference, (int)response.StatusCode);
                return PaymentChargeResult.Rejected($"Xendit menolak permintaan ({(int)response.StatusCode}).");
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            var providerReference = ReadString(root, "id") ?? request.Reference;
            var instrument = ExtractInstrument(root, request.Method);

            if (instrument is null)
                return PaymentChargeResult.Rejected("Xendit tidak mengembalikan data pembayaran.");

            var expiresAt = ParseTime(ReadString(root, "expires_at") ?? ReadString(root, "expiration_date"))
                            ?? request.ExpiresAt;

            return PaymentChargeResult.Awaiting(providerReference, instrument, expiresAt);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(ex, "Xendit charge for {Reference} could not be completed.", request.Reference);
            return PaymentChargeResult.Rejected("Tidak bisa menghubungi Xendit.");
        }
    }

    public async Task<PaymentStatusResult> QueryAsync(PaymentQueryRequest request, CancellationToken ct = default)
    {
        try
        {
            using var client = CreateClient();
            using var message = new HttpRequestMessage(
                HttpMethod.Get, $"/qr_codes/{Uri.EscapeDataString(request.ProviderReference)}");

            message.Headers.TryAddWithoutValidation("api-version", "2022-07-31");

            using var response = await client.SendAsync(message, ct);

            if (!response.IsSuccessStatusCode)
                return PaymentStatusResult.Unknown($"Xendit membalas {(int)response.StatusCode}.");

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

            return new PaymentStatusResult(
                MapStatus(ReadString(document.RootElement, "status")),
                request.ProviderReference,
                null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(ex, "Xendit status lookup for {Reference} failed.", request.Reference);
            return PaymentStatusResult.Unknown("Tidak bisa menghubungi Xendit.");
        }
    }

    public PaymentCallback? ReadCallback(PaymentCallbackContext context)
    {
        if (!context.Headers.TryGetValue("x-callback-token", out var token))
        {
            logger.LogWarning("Xendit callback rejected: no callback token.");
            return null;
        }

        var expected = context.WebhookSecret ?? string.Empty;

        if (expected.Length == 0)
        {
            logger.LogWarning("Xendit callback rejected: no verification token configured.");
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(expected)))
        {
            logger.LogWarning("Xendit callback rejected: token mismatch.");
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(context.Body);
            var root = document.RootElement;

            // QR callbacks nest the transaction under "data"; wallet callbacks are flat.
            var data = root.TryGetProperty("data", out var nested) ? nested : root;

            var reference = ReadString(data, "reference_id")
                            ?? ReadString(data, "external_id")
                            ?? ReadString(data, "reference");

            if (reference is null)
            {
                logger.LogWarning("Xendit callback rejected: no reference field.");
                return null;
            }

            var status = MapStatus(ReadString(data, "status") ?? ReadString(root, "event"));

            decimal? amount = data.TryGetProperty("amount", out var amountValue) && amountValue.TryGetDecimal(out var parsed)
                ? parsed
                : null;

            return new PaymentCallback(
                reference,
                ReadString(data, "id"),
                status,
                amount,
                status is PaymentStatus.Failed or PaymentStatus.Expired
                    ? ReadString(data, "failure_code") ?? "Pembayaran tidak berhasil."
                    : null);
        }
        catch (JsonException)
        {
            logger.LogWarning("Xendit callback rejected: body is not valid JSON.");
            return null;
        }
    }

    // ─────────────────────────── helpers ───────────────────────────

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient(nameof(XenditPaymentProvider));
        client.BaseAddress = new Uri(BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.ServerKey}:"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return client;
    }

    private static Dictionary<string, object?> QrPayload(PaymentChargeRequest request) => new()
    {
        ["reference_id"] = request.Reference,
        ["type"] = "DYNAMIC",
        ["currency"] = "IDR",
        ["amount"] = Math.Round(request.Amount, 0, MidpointRounding.AwayFromZero),
        ["expires_at"] = request.ExpiresAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
    };

    private static Dictionary<string, object?> WalletPayload(PaymentChargeRequest request) => new()
    {
        ["reference_id"] = request.Reference,
        ["currency"] = "IDR",
        ["amount"] = Math.Round(request.Amount, 0, MidpointRounding.AwayFromZero),
        ["checkout_method"] = "ONE_TIME_PAYMENT",
        ["channel_code"] = request.WalletChannel switch
        {
            EWalletChannel.Ovo => "ID_OVO",
            EWalletChannel.Dana => "ID_DANA",
            EWalletChannel.ShopeePay => "ID_SHOPEEPAY",
            EWalletChannel.LinkAja => "ID_LINKAJA",
            _ => "ID_DANA"
        },
        ["channel_properties"] = new Dictionary<string, object?>
        {
            ["success_redirect_url"] = "https://fastride.local/payments/return",
            ["mobile_number"] = request.CustomerPhone
        }
    };

    private static Dictionary<string, object?> VirtualAccountPayload(PaymentChargeRequest request) => new()
    {
        ["external_id"] = request.Reference,
        ["bank_code"] = "BCA",
        ["name"] = request.CustomerName,
        ["expected_amount"] = Math.Round(request.Amount, 0, MidpointRounding.AwayFromZero),
        ["is_closed"] = true,
        ["is_single_use"] = true,
        ["expiration_date"] = request.ExpiresAt.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
    };

    private static string? ExtractInstrument(JsonElement root, PaymentMethod method) => method switch
    {
        PaymentMethod.Qris => ReadString(root, "qr_string"),

        PaymentMethod.EWallet => root.TryGetProperty("actions", out var actions)
            ? ReadString(actions, "desktop_web_checkout_url")
              ?? ReadString(actions, "mobile_web_checkout_url")
              ?? ReadString(actions, "mobile_deeplink_checkout_url")
            : null,

        _ => ReadString(root, "account_number")
    };

    private static PaymentStatus MapStatus(string? status) => status?.ToUpperInvariant() switch
    {
        "COMPLETED" or "SUCCEEDED" or "PAID" or "ACTIVE_PAID" => PaymentStatus.Completed,
        "ACTIVE" or "PENDING" => PaymentStatus.AwaitingPayment,
        "EXPIRED" => PaymentStatus.Expired,
        "FAILED" or "VOIDED" => PaymentStatus.Failed,
        "REFUNDED" => PaymentStatus.Refunded,
        _ => PaymentStatus.Pending
    };

    private static DateTime? ParseTime(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
