using FastRide.Shared.Models;

namespace FastRide.Shared.Payments;

/// <summary>
/// A payment service provider.
///
/// Everything the platform needs from a PSP is behind this: start a charge, ask how it is
/// going, and interpret the callback it sends when the payer acts. Adding a provider means
/// implementing this and switching it on in configuration — no changes anywhere else.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>Stable key used in configuration and stored on the payment row.</summary>
    string Name { get; }

    /// <summary>Shown in the admin console.</summary>
    string DisplayName { get; }

    /// <summary>Methods this provider can technically handle; configuration narrows it further.</summary>
    IReadOnlyCollection<PaymentMethod> SupportedMethods { get; }

    /// <summary>
    /// True when the provider settles without anyone leaving the app — cash collected by the
    /// driver, for instance. Such charges complete immediately instead of waiting on a payer.
    /// </summary>
    bool SettlesImmediately { get; }

    /// <summary>Begin a charge. Returns what the payer needs in order to complete it.</summary>
    Task<PaymentChargeResult> ChargeAsync(PaymentChargeRequest request, CancellationToken ct = default);

    /// <summary>
    /// Ask the provider where a charge stands. Used to reconcile when a callback is late or
    /// lost — a webhook must never be the only way a payment can complete.
    /// </summary>
    Task<PaymentStatusResult> QueryAsync(PaymentQueryRequest request, CancellationToken ct = default);

    /// <summary>
    /// Interpret a callback. Implementations must verify the signature before trusting a
    /// single field — an unauthenticated endpoint that marks orders paid is a free ride.
    /// Returns null when the payload is not authentic or not understood.
    /// </summary>
    PaymentCallback? ReadCallback(PaymentCallbackContext context);
}

/// <summary>Everything a provider needs to start a charge.</summary>
public sealed record PaymentChargeRequest(
    Guid PaymentId,
    string Reference,
    decimal Amount,
    PaymentMethod Method,
    EWalletChannel WalletChannel,
    string OrderCode,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    DateTime ExpiresAt);

/// <summary>Outcome of starting a charge.</summary>
public sealed record PaymentChargeResult(
    bool Success,
    PaymentStatus Status,
    string? ProviderReference,
    string? Payload,
    DateTime? ExpiresAt,
    string? Error)
{
    public static PaymentChargeResult Settled(string providerReference) =>
        new(true, PaymentStatus.Completed, providerReference, null, null, null);

    public static PaymentChargeResult Awaiting(string providerReference, string payload, DateTime expiresAt) =>
        new(true, PaymentStatus.AwaitingPayment, providerReference, payload, expiresAt, null);

    public static PaymentChargeResult Rejected(string error) =>
        new(false, PaymentStatus.Failed, null, null, null, error);
}

public sealed record PaymentQueryRequest(string ProviderReference, string Reference);

/// <summary>Where a charge stands according to the provider.</summary>
public sealed record PaymentStatusResult(PaymentStatus Status, string? ProviderReference, string? Error)
{
    public static PaymentStatusResult Unknown(string error) => new(PaymentStatus.Pending, null, error);
}

/// <summary>The raw callback, before any of it is trusted.</summary>
public sealed record PaymentCallbackContext(
    string Body,
    IReadOnlyDictionary<string, string> Headers,
    string? WebhookSecret);

/// <summary>A verified callback, reduced to what the platform acts on.</summary>
public sealed record PaymentCallback(
    string Reference,
    string? ProviderReference,
    PaymentStatus Status,
    decimal? Amount,
    string? FailureReason);
