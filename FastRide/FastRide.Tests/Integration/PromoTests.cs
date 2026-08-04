using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using FastRide.Tests.Infrastructure;

namespace FastRide.Tests.Integration;

[Collection(ApiCollection.Name)]
public class PromoTests(ApiFixture fixture)
{
    private static readonly FareQuoteRequest SampleTrip =
        new(-6.2088, 106.8456, -6.1751, 106.8650, VehicleCategory.Economy);

    private static CreateOrderRequest Booking(Guid riderId, string? promo) =>
        new(riderId,
            -6.2088, 106.8456, "Jl. Sudirman No. 1",
            -6.1751, 106.8650, "Jl. Thamrin No. 9",
            VehicleCategory.Economy, PaymentMethod.Cash, promo);

    private async Task<PromoResponse> NewPromoAsync(
        PromoType type = PromoType.Percentage,
        decimal value = 50,
        decimal maxDiscount = 20000,
        decimal minOrderAmount = 0,
        VehicleCategory? category = null,
        int usageLimit = 100,
        DateTime? validUntil = null)
    {
        var code = $"UJI{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        return await fixture.Admin.PostAndReadAsync<SavePromoRequest, PromoResponse>(
            "/api/promos",
            new SavePromoRequest(code, "Promo uji", type, value, maxDiscount, minOrderAmount,
                category, DateTime.UtcNow.AddDays(-1), validUntil ?? DateTime.UtcNow.AddDays(30),
                true, usageLimit));
    }

    [Fact]
    public async Task Validate_AcceptsALivePromo()
    {
        var promo = await NewPromoAsync(PromoType.FixedAmount, value: 15000);
        var rider = await fixture.NewRiderAsync();

        var result = await rider.Client.PostAndReadAsync<ValidatePromoRequest, ValidatePromoResponse>(
            "/api/promos/validate", new ValidatePromoRequest(promo.Code, 50000m));

        Assert.True(result.Valid);
        Assert.Equal(15000m, result.Discount);
        Assert.Equal(35000m, result.FinalAmount);
    }

    [Fact]
    public async Task Validate_RejectsACodeThatDoesNotExist()
    {
        var rider = await fixture.NewRiderAsync();

        var result = await rider.Client.PostAndReadAsync<ValidatePromoRequest, ValidatePromoResponse>(
            "/api/promos/validate", new ValidatePromoRequest("TIDAKADA", 50000m));

        Assert.False(result.Valid);
        Assert.Equal(50000m, result.FinalAmount);
    }

    [Fact]
    public async Task APercentagePromo_IsCappedAtItsMaximum()
    {
        var promo = await NewPromoAsync(PromoType.Percentage, value: 50, maxDiscount: 5000);
        var rider = await fixture.NewRiderAsync();

        var result = await rider.Client.PostAndReadAsync<ValidatePromoRequest, ValidatePromoResponse>(
            "/api/promos/validate", new ValidatePromoRequest(promo.Code, 100000m));

        Assert.Equal(5000m, result.Discount);
    }

    [Fact]
    public async Task APromo_CanRequireAMinimumSpend()
    {
        var promo = await NewPromoAsync(PromoType.FixedAmount, value: 10000, minOrderAmount: 80000);
        var rider = await fixture.NewRiderAsync();

        var tooSmall = await rider.Client.PostAndReadAsync<ValidatePromoRequest, ValidatePromoResponse>(
            "/api/promos/validate", new ValidatePromoRequest(promo.Code, 20000m));

        var bigEnough = await rider.Client.PostAndReadAsync<ValidatePromoRequest, ValidatePromoResponse>(
            "/api/promos/validate", new ValidatePromoRequest(promo.Code, 90000m));

        Assert.False(tooSmall.Valid);
        Assert.Contains("Minimum", tooSmall.Message);
        Assert.True(bigEnough.Valid);
    }

    [Fact]
    public async Task APromo_CanBeLimitedToOneVehicleCategory()
    {
        var promo = await NewPromoAsync(PromoType.FixedAmount, value: 5000, category: VehicleCategory.Bike);
        var rider = await fixture.NewRiderAsync();

        var wrongCategory = await rider.Client.PostAndReadAsync<ValidatePromoRequest, ValidatePromoResponse>(
            "/api/promos/validate", new ValidatePromoRequest(promo.Code, 50000m, VehicleCategory.Premium));

        var rightCategory = await rider.Client.PostAndReadAsync<ValidatePromoRequest, ValidatePromoResponse>(
            "/api/promos/validate", new ValidatePromoRequest(promo.Code, 50000m, VehicleCategory.Bike));

        Assert.False(wrongCategory.Valid);
        Assert.True(rightCategory.Valid);
    }

    [Fact]
    public async Task AnExpiredPromo_IsRefused()
    {
        var promo = await NewPromoAsync(validUntil: DateTime.UtcNow.AddDays(-1));
        var rider = await fixture.NewRiderAsync();

        var result = await rider.Client.PostAndReadAsync<ValidatePromoRequest, ValidatePromoResponse>(
            "/api/promos/validate", new ValidatePromoRequest(promo.Code, 50000m));

        Assert.False(result.Valid);
        Assert.Contains("kedaluwarsa", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Quoting_ShowsTheDiscountWithoutSpendingIt()
    {
        // Previewing a fare ten times must not burn ten redemptions.
        var promo = await NewPromoAsync(PromoType.FixedAmount, value: 10000);
        var rider = await fixture.NewRiderAsync();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var quote = await rider.Client.PostAndReadAsync<FareQuoteRequest, FareQuoteResponse>(
                "/api/orders/quote", SampleTrip with { PromoCode = promo.Code });

            Assert.Equal(promo.Code, quote.PromoApplied);
            Assert.Equal(10000m, quote.Discount);
        }

        Assert.Equal(0, (await ReloadAsync(promo.Code)).UsageCount);
    }

    [Fact]
    public async Task Booking_SpendsExactlyOneRedemption()
    {
        var promo = await NewPromoAsync(PromoType.FixedAmount, value: 10000);
        var rider = await fixture.NewRiderAsync();

        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders", Booking(rider.Id, promo.Code));

        Assert.Equal(promo.Code, order.PromoApplied);
        Assert.Equal(10000m, order.DiscountAmount);
        Assert.Equal(order.EstimatedFare - 10000m, order.FinalFare);
        Assert.Equal(1, (await ReloadAsync(promo.Code)).UsageCount);
    }

    [Fact]
    public async Task Cancelling_HandsTheRedemptionBack()
    {
        var promo = await NewPromoAsync(PromoType.FixedAmount, value: 10000);
        var rider = await fixture.NewRiderAsync();

        var order = await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders", Booking(rider.Id, promo.Code));

        Assert.Equal(1, (await ReloadAsync(promo.Code)).UsageCount);

        using var cancel = await rider.Client.PostJsonAsync(
            $"/api/orders/{order.Id}/cancel", new CancelOrderRequest("Berubah pikiran"));
        cancel.EnsureSuccessStatusCode();

        Assert.Equal(0, (await ReloadAsync(promo.Code)).UsageCount);
    }

    [Fact]
    public async Task AnExhaustedPromo_StopsDiscounting()
    {
        var promo = await NewPromoAsync(PromoType.FixedAmount, value: 10000, usageLimit: 1);

        var firstRider = await fixture.NewRiderAsync();
        var first = await firstRider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders", Booking(firstRider.Id, promo.Code));

        Assert.Equal(10000m, first.DiscountAmount);

        var secondRider = await fixture.NewRiderAsync();
        var second = await secondRider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders", Booking(secondRider.Id, promo.Code));

        // The booking still succeeds — it is simply charged in full.
        Assert.Equal(0m, second.DiscountAmount);
        Assert.Null(second.PromoApplied);
        Assert.Equal(1, (await ReloadAsync(promo.Code)).UsageCount);
    }

    [Fact]
    public async Task ARedeemedPromo_IsDeactivatedRatherThanDeleted()
    {
        // Deleting it would strip the code off past orders.
        var promo = await NewPromoAsync(PromoType.FixedAmount, value: 5000);
        var rider = await fixture.NewRiderAsync();

        await rider.Client.PostAndReadAsync<CreateOrderRequest, CreateOrderResponse>(
            "/api/orders", Booking(rider.Id, promo.Code));

        using var delete = await fixture.Admin.DeleteAsync($"/api/promos/{promo.Id}");
        delete.EnsureSuccessStatusCode();

        var stored = await ReloadAsync(promo.Code);

        Assert.False(stored.IsActive);
    }

    [Fact]
    public async Task AnUnusedPromo_IsDeletedOutright()
    {
        var promo = await NewPromoAsync();

        using var delete = await fixture.Admin.DeleteAsync($"/api/promos/{promo.Id}");
        delete.EnsureSuccessStatusCode();

        var all = await fixture.Admin.GetAndReadAsync<List<PromoResponse>>("/api/promos");

        Assert.DoesNotContain(all, p => p.Code == promo.Code);
    }

    [Fact]
    public async Task ARider_CannotCreateAPromo()
    {
        var rider = await fixture.NewRiderAsync();

        using var response = await rider.Client.PostJsonAsync("/api/promos",
            new SavePromoRequest("GRATISAN", "Diskon buatan sendiri", PromoType.Percentage, 100));

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DuplicateCodes_AreRefused()
    {
        var promo = await NewPromoAsync();

        using var response = await fixture.Admin.PostJsonAsync("/api/promos",
            new SavePromoRequest(promo.Code, "Kembar", PromoType.Percentage, 10));

        Assert.Equal(System.Net.HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task APercentagePromoOutsideOneToAHundred_IsRefused()
    {
        using var response = await fixture.Admin.PostJsonAsync("/api/promos",
            new SavePromoRequest($"UJI{Guid.NewGuid():N}"[..12], "Terlalu besar", PromoType.Percentage, 150));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<PromoResponse> ReloadAsync(string code)
    {
        var all = await fixture.Admin.GetAndReadAsync<List<PromoResponse>>("/api/promos");

        return all.Single(promo => promo.Code == code);
    }
}
