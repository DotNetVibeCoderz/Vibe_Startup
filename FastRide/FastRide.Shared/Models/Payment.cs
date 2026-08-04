namespace FastRide.Shared.Models;

/// <summary>
/// A payment for one order.
///
/// The row is the payment *intent*, not just a receipt: it is created before the money moves
/// and is updated as the provider reports back. There is exactly one row per order — the
/// unique index on <see cref="OrderId"/> is what stops a trip being charged twice — so a
/// failed attempt is retried by resetting this same row rather than inserting another.
/// That keeps the guarantee while still allowing a second try, which a plain
/// "one row per attempt" model could not.
/// </summary>
public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Discount applied at booking time, carried over for reporting.</summary>
    public decimal DiscountAmount { get; set; }

    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    /// <summary>Which wallet the charge is routed to, when the method is an e-wallet.</summary>
    public EWalletChannel WalletChannel { get; set; } = EWalletChannel.Unspecified;

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    /// <summary>Our own reference, shown to the rider and quoted in support tickets.</summary>
    public string? TransactionReference { get; set; }

    public string? FailureReason { get; set; }

    // ─────────────────────── provider-side state ───────────────────────

    /// <summary>Name of the provider handling this charge (<c>manual</c>, <c>midtrans</c>, ...).</summary>
    public string? ProviderName { get; set; }

    /// <summary>The provider's own id for the charge. Used to reconcile and to poll status.</summary>
    public string? ProviderReference { get; set; }

    /// <summary>
    /// QRIS payload (EMVCo string), virtual account number, or redirect URL — whatever the
    /// payer needs in order to complete this charge.
    /// </summary>
    public string? PaymentPayload { get; set; }

    /// <summary>When the provider's charge stops being payable.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>How many times this order's payment has been attempted.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Last provider callback we accepted, for auditing and idempotency.</summary>
    public DateTime? LastCallbackAt { get; set; }

    // Navigation
    public Order Order { get; set; } = null!;

    /// <summary>Money has changed hands; nothing further should be charged for this order.</summary>
    public bool IsSettled => Status == PaymentStatus.Completed;

    /// <summary>Waiting on the payer — the app should keep showing the QR or VA details.</summary>
    public bool IsInFlight => Status is PaymentStatus.Pending or PaymentStatus.AwaitingPayment;

    /// <summary>A finished-but-unpaid charge can be attempted again on this same row.</summary>
    public bool CanRetry => Status is PaymentStatus.Failed or PaymentStatus.Expired;
}

/// <summary>
/// Represents a promo or discount applied to an order.
/// </summary>
public class Promo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PromoType Type { get; set; } = PromoType.Percentage;

    /// <summary>Percentage (0-100) when <see cref="Type"/> is Percentage, otherwise a fixed IDR amount.</summary>
    public decimal Value { get; set; }

    /// <summary>Discount ceiling for percentage promos. 0 means uncapped.</summary>
    public decimal MaxDiscount { get; set; }

    /// <summary>Promo only applies to orders at or above this fare. 0 means no minimum.</summary>
    public decimal MinOrderAmount { get; set; }

    /// <summary>Restrict to one vehicle category. Null means every category.</summary>
    public VehicleCategory? VehicleCategory { get; set; }

    public DateTime ValidFrom { get; set; } = DateTime.UtcNow;
    public DateTime ValidUntil { get; set; } = DateTime.UtcNow.AddMonths(1);
    public bool IsActive { get; set; } = true;
    public int UsageLimit { get; set; } = 100;
    public int UsageCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Active, inside its window and not exhausted.</summary>
    public bool IsUsable(DateTime now) =>
        IsActive && ValidFrom <= now && ValidUntil >= now && UsageCount < UsageLimit;
}
