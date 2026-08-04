using System.Net;
using FastRide.Shared.DTOs;
using FastRide.Shared.Models;
using FastRide.Tests.Infrastructure;

namespace FastRide.Tests.Integration;

/// <summary>
/// Editing the fare table changes global state, so this class runs against its own API
/// instance rather than the shared one.
/// </summary>
public class FareConfigEndpointTests : IAsyncLifetime
{
    private readonly ApiFixture _fixture = new();

    public Task InitializeAsync() => _fixture.InitializeAsync();

    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task TheFareTable_CoversEveryVehicleCategory()
    {
        var fares = await _fixture.Admin.GetAndReadAsync<List<FareConfigResponse>>("/api/fares");

        foreach (var category in Enum.GetValues<VehicleCategory>())
            Assert.Contains(fares, fare => fare.VehicleCategory == category);
    }

    [Fact]
    public async Task ARider_CannotEditTheFareTable()
    {
        var rider = await _fixture.NewRiderAsync();

        using var response = await rider.Client.PutJsonAsync("/api/fares/Economy",
            new UpdateFareConfigRequest(1, 1, 1, 1, 1, 0, true));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ChangingAFare_ImmediatelyChangesWhatIsQuoted()
    {
        // The fare table is cached, so a stale copy would keep quoting the old price.
        var rider = await _fixture.NewRiderAsync();
        var trip = new FareQuoteRequest(-6.2088, 106.8456, -6.1751, 106.8650, VehicleCategory.Comfort);

        var before = await rider.Client.PostAndReadAsync<FareQuoteRequest, FareQuoteResponse>(
            "/api/orders/quote", trip);

        var current = (await _fixture.Admin.GetAndReadAsync<List<FareConfigResponse>>("/api/fares"))
            .Single(fare => fare.VehicleCategory == VehicleCategory.Comfort);

        using var update = await _fixture.Admin.PutJsonAsync("/api/fares/Comfort",
            new UpdateFareConfigRequest(
                current.BaseFare, current.CostPerKm * 2, current.CostPerMinute,
                current.MinimumFare, current.SurgeMultiplier, current.CancellationFee, true));

        update.EnsureSuccessStatusCode();

        var after = await rider.Client.PostAndReadAsync<FareQuoteRequest, FareQuoteResponse>(
            "/api/orders/quote", trip);

        Assert.True(after.EstimatedFare > before.EstimatedFare);
    }

    [Fact]
    public async Task Surge_IsAppliedToNewQuotes()
    {
        var rider = await _fixture.NewRiderAsync();
        var trip = new FareQuoteRequest(-6.2088, 106.8456, -6.1751, 106.8650, VehicleCategory.Premium);

        var before = await rider.Client.PostAndReadAsync<FareQuoteRequest, FareQuoteResponse>(
            "/api/orders/quote", trip);

        var current = (await _fixture.Admin.GetAndReadAsync<List<FareConfigResponse>>("/api/fares"))
            .Single(fare => fare.VehicleCategory == VehicleCategory.Premium);

        using var update = await _fixture.Admin.PutJsonAsync("/api/fares/Premium",
            new UpdateFareConfigRequest(
                current.BaseFare, current.CostPerKm, current.CostPerMinute,
                current.MinimumFare, 2.0m, current.CancellationFee, true));

        update.EnsureSuccessStatusCode();

        var after = await rider.Client.PostAndReadAsync<FareQuoteRequest, FareQuoteResponse>(
            "/api/orders/quote", trip);

        Assert.Equal(2.0m, after.SurgeMultiplier);
        Assert.Equal(before.EstimatedFare * 2, after.EstimatedFare);
    }

    [Fact]
    public async Task TheMinimumFare_IsAFloorOnShortTrips()
    {
        var rider = await _fixture.NewRiderAsync();

        var current = (await _fixture.Admin.GetAndReadAsync<List<FareConfigResponse>>("/api/fares"))
            .Single(fare => fare.VehicleCategory == VehicleCategory.Bike);

        using var update = await _fixture.Admin.PutJsonAsync("/api/fares/Bike",
            new UpdateFareConfigRequest(
                current.BaseFare, current.CostPerKm, current.CostPerMinute,
                MinimumFare: 99_000m, current.SurgeMultiplier, current.CancellationFee, true));

        update.EnsureSuccessStatusCode();

        // A hop of a few hundred metres, well below the new minimum.
        var quote = await rider.Client.PostAndReadAsync<FareQuoteRequest, FareQuoteResponse>(
            "/api/orders/quote",
            new FareQuoteRequest(-6.2088, 106.8456, -6.2090, 106.8460, VehicleCategory.Bike));

        Assert.Equal(99_000m, quote.EstimatedFare);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(9.0)]
    public async Task AnOutOfRangeSurge_IsRefused(decimal surge)
    {
        using var response = await _fixture.Admin.PutJsonAsync("/api/fares/Economy",
            new UpdateFareConfigRequest(5000, 3000, 500, 10000, surge, 5000, true));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
