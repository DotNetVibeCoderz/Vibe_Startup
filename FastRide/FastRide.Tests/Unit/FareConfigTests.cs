using FastRide.Shared.Models;

namespace FastRide.Tests.Unit;

/// <summary>
/// The fare formula. `MinimumFare` and `SurgeMultiplier` were columns that existed in the
/// schema but were never applied, so a short trip could be quoted below the minimum.
/// </summary>
public class FareConfigTests
{
    private static FareConfig Economy() => new()
    {
        VehicleCategory = VehicleCategory.Economy,
        BaseFare = 5000m,
        CostPerKm = 3000m,
        CostPerMinute = 500m,
        MinimumFare = 10000m,
        SurgeMultiplier = 1.0m
    };

    [Fact]
    public void Quote_ChargesBasePlusDistancePlusTime()
    {
        var fare = Economy();

        // 5000 + (10 × 3000) + (30 × 500) = 50000
        Assert.Equal(50000m, fare.Quote(distanceKm: 10, durationMinutes: 30));
    }

    [Fact]
    public void Quote_NeverGoesBelowTheMinimumFare()
    {
        var fare = Economy();

        // Metered total for a 100 m hop is well under 10000.
        Assert.Equal(fare.MinimumFare, fare.Quote(distanceKm: 0.1, durationMinutes: 1));
    }

    [Fact]
    public void Quote_AppliesSurgeToTheMeteredTotal()
    {
        var fare = Economy();
        var normal = fare.Quote(10, 30);

        fare.SurgeMultiplier = 2.0m;

        Assert.Equal(normal * 2, fare.Quote(10, 30));
    }

    [Fact]
    public void Quote_AppliesSurgeBeforeTheMinimumFloor()
    {
        var fare = Economy();
        fare.MinimumFare = 30000m;
        fare.SurgeMultiplier = 2.0m;

        // Metered 50000 × 2 = 100000, comfortably above the floor, so the floor is not used.
        Assert.Equal(100000m, fare.Quote(10, 30));
    }

    [Fact]
    public void Quote_TreatsAnInvalidSurgeAsNoSurge()
    {
        var fare = Economy();
        var normal = fare.Quote(10, 30);

        fare.SurgeMultiplier = 0m;

        Assert.Equal(normal, fare.Quote(10, 30));
    }

    [Fact]
    public void Quote_IncreasesWithDistance()
    {
        var fare = Economy();

        Assert.True(fare.Quote(20, 60) > fare.Quote(5, 20));
    }

    [Fact]
    public void Quote_ReturnsWholeRupiah()
    {
        var fare = Economy();
        fare.CostPerKm = 3333m;
        fare.SurgeMultiplier = 1.15m;

        var quote = fare.Quote(7.7, 23);

        Assert.Equal(Math.Round(quote, 0), quote);
    }

    [Theory]
    [InlineData(VehicleCategory.Bike, 3000, 2000, 300, 7000)]
    [InlineData(VehicleCategory.Premium, 10000, 6000, 1000, 25000)]
    public void Quote_UsesEachCategorysOwnRates(
        VehicleCategory category, decimal baseFare, decimal perKm, decimal perMinute, decimal minimum)
    {
        var fare = new FareConfig
        {
            VehicleCategory = category,
            BaseFare = baseFare,
            CostPerKm = perKm,
            CostPerMinute = perMinute,
            MinimumFare = minimum
        };

        var expected = Math.Max(baseFare + (perKm * 6) + (perMinute * 19), minimum);

        Assert.Equal(expected, fare.Quote(6, 19));
    }
}
