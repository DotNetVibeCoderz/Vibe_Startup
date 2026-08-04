using FastRide.Api.Security;
using FastRide.Api.Services;
using FastRide.Data;
using FastRide.Shared.Common;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace FastRide.Api.Endpoints;

/// <summary>Promo and fare-table management — the "manajemen tarif &amp; promo" module.</summary>
public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder api)
    {
        var promos = api.MapGroup("/promos").WithTags("Promos").RequireAuthorization();

        promos.MapGet("/", ListPromos);
        promos.MapPost("/validate", ValidatePromo).WithSummary("Check a code against an amount");
        promos.MapPost("/", CreatePromo).RequireAuthorization(Policies.AdminOnly);
        promos.MapPut("/{id:guid}", UpdatePromo).RequireAuthorization(Policies.AdminOnly);
        promos.MapDelete("/{id:guid}", DeletePromo).RequireAuthorization(Policies.AdminOnly);

        var fares = api.MapGroup("/fares").WithTags("Fare Config").RequireAuthorization();

        fares.MapGet("/", ListFares);
        fares.MapPut("/{category}", UpdateFare).RequireAuthorization(Policies.AdminOnly);

        return api;
    }

    // ─────────────────────────── promos ───────────────────────────

    private static async Task<IResult> ListPromos(bool? activeOnly, FastRideDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var promos = db.Promos.AsNoTracking();

        if (activeOnly == true)
            promos = promos.Where(p => p.IsActive && p.ValidFrom <= now && p.ValidUntil >= now && p.UsageCount < p.UsageLimit);

        var data = await promos
            .OrderByDescending(p => p.IsActive)
            .ThenBy(p => p.Code)
            .Select(p => new PromoResponse(
                p.Id, p.Code, p.Description, p.Type, p.Value, p.MaxDiscount, p.MinOrderAmount,
                p.VehicleCategory, p.ValidFrom, p.ValidUntil, p.IsActive, p.UsageLimit, p.UsageCount))
            .ToListAsync(ct);

        return Results.Ok(data);
    }

    private static async Task<IResult> ValidatePromo(
        ValidatePromoRequest request, PricingService pricing, CancellationToken ct)
    {
        var evaluation = await pricing.EvaluatePromoAsync(request.Code, request.Amount, request.VehicleCategory, ct);

        if (evaluation.Promo is null)
            return Results.Ok(new ValidatePromoResponse(false, null, null, null, 0m, request.Amount,
                string.IsNullOrEmpty(evaluation.Message) ? "Kode promo tidak berlaku." : evaluation.Message));

        return Results.Ok(new ValidatePromoResponse(
            true, evaluation.Promo.Code, evaluation.Promo.Description, evaluation.Promo.Type,
            evaluation.Discount, request.Amount - evaluation.Discount, evaluation.Message));
    }

    private static async Task<IResult> CreatePromo(SavePromoRequest request, FastRideDbContext db, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Promos.AnyAsync(p => p.Code == code, ct))
            return Results.Conflict(new ApiError("Conflict", $"Kode promo {code} sudah dipakai."));

        if (request.Type == PromoType.Percentage && request.Value is <= 0 or > 100)
            return Results.BadRequest(new ApiError("Invalid", "Diskon persentase harus antara 1 dan 100."));

        var promo = new Promo
        {
            Code = code,
            Description = request.Description,
            Type = request.Type,
            Value = request.Value,
            MaxDiscount = request.MaxDiscount,
            MinOrderAmount = request.MinOrderAmount,
            VehicleCategory = request.VehicleCategory,
            ValidFrom = request.ValidFrom ?? DateTime.UtcNow,
            ValidUntil = request.ValidUntil ?? DateTime.UtcNow.AddMonths(1),
            IsActive = request.IsActive,
            UsageLimit = Math.Max(1, request.UsageLimit)
        };

        db.Promos.Add(promo);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/promos/{promo.Id}", ToResponse(promo));
    }

    private static async Task<IResult> UpdatePromo(
        Guid id, SavePromoRequest request, FastRideDbContext db, CancellationToken ct)
    {
        var promo = await db.Promos.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (promo is null) return Results.NotFound(new ApiError("NotFound", "Promo tidak ditemukan."));

        var code = request.Code.Trim().ToUpperInvariant();
        if (code != promo.Code && await db.Promos.AnyAsync(p => p.Code == code, ct))
            return Results.Conflict(new ApiError("Conflict", $"Kode promo {code} sudah dipakai."));

        promo.Code = code;
        promo.Description = request.Description;
        promo.Type = request.Type;
        promo.Value = request.Value;
        promo.MaxDiscount = request.MaxDiscount;
        promo.MinOrderAmount = request.MinOrderAmount;
        promo.VehicleCategory = request.VehicleCategory;
        if (request.ValidFrom is { } from) promo.ValidFrom = from;
        if (request.ValidUntil is { } until) promo.ValidUntil = until;
        promo.IsActive = request.IsActive;

        // Never drop the limit below what has already been redeemed.
        promo.UsageLimit = Math.Max(request.UsageLimit, promo.UsageCount);
        promo.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToResponse(promo));
    }

    private static async Task<IResult> DeletePromo(Guid id, FastRideDbContext db, CancellationToken ct)
    {
        var promo = await db.Promos.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (promo is null) return Results.NotFound(new ApiError("NotFound", "Promo tidak ditemukan."));

        // Redeemed promos are deactivated rather than deleted, so past orders keep their history.
        if (promo.UsageCount > 0)
        {
            promo.IsActive = false;
            promo.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new MessageResponse("Promo sudah pernah dipakai, jadi dinonaktifkan alih-alih dihapus."));
        }

        db.Promos.Remove(promo);
        await db.SaveChangesAsync(ct);
        return Results.Ok(new MessageResponse("Promo dihapus."));
    }

    // ────────────────────────── fare table ──────────────────────────

    private static async Task<IResult> ListFares(PricingService pricing, CancellationToken ct)
    {
        var configs = await pricing.GetFareConfigsAsync(ct);

        return Results.Ok(configs.Select(f => new FareConfigResponse(
            f.Id, f.VehicleCategory, f.BaseFare, f.CostPerKm, f.CostPerMinute,
            f.MinimumFare, f.SurgeMultiplier, f.CancellationFee, f.IsActive, f.UpdatedAt)).ToList());
    }

    private static async Task<IResult> UpdateFare(
        VehicleCategory category, UpdateFareConfigRequest request,
        FastRideDbContext db, PricingService pricing, CancellationToken ct)
    {
        var fare = await db.FareConfigs.FirstOrDefaultAsync(f => f.VehicleCategory == category, ct);
        if (fare is null) return Results.NotFound(new ApiError("NotFound", $"Tarif untuk kategori {category} tidak ditemukan."));

        if (request.SurgeMultiplier is < 1 or > 5)
            return Results.BadRequest(new ApiError("Invalid", "Surge multiplier harus antara 1.0 dan 5.0."));

        fare.BaseFare = request.BaseFare;
        fare.CostPerKm = request.CostPerKm;
        fare.CostPerMinute = request.CostPerMinute;
        fare.MinimumFare = request.MinimumFare;
        fare.SurgeMultiplier = request.SurgeMultiplier;
        fare.CancellationFee = request.CancellationFee;
        fare.IsActive = request.IsActive;
        fare.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        // The fare table is cached; a stale copy would keep quoting the old price.
        await pricing.InvalidateFareCacheAsync(ct);

        return Results.Ok(new FareConfigResponse(
            fare.Id, fare.VehicleCategory, fare.BaseFare, fare.CostPerKm, fare.CostPerMinute,
            fare.MinimumFare, fare.SurgeMultiplier, fare.CancellationFee, fare.IsActive, fare.UpdatedAt));
    }

    private static PromoResponse ToResponse(Promo promo) => new(
        promo.Id, promo.Code, promo.Description, promo.Type, promo.Value, promo.MaxDiscount,
        promo.MinOrderAmount, promo.VehicleCategory, promo.ValidFrom, promo.ValidUntil,
        promo.IsActive, promo.UsageLimit, promo.UsageCount);
}
