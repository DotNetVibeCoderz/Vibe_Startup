using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FastRide.Shared.Common;
using FastRide.Shared.Models;
using FastRide.Shared.Payments;

namespace FastRide.Api.Payments;

/// <summary>
/// A provider that behaves like a real one without any money or network involved.
///
/// This is what the demo, the simulator and the test suite run against. It issues genuine
/// EMVCo QRIS payloads — correct tag structure and CRC, so a scanner parses them — and moves
/// a charge through the same states a live acquirer would. The point is that the code path
/// exercised here is the code path that runs in production; only the counterparty differs.
///
/// It never contacts anything and never settles real money.
/// </summary>
public sealed class SimulatedPaymentProvider(
    PaymentProviderConfig config,
    ILogger<SimulatedPaymentProvider> logger,
    TimeProvider clock) : IPaymentProvider
{
    /// <summary>Charges in flight. A restart clears them, which is fine for a sandbox.</summary>
    private readonly ConcurrentDictionary<string, SimulatedCharge> _charges = new(StringComparer.Ordinal);

    public string Name => "simulated";

    public string DisplayName => "Simulasi (sandbox)";

    public IReadOnlyCollection<PaymentMethod> SupportedMethods { get; } =
    [
        PaymentMethod.Qris,
        PaymentMethod.EWallet,
        PaymentMethod.CreditCard,
        PaymentMethod.VirtualAccount,
        PaymentMethod.BankTransfer
    ];

    public bool SettlesImmediately => false;

    public Task<PaymentChargeResult> ChargeAsync(PaymentChargeRequest request, CancellationToken ct = default)
    {
        var providerReference = $"SIM-{Guid.NewGuid().ToString("N")[..16].ToUpperInvariant()}";

        var payload = request.Method switch
        {
            PaymentMethod.Qris => QrisPayload.Build(
                config.MerchantId ?? "ID1234567890123",
                config.MerchantName ?? "FASTRIDE SANDBOX",
                config.MerchantCity ?? "JAKARTA",
                request.Amount,
                request.Reference),

            // A plausible-looking sandbox VA number; the bank prefix is a test range.
            PaymentMethod.VirtualAccount or PaymentMethod.BankTransfer =>
                $"8808{DateTime.UnixEpoch.Ticks % 1000:D3}{Random.Shared.Next(100000, 999999)}",

            // Wallets and cards send the payer somewhere to approve the charge.
            _ => $"https://sandbox.fastride.local/pay/{providerReference}"
        };

        _charges[providerReference] = new SimulatedCharge(
            request.Reference, request.Amount, PaymentStatus.AwaitingPayment, request.ExpiresAt);

        logger.LogInformation(
            "Simulated charge {ProviderReference} opened for {Reference} ({Method}, Rp {Amount}).",
            providerReference, request.Reference, request.Method, request.Amount);

        return Task.FromResult(PaymentChargeResult.Awaiting(providerReference, payload, request.ExpiresAt));
    }

    public Task<PaymentStatusResult> QueryAsync(PaymentQueryRequest request, CancellationToken ct = default)
    {
        if (!_charges.TryGetValue(request.ProviderReference, out var charge))
        {
            // The process restarted, or the reference is not ours.
            return Task.FromResult(PaymentStatusResult.Unknown("Charge tidak dikenal oleh provider simulasi."));
        }

        // Expiry is evaluated on read rather than on a timer, so time can be moved forward
        // in tests without waiting.
        if (charge.Status == PaymentStatus.AwaitingPayment && clock.GetUtcNow().UtcDateTime > charge.ExpiresAt)
        {
            charge = charge with { Status = PaymentStatus.Expired };
            _charges[request.ProviderReference] = charge;
        }

        return Task.FromResult(new PaymentStatusResult(charge.Status, request.ProviderReference, null));
    }

    /// <summary>
    /// Stand in for the payer. The demo console and the tests call this instead of waiting
    /// for a human to scan a QR.
    /// </summary>
    public bool Advance(string providerReference, PaymentStatus status, out string? reference)
    {
        reference = null;

        if (!_charges.TryGetValue(providerReference, out var charge)) return false;

        _charges[providerReference] = charge with { Status = status };
        reference = charge.Reference;

        logger.LogInformation("Simulated charge {ProviderReference} moved to {Status}.", providerReference, status);
        return true;
    }

    /// <summary>Build the callback body this provider would post, signed the same way.</summary>
    public (string Body, string Signature) BuildCallback(
        string providerReference, string reference, PaymentStatus status, decimal amount)
    {
        var body = JsonSerializer.Serialize(new SimulatedCallbackBody(
            reference,
            providerReference,
            status.ToString(),
            amount.ToString("0.##", CultureInfo.InvariantCulture),
            status == PaymentStatus.Failed ? "Ditolak pada simulasi" : null));

        return (body, Sign(body, config.WebhookSecret ?? string.Empty));
    }

    public PaymentCallback? ReadCallback(PaymentCallbackContext context)
    {
        // Signature first: nothing in the body is trusted until it is proven authentic.
        if (!context.Headers.TryGetValue("x-fastride-signature", out var signature))
        {
            logger.LogWarning("Simulated callback rejected: no signature header.");
            return null;
        }

        var expected = Sign(context.Body, context.WebhookSecret ?? string.Empty);

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(signature), Encoding.UTF8.GetBytes(expected)))
        {
            logger.LogWarning("Simulated callback rejected: signature mismatch.");
            return null;
        }

        SimulatedCallbackBody? parsed;

        try
        {
            parsed = JsonSerializer.Deserialize<SimulatedCallbackBody>(context.Body);
        }
        catch (JsonException)
        {
            logger.LogWarning("Simulated callback rejected: body is not valid JSON.");
            return null;
        }

        if (parsed is null || string.IsNullOrWhiteSpace(parsed.Reference)) return null;

        if (!Enum.TryParse<PaymentStatus>(parsed.Status, ignoreCase: true, out var status)) return null;

        decimal? amount = decimal.TryParse(
            parsed.Amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;

        return new PaymentCallback(parsed.Reference, parsed.ProviderReference, status, amount, parsed.FailureReason);
    }

    internal static string Sign(string body, string secret) =>
        Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));

    private sealed record SimulatedCharge(string Reference, decimal Amount, PaymentStatus Status, DateTime ExpiresAt);

    private sealed record SimulatedCallbackBody(
        string Reference, string? ProviderReference, string Status, string? Amount, string? FailureReason);
}
