namespace FastRide.Shared.Models;

/// <summary>
/// Represents a payment transaction in the system.
/// </summary>
public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? TransactionReference { get; set; }

    // Navigation
    public Order Order { get; set; } = null!;
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
    public decimal Value { get; set; } // Percentage or fixed amount
    public decimal MaxDiscount { get; set; } = 0m;
    public DateTime ValidFrom { get; set; } = DateTime.UtcNow;
    public DateTime ValidUntil { get; set; } = DateTime.UtcNow.AddMonths(1);
    public bool IsActive { get; set; } = true;
    public int UsageLimit { get; set; } = 100;
    public int UsageCount { get; set; } = 0;
}
