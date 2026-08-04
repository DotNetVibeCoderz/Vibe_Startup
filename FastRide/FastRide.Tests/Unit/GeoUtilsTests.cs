using FastRide.Shared.Common;

namespace FastRide.Tests.Unit;

public class GeoUtilsTests
{
    // Jakarta reference points used throughout the product.
    private const double SudirmanLat = -6.2088, SudirmanLon = 106.8456;
    private const double ThamrinLat = -6.1751, ThamrinLon = 106.8650;

    [Fact]
    public void DistanceKm_IsZero_ForTheSamePoint() =>
        Assert.Equal(0, GeoUtils.DistanceKm(SudirmanLat, SudirmanLon, SudirmanLat, SudirmanLon), 6);

    [Fact]
    public void DistanceKm_MatchesKnownJakartaDistance()
    {
        var distance = GeoUtils.DistanceKm(SudirmanLat, SudirmanLon, ThamrinLat, ThamrinLon);

        // Roughly 4.3 km as the crow flies.
        Assert.InRange(distance, 4.2, 4.4);
    }

    [Fact]
    public void DistanceKm_IsSymmetric()
    {
        var there = GeoUtils.DistanceKm(SudirmanLat, SudirmanLon, ThamrinLat, ThamrinLon);
        var back = GeoUtils.DistanceKm(ThamrinLat, ThamrinLon, SudirmanLat, SudirmanLon);

        Assert.Equal(there, back, 9);
    }

    [Fact]
    public void DistanceKm_HandlesAntipodalPointsWithoutNaN()
    {
        // The old implementation used a sqrt(1 - x) term that could go slightly negative
        // for near-antipodal inputs and produce NaN.
        var distance = GeoUtils.DistanceKm(0, 0, 0, 180);

        Assert.False(double.IsNaN(distance));
        Assert.InRange(distance, 20_000, 20_040);
    }

    [Fact]
    public void BearingDegrees_PointsNorth_WhenTravellingNorth()
    {
        var bearing = GeoUtils.BearingDegrees(-6.20, 106.80, -6.10, 106.80);

        Assert.InRange(bearing, 0, 1);
    }

    [Fact]
    public void BearingDegrees_StaysWithinACircle()
    {
        var bearing = GeoUtils.BearingDegrees(-6.20, 106.90, -6.30, 106.80);

        Assert.InRange(bearing, 0, 360);
    }

    [Fact]
    public void BoundingBox_ContainsItsOwnCentre()
    {
        var box = GeoUtils.BoundingBox(SudirmanLat, SudirmanLon, 5);

        Assert.InRange(SudirmanLat, box.MinLat, box.MaxLat);
        Assert.InRange(SudirmanLon, box.MinLon, box.MaxLon);
    }

    [Fact]
    public void BoundingBox_GrowsWithRadius()
    {
        var small = GeoUtils.BoundingBox(SudirmanLat, SudirmanLon, 1);
        var large = GeoUtils.BoundingBox(SudirmanLat, SudirmanLon, 10);

        Assert.True(large.MaxLat - large.MinLat > small.MaxLat - small.MinLat);
        Assert.True(large.MaxLon - large.MinLon > small.MaxLon - small.MinLon);
    }

    [Fact]
    public void BoundingBox_CoversEveryPointWithinTheRadius()
    {
        // The dispatcher pre-filters candidates with this box, so a driver inside the radius
        // must never be excluded before the exact distance is computed.
        const double radiusKm = 5;
        var box = GeoUtils.BoundingBox(SudirmanLat, SudirmanLon, radiusKm);

        foreach (var bearing in new[] { 0, 45, 90, 135, 180, 225, 270, 315 })
        {
            var (lat, lon) = Offset(SudirmanLat, SudirmanLon, radiusKm * 0.98, bearing);

            Assert.InRange(lat, box.MinLat, box.MaxLat);
            Assert.InRange(lon, box.MinLon, box.MaxLon);
        }
    }

    [Theory]
    [InlineData(0.0, 4)]
    [InlineData(1.0, 7)]
    [InlineData(24.0, 64)]
    public void EstimateDurationMinutes_AddsAFixedPickupAllowance(double distanceKm, int expected) =>
        Assert.Equal(expected, GeoUtils.EstimateDurationMinutes(distanceKm));

    [Fact]
    public void EstimateDurationMinutes_IncreasesWithDistance()
    {
        var shortTrip = GeoUtils.EstimateDurationMinutes(2);
        var longTrip = GeoUtils.EstimateDurationMinutes(20);

        Assert.True(longTrip > shortTrip);
    }

    /// <summary>Move a point a given distance along a bearing, for boundary checks.</summary>
    private static (double Lat, double Lon) Offset(double lat, double lon, double distanceKm, double bearingDegrees)
    {
        const double earthRadiusKm = 6371.0;
        var angular = distanceKm / earthRadiusKm;
        var bearing = bearingDegrees * Math.PI / 180;
        var latRad = lat * Math.PI / 180;
        var lonRad = lon * Math.PI / 180;

        var newLat = Math.Asin((Math.Sin(latRad) * Math.Cos(angular)) +
                               (Math.Cos(latRad) * Math.Sin(angular) * Math.Cos(bearing)));

        var newLon = lonRad + Math.Atan2(
            Math.Sin(bearing) * Math.Sin(angular) * Math.Cos(latRad),
            Math.Cos(angular) - (Math.Sin(latRad) * Math.Sin(newLat)));

        return (newLat * 180 / Math.PI, newLon * 180 / Math.PI);
    }
}
