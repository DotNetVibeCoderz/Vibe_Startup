using FastRide.Api.Payments;
using FastRide.Api.Security;
using FastRide.Data;
using FastRide.Shared.Common;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FastRide.Api.Endpoints;

/// <summary>
/// Payment provider configuration, for the admin console.
///
/// Credentials go in but never come back out: responses report only whether a key is set.
/// An operator who needs to check a key looks it up at the provider, not here — a console
/// that can display secrets is a console that can leak them.
/// </summary>
public static class PaymentProviderEndpoints
{
    public static IEndpointRouteBuilder MapPaymentProviderEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/admin/payment-providers")
            .WithTags("Admin · Payment Providers")
            .RequireAuthorization(Policies.AdminOnly);

        group.MapGet("/", List);
        group.MapPut("/{name}", Save);
        group.MapPost("/{name}/test", Test).WithSummary("Check the provider answers with the stored credentials");

        return api;
    }

    private static async Task<IResult> List(FastRideDbContext db, CancellationToken ct)
    {
        var providers = await db.PaymentProviderConfigs
            .AsNoTracking()
            .OrderBy(provider => provider.Priority)
            .ThenBy(provider => provider.Name)
            .ToListAsync(ct);

        return Results.Ok(providers.Select(ToResponse).ToList());
    }

    private static async Task<IResult> Save(
        string name,
        SavePaymentProviderRequest request,
        FastRideDbContext db,
        PaymentProviderRegistry registry,
        CancellationToken ct)
    {
        var provider = await db.PaymentProviderConfigs
            .FirstOrDefaultAsync(entry => entry.Name == name.ToLowerInvariant(), ct);

        if (provider is null) return Results.NotFound(new ApiError("NotFound", "Provider tidak ditemukan."));

        if (request.ChargeExpiryMinutes is < 1 or > 1440)
            return Results.BadRequest(new ApiError("Invalid", "Masa berlaku tagihan harus 1–1440 menit."));

        // Switching a provider on without the credentials it needs would fail on the first
        // real charge, in front of a rider. Catch it here instead.
        var needsCredentials = provider.Name is not ("manual" or "simulated");

        if (request.IsEnabled && needsCredentials && string.IsNullOrWhiteSpace(request.ServerKey ?? provider.ServerKey))
            return Results.BadRequest(new ApiError("Invalid", $"Provider {provider.DisplayName} butuh server key sebelum diaktifkan."));

        if (request.IsEnabled && request.Methods.Count == 0)
            return Results.BadRequest(new ApiError("Invalid", "Pilih minimal satu metode pembayaran."));

        provider.IsEnabled = request.IsEnabled;
        provider.IsSandbox = request.IsSandbox;
        provider.SupportedMethods = string.Join(',', request.Methods.Distinct());
        provider.Priority = request.Priority;
        provider.MerchantId = request.MerchantId;
        provider.MerchantName = request.MerchantName;
        provider.MerchantCity = request.MerchantCity;
        provider.ChargeExpiryMinutes = request.ChargeExpiryMinutes;
        provider.BaseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? null : request.BaseUrl.Trim();

        // A null credential means "leave what is stored" — the console never round-trips a
        // secret it was not shown, so sending null is how an unchanged field arrives.
        if (!string.IsNullOrWhiteSpace(request.ServerKey)) provider.ServerKey = request.ServerKey.Trim();
        if (!string.IsNullOrWhiteSpace(request.ClientKey)) provider.ClientKey = request.ClientKey.Trim();
        if (!string.IsNullOrWhiteSpace(request.WebhookSecret)) provider.WebhookSecret = request.WebhookSecret.Trim();

        provider.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await registry.InvalidateAsync(ct);

        return Results.Ok(ToResponse(provider));
    }

    /// <summary>
    /// Prove the provider can actually take a charge, without charging anyone: open a token
    /// amount, read it back, and report what happened.
    /// </summary>
    private static async Task<IResult> Test(
        string name, PaymentProviderRegistry registry, CancellationToken ct)
    {
        var resolved = await registry.ResolveByNameAsync(name, ct);

        if (resolved is not { } entry)
            return Results.NotFound(new ApiError("NotFound", "Provider tidak ditemukan atau tidak aktif."));

        var methods = entry.Config.ParseMethods();

        if (methods.Count == 0)
            return Results.BadRequest(new ApiError("Invalid", "Provider ini belum punya metode pembayaran aktif."));

        var reference = $"TEST-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";

        var result = await entry.Provider.ChargeAsync(new Shared.Payments.PaymentChargeRequest(
            Guid.NewGuid(),
            reference,
            // Smallest amount that is meaningful in rupiah, so a sandbox charge costs nothing real.
            1000m,
            methods[0],
            EWalletChannel.Unspecified,
            "TEST",
            "Uji Koneksi",
            "test@fastride.local",
            "0800000000",
            DateTime.UtcNow.AddMinutes(5)), ct);

        return result.Success
            ? Results.Ok(new MessageResponse(
                $"{entry.Provider.DisplayName} merespons. Referensi uji: {result.ProviderReference}."))
            : Results.BadRequest(new ApiError("ProviderError", result.Error ?? "Provider tidak merespons."));
    }

    private static PaymentProviderResponse ToResponse(PaymentProviderConfig provider) => new(
        provider.Id,
        provider.Name,
        provider.DisplayName,
        provider.IsEnabled,
        provider.IsSandbox,
        provider.ParseMethods().ToList(),
        provider.Priority,
        provider.MerchantId,
        provider.MerchantName,
        provider.MerchantCity,
        provider.ChargeExpiryMinutes,
        // Presence only — the values themselves never leave the server.
        !string.IsNullOrWhiteSpace(provider.ServerKey),
        !string.IsNullOrWhiteSpace(provider.ClientKey),
        !string.IsNullOrWhiteSpace(provider.WebhookSecret),
        provider.BaseUrl,
        provider.UpdatedAt);
}
