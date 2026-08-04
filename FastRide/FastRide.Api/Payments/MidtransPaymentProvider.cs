using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FastRide.Shared.Models;
using FastRide.Shared.Payments;

namespace FastRide.Api.Payments;

/// <summary>
/// Midtrans, via its Core API.
///
/// Docs: https://docs.midtrans.com/reference/core-api-overview
///
/// Auth is HTTP Basic with the server key as the username and an empty password. Callbacks
/// are verified with the documented SHA-512 of
/// <c>order_id + status_code + gross_amount + serverKey</c> — Midtrans does not sign the body
/// itself, so the signature must be recomputed from the fields it sends.
/// </summary>
public sealed class MidtransPaymentProvider(
    PaymentProviderConfig config,
    IHttpClientFactory httpClientFactory,
    ILogger<MidtransPaymentProvider> logger) : IPaymentProvider
{
    private const string ProductionUrl = "https://api.midtrans.com";
    private const string SandboxUrl = "https://api.sandbox.midtrans.com";

    public string Name => "midtrans";

    public string DisplayName => "Midtrans";

    public IReadOnlyCollection<PaymentMethod> SupportedMethods { get; } =
    [
        PaymentMethod.Qris,
        PaymentMethod.EWallet,
        PaymentMethod.CreditCard,
        PaymentMethod.VirtualAccount,
        PaymentMethod.BankTransfer
    ];

    public bool SettlesImmediately => false;

    private string BaseUrl => (config.BaseUrl ?? (config.IsSandbox ? SandboxUrl : ProductionUrl)).TrimEnd('/');

    public async Task<PaymentChargeResult> ChargeAsync(PaymentChargeRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(config.ServerKey))
            return PaymentChargeResult.Rejected("Server key Midtrans belum diisi.");

        // Midtrans wants the amount as a whole rupiah integer.
        var payload = new Dictionary<string, object?>
        {
            ["payment_type"] = MidtransPaymentType(request.Method, request.WalletChannel),
            ["transaction_details"] = new Dictionary<string, object?>
            {
                ["order_id"] = request.Reference,
                ["gross_amount"] = (long)Math.Round(request.Amount, 0, MidpointRounding.AwayFromZero)
            },
            ["customer_details"] = new Dictionary<string, object?>
            {
                ["first_name"] = request.CustomerName,
                ["email"] = request.CustomerEmail,
                ["phone"] = request.CustomerPhone
            },
            ["custom_expiry"] = new Dictionary<string, object?>
            {
                ["unit"] = "minute",
                ["expiry_duration"] = Math.Max(1, (int)(request.ExpiresAt - DateTime.UtcNow).TotalMinutes)
            }
        };

        AddMethodDetails(payload, request);

        try
        {
            using var client = CreateClient();
            using var response = await client.PostAsync(
                "/v2/charge",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
                ct);

            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                // The body can carry card or customer details; only the status code is logged.
                logger.LogError("Midtrans charge for {Reference} failed with {Status}.", request.Reference, (int)response.StatusCode);
                return PaymentChargeResult.Rejected($"Midtrans menolak permintaan ({(int)response.StatusCode}).");
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            var statusCode = ReadString(root, "status_code");

            // 200 settled outright, 201 is pending and expected for QRIS and virtual accounts.
            if (statusCode is not ("200" or "201"))
            {
                var message = ReadString(root, "status_message") ?? "Permintaan ditolak.";
                return PaymentChargeResult.Rejected(message);
            }

            var providerReference = ReadString(root, "transaction_id") ?? request.Reference;
            var expiresAt = ParseMidtransTime(ReadString(root, "expiry_time")) ?? request.ExpiresAt;

            if (MapStatus(ReadString(root, "transaction_status")) == PaymentStatus.Completed)
                return PaymentChargeResult.Settled(providerReference);

            var instrument = ExtractInstrument(root, request.Method);

            return instrument is null
                ? PaymentChargeResult.Rejected("Midtrans tidak mengembalikan data pembayaran.")
                : PaymentChargeResult.Awaiting(providerReference, instrument, expiresAt);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(ex, "Midtrans charge for {Reference} could not be completed.", request.Reference);
            return PaymentChargeResult.Rejected("Tidak bisa menghubungi Midtrans.");
        }
    }

    public async Task<PaymentStatusResult> QueryAsync(PaymentQueryRequest request, CancellationToken ct = default)
    {
        try
        {
            using var client = CreateClient();

            // Midtrans keys status by our order id, not by its own transaction id.
            using var response = await client.GetAsync($"/v2/{Uri.EscapeDataString(request.Reference)}/status", ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
                return PaymentStatusResult.Unknown($"Midtrans membalas {(int)response.StatusCode}.");

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            return new PaymentStatusResult(
                MapStatus(ReadString(root, "transaction_status")),
                ReadString(root, "transaction_id") ?? request.ProviderReference,
                null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(ex, "Midtrans status lookup for {Reference} failed.", request.Reference);
            return PaymentStatusResult.Unknown("Tidak bisa menghubungi Midtrans.");
        }
    }

    public PaymentCallback? ReadCallback(PaymentCallbackContext context)
    {
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(context.Body);
        }
        catch (JsonException)
        {
            logger.LogWarning("Midtrans callback rejected: body is not valid JSON.");
            return null;
        }

        using (document)
        {
            var root = document.RootElement;

            var orderId = ReadString(root, "order_id");
            var statusCode = ReadString(root, "status_code");
            var grossAmount = ReadString(root, "gross_amount");
            var signature = ReadString(root, "signature_key");

            if (orderId is null || statusCode is null || grossAmount is null || signature is null)
            {
                logger.LogWarning("Midtrans callback rejected: required fields missing.");
                return null;
            }

            // The server key is the shared secret; a callback that cannot be reproduced from
            // it did not come from Midtrans.
            var serverKey = context.WebhookSecret ?? config.ServerKey ?? string.Empty;
            var expected = Convert.ToHexStringLower(
                SHA512.HashData(Encoding.UTF8.GetBytes(orderId + statusCode + grossAmount + serverKey)));

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(signature.ToLowerInvariant()), Encoding.UTF8.GetBytes(expected)))
            {
                logger.LogWarning("Midtrans callback for {OrderId} rejected: signature mismatch.", orderId);
                return null;
            }

            var status = MapStatus(ReadString(root, "transaction_status"), ReadString(root, "fraud_status"));

            decimal? amount = decimal.TryParse(
                grossAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

            return new PaymentCallback(
                orderId,
                ReadString(root, "transaction_id"),
                status,
                amount,
                status is PaymentStatus.Failed or PaymentStatus.Expired
                    ? ReadString(root, "status_message") ?? "Pembayaran tidak berhasil."
                    : null);
        }
    }

    // ─────────────────────────── helpers ───────────────────────────

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient(nameof(MidtransPaymentProvider));
        client.BaseAddress = new Uri(BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);

        // Basic auth: server key as username, empty password.
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.ServerKey}:"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return client;
    }

    private static string MidtransPaymentType(PaymentMethod method, EWalletChannel channel) => method switch
    {
        PaymentMethod.Qris => "qris",
        PaymentMethod.CreditCard => "credit_card",
        PaymentMethod.VirtualAccount or PaymentMethod.BankTransfer => "bank_transfer",
        PaymentMethod.EWallet => channel switch
        {
            EWalletChannel.ShopeePay => "shopeepay",
            EWalletChannel.Dana or EWalletChannel.Ovo or EWalletChannel.LinkAja => "qris",
            _ => "gopay"
        },
        _ => "qris"
    };

    private static void AddMethodDetails(Dictionary<string, object?> payload, PaymentChargeRequest request)
    {
        switch (request.Method)
        {
            case PaymentMethod.Qris:
                payload["qris"] = new Dictionary<string, object?> { ["acquirer"] = "gopay" };
                break;

            case PaymentMethod.VirtualAccount or PaymentMethod.BankTransfer:
                payload["bank_transfer"] = new Dictionary<string, object?> { ["bank"] = "bca" };
                break;

            case PaymentMethod.EWallet when request.WalletChannel == EWalletChannel.ShopeePay:
                payload["shopeepay"] = new Dictionary<string, object?>
                {
                    ["callback_url"] = "https://fastride.local/payments/return"
                };
                break;

            case PaymentMethod.EWallet:
                payload["gopay"] = new Dictionary<string, object?> { ["enable_callback"] = true };
                break;
        }
    }

    /// <summary>Pull out whatever the payer needs: a QR string, a VA number, or a redirect.</summary>
    private static string? ExtractInstrument(JsonElement root, PaymentMethod method)
    {
        if (method == PaymentMethod.Qris && root.TryGetProperty("actions", out var actions))
        {
            foreach (var action in actions.EnumerateArray())
            {
                var name = ReadString(action, "name");
                if (name is "generate-qr-code" or "generate-qr-code-v2")
                    return ReadString(action, "url");
            }
        }

        if (method is PaymentMethod.VirtualAccount or PaymentMethod.BankTransfer)
        {
            if (root.TryGetProperty("va_numbers", out var vaNumbers) &&
                vaNumbers.ValueKind == JsonValueKind.Array &&
                vaNumbers.GetArrayLength() > 0)
            {
                return ReadString(vaNumbers[0], "va_number");
            }

            return ReadString(root, "permata_va_number");
        }

        if (root.TryGetProperty("actions", out var walletActions))
        {
            foreach (var action in walletActions.EnumerateArray())
            {
                if (ReadString(action, "name") is "deeplink-redirect" or "generate-qr-code")
                    return ReadString(action, "url");
            }
        }

        return ReadString(root, "redirect_url");
    }

    private static PaymentStatus MapStatus(string? transactionStatus, string? fraudStatus = null)
    {
        // A "capture" that fraud review has flagged is not money in hand.
        if (transactionStatus == "capture" && fraudStatus == "challenge") return PaymentStatus.AwaitingPayment;

        return transactionStatus switch
        {
            "capture" or "settlement" => PaymentStatus.Completed,
            "pending" => PaymentStatus.AwaitingPayment,
            "deny" or "cancel" or "failure" => PaymentStatus.Failed,
            "expire" => PaymentStatus.Expired,
            "refund" or "partial_refund" => PaymentStatus.Refunded,
            _ => PaymentStatus.Pending
        };
    }

    private static DateTime? ParseMidtransTime(string? value) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
