namespace FastRide.Shared.Common;

/// <summary>
/// Distance and bearing helpers shared by the API, the simulator and the apps.
/// </summary>
public static class GeoUtils
{
    private const double EarthRadiusKm = 6371.0;
    private const double DegToRad = Math.PI / 180.0;

    /// <summary>Great-circle distance in kilometres between two WGS84 points.</summary>
    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = (lat2 - lat1) * DegToRad;
        var dLon = (lon2 - lon1) * DegToRad;
        var sinLat = Math.Sin(dLat / 2);
        var sinLon = Math.Sin(dLon / 2);

        var a = (sinLat * sinLat) +
                (Math.Cos(lat1 * DegToRad) * Math.Cos(lat2 * DegToRad) * sinLon * sinLon);

        return EarthRadiusKm * 2 * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
    }

    /// <summary>Initial bearing in degrees (0-359) from one point to another.</summary>
    public static double BearingDegrees(double lat1, double lon1, double lat2, double lon2)
    {
        var dLon = (lon2 - lon1) * DegToRad;
        var y = Math.Sin(dLon) * Math.Cos(lat2 * DegToRad);
        var x = (Math.Cos(lat1 * DegToRad) * Math.Sin(lat2 * DegToRad)) -
                (Math.Sin(lat1 * DegToRad) * Math.Cos(lat2 * DegToRad) * Math.Cos(dLon));

        var deg = Math.Atan2(y, x) / DegToRad;
        return (deg + 360) % 360;
    }

    /// <summary>
    /// Bounding box around a point, used to pre-filter candidate drivers in SQL before the
    /// exact haversine runs in memory. Cheap and index-friendly.
    /// </summary>
    public static (double MinLat, double MaxLat, double MinLon, double MaxLon) BoundingBox(
        double lat, double lon, double radiusKm)
    {
        var latDelta = radiusKm / 111.0;
        var cos = Math.Cos(lat * DegToRad);
        var lonDelta = radiusKm / (111.0 * (Math.Abs(cos) < 1e-6 ? 1e-6 : cos));
        return (lat - latDelta, lat + latDelta, lon - Math.Abs(lonDelta), lon + Math.Abs(lonDelta));
    }

    /// <summary>Rough city-traffic ETA: Jakarta average ~24 km/h plus a fixed pickup allowance.</summary>
    public static int EstimateDurationMinutes(double distanceKm) =>
        (int)Math.Ceiling((distanceKm / 24.0 * 60.0) + 4);
}
