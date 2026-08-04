using FastRide.Shared.Models;
using FastRide.Shared.Payments;

namespace FastRide.Api.Payments;

/// <summary>
/// Cash, and anything else settled outside the platform.
///
/// There is no third party here: the driver takes the money and the trip is done. The
/// provider exists so that cash flows through exactly the same code path as a card or a QR,
/// instead of being a special case scattered through the order service.
/// </summary>
public sealed class ManualPaymentProvider : IPaymentProvider
{
    public string Name => "manual";

    public string DisplayName => "Tunai / manual";

    public IReadOnlyCollection<PaymentMethod> SupportedMethods { get; } =
        [PaymentMethod.Cash, PaymentMethod.BankTransfer];

    public bool SettlesImmediately => true;

    public Task<PaymentChargeResult> ChargeAsync(PaymentChargeRequest request, CancellationToken ct = default) =>
        Task.FromResult(PaymentChargeResult.Settled(request.Reference));

    public Task<PaymentStatusResult> QueryAsync(PaymentQueryRequest request, CancellationToken ct = default) =>
        Task.FromResult(new PaymentStatusResult(PaymentStatus.Completed, request.ProviderReference, null));

    /// <summary>Nothing calls back about cash.</summary>
    public PaymentCallback? ReadCallback(PaymentCallbackContext context) => null;
}
