using FastRide.Data;
using FastRide.Shared.Common;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FastRide.Api.Services;

/// <summary>
/// Everything that decides what a trip costs: distance, the fare table, surge and promos.
///
/// It lives in one place because the rider's fare preview and the actual booking must agree
/// to the rupiah — previously the mobile app showed a hardcoded price and the API charged
/// something else.
/// </summary>
public sealed class PricingService(FastRideDbContext db, ICacheService cache)
{
    private static readonly TimeSpan FareCacheTtl = TimeSpan.FromMinutes(10);

    /// <summary>Fare table, cached — it changes when an admin edits it, not per request.</summary>
    public async Task<IReadOnlyList<FareConfig>> GetFareConfigsAsync(CancellationToken ct = default) =>
        await cache.GetOrCreateAsync(
            CacheKeys.FareConfigs,
            FareCacheTtl,
            async token => await db.FareConfigs.AsNoTracking().OrderBy(f => f.VehicleCategory).ToListAsync(token),
            ct);

    public Task InvalidateFareCacheAsync(CancellationToken ct = default) =>
        cache.RemoveAsync(CacheKeys.FareConfigs, ct);

    public async Task<FareConfig> GetFareConfigAsync(VehicleCategory category, CancellationToken ct = default)
    {
        var configs = await GetFareConfigsAsync(ct);
        return configs.FirstOrDefault(f => f.VehicleCategory == category)
               ?? new FareConfig { VehicleCategory = category };
    }

    /// <summary>Total route distance including any waypoints, in the order they will be visited.</summary>
    public static double RouteDistanceKm(
        double pickupLat, double pickupLon,
        double dropoffLat, double dropoffLon,
        IReadOnlyList<TripStopRequest>? stops)
    {
        var legs = 0.0;
        var currentLat = pickupLat;
        var currentLon = pickupLon;

        if (stops is { Count: > 0 })
        {
            foreach (var stop in stops)
            {
                legs += GeoUtils.DistanceKm(currentLat, currentLon, stop.Latitude, stop.Longitude);
                currentLat = stop.Latitude;
                currentLon = stop.Longitude;
            }
        }

        legs += GeoUtils.DistanceKm(currentLat, currentLon, dropoffLat, dropoffLon);
        return Math.Round(legs, 2);
    }

    /// <summary>Price a trip without touching promo usage counters.</summary>
    public async Task<FareQuoteResponse> QuoteAsync(FareQuoteRequest request, CancellationToken ct = default)
    {
        var distance = RouteDistanceKm(
            request.PickupLatitude, request.PickupLongitude,
            request.DropoffLatitude, request.DropoffLongitude,
            request.Stops);

        var duration = GeoUtils.EstimateDurationMinutes(distance);
        var fare = await GetFareConfigAsync(request.VehicleCategory, ct);
        var estimated = fare.Quote(distance, duration);

        var evaluation = await EvaluatePromoAsync(request.PromoCode, estimated, request.VehicleCategory, ct);

        return new FareQuoteResponse(
            request.VehicleCategory,
            Math.Round(distance, 1),
            duration,
            fare.BaseFare,
            fare.SurgeMultiplier,
            estimated,
            evaluation.Discount,
            estimated - evaluation.Discount,
            evaluation.Promo?.Code,
            evaluation.Message);
    }

    /// <summary>
    /// Check whether a promo code applies to an amount. Read-only: nothing is consumed here,
    /// so previewing a fare ten times does not burn ten redemptions.
    /// </summary>
    public async Task<PromoEvaluation> EvaluatePromoAsync(
        string? code, decimal amount, VehicleCategory? category, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return PromoEvaluation.None;

        var normalized = code.Trim().ToUpperInvariant();
        var promo = await db.Promos.AsNoTracking().FirstOrDefaultAsync(p => p.Code == normalized, ct);

        if (promo is null) return PromoEvaluation.Rejected("Kode promo tidak ditemukan.");
        if (!promo.IsActive) return PromoEvaluation.Rejected("Promo sudah tidak aktif.");

        var now = DateTime.UtcNow;
        if (promo.ValidFrom > now) return PromoEvaluation.Rejected("Promo belum berlaku.");
        if (promo.ValidUntil < now) return PromoEvaluation.Rejected("Promo sudah kedaluwarsa.");
        if (promo.UsageCount >= promo.UsageLimit) return PromoEvaluation.Rejected("Kuota promo sudah habis.");

        if (promo.VehicleCategory is not null && category is not null && promo.VehicleCategory != category)
            return PromoEvaluation.Rejected($"Promo hanya berlaku untuk kategori {promo.VehicleCategory}.");

        if (promo.MinOrderAmount > 0 && amount < promo.MinOrderAmount)
            return PromoEvaluation.Rejected($"Minimum transaksi Rp {promo.MinOrderAmount:N0}.");

        var discount = promo.Type == PromoType.Percentage
            ? amount * promo.Value / 100m
            : promo.Value;

        if (promo.Type == PromoType.Percentage && promo.MaxDiscount > 0)
            discount = Math.Min(discount, promo.MaxDiscount);

        // Never discount below zero — a promo cannot pay the rider.
        discount = Math.Round(Math.Clamp(discount, 0m, amount), 0);

        return new PromoEvaluation(promo, discount, $"Hemat Rp {discount:N0} dengan kode {promo.Code}.");
    }

    /// <summary>
    /// Atomically take one redemption slot. The conditional UPDATE means two riders racing for
    /// the last slot cannot both win, which a read-then-increment could not guarantee.
    /// </summary>
    public async Task<bool> TryConsumePromoAsync(Guid promoId, CancellationToken ct = default)
    {
        var affected = await db.Promos
            .Where(p => p.Id == promoId && p.UsageCount < p.UsageLimit && p.IsActive)
            .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.UsageCount, p => p.UsageCount + 1), ct);

        return affected > 0;
    }
}

/// <summary>Outcome of checking a promo code against an amount.</summary>
public readonly record struct PromoEvaluation(Promo? Promo, decimal Discount, string Message)
{
    public bool IsValid => Promo is not null && Discount > 0;

    public static PromoEvaluation None => new(null, 0m, string.Empty);

    public static PromoEvaluation Rejected(string message) => new(null, 0m, message);
}
