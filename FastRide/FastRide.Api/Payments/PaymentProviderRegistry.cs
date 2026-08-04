using FastRide.Api.Services;
using FastRide.Data;
using FastRide.Shared.Models;
using FastRide.Shared.Payments;
using Microsoft.EntityFrameworkCore;

namespace FastRide.Api.Payments;

/// <summary>
/// Decides which provider handles a given payment method, and builds it.
///
/// Configuration comes from two places: <c>Payments:Providers</c> in appsettings, and rows in
/// the database that the admin console writes. The database wins, so an operator can switch
/// provider, rotate a key or drop to sandbox without a redeploy — but a fresh deployment
/// still works from the file alone.
/// </summary>
public sealed class PaymentProviderRegistry(
    FastRideDbContext db,
    ICacheService cache,
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory,
    TimeProvider clock,
    ILogger<PaymentProviderRegistry> logger)
{
    private static readonly TimeSpan ConfigCacheTtl = TimeSpan.FromMinutes(2);

    /// <summary>
    /// The simulated provider keeps charges in memory, so every request must reach the same
    /// instance or a QR issued by one request would be unknown to the next.
    /// </summary>
    private static readonly Dictionary<string, SimulatedPaymentProvider> SimulatedInstances = new(StringComparer.Ordinal);

    private static readonly Lock SimulatedLock = new();

    /// <summary>Providers enabled for this deployment, best first.</summary>
    public async Task<IReadOnlyList<PaymentProviderConfig>> GetConfigsAsync(CancellationToken ct = default) =>
        await cache.GetOrCreateAsync(
            CacheKeys.PaymentProviders,
            ConfigCacheTtl,
            async token => await db.PaymentProviderConfigs
                .AsNoTracking()
                .OrderBy(provider => provider.Priority)
                .ThenBy(provider => provider.Name)
                .ToListAsync(token),
            ct);

    public Task InvalidateAsync(CancellationToken ct = default) =>
        cache.RemoveAsync(CacheKeys.PaymentProviders, ct);

    /// <summary>Methods a rider can actually choose right now.</summary>
    public async Task<IReadOnlyList<PaymentMethod>> GetAvailableMethodsAsync(CancellationToken ct = default)
    {
        var configs = await GetConfigsAsync(ct);

        return configs
            .Where(config => config.IsEnabled)
            .SelectMany(config => config.ParseMethods())
            .Distinct()
            .OrderBy(method => method)
            .ToList();
    }

    /// <summary>
    /// Pick the provider for a method. Returns null when nothing is configured to handle it,
    /// which the caller must report rather than silently falling back to "paid".
    /// </summary>
    public async Task<IPaymentProvider?> ResolveAsync(PaymentMethod method, CancellationToken ct = default)
    {
        var configs = await GetConfigsAsync(ct);

        var chosen = configs.FirstOrDefault(config => config.IsEnabled && config.Handles(method));

        if (chosen is null)
        {
            logger.LogWarning("No enabled payment provider handles {Method}.", method);
            return null;
        }

        return Build(chosen);
    }

    /// <summary>Build a provider by name, for callbacks that arrive addressed to one.</summary>
    public async Task<(IPaymentProvider Provider, PaymentProviderConfig Config)?> ResolveByNameAsync(
        string name, CancellationToken ct = default)
    {
        var configs = await GetConfigsAsync(ct);

        var config = configs.FirstOrDefault(entry =>
            string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));

        if (config is null || !config.IsEnabled) return null;

        var provider = Build(config);

        return provider is null ? null : (provider, config);
    }

    private IPaymentProvider? Build(PaymentProviderConfig config) => config.Name.ToLowerInvariant() switch
    {
        "manual" => new ManualPaymentProvider(),

        "simulated" => GetOrCreateSimulated(config),

        "midtrans" => new MidtransPaymentProvider(
            config, httpClientFactory, loggerFactory.CreateLogger<MidtransPaymentProvider>()),

        "xendit" => new XenditPaymentProvider(
            config, httpClientFactory, loggerFactory.CreateLogger<XenditPaymentProvider>()),

        _ => null
    };

    private SimulatedPaymentProvider GetOrCreateSimulated(PaymentProviderConfig config)
    {
        lock (SimulatedLock)
        {
            if (SimulatedInstances.TryGetValue(config.Name, out var existing)) return existing;

            var created = new SimulatedPaymentProvider(
                config, loggerFactory.CreateLogger<SimulatedPaymentProvider>(), clock);

            SimulatedInstances[config.Name] = created;
            return created;
        }
    }

    /// <summary>Forget the cached simulated instances. Test hosts call this between runs.</summary>
    internal static void ResetSimulated()
    {
        lock (SimulatedLock) SimulatedInstances.Clear();
    }

    /// <summary>
    /// Copy providers declared in appsettings into the database on first start, so a fresh
    /// deployment has something enabled and the console has rows to edit. Existing rows are
    /// left alone — the operator's choices outrank the file.
    /// </summary>
    public static async Task SeedFromConfigurationAsync(
        FastRideDbContext database, IConfiguration configuration, ILogger logger, CancellationToken ct = default)
    {
        var declared = configuration.GetSection("Payments:Providers").Get<List<PaymentProviderOptions>>() ?? [];

        if (declared.Count == 0)
        {
            logger.LogWarning("No payment providers declared in configuration.");
            return;
        }

        var existing = await database.PaymentProviderConfigs
            .Select(provider => provider.Name)
            .ToListAsync(ct);

        var added = 0;

        foreach (var option in declared)
        {
            if (string.IsNullOrWhiteSpace(option.Name)) continue;
            if (existing.Contains(option.Name, StringComparer.OrdinalIgnoreCase)) continue;

            database.PaymentProviderConfigs.Add(new PaymentProviderConfig
            {
                Name = option.Name.ToLowerInvariant(),
                DisplayName = option.DisplayName ?? option.Name,
                IsEnabled = option.Enabled,
                IsSandbox = option.Sandbox,
                SupportedMethods = string.Join(',', option.Methods ?? []),
                Priority = option.Priority,
                ClientKey = option.ClientKey,
                ServerKey = option.ServerKey,
                WebhookSecret = option.WebhookSecret,
                BaseUrl = option.BaseUrl,
                MerchantId = option.MerchantId,
                MerchantName = option.MerchantName,
                MerchantCity = option.MerchantCity,
                ChargeExpiryMinutes = option.ChargeExpiryMinutes
            });

            added++;
        }

        if (added > 0)
        {
            await database.SaveChangesAsync(ct);
            logger.LogInformation("Seeded {Count} payment provider(s) from configuration.", added);
        }
    }
}

/// <summary>Shape of one entry under <c>Payments:Providers</c>.</summary>
public sealed class PaymentProviderOptions
{
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool Enabled { get; set; }
    public bool Sandbox { get; set; } = true;
    public List<string>? Methods { get; set; }
    public int Priority { get; set; } = 100;
    public string? ClientKey { get; set; }
    public string? ServerKey { get; set; }
    public string? WebhookSecret { get; set; }
    public string? BaseUrl { get; set; }
    public string? MerchantId { get; set; }
    public string? MerchantName { get; set; }
    public string? MerchantCity { get; set; }
    public int ChargeExpiryMinutes { get; set; } = 15;
}
