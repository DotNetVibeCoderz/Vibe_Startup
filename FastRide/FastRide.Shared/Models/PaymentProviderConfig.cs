namespace FastRide.Shared.Models;

/// <summary>
/// A payment service provider as configured for this deployment.
///
/// Providers are declared in <c>appsettings.json</c> and may be overridden from the admin
/// console; a row here always wins over the file so an operator can switch provider or flip
/// to sandbox without a redeploy. Rows are seeded from configuration on first start.
/// </summary>
public class PaymentProviderConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Matches <c>IPaymentProvider.Name</c>: <c>manual</c>, <c>simulated</c>, <c>midtrans</c>, <c>xendit</c>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>What an operator sees in the console.</summary>
    public string DisplayName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    /// <summary>Sandbox keys hit the provider's test endpoints and move no real money.</summary>
    public bool IsSandbox { get; set; } = true;

    /// <summary>
    /// Comma-separated <see cref="PaymentMethod"/> names this provider is allowed to handle
    /// here. A provider may support more than the operator wants to switch on.
    /// </summary>
    public string SupportedMethods { get; set; } = string.Empty;

    /// <summary>
    /// Ranking when several enabled providers can serve the same method — lowest wins.
    /// Lets an operator move traffic between providers without disabling either.
    /// </summary>
    public int Priority { get; set; } = 100;

    // ─────────────────────────── credentials ───────────────────────────
    // Never returned to any client in full; the API masks them on the way out.

    public string? ClientKey { get; set; }
    public string? ServerKey { get; set; }

    /// <summary>Shared secret used to verify that a callback really came from the provider.</summary>
    public string? WebhookSecret { get; set; }

    /// <summary>Overrides the provider's default endpoint. Useful for a local mock.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Merchant identity printed into QRIS payloads.</summary>
    public string? MerchantId { get; set; }
    public string? MerchantName { get; set; }
    public string? MerchantCity { get; set; }

    /// <summary>How long a QR or virtual account stays payable.</summary>
    public int ChargeExpiryMinutes { get; set; } = 15;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Methods this row switches on, parsed from <see cref="SupportedMethods"/>.</summary>
    public IReadOnlyList<PaymentMethod> ParseMethods()
    {
        if (string.IsNullOrWhiteSpace(SupportedMethods)) return [];

        return SupportedMethods
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(name => Enum.TryParse<PaymentMethod>(name, ignoreCase: true, out var method)
                ? (PaymentMethod?)method
                : null)
            .Where(method => method is not null)
            .Select(method => method!.Value)
            .Distinct()
            .ToList();
    }

    public bool Handles(PaymentMethod method) => ParseMethods().Contains(method);
}
